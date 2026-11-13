using UnityEngine;

namespace SketchLand
{
    public class PlayerRangedHandler : MonoBehaviour
    {
        private PlayerBowPose _bowPose;
        private PlayerArmHandler _armHandler;
        private ServerCharacter _character;
        private BowItemSO _weapon;
        private ArrowItemSO _arrow;
        private InventorySlot _sourceSlot;
        private float _chargeTime;

        public bool IsCharging => _weapon != null;
        public bool IsAiming => IsCharging || (_bowPose != null && _bowPose.IsActive);
        public bool IsShooting => IsAiming;
        public float Charge => _weapon == null ? 0f : Mathf.Clamp01(_chargeTime / Mathf.Max(0.01f, _weapon.ChargeDuration));

        private void Awake()
        {
            _bowPose = GetComponent<PlayerBowPose>();
            _armHandler = GetComponent<PlayerArmHandler>();
            _character = GetComponent<ServerCharacter>();
        }

        private void Update()
        {
            if (!IsCharging) return;

            if (!CanContinueCharge())
            {
                CancelCharge();
                return;
            }

            _chargeTime += Time.deltaTime;
            _bowPose.SetCharge(Charge);
        }

        public void StartCharge(BowItemSO weapon)
        {
            if (weapon == null || IsCharging || _bowPose == null) return;
            if (_character.Health.CurrentLifeState.Value == LifeState.Dead) return;

            ArrowItemSO arrow = FindArrow();
            if (arrow == null) return;

            _armHandler.CancelSwing();
            // A fresh press can interrupt release/recovery and begin charging immediately.
            _bowPose.ResetPose();
            _sourceSlot = InventoryInputManager.Instance.GetActiveInvSlot();
            _weapon = weapon;
            _arrow = arrow;
            _chargeTime = 0f;
            _bowPose.BeginDraw(weapon, arrow);
        }

        public void HoldCharge(BowItemSO weapon)
        {
            // Update owns the charge clock. Holding never repeats a shot.
        }

        public void ReleaseCharge(BowItemSO weapon)
        {
            if (!IsCharging || weapon != _weapon) return;
            if (!CanContinueCharge() || WorldManager.Instance.MouseOverUI || Charge < weapon.MinimumCharge)
            {
                CancelCharge();
                return;
            }

            // Resolve aim now, rather than using the preceding frame's pose.
            _bowPose.UpdateAim();
            Vector2 velocity = _bowPose.AimDirection * weapon.GetLaunchSpeed(Charge);
            Projectile projectile = ProjectileManager.SpawnProjectile(_arrow.Projectile,
                _bowPose.LaunchPosition, velocity, _character);
            if (projectile == null)
            {
                CancelCharge();
                return;
            }

            InventoryManager.Instance.RemoveItem(_arrow, 1);
            if (!weapon.ShootSFX.IsNull)
                AudioManager.Instance.PlayOneShot(weapon.ShootSFX, transform.position);
            ClearCharge();
            _bowPose.ReleaseDraw();
        }

        public void CancelCharge()
        {
            ClearCharge();
            if (_bowPose != null)
                _bowPose.ResetPose();
        }

        private bool CanContinueCharge()
        {
            if (_character.Health.CurrentLifeState.Value == LifeState.Dead) return false;
            var slot = InventoryInputManager.Instance.GetActiveInvSlot();
            return slot == _sourceSlot && !slot.IsEmpty && slot.GetItemSO() == _weapon
                && _arrow != null && _arrow.Projectile != null && _arrow.Projectile.ProjectilePrefab != null
                && InventoryManager.Instance.GetTotalItemCount(_arrow) > 0;
        }

        private static ArrowItemSO FindArrow()
        {
            foreach (var slot in InventoryManager.Instance.PlayerInventory)
            {
                if (!slot.IsEmpty && slot.GetItemSO() is ArrowItemSO arrow
                    && arrow.Projectile != null && arrow.Projectile.ProjectilePrefab != null)
                    return arrow;
            }
            return null;
        }

        private void ClearCharge()
        {
            _weapon = null;
            _arrow = null;
            _sourceSlot = null;
            _chargeTime = 0f;
        }

        private void OnDisable() => CancelCharge();
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) CancelCharge();
        }
    }
}
