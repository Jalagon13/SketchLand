using System;

namespace SketchLand
{
    public readonly struct WardrobeHandle : IEquatable<WardrobeHandle>
    {
        internal StickFigureWardrobe Owner { get; }
        internal int Id { get; }
        public bool IsValid => Owner != null && Id > 0;

        internal WardrobeHandle(StickFigureWardrobe owner, int id)
        {
            Owner = owner;
            Id = id;
        }

        public bool Equals(WardrobeHandle other) => ReferenceEquals(Owner, other.Owner) && Id == other.Id;
        public override bool Equals(object obj) => obj is WardrobeHandle other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Owner, Id);
        public static bool operator ==(WardrobeHandle left, WardrobeHandle right) => left.Equals(right);
        public static bool operator !=(WardrobeHandle left, WardrobeHandle right) => !left.Equals(right);
    }
}
