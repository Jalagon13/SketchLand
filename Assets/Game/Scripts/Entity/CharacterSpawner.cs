using System.Collections.Generic;
using UnityEngine;

namespace SketchLand
{
    public class CharacterSpawner
    {
        private RectInt _spawnArea; // Area around player characters can spawn
        public RectInt SpawnArea => _spawnArea;

        private RectInt _noSpawnArea; // Area around the player where mobs cannots spawn (so they dont just spawn within the view of the player)
        public RectInt NoSpawnArea => _noSpawnArea;

        private RectInt _activeArea; // any characters out any active areas will instantly despawn
        public RectInt ActiveArea => _activeArea;

        private RectInt _timerSafeArea; // Any characters outside this area will tick a despawn timer
        public RectInt TimerSafeArea => _timerSafeArea;

        private readonly ServerCharacter _host;
        public ServerCharacter Host => _host;

        private readonly HashSet<ServerCharacter> _ownedCharacters = new();
        public int LocalCurrentCharCount => _ownedCharacters.Count;

        public CharacterSpawnCircumstance CurrentCircumstance { get; private set; }

        private static readonly Vector2Int _spawnAreaDimensions = new(84, 46);
        private static readonly Vector2Int _noSpawnAreaDimensions = new(62, 36);
        private static readonly Vector2Int _activeAreaDimensions = new(252, 142);
        private static readonly Vector2Int _timerSafeAreaDimensions = new(62, 36);

        public CharacterSpawner(ServerCharacter host)
        {
            _host = host;
        }

        public void TickSpawner()
        {
            UpdateAreas();

            CurrentCircumstance = CharacterSpawnRules.Instance.GetCircumstances(_host);

            float newPerSecondSpawnChance = CalculatePerSecondSpawnChance();
            float tickChance = ConvertSpawnChancePerSecondToPerTick(newPerSecondSpawnChance);

            if (Random.value >= tickChance)
                return;

            BeginCharacterSpawnAttempt();
        }

        private void BeginCharacterSpawnAttempt()
        {
            if (!CharacterManager.Instance.CanSpawnCharacter())
                return;

            if (LocalCurrentCharCount >= CurrentCircumstance.MaxCharacterCount)
                return; // Enforce local cap

            if (!TryToFindSpawnSpot(out CharacterSpawnEntry spawnEntry, out Vector2 spawnSpot))
                return;
            
            if(!CharacterManager.Instance.TrySpawnCharacter(this, spawnEntry.Character.CharacterPrefab, spawnSpot, out ServerCharacter spawnedCharacter))
                return;
                
            RegisterOwnedCharacter(spawnedCharacter);
            // Debug.Log($"Host's local char count: {LocalCurrentCharCount}/{CurrentCircumstance.MaxCharacterCount}");
            // Debug.Log($"Global Character count: {CharacterManager.Instance.CurrentCharacterCount}/{CharacterManager.Instance.GlobalMaxCharCap}");
        }

        private bool TryToFindSpawnSpot(out CharacterSpawnEntry spawnEntry, out Vector2 spawnSpot)
        {
            var world = WorldManager.Instance;
            spawnEntry = default;
            spawnSpot = default;

            for (int attempt = 0; attempt < 50; attempt++)
            {
                // Find a random position in spawn area
                int randX = Random.Range(_spawnArea.xMin, _spawnArea.xMax);
                int randY = Random.Range(_spawnArea.yMin, _spawnArea.yMax);
                var candidate = new Vector2Int(randX, randY);
                
                if (!WorldManager.Instance.FgGrid.IsInBounds(candidate.x, candidate.y))
                    continue; // If out of bounds try again
                    
                var fgTd = WorldManager.Instance.FgGrid.GetTileData(candidate.x, candidate.y);

                if (fgTd.HasTile)
                    continue; // If has tile, try again
                    
                if(!TryFindGround(candidate, out Vector2Int foundGroundSpot))
                    continue;
                    
                var zone = world.GetZoneAtY(foundGroundSpot.y);
                bool openBackground = world.BgGrid.GetTileData(foundGroundSpot.x, foundGroundSpot.y).IsAir;
                if (!CurrentCircumstance.SpawnPool.TrySelectCharacter(zone, world.Clock.CurrentPhase, openBackground, out spawnEntry))
                    continue;

                if(!TestSpotForSpace(spawnEntry, foundGroundSpot, out Vector2 spawnPosition))
                    continue;

                spawnSpot = spawnPosition;
                return true;
            }
            
            // If all attempts dont make it return false
            return false;
        }

