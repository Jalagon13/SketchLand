using System;

namespace SketchLand
{
    [Serializable]
    public class BatRootState : CharacterRootState
    {
        public override CharacterRootState CreateInstance() => new BatRootState();
    }
}
