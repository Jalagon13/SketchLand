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
        [SerializeField] private SpriteRenderer _swingToolSr;
        [SerializeField] private PlayerHandGripIK _handGripIK;

        public bool IsSwinging { get; private set; }
        public bool IsActionHeld { get; private set; }

        private Sequence _swingSequence;
        private readonly HashSet<Entity> _hitEntitiesThisSwing = new();
        private bool _isHitboxActive;
        private ToolItemSO _currentTool;
        private ServerCharacter _character;
        private static readonly int LocomotionPlayback = Animator.StringToHash("LocomotionPlayback");

        private void Awake() 
        {
            _character = GetComponent<ServerCharacter>();
            _armPivot.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            _swingSequence?.Kill();
            EndSwing();
            IsActionHeld = false;
            if (_character != null && _character.Animator != null)
                _character.Animator.SetFloat(LocomotionPlayback, 1f);
        }

        private void Update()
        {
            UpdateLocomotionPlayback();

            if (!_isHitboxActive || _currentTool == null) return;
            
            CheckWeaponHits();
        }

        private void UpdateLocomotionPlayback()
        {
            // Use actual motion so reversing input does not reverse the
            // footsteps before the character has changed travel direction.
            bool isMovingBackwards = IsSwinging && _character.Velocity.x * _character.FacingDirection < -0.05f;
            
            _character.Animator.SetFloat(LocomotionPlayback, isMovingBackwards ? -1f : 1f);
        }

        private void OnDrawGizmos()
        {
            if (_hitboxPoint != null && _isHitboxActive && _currentTool != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(_hitboxPoint.position, _currentTool.HitboxSize);
            }
        }

        public void StartPrimaryAction(ToolItemSO tool)
        {
            IsActionHeld = true;
            ExecuteSwing(tool);
        }
        
        public void HoldPrimaryAction(ToolItemSO tool)
        {
            ExecuteSwing(tool);
        }
        
        public void ReleasePrimaryAction(ToolItemSO tool)
        {
            IsActionHeld = false;
        }

        private void ExecuteSwing(ToolItemSO tool)
        {
            if (IsSwinging || tool == null)
                return;

            // Clean up any previous sequence before initializing this swing.
            _swingSequence?.Kill();
            _swingSequence = null;

            AudioManager.Instance.PlayOneShot(FMODEvents.Instance.PlayerAirThrustSFX, transform.position);

            IsSwinging = true;
            _currentTool = tool;

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
            float halfArc = tool.SwingArcAngle * 0.5f;
            float startAngle = centerAngle + halfArc;
            float endAngle = centerAngle - halfArc;
            float recoveryAngle = endAngle - tool.SwingEndArcAngle;

            _swingToolSr.sprite = tool.DisplayIcon;

            _hitboxPoint.localPosition = new Vector3(tool.HitboxOffset, 0f, 0f);

            // Keep the old working RIGHT-facing art orientation constant.
            // Visuals now supplies the character's left/right reflection.
            _arm.localScale = new Vector3(-1f, 1f, 1f);
            _arm.localRotation = Quaternion.Euler(0f, 0f, 180f);

            _armPivot.localRotation = Quaternion.Euler(0f, 0f, startAngle);
            _armPivot.gameObject.SetActive(true);

            _hitEntitiesThisSwing.Clear();
            _isHitboxActive = false;

            _handGripIK.BeginGrip();

            _swingSequence = DOTween.Sequence();

            // Hold the tool still while the hand moves onto its handle.
            _swingSequence.AppendInterval(_handGripIK.BlendDuration);

            // Damage begins only once the hand has reached the grip.
            _swingSequence.AppendCallback(() => _isHitboxActive = true);

            _swingSequence.Append(
            DOVirtual.Float(
                startAngle,
                endAngle,
                tool.SwingDuration,
                angle => _armPivot.localRotation =
                    Quaternion.Euler(0f, 0f, angle))
            .SetEase(Ease.InOutCubic));

            _swingSequence.AppendCallback(() => _isHitboxActive = false);

            _swingSequence.Append(
            DOVirtual.Float(
                endAngle,
                recoveryAngle,
                tool.SwingEndDuration,
                angle => _armPivot.localRotation =
                    Quaternion.Euler(0f, 0f, angle))
            .SetEase(Ease.Linear));

            // Keep the tool visible while the hand returns to locomotion.
            _swingSequence.AppendCallback(() => _handGripIK.ReleaseGrip());
            _swingSequence.AppendInterval(_handGripIK.BlendDuration);

            _swingSequence.OnComplete(EndSwing);
            _swingSequence.OnKill(EndSwing);
        }

        private void EndSwing()
        {
            _character.ClearFacingOverride();

            _isHitboxActive = false;
            _currentTool = null;
            _hitEntitiesThisSwing.Clear();

            _handGripIK.ResetGrip();
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
                bool isOverlapping = GridPhysics.IsOverlapping(hitCenter, _currentTool.HitboxSize, entity.transform.position, entity.ColliderSize);

                if (isOverlapping)
                {
                    // Debug.Log($"Hitting {entity.name} for {_currentTool.MeleeDamage} damage");
                    _hitEntitiesThisSwing.Add(entity);

                    if (entity is ServerCharacter enemy && enemy.DamageReceiver != null)
                    {
                        var hitData = new SyncHitData(_currentTool.MeleeDamage, _currentTool.KnockbackForce, Player.Instance.Character.transform.position);
                        enemy.DamageReceiver.ReceiveHit(hitData);
                    }
                }
            }
        }
    }
}
