using System;
using UnityEngine;

namespace SketchLand
{
    public enum PlayerActionKind { None, Swing, BowCharge, BowReleased, Eating }

    /// <summary>Gameplay only. No Animator, bones, IK, tween, or animation profile dependencies.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerActionController : MonoBehaviour
    {
        [Tooltip("World-space offset from the character root. Never follows animated bones.")]
        [SerializeField] private Vector2 _projectileOriginOffset = new(0f, 0.4f);
        [SerializeField] private Vector2 _meleeOriginOffset = new(0f, 0.55f);

        public PlayerActionKind Kind { get; private set; }
        public uint Revision { get; private set; }
        public uint ShotSequence { get; private set; }
        public uint ImpactSequence { get; private set; }
        public float StartedAt { get; private set; }
        public float ReleasedAt { get; private set; }
        public float Charge { get; private set; }
        public float SwingProgress => _swingClock.Progress;
        public float SwingRate { get; private set; }
        public float SwingHalfArc { get; private set; }
        public float SwingReach { get; private set; }
        public Vector2 AimDirection { get; private set; } = Vector2.right;
        public Vector2 AimPoint { get; private set; }
        public Vector2 ProjectileOrigin => (Vector2)transform.position + _projectileOriginOffset;
        public Vector2 MeleeOrigin => (Vector2)transform.position + _meleeOriginOffset;
        public ItemSO Item { get; private set; }
        public ArrowItemSO Arrow { get; private set; }
        public bool IsCharging => Kind == PlayerActionKind.BowCharge;
        public bool IsSwinging => Kind == PlayerActionKind.Swing;
        public bool IsAiming => IsCharging || IsSwinging;
        public bool IsEating => Kind == PlayerActionKind.Eating;
        public bool BlocksSprint => IsCharging || IsEating;
        public float EatingProgress => IsEating && Item is ConsumableItemSO food
            ? Mathf.Clamp01((Time.time - StartedAt) / Mathf.Max(0.01f, food.EatDuration)) : 0f;

        // Optional semantic gameplay cues, e.g. "Interact". Presentation chooses how to show them.
        public event Action<string> Cue;
        public void ReportCue(string cue) => Cue?.Invoke(cue);

        private ServerCharacter _character;
        private Player _actor;
        private InventorySlot _sourceSlot;
        private readonly PlayerActionClock _swingClock = new();

#if UNITY_EDITOR
        private const float StrikeGizmoDuration = 0.18f;
        private StrikeZone _strikeGizmoZone;
        private float _strikeGizmoUntil;
        private float _strikeGizmoZ;
#endif

        private void Awake() => _character = GetComponent<ServerCharacter>();

        public void SetAimPoint(Vector2 point)
        {
            AimPoint = point;
            if (!IsSwinging)
                AimDirection = DirectionTo(ProjectileOrigin, point, AimDirection);
            if (IsAiming) _character.SetFacingOverride(AimDirection.x);
        }

        public static Vector2 DirectionTo(Vector2 origin, Vector2 target, Vector2 fallback)
        {
            Vector2 delta = target - origin;
            return delta.sqrMagnitude > 0.0001f ? delta.normalized : fallback;
        }

        private bool CanAct => isActiveAndEnabled && _character.Health != null
            && _character.Health.CurrentLifeState.Value != LifeState.Dead;

        private void Update()
        {
            if (Kind == PlayerActionKind.None) return;
            if (!CanAct || _actor == null || _actor.Character != _character)
            {
                Cancel();
                return;
            }
            if (IsSwinging)
            {
                // Crossings are processed even when a slow frame skips over the impact moment.
                if (_swingClock.Advance(Time.deltaTime)) ResolveImpact();
                if (_swingClock.IsComplete) Cancel();
            }
            else if (IsCharging)
            {
                if (!CanContinueCharge()) { Cancel(); return; }
                var bow = (BowItemSO)Item;
                Charge = Mathf.Clamp01((Time.time - StartedAt) / Mathf.Max(0.01f, bow.ChargeDuration));
            }
            else if (IsEating)
            {
                var food = (ConsumableItemSO)Item;
                if (!GameInput.Instance.PrimaryActionHeld || WorldManager.Instance.MouseOverUI
                    || !WorldManager.Instance.IsWorldReady || !IsSameItemSelected() || !_actor.Food.CanEat(food))
                { Cancel(); return; }
                if (EatingProgress >= 1f)
                {
                    _actor.Food.Eat(_sourceSlot, food);
                    Cancel();
                }
            }
            else if (!IsSameItemSelected()) Cancel();
        }

        public void StartEating(Player actor, ConsumableItemSO food)
        {
            if (!CanAct || IsSwinging || IsEating || actor == null || actor.Character != _character
                || actor.Food == null || !actor.Food.CanEat(food)) return;
            var slot = InventoryInputManager.Instance.GetActiveInvSlot();
            if (slot == null || slot.IsEmpty || slot.GetItemSO() != food) return;
            Cancel();
            _actor = actor;
            Item = food;
            _sourceSlot = slot;
            Begin(PlayerActionKind.Eating);
        }

        public bool TrySwing(Player actor, SwingItemSO item, Vector2 aimPoint)
        {
            if (!CanAct || IsSwinging || item == null || actor == null || actor.Character != _character) return false;
            Cancel();
            _actor = actor;
            Item = item;
            AimPoint = aimPoint;
            AimDirection = DirectionTo(MeleeOrigin, aimPoint, AimDirection);
            SwingRate = Mathf.Max(0.01f, item.GetSwingRate(actor.Food));
            SwingHalfArc = Mathf.Clamp(item.SwingArcAngle, 1, 360) * 0.5f;
            SwingReach = Mathf.Max(0.01f, item.GetStrikeReach(actor.Food));
            _swingClock.Begin(1f / SwingRate, item.ImpactFraction);
            Begin(PlayerActionKind.Swing);
            _character.SetFacingOverride(AimDirection.x);
            return true;
        }

        public void StartCharge(Player actor, BowItemSO bow, Vector2 aimPoint)
        {
            if (!CanAct || IsSwinging || IsCharging || actor == null || actor.Character != _character || bow == null) return;
            ArrowItemSO arrow = FindArrow();
            if (arrow == null) return;
            Cancel();
            _actor = actor;
            Item = bow;
            Arrow = arrow;
            _sourceSlot = InventoryInputManager.Instance.GetActiveInvSlot();
            Begin(PlayerActionKind.BowCharge);
            SetAimPoint(aimPoint);
        }

        public void ReleaseCharge(BowItemSO bow, Vector2 aimPoint)
        {
            if (!IsCharging || Item != bow) return;
            Charge = Mathf.Clamp01((Time.time - StartedAt) / Mathf.Max(0.01f, bow.ChargeDuration));
            if (!CanAct || !CanContinueCharge() || WorldManager.Instance.MouseOverUI || Charge < bow.MinimumCharge)
            { Cancel(); return; }
            SetAimPoint(aimPoint);
            Vector2 origin = ProjectileOrigin;
            if (SpawnOverlapsTerrain(origin, Arrow.Projectile)) { Cancel(); return; }
            int damage = Mathf.RoundToInt(Mathf.Lerp(Arrow.Projectile.MinBaseDamage, Arrow.Projectile.MaxBaseDamage, Charge));
            float knockback = Mathf.Lerp(Arrow.Projectile.MinBaseKnockbackForce, Arrow.Projectile.MaxBaseKnockbackForce, Charge);
            var projectile = ProjectileManager.SpawnProjectile(Arrow.Projectile, origin,
                AimDirection * bow.GetLaunchSpeed(Charge), _character, damage, knockback);
            if (projectile == null) { Cancel(); return; }
            InventoryManager.Instance.RemoveItem(Arrow, 1);
            Kind = PlayerActionKind.BowReleased;
            ReleasedAt = Time.time;
            ShotSequence++;
            _character.ClearFacingOverride();
            // Retain the release snapshot so presentation can observe it on its next frame.
        }

        private static bool SpawnOverlapsTerrain(Vector2 origin, ProjectileSO projectile)
        {
            if (projectile.IgnoreTileCollision) return false;
            Vector2 half = projectile.ColliderSize * 0.5f;
            for (int y = Mathf.FloorToInt(origin.y - half.y); y <= Mathf.FloorToInt(origin.y + half.y); y++)
                for (int x = Mathf.FloorToInt(origin.x - half.x); x <= Mathf.FloorToInt(origin.x + half.x); x++)
                {
                    var tile = WorldManager.Instance.FgGrid.GetTileData(x, y);
                    if (tile.HasTile && tile.IsSolid) return true;
                }
            return false;
        }

        private void ResolveImpact()
        {
            var item = (SwingItemSO)Item;
            var zone = new StrikeZone(MeleeOrigin, AimDirection, SwingHalfArc, SwingReach);
#if UNITY_EDITOR
            // Capture the exact hit-test sector; do not follow the player after impact.
            _strikeGizmoZone = zone;
            _strikeGizmoZ = transform.position.z;
            _strikeGizmoUntil = Time.unscaledTime + StrikeGizmoDuration;
#endif
            var hit = new SyncHitData(item.GetMeleeDamage(_actor.Food), item.KnockbackForce, _character.transform.position);
            var entities = EntityManager.Instance.Entities;
            for (int i = entities.Count - 1; i >= 0; i--)
                if (entities[i] is ServerCharacter enemy && enemy != _character && enemy.DamageReceiver != null
                    && zone.Intersects(enemy.transform.position, enemy.ColliderSize)) enemy.DamageReceiver.ReceiveHit(hit);
            if (item is ToolItemSO tool)
            {
                var target = MiningManager.Instance.CaptureTarget(_actor, tool, AimPoint);
                if (target.HasValue) MiningManager.Instance.TryPerformMine(_actor, tool, target.Value);
            }
            ImpactSequence++;
        }

        private void Begin(PlayerActionKind kind)
        {
            Kind = kind;
            StartedAt = Time.time;
            Charge = 0f;
            Revision++;
        }

        public void Cancel()
        {
            if (Kind == PlayerActionKind.None) return;
            Kind = PlayerActionKind.None;
            Item = null;
            Arrow = null;
            _sourceSlot = null;
            _actor = null;
            Charge = 0f;
            _swingClock.Cancel();
            Revision++;
            _character.ClearFacingOverride();
        }

        private bool IsSameItemSelected()
        {
            var slot = InventoryInputManager.Instance.GetActiveInvSlot();
            return slot != null && slot == _sourceSlot && !slot.IsEmpty && slot.GetItemSO() == Item;
        }

        private bool CanContinueCharge() => IsSameItemSelected() && Arrow != null
            && Arrow.Projectile != null && Arrow.Projectile.ProjectilePrefab != null
            && InventoryManager.Instance.GetTotalItemCount(Arrow) > 0;

        private static ArrowItemSO FindArrow()
        {
            foreach (var slot in InventoryManager.Instance.PlayerInventory)
                if (!slot.IsEmpty && slot.GetItemSO() is ArrowItemSO arrow
                    && arrow.Projectile != null && arrow.Projectile.ProjectilePrefab != null) return arrow;
            return null;
        }

        private void OnDisable() => Cancel();
        private void OnApplicationFocus(bool focused) { if (!focused && (IsCharging || IsEating)) Cancel(); }
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || Time.unscaledTime >= _strikeGizmoUntil) return;

