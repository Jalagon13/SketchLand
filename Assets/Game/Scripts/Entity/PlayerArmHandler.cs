using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

namespace SketchLand
{
    public class PlayerArmHandler : MonoBehaviour
    {
        [SerializeField] private Transform _armPivot;
        [SerializeField] private Transform _hitboxPoint;
        [SerializeField] private Transform _arm;
        [SerializeField] private PlayerHandGripIK _handGripIK;

        public bool IsSwinging { get; private set; }

        private Sequence _swingSequence;
        private float _nextSwingTime;
        private readonly HashSet<Entity> _hitEntitiesThisSwing = new();
        private bool _isHitboxActive;
        private ToolItemSO _currentTool;
        private ServerCharacter _character;
        private PlayerHeldItemDisplay _heldItemDisplay;
        private PlayerRangedHandler _rangedHandler;
        private static readonly int LocomotionPlayback = Animator.StringToHash("LocomotionPlayback");

        private void Awake() 
        {
            _character = GetComponent<ServerCharacter>();
            _rangedHandler = GetComponent<PlayerRangedHandler>();
            _heldItemDisplay = GetComponent<PlayerHeldItemDisplay>();
            _armPivot.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            _swingSequence?.Kill();
            EndSwing();
            if (_character != null && _character.Animator != null)
                _character.Animator.SetFloat(LocomotionPlayback, 1f);
        }

        private void Update()
        {
            UpdateLocomotionPlayback();

            if (!_isHitboxActive) return;
            
            CheckWeaponHits();
        }

        private void UpdateLocomotionPlayback()
        {
            // Use actual motion so reversing input does not reverse the
            // footsteps before the character has changed travel direction.
            bool isMovingBackwards = (IsSwinging || _rangedHandler.IsAiming) && _character.Velocity.x * _character.FacingDirection < -0.05f;
            
            _character.Animator.SetFloat(LocomotionPlayback, isMovingBackwards ? -1f : 1f);
        }

        private void OnDrawGizmos()
        {
            if (_hitboxPoint != null && _isHitboxActive)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(_hitboxPoint.position, _currentTool?.HitboxSize ?? Player.Instance.HitboxSize);
            }
        }

        public void CancelSwing()
        {
            _swingSequence?.Kill();
            EndSwing();
        }

