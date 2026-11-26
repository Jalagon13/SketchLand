// Run Core() in a stopped Editor; Runtime() in a running local game with a ready world.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using SketchLand;

public static class BatAiChecks
{
    private static int _checks;
    private static void Check(bool condition,string message)
    {
        _checks++;
        if(!condition) throw new InvalidOperationException(message);
    }
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly WorldZone Surface=new WorldZone(new WorldZoneAppearance(),5,30,true);

    public static object Core()
    {
        _checks=0;
        var flight=new FlyingNpcMovement();
        Vector2 once=flight.Steer(Vector2.zero,new Vector2(3,4),3f,1f), many=Vector2.zero;
        for(int i=0;i<50;i++)many=flight.Steer(many,new Vector2(3,4),3f,.02f);
        Check((once-many).magnitude<.0001f,"Flight steering depends on tick size.");
        var velocity=new Vector2(4,-3);
        var corner=new GridPhysics.CollisionResult{TouchingRight=true,TouchingBottom=true};
        Check(flight.Bounce(velocity,corner,.75f,.3f,out var reflected),"Corner collision did not bounce.");
        Check((reflected-new Vector2(-3,2.25f)).sqrMagnitude<.0001f,"Corner reflection/damping is incorrect.");
        Check(flight.Steer(reflected,-reflected,100f,.02f)==reflected,"Steering cancels a bounce immediately.");
        flight.Tick(.31f);
        Check(flight.Steer(reflected,-reflected,3f,.02f)!=reflected,"Bounce recovery never resumes steering.");
        Check(!flight.Bounce(new Vector2(-3,1),corner,.75f,.3f,out _),"Touching a wall while moving away bounces twice.");
        var recoveryFlight=new FlyingNpcMovement();
        recoveryFlight.Bounce(Vector2.right*3f,new GridPhysics.CollisionResult{TouchingRight=true},.75f,.4f,out var recovering,1.2f);
        recoveryFlight.Tick(.42f);
        var gradual=recoveryFlight.Steer(recovering,Vector2.right*3f,3f,.02f);
        var full=flight.Steer(recovering,Vector2.right*3f,3f,.02f);
        Check((gradual-recovering).magnitude<(full-recovering).magnitude*.01f,"Bounce recovery snaps straight back to full steering.");
        recoveryFlight.Tick(1.2f);
        Check((recoveryFlight.Steer(recovering,Vector2.right*3f,3f,.02f)-full).sqrMagnitude<.0001f,"Normal steering does not resume after recovery.");
        foreach(var contact in new[]{new GridPhysics.CollisionResult{TouchingLeft=true},new GridPhysics.CollisionResult{TouchingRight=true},new GridPhysics.CollisionResult{TouchingTop=true},new GridPhysics.CollisionResult{TouchingBottom=true}})
        {
            var incoming=contact.TouchingLeft?Vector2.left:contact.TouchingRight?Vector2.right:contact.TouchingTop?Vector2.up:Vector2.down;
            Check(flight.Bounce(incoming,contact,.5f,.2f,out reflected)&&Vector2.Dot(incoming,reflected)<0,"A collision face did not reflect.");
        }
        var fg=new TileGrid(32,32);var bg=new TileGrid(32,32);var center=new Vector2(16.5f,16.5f);var size=new Vector2(1.5f,.75f);
        Check(CharacterSpawnSpace.IsOpenSurfaceAir(fg,bg,Surface,center,size),"Empty surface sky rejected.");
        Check(!CharacterSpawnSpace.IsOpenSurfaceAir(fg,bg,new WorldZone(new WorldZoneAppearance(),0,32,false),center,size),"Underground air accepted.");
        Check(!CharacterSpawnSpace.IsOpenSurfaceAir(fg,bg,Surface,new Vector2(1f,10f),size),"World boundary clearance ignored.");
        Check(!CharacterSpawnSpace.IsOpenSurfaceAir(fg,bg,Surface,new Vector2(16.5f,5.5f),size),"Zone boundary clearance ignored.");
        foreach(var grid in new[]{fg,bg})
        for(int x=14;x<=18;x++)for(int y=15;y<=17;y++)
        {
            grid.SetTileDataDirect(x,y,new TileData{TileId=1,IsSolid=false});
            Check(!CharacterSpawnSpace.IsOpenSurfaceAir(fg,bg,Surface,center,size),"A body/adjacent foreground or background tile was accepted.");
            grid.SetTileDataDirect(x,y,TileData.Air);
        }
        var data=AssetDatabase.LoadAssetAtPath<CharacterSO>("Assets/Game/Content/Data/CharacterData/BatData.asset");
        Check(data.RootState is BatRootState,"BatData has the wrong root state.");
        Check(!ReferenceEquals(data.RootState.CreateInstance(),data.RootState.CreateInstance()),"Bats share a state instance.");
        for(int i=0;i<10;i++)
        {
            float interval=((BatRootState)data.RootState).RandomSwoopInterval();
            Check(interval>=5f&&interval<=8f,"Hover interval is not configured to 5–8 seconds.");
        }
        return new{assertions=_checks,steeringFrameIndependent=true,bounceAllFaces=true,spawnClearanceBothLayers=true};
    }