            var zone = _strikeGizmoZone;
            Vector3 origin = new(zone.Origin.x, zone.Origin.y, _strikeGizmoZ);
            Vector3 start = Quaternion.Euler(0f, 0f, -zone.HalfAngle) * zone.Direction;
            Vector3 end = Quaternion.Euler(0f, 0f, zone.HalfAngle) * zone.Direction;
            var previousColor = UnityEditor.Handles.color;
            var previousMatrix = UnityEditor.Handles.matrix;
            var previousDepthTest = UnityEditor.Handles.zTest;
            try
            {
                UnityEditor.Handles.matrix = Matrix4x4.identity;
                UnityEditor.Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                UnityEditor.Handles.color = new Color(0f, 1f, 1f, 0.2f);
                UnityEditor.Handles.DrawSolidArc(origin, Vector3.forward, start, zone.HalfAngle * 2f, zone.Reach);
                UnityEditor.Handles.color = new Color(0f, 1f, 1f, 0.7f);
                UnityEditor.Handles.DrawWireArc(origin, Vector3.forward, start, zone.HalfAngle * 2f, zone.Reach);
                if (zone.HalfAngle < 180f)
                {
                    UnityEditor.Handles.DrawLine(origin, origin + start * zone.Reach);
                    UnityEditor.Handles.DrawLine(origin, origin + end * zone.Reach);
                }
            }
            finally
            {
                UnityEditor.Handles.color = previousColor;
                UnityEditor.Handles.matrix = previousMatrix;
                UnityEditor.Handles.zTest = previousDepthTest;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(ProjectileOrigin, 0.045f);
            Gizmos.DrawLine(ProjectileOrigin, ProjectileOrigin + AimDirection * 0.4f);
        }
#endif
    }
}