        public void Swing(ItemSO item, bool applyDamage = true)
        {
            if (IsSwinging || (applyDamage && Time.time < _nextSwingTime))
                return;

            // Clean up any previous sequence before initializing this swing.
            _swingSequence?.Kill();
            _swingSequence = null;

            AudioManager.Instance.PlayOneShot(FMODEvents.Instance.PlayerAirThrustSFX, transform.position);

            IsSwinging = true;
            var tool = applyDamage ? item as ToolItemSO : null;
            var player = Player.Instance;
            _currentTool = tool;
            _rangedHandler.CancelCharge();

            // Convert world-space aim into the mirrored Visuals local space.
            Vector3 worldAim = (Vector3)WorldManager.MouseWorldPosition - transform.position;
            worldAim.z = 0f;

            // Turn before converting aim into the mirrored local space.
            // Keep this facing through the swing and its IK transitions.
            _character.SetFacingOverride(worldAim.x);
            UpdateLocomotionPlayback();

            Vector3 localAim = _armPivot.parent.InverseTransformVector(worldAim);

            if (localAim.sqrMagnitude < 0.0001f)
                localAim = Vector3.right;

            float centerAngle = Mathf.Atan2(localAim.y, localAim.x) * Mathf.Rad2Deg;
            float halfArc = (tool?.SwingArcAngle ?? player.SwingArcAngle) * 0.5f;
            float startAngle = centerAngle + halfArc;
            float endAngle = centerAngle - halfArc;

            _hitboxPoint.localPosition = new Vector3(tool?.HitboxOffset ?? player.HitboxOffset, 0f, 0f);

            // Keep the old working RIGHT-facing art orientation constant.
            // Visuals now supplies the character's left/right reflection.
            _arm.localScale = new Vector3(-1f, 1f, 1f);
            _arm.localRotation = Quaternion.Euler(0f, 0f, 180f);

            _armPivot.localRotation = Quaternion.Euler(0f, 0f, startAngle);
            _armPivot.gameObject.SetActive(true);

            _heldItemDisplay.BeginSwing(item);

            _hitEntitiesThisSwing.Clear();
            _isHitboxActive = false;

            _handGripIK.BeginGrip();

            _swingSequence = DOTween.Sequence();

            // Lift the visible held item into the windup while IK takes over.
            _swingSequence.Append(DOVirtual.Float(0f, 1f,
                _handGripIK.BlendDuration, _heldItemDisplay.SetTransitionProgress).SetEase(Ease.Linear));

            // Damage begins only once the hand has reached the grip.
            _swingSequence.AppendCallback(() =>
            {
                _heldItemDisplay.FinishRaise();
                _isHitboxActive = applyDamage;
            });

            float duration = tool?.SwingDuration ?? player.SwingDuration;
            float miningImpactTime = _swingSequence.Duration() + duration * 0.5f;

            _swingSequence.Append(
            DOVirtual.Float(
                startAngle,
                endAngle,
                duration,
                angle => _armPivot.localRotation =
                    Quaternion.Euler(0f, 0f, angle))
            .SetEase(Ease.Linear));

            // One mining attempt per swing, halfway through the active arc.
            // Killing the sequence before impact also cancels the mining hit.
            if (applyDamage)
            {
                _swingSequence.InsertCallback(miningImpactTime, () =>
                {
                    if (Player.Instance != null && Player.Instance.Character == _character)
                        MiningManager.Instance?.TryPerformMine(tool);
                });
            }

            // Bring the same item back to the animated hand before reattaching.
            _swingSequence.AppendCallback(() =>
            {
                _isHitboxActive = false;
                if (applyDamage)
                    _nextSwingTime = Time.time + (tool?.SwingCooldown ?? player.SwingCooldown);
                _heldItemDisplay.BeginReturn();
                _handGripIK.ReleaseGrip();
            });
            _swingSequence.Append(DOVirtual.Float(0f, 1f,
                _handGripIK.BlendDuration, _heldItemDisplay.SetTransitionProgress).SetEase(Ease.Linear));

            _swingSequence.OnComplete(EndSwing);
            _swingSequence.OnKill(EndSwing);
        }

        private void EndSwing()
        {
            if (_character != null)
                _character.ClearFacingOverride();

            _isHitboxActive = false;
            _currentTool = null;
            _hitEntitiesThisSwing.Clear();

            if (_handGripIK != null)
                _handGripIK.ResetGrip();
            if (_heldItemDisplay != null)
                _heldItemDisplay.EndSwing();
            if (_armPivot != null)
                _armPivot.gameObject.SetActive(false);

            IsSwinging = false;
            _swingSequence = null;
        }

        private void CheckWeaponHits()
        {
            Vector2 hitCenter = _hitboxPoint.position;

            var entities = EntityManager.Instance.Entities;

            for (int i = 0; i < entities.Count; i++)
            {
                Entity entity = entities[i];
                if (entity == null) continue;

                // Skip self (Player) and already-hit targets
                if (entity == Player.Instance.Character) continue;
                if (entity is not ServerCharacter) continue;
                if (_hitEntitiesThisSwing.Contains(entity)) continue;

                // Check AABB overlap
                bool isOverlapping = GridPhysics.IsOverlapping(hitCenter, _currentTool?.HitboxSize ?? Player.Instance.HitboxSize, entity.transform.position, entity.ColliderSize);

                if (isOverlapping)
                {
                    _hitEntitiesThisSwing.Add(entity);

                    if (entity is ServerCharacter enemy && enemy.DamageReceiver != null)
                    {
                        var hitData = new SyncHitData(_currentTool?.MeleeDamage ?? Player.Instance.MeleeDamage, _currentTool?.KnockbackForce ?? Player.Instance.KnockbackForce, Player.Instance.Character.transform.position);
                        enemy.DamageReceiver.ReceiveHit(hitData);
                    }
                }
            }
        }
    }
}
