using System;
using UnityEngine;

namespace SketchLand
{
    /// <summary>Copies a dying character's appearance into an independent local visual.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ServerCharacter))]
    public sealed class CharacterDeathEffect : MonoBehaviour
    {
        [Serializable]
        public sealed class Settings
        {
            [Min(0.1f)] public float Lifetime = 5f;
            [Min(0f)] public float FadeDuration = 0.8f;
            [Min(0f)] public float Gravity = 22f;
            [Min(0f)] public float HorizontalKick = 2.5f;
            [Min(0f)] public float UpwardKick = 3.5f;
            [Range(0f, 15f)] public float Tumble = 4f;
            [Range(0f, 1f)] public float GroundFriction = 0.65f;
            [Min(0f)] public float AirDrag = 1.5f;
            [Range(0.02f, 0.2f)] public float BoneRadius = 0.055f;
        }

        [Serializable]
        public struct BoneOverride
        {
            public Transform Bone;
            [Range(0.02f, 0.3f)] public float Radius;
            [Tooltip("Maximum bend away from the captured pose, relative to its parent bone.")]
            [Range(0f, 180f)] public float BendLimit;
        }

        [SerializeField] private Transform _visuals;
        [SerializeField] private Settings _settings = new();
        [Tooltip("Optional joint tuning. Unlisted bones use the default radius and a loose bend limit.")]
        [SerializeField] private BoneOverride[] _boneOverrides = Array.Empty<BoneOverride>();

        private CharacterHealth _health;
        private SpriteRenderer[] _hiddenRenderers;
        private bool[] _rendererStates;

        private void Awake() => _health = GetComponent<CharacterHealth>();

        private void OnEnable()
        {
            _health.OnDied += OnDied;
            _health.CurrentLifeState.OnValueChanged += OnLifeStateChanged;
        }

        private void OnDisable()
        {
            _health.OnDied -= OnDied;
            _health.CurrentLifeState.OnValueChanged -= OnLifeStateChanged;
            RestoreRenderers();
        }

        private void OnDied(Vector2 velocity)
        {
            if (_hiddenRenderers != null) return;
            Transform visuals = _visuals != null ? _visuals : GetComponent<ServerCharacter>().Animator?.transform;
            if (visuals == null || WorldManager.Instance == null || !WorldManager.Instance.IsWorldReady) return;
            if (!CharacterRagdollVisual.TryCreate(visuals, _settings, _boneOverrides, velocity)) return;

            // Only visibility changes on the original. Player death/respawn and NPC despawn stay in control.
            _hiddenRenderers = visuals.GetComponentsInChildren<SpriteRenderer>(true);
            _rendererStates = new bool[_hiddenRenderers.Length];
            for (int i = 0; i < _hiddenRenderers.Length; i++)
            {
                _rendererStates[i] = _hiddenRenderers[i].enabled;
                _hiddenRenderers[i].enabled = false;
            }
        }

        private void OnLifeStateChanged(LifeState previous, LifeState current)
        {
            if (current == LifeState.Alive) RestoreRenderers();
        }

        private void RestoreRenderers()
        {
            if (_hiddenRenderers == null) return;
            for (int i = 0; i < _hiddenRenderers.Length; i++)
                if (_hiddenRenderers[i] != null) _hiddenRenderers[i].enabled = _rendererStates[i];
            _hiddenRenderers = null;
            _rendererStates = null;
        }
    }
}
