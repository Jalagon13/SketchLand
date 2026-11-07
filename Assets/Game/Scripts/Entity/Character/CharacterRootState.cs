using System;

namespace SketchLand
{
    [Serializable]
    public abstract class CharacterRootState : State, IDisposable
    {
        protected ServerCharacter _ctx;
        
        // Parameterless constructor so Unity can instantiate it in the inspector
        public CharacterRootState() : base(null, null) { }

        // Create a runtime state from the template's configuration, without sharing runtime state.
        public abstract CharacterRootState CreateInstance();
        
        // Called on spawn to hook up the character context and initialize child states
        public virtual void Initialize(ServerCharacter ctx)
        {
            Dispose();
            _ctx = ctx;
            _ctx.Health.OnDamaged += OnDamaged;
        }

        public void Dispose()
        {
            if (_ctx != null && _ctx.Health != null)
                _ctx.Health.OnDamaged -= OnDamaged;

            _ctx = null;
        }

        protected virtual void OnDamaged() { }

        protected void PlayDamageAnimation(int stateHash)
        {
            var animator = _ctx.Animator;
            if (animator == null || !animator.isActiveAndEnabled) return;

            // Every hit samples time zero immediately, even during damage playback.
            animator.Play(stateHash, 0, 0f);
            animator.Update(0f);
        }
    }
}
