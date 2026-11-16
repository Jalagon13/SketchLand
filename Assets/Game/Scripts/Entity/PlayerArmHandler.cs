using DG.Tweening;
using UnityEngine;

namespace SketchLand
{
    [DefaultExecutionOrder(-30)]
    public class PlayerArmHandler : MonoBehaviour
    {
        [SerializeField] private Transform _armPivot;
        [SerializeField] private Transform _arm;
        [SerializeField] private PlayerHandGripIK _handGripIK;
        [SerializeField] private SwingPose _pose = new();

        public bool IsSwinging => _swingSequence != null;
        private Sequence _swingSequence;
        private SwingSlashEffect _slash;
        private ServerCharacter _character;
        private PlayerHeldItemDisplay _heldItemDisplay;
        private PlayerRangedHandler _rangedHandler;
        private static readonly int LocomotionPlayback = Animator.StringToHash("LocomotionPlayback");
#if UNITY_EDITOR
        private StrikeZone _lastStrike;
        private float _hideStrikeTime;
#endif

        private void Awake()
        {
            _character = GetComponent<ServerCharacter>();
            _rangedHandler = GetComponent<PlayerRangedHandler>();
            _heldItemDisplay = GetComponent<PlayerHeldItemDisplay>();
            _pose.Initialize(_arm, _armPivot);
            _slash = new SwingSlashEffect(_armPivot, _heldItemDisplay.HeldItemSprite);
            _armPivot.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (IsSwinging && _character.Health.CurrentLifeState.Value == LifeState.Dead) CancelSwing();
            _pose.RestorePose();
            UpdateLocomotionPlayback();
        }

        private void LateUpdate()
        {
            if (!IsSwinging) return;
            _pose.Apply(_swingSequence.Elapsed());
            _heldItemDisplay.UpdateSwingTarget();
        }

        private void OnDisable()
        {
            CancelSwing();
            if (_character != null && _character.Animator != null)
                _character.Animator.SetFloat(LocomotionPlayback, 1f);
        }

        public void CancelSwing() => _swingSequence?.Kill();

        // Input supplies the actor and committed world point. Only this local action
        // pays stamina and schedules gameplay damage; the presentation only needs its impact callback.
        public bool TrySwing(Player player, ToolItemSO tool, Vector2 aimPoint)
        {
            if (!isActiveAndEnabled || IsSwinging || tool == null || player == null || player.Character != _character)
                return false;
            if (_character.Health.CurrentLifeState.Value == LifeState.Dead || !player.Stamina.TrySpendSwing())
                return false;

            _rangedHandler.CancelCharge();
            _character.SetFacingOverride(aimPoint.x - transform.position.x);
            Vector2 localAim = _armPivot.parent.InverseTransformVector(aimPoint - (Vector2)_armPivot.position);
            localAim = localAim.sqrMagnitude < 0.0001f ? Vector2.right : localAim.normalized;
            Vector2 direction = _armPivot.parent.TransformVector(localAim).normalized;
            float halfArc = Mathf.Clamp(tool.SwingArcAngle, 1, 360) * 0.5f;
            float reach = Mathf.Max(0.01f, tool.StrikeReach);
            var target = MiningManager.Instance.CaptureTarget(player, tool, aimPoint);
            PlaySwing(tool, localAim, halfArc, reach, Mathf.Max(0.01f, tool.SwingsPerSecond), () =>
            {
                if (player.Character != _character || _character.Health.CurrentLifeState.Value == LifeState.Dead) return;
                ResolveImpact(player, tool, new StrikeZone(_armPivot.position, direction, halfArc, reach), target);
            });
            UpdateLocomotionPlayback();
            return true;
        }