    private static CharacterSpawnEntry BatEntry()
    {
        var rules=UnityEngine.Object.FindFirstObjectByType<CharacterSpawnRules>();
        return ((List<CharacterSpawnEntry>)typeof(CharacterSpawnRules).GetField("_spawnEntries",Private).GetValue(rules)).Single(e=>e.Character.RootState is BatRootState);
    }
    private static void SetTime(WorldClock clock,bool night)
    {
        double start=0;
        if(night)foreach(string f in new[]{"_sunriseMinutes","_dayMinutes","_sunsetMinutes"})start+=(float)typeof(WorldClock).GetField(f,Private).GetValue(clock);
        typeof(WorldClock).GetProperty("ElapsedMinutes").GetSetMethod(true).Invoke(clock,new object[]{night?start+1:0d});
    }
    private static void Tick(ServerCharacter bat,int steps)
    {
        for(int i=0;i<steps&&bat!=null&&bat.IsSpawned;i++)
        {
            bat.Machine.UpdateTick(.02f);
            bat.FixedTick(.02f);
        }
    }
    private static void Change(ServerCharacter bat,State state)
    {
        bat.Machine.ChangeState(bat.RootState.ActiveChild,state);
    }
    public static async Task<object> Runtime()
    {
        Check(Application.isPlaying&&WorldManager.Instance.IsWorldReady,"Requires a running world.");
        _checks=0;
        var world=WorldManager.Instance;var clock=world.Clock;double oldTime=clock.ElapsedMinutes;
        var player=Player.Instance.Character;int hp=player.Health.CurrentHealth.Value;var life=player.Health.CurrentLifeState.Value;
        Vector3 originalPlayerPosition=player.transform.position;
        var owner=((List<CharacterSpawner>)typeof(CharacterManager).GetField("_spawners",Private).GetValue(CharacterManager.Instance)).First(s=>s.Host==player);
        var entry=BatEntry();var data=entry.Character;ServerCharacter bat=null,idleBat=null;
        var combatAge=typeof(BatRootState).GetField("_timeSinceCombat",Private);
        float inactivityTimeout=(float)typeof(BatRootState).GetField("_combatInactivityTimeout",Private).GetValue(data.RootState);
        var changedTiles=new Dictionary<Vector2Int,TileData>();
        try
        {
            Check(entry.SpawnLocation==CharacterSpawnLocation.OpenAir,"Bat spawn mode is not open air.");
            foreach(DayPhase phase in Enum.GetValues(typeof(DayPhase)))
                Check(entry.CanSpawn(Surface,phase,true)==(phase==DayPhase.Night),"Bat day/night spawn filter is incorrect.");
            Check(!entry.CanSpawn(new WorldZone(new WorldZoneAppearance(),0,32,false),DayPhase.Night,true),"Bat is allowed underground.");
            Check(!entry.CanSpawn(Surface,DayPhase.Night,false),"Bat is allowed inside background walls.");
            SetTime(clock,true);
            owner.TickSpawner();
            var find=typeof(CharacterSpawner).GetMethod("TryToFindSpawnSpot",Private);
            int airCandidates=0;
            for(int i=0;i<80;i++)
            {
                object[] args={null,Vector2.zero};
                if(!(bool)find.Invoke(owner,args)||((CharacterSpawnEntry)args[0]).Character!=data)continue;
                var p=(Vector2)args[1];airCandidates++;
                Check(CharacterSpawnSpace.IsOpenSurfaceAir(world.FgGrid,world.BgGrid,world.GetZoneAtY(p.y),p,data.BodyColliderSize),"Natural spawner returned an obstructed air position.");
                Check(!owner.NoSpawnArea.Contains(Vector2Int.FloorToInt(p)),"Bat spawned inside the exclusion zone.");
            }
            Check(airCandidates>0,"No natural bat spawn locations found at night.");
            SetTime(clock,false);owner.TickSpawner();
            for(int i=0;i<20;i++)
            {
                object[] args={null,Vector2.zero};
                if((bool)find.Invoke(owner,args))Check(((CharacterSpawnEntry)args[0]).Character!=data,"Natural spawner selected a bat during daytime.");
            }
            SetTime(clock,true);
            Check(CharacterManager.Instance.TrySpawnCharacter(owner,data.CharacterPrefab,(Vector2)player.transform.position+new Vector2(3,8),out bat),"Could not spawn the test bat.");
            owner.RegisterOwnedCharacter(bat);
            var root=(BatRootState)bat.RootState;
            Check(root.ActiveChild==root.Pursue,"Bat did not start in Pursue.");
            EntityManager.Instance.UnRegister(bat); // Drive exact fixed steps for deterministic checks below.
            bat.transform.position=player.transform.position+new Vector3(6,2,0);bat.Velocity=Vector2.zero;
            Tick(bat,1);
            Check(bat.Velocity.y>0&&bat.Velocity.magnitude<root.FlightSpeed,"Altitude priority or momentum steering is missing.");
            bat.transform.position=player.transform.position+new Vector3(3,8,0);bat.Velocity=Vector2.zero;
            var visited=new HashSet<string>();
            for(int i=0;i<700;i++){Tick(bat,1);visited.Add(root.ActiveChild.GetType().Name);}
            Check(visited.Contains(nameof(BatPursueState))&&visited.Contains(nameof(BatSwoopState)),"Normal AI never cycled through Pursue and Swoop.");

            Check(root.FlightSpeed==bat.Stats.MoveSpeed.GetValue(),"Flight speed is not using the character's base movement stat.");
            foreach(float side in new[]{-1f,1f})
            {
                player.transform.position=originalPlayerPosition;
                bat.transform.position=originalPlayerPosition+Vector3.up*8;bat.Velocity=Vector2.zero;
                Change(bat,root.Pursue);Change(bat,root.Swoop);
                player.transform.position=originalPlayerPosition+Vector3.right*side*10f;
                for(int i=0;i<50;i++)
                {
                    bat.Machine.FixedTick(.02f);
                    Check(bat.Velocity.magnitude<=root.FlightSpeed+.001f,"Swoop exceeds the normal flight speed.");
                }
                Check(bat.Velocity.x*side>.2f,"Swoop does not turn toward the player's new position.");
            }
            player.transform.position=originalPlayerPosition;
            bat.transform.position=originalPlayerPosition+Vector3.up*8;bat.Velocity=Vector2.zero;
            Change(bat,root.Pursue);Change(bat,root.Swoop);
            for(int i=0;i<6;i++)bat.Machine.FixedTick(.02f);
            player.transform.position=originalPlayerPosition-Vector3.up*3;
            bat.transform.position=originalPlayerPosition-Vector3.up*.5f;
            bat.Machine.UpdateTick(.02f);
            Check(root.ActiveChild==root.Swoop,"Crossing the player's old position cancelled a swoop on the moving target.");
            player.transform.position=originalPlayerPosition;

            bat.transform.position=player.transform.position+Vector3.up*8;bat.Velocity=Vector2.zero;Change(bat,root.Pursue);Change(bat,root.Swoop);
            for(int i=0;i<6;i++)bat.Machine.FixedTick(.02f);
            bat.transform.position=player.transform.position-Vector3.up;
            bat.Machine.UpdateTick(.02f);
            Check(root.ActiveChild==root.Pursue,"Passing the current target did not end a swoop.");
            bat.transform.position=player.transform.position+Vector3.up*8;Change(bat,root.Swoop);
            for(int i=0;i<Mathf.CeilToInt(root.MaximumSwoopDuration/.02f)+2&&root.ActiveChild==root.Swoop;i++){bat.transform.position=player.transform.position+Vector3.up*8;Tick(bat,1);}
            Check(root.ActiveChild==root.Pursue,"Swoop timeout failed.");

            bat.transform.position=player.transform.position+Vector3.up*8;Change(bat,root.Swoop);
            combatAge.SetValue(root,inactivityTimeout-.05f);
            Check(bat.DamageReceiver.ReceiveHit(new SyncHitData(1,10,player.transform.position)),"Bat rejected a valid damage hit.");
            Check((float)combatAge.GetValue(root)==0f,"Receiving damage did not reset the inactivity timer.");
            Check(root.ActiveChild==root.Pursue&&bat.IsKnockedBack,"Taking damage did not cancel the swoop and preserve knockback.");
            var knocked=bat.Velocity;Tick(bat,1);
            Check((bat.Velocity-knocked).sqrMagnitude<.0001f,"AI overwrote knockback velocity.");
            bat.ClearKnockback();Change(bat,root.Swoop);
            player.Health.CurrentLifeState.Value=LifeState.IFrame;int before=player.Health.CurrentHealth.Value;
            combatAge.SetValue(root,inactivityTimeout-.05f);
            bat.OnEntityOverlap(player);
            Check(root.ActiveChild==root.Swoop&&player.Health.CurrentHealth.Value==before,"Iframe contact falsely completed the swoop.");
            Check((float)combatAge.GetValue(root)>inactivityTimeout-.1f,"Blocked contact reset the inactivity timer.");
            player.Health.CurrentLifeState.Value=LifeState.Alive;
            bat.OnEntityOverlap(player);
            Check(root.ActiveChild==root.Pursue&&player.Health.CurrentHealth.Value<before,"A confirmed contact hit did not finish the swoop.");
            Check((float)combatAge.GetValue(root)==0f,"Dealing damage did not reset the inactivity timer.");

            int wallX=Mathf.FloorToInt(player.transform.position.x)+14,wallY=Mathf.FloorToInt(player.transform.position.y)+14;
            for(int x=wallX-4;x<=wallX+2;x++)for(int y=wallY-3;y<=wallY+3;y++)
            {
                var tile=new Vector2Int(x,y);changedTiles[tile]=world.FgGrid.GetTileData(x,y);world.FgGrid.SetTileDataDirect(x,y,TileData.Air);
            }
            world.FgGrid.SetTileDataDirect(wallX,wallY,new TileData{TileId=1,IsSolid=true});
            foreach(State state in new State[]{root.Pursue,root.Swoop})
            {
                Change(bat,state);bat.ClearKnockback();bat.transform.position=new Vector3(wallX-data.BodyColliderSize.x*.5f-.03f,wallY+.5f,0);bat.Velocity=Vector2.right*6f;
                Tick(bat,1);Check(bat.Velocity.x<0,"Actual grid wall collision did not bounce in "+state.GetType().Name);
                var bounce=bat.Velocity;Tick(bat,5);
                Check((bat.Velocity-bounce).sqrMagnitude<.0001f,"State steering cancelled the bounce hold.");
            }
            foreach(var tile in changedTiles)world.FgGrid.SetTileDataDirect(tile.Key.x,tile.Key.y,tile.Value);changedTiles.Clear();
            bat.transform.position=player.transform.position+new Vector3(3,8,0);bat.ClearKnockback();bat.Velocity=Vector2.zero;Change(bat,root.Pursue);
            EntityManager.Instance.Register(bat);
            int frame=Time.frameCount;Vector3 initial=bat.transform.position;
            await Task.Delay(750);
            Check(Time.frameCount>frame&&bat!=null&&(bat.transform.position-initial).sqrMagnitude>.001f,"Live game loop did not move the bat.");
            EntityManager.Instance.UnRegister(bat);

            Check(CharacterManager.Instance.TrySpawnCharacter(owner,data.CharacterPrefab,(Vector2)player.transform.position+Vector2.up*8,out idleBat),"Could not spawn the inactivity test bat.");
            owner.RegisterOwnedCharacter(idleBat);EntityManager.Instance.UnRegister(idleBat);
            var idleRoot=(BatRootState)idleBat.RootState;
            bool died=false;idleBat.Health.OnDied+=_=>died=true;
            combatAge.SetValue(idleRoot,inactivityTimeout-.25f);
            Tick(idleBat,10);Check(!idleRoot.IsLeaving,"Bat left before the inactivity deadline.");
            Tick(idleBat,5);Check(idleRoot.IsLeaving&&clock.CurrentPhase==DayPhase.Night,"Inactivity did not trigger departure at night.");
            float idleY=idleBat.transform.position.y;Tick(idleBat,20);
            Check(idleBat.transform.position.y>idleY,"Inactive bat did not fly upward to leave.");
            for(int i=0;i<510&&idleBat!=null&&idleBat.IsSpawned;i++)Tick(idleBat,1);
            Check(idleBat==null||!idleBat.IsSpawned,"Inactive bat did not despawn after flying away.");
            Check(!died,"Inactivity departure incorrectly caused a death event.");

            bat.ClearKnockback();SetTime(clock,false);
            float departureY=bat.transform.position.y;Tick(bat,15);
            Check(root.IsLeaving&&bat.transform.position.y>departureY,"Bat did not ascend at dawn.");
            for(int i=0;i<510&&bat!=null&&bat.IsSpawned;i++)Tick(bat,1);
            Check(bat==null||!bat.IsSpawned,"Daytime departure did not despawn the bat.");
            return new{assertions=_checks,naturalAirCandidates=airCandidates,visitedStates=visited.ToArray(),contactDamageAndIframes=true,knockback=true,gridBounceBothStates=true,liveMovement=true,dawnDeparture=true,inactivityDeparture=true,combatResetsInactivity=true};
        }
        finally
        {
            foreach(var tile in changedTiles)world.FgGrid.SetTileDataDirect(tile.Key.x,tile.Key.y,tile.Value);
            if(bat!=null&&bat.IsSpawned)CharacterManager.Instance.DespawnCharacter(bat);
            if(idleBat!=null&&idleBat.IsSpawned)CharacterManager.Instance.DespawnCharacter(idleBat);
            typeof(WorldClock).GetProperty("ElapsedMinutes").GetSetMethod(true).Invoke(clock,new object[]{oldTime});
            player.Health.CurrentHealth.Value=hp;player.Health.CurrentLifeState.Value=life;player.ClearKnockback();
            player.transform.position=originalPlayerPosition;
        }
    }
}
