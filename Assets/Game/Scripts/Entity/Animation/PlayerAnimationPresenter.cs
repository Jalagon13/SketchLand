using System;
using System.Collections.Generic;
using UnityEngine;

namespace SketchLand
{
    /// <summary>Reads gameplay. Owns Animator parameters, action visuals, IK requests and sounds.</summary>
    [DefaultExecutionOrder(-30)]
    public sealed class PlayerAnimationPresenter : MonoBehaviour
    {
        [SerializeField] private PlayerAnimationProfile _defaultSwing;
        [SerializeField] private PlayerAnimationProfile _defaultBow;
        [SerializeField] private ItemAnimationBinding[] _itemAnimations = Array.Empty<ItemAnimationBinding>();
        [SerializeField] private CueAnimationBinding[] _cueAnimations = Array.Empty<CueAnimationBinding>();
        [SerializeField] private SwingAnimationPose _swing = new();
        [SerializeField] private BowAnimationPose _bow = new();
        private PlayerActionController _actions;
        private ServerCharacter _character;
        private PlayerClipLayer _clips;
        private uint _revision, _shot, _impact;
        private bool _initialized, _wasAirborne;
        private readonly Queue<string> _cues = new();

        private void Awake()
        {
            _actions = GetComponent<PlayerActionController>();
            _character = GetComponent<ServerCharacter>();
        }
        private void Start()
        {
            var display = GetComponent<PlayerHeldItemDisplay>();
            _swing.Initialize(display);
            _bow.Initialize(display);
            _initialized = true;
            Connect();
        }
        private void OnEnable() { if (_initialized) Connect(); }
        private void Connect()
        {
            _clips = new PlayerClipLayer();
            _clips.InitializeMask(_character.Animator.transform);
            _clips.Initialize(_character.Animator);
            _actions.Cue += OnCue;
            _character.Health.OnDamaged += OnDamaged;
            _revision = uint.MaxValue;
            _shot = _actions.ShotSequence;
            _impact = _actions.ImpactSequence;
        }
        private void OnCue(string cue) => _cues.Enqueue(cue);
        private void OnDamaged() => _cues.Enqueue("Damaged");

        private void Update()
        {
            if (!_initialized) return;
            // Remove only our last additive offsets before the Animator samples the next frame.
            _swing.RestorePose();
            _bow.RestorePose();
            var root = _character.RootState as PlayerRootState;
            bool airborne = root != null ? root.IsAirborne : !_character.CollisionResult.TouchingBottom;
            float speedSquared = _character.Velocity.x * _character.Velocity.x;
            _clips.SetFloat("Velocity", speedSquared);
            _clips.SetBool("IsAirborne", airborne);
            _clips.SetBool("IsSprinting", !airborne && !_character.IsKnockedBack && root != null
                && root.IsSprinting && Mathf.Abs(_character.DesiredDirection.x) > 0.01f && speedSquared > 0.8f);
            _clips.SetFloat("LocomotionPlayback", _actions.IsAiming
                && _character.Velocity.x * _character.FacingDirection < -0.05f ? -1f : 1f);
            if (airborne != _wasAirborne) _cues.Enqueue(airborne ? "Airborne" : "Landed");
            _wasAirborne = airborne;
            _clips.Tick(Time.deltaTime);
            while (_cues.Count > 0)
            {
                string cue = _cues.Dequeue();
                bool handled = false;
                foreach (var binding in _cueAnimations)
                    if (binding.Cue == cue && binding.Animation != null && binding.Animation.Style == PlayerAnimationStyle.Clip)
                    {
                        if (!_actions.IsAiming) _clips.Play(binding.Animation);
                        handled = true;
                        break;
                    }
                if (cue == "Damaged" && !handled) _clips.PlayDamage();
            }
        }
        private void LateUpdate()
        {
            if (!_initialized) return;
            if (_revision != _actions.Revision)
            {
                _swing.ResetPose();
                _bow.ResetPose();
                _revision = _actions.Revision;
                if (_actions.IsAiming) _clips.Stop();
                if (_actions.Kind == PlayerActionKind.Swing)
                {
                    var profile = ProfileFor(_actions.Item, _defaultSwing);
                    if (profile != null) _swing.Begin(_actions, profile);
                }
                else if (_actions.Kind == PlayerActionKind.BowCharge || _actions.Kind == PlayerActionKind.BowReleased)
                {
                    var profile = ProfileFor(_actions.Item, _defaultBow);
                    if (profile != null) _bow.BeginDraw((BowItemSO)_actions.Item, _actions.Arrow, profile, _actions.AimDirection);
                }
            }
            if (_actions.Kind == PlayerActionKind.Swing) _swing.Sample(_actions);
            else _bow.Sample(_actions);
            if (_impact != _actions.ImpactSequence)
            {
                _impact = _actions.ImpactSequence;
                AudioManager.Instance.PlayOneShot(FMODEvents.Instance.PlayerAirThrustSFX, transform.position);
            }
            if (_shot != _actions.ShotSequence)
            {
                _shot = _actions.ShotSequence;
                if (_actions.Item is BowItemSO bow && !bow.ShootSFX.IsNull)
                    AudioManager.Instance.PlayOneShot(bow.ShootSFX, transform.position);
            }
        }
        private PlayerAnimationProfile ProfileFor(ItemSO item, PlayerAnimationProfile fallback)
        {
            foreach (var binding in _itemAnimations)
                if (binding.Item == item && binding.Animation != null && binding.Animation.Style == (item is BowItemSO ? PlayerAnimationStyle.Bow : PlayerAnimationStyle.Swing))
                    return binding.Animation;
            return fallback;
        }
        private void OnDisable()
        {
            if (!_initialized) return;
            _actions.Cue -= OnCue;
            _character.Health.OnDamaged -= OnDamaged;
            _swing.ResetPose();
            _bow.ResetPose();
            _clips.Dispose();
            _cues.Clear();
        }
    }

    [Serializable]
    public sealed class ItemAnimationBinding
    {
        public ItemSO Item;
        public PlayerAnimationProfile Animation;
    }
    [Serializable]
    public sealed class CueAnimationBinding
    {
        [Tooltip("Semantic gameplay event. Built-in: Damaged, Airborne, Landed. Custom: Actions.ReportCue(name).")]
        public string Cue;
        public PlayerAnimationProfile Animation;
    }
}