        private void PlaySwing(ToolItemSO tool, Vector2 localAim, float halfArc, float reach, float rate, TweenCallback onImpact)
        {
            _pose.Begin(localAim);
            float centerAngle = Mathf.Atan2(localAim.y, localAim.x) * Mathf.Rad2Deg;
            float anticipation = SwingPose.Exaggeration * Mathf.Min(32f, halfArc * 0.4f);
            float startAngle = centerAngle + halfArc + anticipation;
            float endAngle = centerAngle - halfArc - anticipation;

            _arm.localScale = new Vector3(-1f, 1f, 1f);
            _arm.localRotation = Quaternion.Euler(0f, 0f, 180f);

            _armPivot.localRotation = Quaternion.Euler(0f, 0f, centerAngle);
            _armPivot.gameObject.SetActive(true);

            _heldItemDisplay.BeginSwing(tool);

            _handGripIK.BeginGrip(rate);
            const float windUp = SwingPose.WindUp;
            const float slash = 0.10f;
            _swingSequence = DOTween.Sequence().SetAutoKill(true);
            _swingSequence.timeScale = rate;
            _swingSequence.Append(RotateArm(centerAngle, startAngle, windUp, Ease.InOutCubic));
            _swingSequence.Join(DOVirtual.Float(0f, 1f, windUp,
                _heldItemDisplay.SetTransitionProgress).SetEase(Ease.Linear));
            _swingSequence.AppendCallback(_heldItemDisplay.FinishRaise);

            // Match the slash exit speed to the start of the cubic slowdown.
            const float followDuration = 0.30f;
            float followArc = SwingPose.Exaggeration * halfArc * 0.56f;
            float exitSlope = (3f * followArc / followDuration) * slash / (startAngle - endAngle);
            var slashCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.5f, 0.5f, 1.65f, 1.65f), new Keyframe(1f, 1f, exitSlope, exitSlope));
            _swingSequence.Append(DOVirtual.Float(startAngle, endAngle, slash, angle =>
            {
                _armPivot.localRotation = Quaternion.Euler(0f, 0f, angle);
                _slash.Draw(Mathf.Clamp01((centerAngle + halfArc - angle) / (2f * halfArc)),
                    centerAngle, halfArc, reach);
            }).SetEase(slashCurve));
            _swingSequence.InsertCallback(windUp + slash * 0.5f, onImpact);

            _swingSequence.Append(RotateArm(endAngle, endAngle - followArc, followDuration, Ease.OutCubic));
            // Let the trailing point catch up to the blade after it leaves the zone.
            _swingSequence.Insert(windUp + slash, DOVirtual.Float(1f, 1.82f, 0.075f,
                travel => _slash.Draw(travel, centerAngle, halfArc, reach)).SetEase(Ease.OutQuad));
            _swingSequence.AppendCallback(() =>
            {
                _heldItemDisplay.BeginReturn();
                _handGripIK.ReleaseGrip();
            });
            _swingSequence.Append(DOVirtual.Float(0f, 1f, 0.18f,
                _heldItemDisplay.SetTransitionProgress).SetEase(Ease.Linear));

            _swingSequence.OnKill(EndSwing);
        }

        private void EndSwing()
        {
            _swingSequence = null;
            _pose.End();
            _slash.Hide();
            _character.ClearFacingOverride();
            _handGripIK.ResetGrip();
            _heldItemDisplay.EndSwing();
            _armPivot.gameObject.SetActive(false);
        }

        private void ResolveImpact(Player player, ToolItemSO tool, StrikeZone zone, MiningManager.SwingTarget? target)
        {
#if UNITY_EDITOR
            _lastStrike = zone;
            _hideStrikeTime = Time.time + 0.18f;
#endif
            var entities = EntityManager.Instance.Entities;
            var hit = new SyncHitData(tool.MeleeDamage, tool.KnockbackForce, _character.transform.position);
            for (int i = entities.Count - 1; i >= 0; i--)
            {
                if (entities[i] is ServerCharacter enemy && enemy != _character && enemy.DamageReceiver != null
                    && zone.Intersects(enemy.transform.position, enemy.ColliderSize))
                    enemy.DamageReceiver.ReceiveHit(hit);
            }
            if (target.HasValue) MiningManager.Instance.TryPerformMine(player, tool, target.Value);
            AudioManager.Instance.PlayOneShot(FMODEvents.Instance.PlayerAirThrustSFX, transform.position);
        }

        private void UpdateLocomotionPlayback()
        {
            // Use actual motion so reversing input does not reverse the
            // footsteps before the character has changed travel direction.
            bool isMovingBackwards = (IsSwinging || _rangedHandler.IsAiming) && _character.Velocity.x * _character.FacingDirection < -0.05f;

            _character.Animator.SetFloat(LocomotionPlayback, isMovingBackwards ? -1f : 1f);
        }

        private Tween RotateArm(float from, float to, float duration, Ease ease)
        {
            return DOVirtual.Float(from, to, duration,
                angle => _armPivot.localRotation = Quaternion.Euler(0f, 0f, angle)).SetEase(ease);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || Time.time >= _hideStrikeTime) return;
            Vector3 edge = Quaternion.Euler(0f, 0f, -_lastStrike.HalfAngle) * _lastStrike.Direction;
            UnityEditor.Handles.color = new Color(0f, 1f, 1f, 0.25f);
            UnityEditor.Handles.DrawSolidArc(_lastStrike.Origin, Vector3.forward, edge, 2f * _lastStrike.HalfAngle, _lastStrike.Reach);
        }
#endif

    }
}
