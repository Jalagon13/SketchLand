using System;
using System.Collections.Generic;

namespace SketchLand
{
    [Serializable]
    public abstract class State 
    {
        public readonly StateMachine Machine;
        public readonly State Parent;
        public State ActiveChild;

        public State(StateMachine machine, State parent = null)
        {
            Machine = machine;
            Parent = parent;
        }

        protected virtual State GetInitialState() => null; // Initial child to enter when this state starts (null = this is the leaf)
        protected virtual State GetTransition() => null; // Target state to switch to this frame (null = stay in current state)

        // Lifecycle hooks
        protected virtual void OnEnter() { }
        protected virtual void OnExit() { }
        protected virtual void OnUpdate(float deltaTime) { }
        protected virtual void OnFixedUpdate(float fixedDeltaTime) { }

        internal void Enter()
        {
            if(Parent != null) Parent.ActiveChild = this;
            OnEnter();
            State init = GetInitialState();
            if(init != null) init.Enter();
        }

        internal void Exit()
        {
            if(ActiveChild != null) ActiveChild.Exit();
            ActiveChild = null;
            OnExit();
        }

        internal void Update(float deltaTime)
        {
            State t = GetTransition();
            if(t != null)
            {
                Machine.Sequencer.RequestTransition(this, t);
                return;
            }

            if(ActiveChild != null) ActiveChild.Update(deltaTime);
            OnUpdate(deltaTime);
        }

        internal void FixedUpdate(float fixedDeltaTime)
        {
            if (ActiveChild != null) ActiveChild.FixedUpdate(fixedDeltaTime);
            OnFixedUpdate(fixedDeltaTime);
        }

        // Returns the deepest currently-active state descendant state (the leaf of the active path).
        public State Leaf()
        {
            State s = this;
            while(s.ActiveChild != null) s = s.ActiveChild;
            return s;
        }

        // Yields this state and then each ancestor up to the root (self -> parent -> ... -> root)
        public IEnumerable<State> PathToRoot()
        {
            for(State s = this; s != null; s = s.Parent) yield return s;
        }
    }
}
