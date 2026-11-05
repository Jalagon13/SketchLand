using System;
using System.Collections.Generic;
using UnityEngine;

namespace SketchLand
{
    [Serializable]
    public class WearableVisualData
    {
        [field: Tooltip("Higher priority wins at overlapping points. Equal priority favors the latest equipped entry.")]
        [field: SerializeField] public int Priority { get; private set; }
        [SerializeField] private List<WearableVisualPiece> _pieces = new();
        public IReadOnlyList<WearableVisualPiece> Pieces => _pieces;
    }

    [Serializable]
    public class WearableVisualPiece
    {
        [field: SerializeField] public VisualAttachmentPointId AttachmentPoint { get; private set; }
        [field: SerializeField] public Sprite Sprite { get; private set; }
    }
}
