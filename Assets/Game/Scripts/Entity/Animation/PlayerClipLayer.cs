using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SketchLand
{
    /// <summary>One reusable clip layer above the existing locomotion controller.</summary>
    internal sealed class PlayerClipLayer
    {
        private PlayableGraph _graph;
        private AnimatorControllerPlayable _base;
        private AnimationLayerMixerPlayable _layers;
        private AnimationClipPlayable _clip;
        private PlayerAnimationProfile _profile;
        private float _elapsed;
        public bool IsPlaying => _profile != null;

        public void Initialize(Animator animator)
        {
            _graph = PlayableGraph.Create("Player animation");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _base = AnimatorControllerPlayable.Create(_graph, animator.runtimeAnimatorController);
            _layers = AnimationLayerMixerPlayable.Create(_graph, 2);
            _graph.Connect(_base, 0, _layers, 0);
            _layers.SetInputWeight(0, 1f);
            var output = AnimationPlayableOutput.Create(_graph, "Player pose", animator);
            output.SetSourcePlayable(_layers);
            _graph.Play();
        }
        public void SetFloat(string name, float value) => _base.SetFloat(name, value);
        public void SetBool(string name, bool value) => _base.SetBool(name, value);
        public void PlayDamage() => _base.Play("Base Layer.StickFigure_Damaged", 0, 0f);

        public void Play(PlayerAnimationProfile profile)
        {
            if (profile == null || profile.Clip == null) return;
            Stop();
            _profile = profile;
            _elapsed = 0f;
            _clip = AnimationClipPlayable.Create(_graph, profile.Clip);
            _clip.SetApplyFootIK(false);
            _clip.SetApplyPlayableIK(false);
            _clip.SetSpeed(Mathf.Max(0.001f, profile.Speed));
            _graph.Connect(_clip, 0, _layers, 1);
            // Explicitly replace the mask, including when returning to a full-body action.
            _layers.SetLayerAdditive(1, profile.Additive);
            _layers.SetLayerMaskFromAvatarMask(1, profile.Mask != null ? profile.Mask : FullBodyMask);
        }
        private AvatarMask _fullBodyMask;
        private AvatarMask FullBodyMask
        {
            get
            {
                if (_fullBodyMask == null)
                {
                    _fullBodyMask = new AvatarMask();
                    _fullBodyMask.AddTransformPath(_root, true);
                    string prefix = _fullBodyMask.GetTransformPath(0);
                    for (int i = 0; i < _fullBodyMask.transformCount; i++)
                    {
                        string path = _fullBodyMask.GetTransformPath(i);
                        _fullBodyMask.SetTransformPath(i, i == 0 ? "" : path.Substring(prefix.Length + 1));
                        _fullBodyMask.SetTransformActive(i, true);
                    }
                }
                return _fullBodyMask;
            }
        }
        private Transform _root;
        public void InitializeMask(Transform root) => _root = root;

        public void Tick(float deltaTime)
        {
            if (_profile == null) return;
            _elapsed += deltaTime;
            float duration = _profile.Clip.length / Mathf.Max(0.001f, _profile.Speed);
            float fadeIn = _profile.BlendIn <= 0f ? 1f : Mathf.Clamp01(_elapsed / _profile.BlendIn);
            float fadeOut = _profile.BlendOut <= 0f ? 1f : Mathf.Clamp01((duration - _elapsed) / _profile.BlendOut);
            _layers.SetInputWeight(1, Mathf.Min(fadeIn, fadeOut));
            if (_elapsed >= duration) Stop();
        }
        public void Stop()
        {
            if (_clip.IsValid())
            {
                _layers.DisconnectInput(1);
                _graph.DestroyPlayable(_clip);
            }
            if (_layers.IsValid()) _layers.SetInputWeight(1, 0f);
            _profile = null;
        }
        public void Dispose()
        {
            if (_graph.IsValid()) _graph.Destroy();
            if (_fullBodyMask != null) Object.Destroy(_fullBodyMask);
            _profile = null;
        }
    }
}