        private bool TestSpotForSpace(CharacterSpawnEntry spawnEntry, Vector2Int foundGroundSpot, out Vector2 spawnPosition)
        {
            TileGrid grid = WorldManager.Instance.FgGrid;
            Vector2 size = spawnEntry.Character.BodyColliderSize;
            Vector2 halfSize = size * 0.5f;
            
            spawnPosition = new(foundGroundSpot.x + 0.5f, foundGroundSpot.y + 0.5f + halfSize.y);

            float left = spawnPosition.x - halfSize.x;
            float right = spawnPosition.x + halfSize.x;
            float bottom = spawnPosition.y - halfSize.y;
            float top = spawnPosition.y + halfSize.y;

            int minX = Mathf.FloorToInt(left);
            int maxX = Mathf.CeilToInt(right) - 1;
            int minY = Mathf.FloorToInt(bottom);
            int maxY = Mathf.CeilToInt(top) - 1;

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (!grid.IsInBounds(x, y))
                        return false;

                    TileData tile = grid.GetTileData(x, y);

                    if (tile.IsSolid)
                        return false;
                }
            }

            return true;
        }

        private bool TryFindGround(Vector2Int candidate, out Vector2Int spawnSpot)
        {
            spawnSpot = default;

            // If it is empty space, seach 22 tiles down to find solid ground
            for (int searchAttempt = 0; searchAttempt < 22; searchAttempt++)
            {
                var grid = WorldManager.Instance.FgGrid;
            
                Vector2Int posToCheck = new(candidate.x, candidate.y - searchAttempt);
                Vector2Int belowPosToCheck = new(candidate.x, candidate.y - (searchAttempt + 1));

                if(!_spawnArea.Contains(posToCheck))
                    continue;
                    
                if (!grid.IsInBounds(posToCheck.x, posToCheck.y) || !grid.IsInBounds(belowPosToCheck.x, belowPosToCheck.y))
                    continue;

                TileData checkTile = grid.GetTileData(posToCheck.x, posToCheck.y);
                TileData belowTile = grid.GetTileData(belowPosToCheck.x, belowPosToCheck.y);

                if (checkTile.IsAir && belowTile.IsSolid)
                {
                    if (_noSpawnArea.Contains(posToCheck))
                        continue;

                    spawnSpot = posToCheck;
                    return true;
                }
            }
            
            return false;
        }

        public void RegisterOwnedCharacter(ServerCharacter character)
        {
            if (character != null)
            {
                _ownedCharacters.Add(character);
            }
        }

        public void UnregisterOwnedCharacter(ServerCharacter character)
        {
            _ownedCharacters.Remove(character);
        }

        private void UpdateAreas()
        {
            Vector3 hostPos = _host.transform.position;
            Vector2Int hostPosInt = new((int)hostPos.x, (int)hostPos.y);

            _spawnArea = CreateRectCenteredAt(hostPosInt, _spawnAreaDimensions);
            _noSpawnArea = CreateRectCenteredAt(hostPosInt, _noSpawnAreaDimensions);
            _activeArea = CreateRectCenteredAt(hostPosInt, _activeAreaDimensions);
            _timerSafeArea = CreateRectCenteredAt(hostPosInt, _timerSafeAreaDimensions);
        }

        private float CalculatePerSecondSpawnChance()
        {
            float perSecondChance = CurrentCircumstance.PerSecondSpawnChance;
            float populationRatio = CurrentCircumstance.MaxCharacterCount > 0 ? (float)LocalCurrentCharCount / CurrentCircumstance.MaxCharacterCount : 1f;
            float chanceMultiplier;

            // Increase the odds of spawning when this spawner's owned population is low.
            if (populationRatio < 0.2f)
                chanceMultiplier = 1.8f;
            else if (populationRatio < 0.4f)
                chanceMultiplier = 1.6f;
            else if (populationRatio < 0.6f)
                chanceMultiplier = 1.4f;
            else if (populationRatio < 0.8f)
                chanceMultiplier = 1.2f;
            else
                chanceMultiplier = 1f;

            return Mathf.Clamp01(perSecondChance * chanceMultiplier);
        }

        private float ConvertSpawnChancePerSecondToPerTick(float perSecondChance)
        {
            float spawnChancePerSecond = Mathf.Clamp01(perSecondChance);

            // Convert per second chance to per tick chance
            float perTickChance = 1f - Mathf.Pow(1f - spawnChancePerSecond, Time.fixedDeltaTime);

            return perTickChance;
        }

        private RectInt CreateRectCenteredAt(Vector2Int center, Vector2Int size)
        {
            Vector2Int position = center - (size / 2);
            return new RectInt(position, size);
        }
    }

    
}