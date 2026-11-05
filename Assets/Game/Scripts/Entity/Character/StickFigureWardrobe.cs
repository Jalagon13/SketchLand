using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SketchLand
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class StickFigureWardrobe : MonoBehaviour
    {
        [Header("Attachment Points")]
        [SerializeField] private List<VisualAttachmentPoint> _attachmentPoints = new();
        [Header("Initial Wearables")]
        [Tooltip("Later entries win when visual priorities match.")]
        [SerializeField] private List<WearableItemSO> _initialItems = new();
        public IReadOnlyList<WearableItemSO> InitialItems => _initialItems;
        public int EquippedVisualCount => _equippedItems.Count;

        private readonly Dictionary<int, WearableItemSO> _equippedItems = new();
        private readonly List<int> _initialHandles = new();
        private readonly List<WearableItemSO> _appliedInitialItems = new();
        private readonly Dictionary<VisualAttachmentPointId, VisualAttachmentPoint> _points = new();
        private readonly List<VisualAttachmentPoint> _previousPoints = new();
        private readonly Dictionary<VisualAttachmentPointId, WinningPiece> _winners = new();
        private readonly HashSet<VisualAttachmentPointId> _itemPoints = new();
        private int _nextHandleId;
        private bool _initialized;
        private volatile bool _visualDirty;

        private struct WinningPiece
        {
            public int EntryId;
            public int Priority;
            public Sprite Sprite;
        }

        private void OnEnable()
        {
            ApplyVisuals();
#if UNITY_EDITOR
            EditorApplication.update += ApplyEditorVisuals;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= ApplyEditorVisuals;
#endif
        }

        private void OnValidate() => _visualDirty = true;

        private void Update()
        {
            if (_visualDirty)
                ApplyVisuals();
        }

#if UNITY_EDITOR
        private void ApplyEditorVisuals()
        {
            // Asset edits do not invoke OnValidate on the rig using the asset.
            if (this != null && isActiveAndEnabled)
                ApplyVisuals();
        }
#endif

        public WardrobeHandle EquipVisual(WearableItemSO item)
        {
            if (item == null)
                return default;

            SynchronizeInitialItems();
            int id = AddEntry(item);
            ApplyVisuals();
            
            return new WardrobeHandle(this, id);
        }

        public bool IsEquipped(WardrobeHandle handle)
        {
            return ReferenceEquals(handle.Owner, this) && _equippedItems.ContainsKey(handle.Id);
        }

        public bool UnequipVisual(WardrobeHandle handle)
        {
            if (!IsEquipped(handle))
                return false;

            _equippedItems.Remove(handle.Id);
            _initialHandles.Remove(handle.Id);
            ApplyVisuals();
            
            return true;
        }

        public WardrobeHandle[] GetEquippedHandles()
        {
            SynchronizeInitialItems();
            var handles = new WardrobeHandle[_equippedItems.Count];
            int index = 0;
            
            foreach (int id in _equippedItems.Keys)
                handles[index++] = new WardrobeHandle(this, id);
                
            return handles;
        }

        public void ClearVisuals()
        {
            SynchronizeInitialItems();
            _equippedItems.Clear();
            _initialHandles.Clear();
            ApplyVisuals();
        }

        private int AddEntry(WearableItemSO item)
        {
            int id = ++_nextHandleId;
            _equippedItems.Add(id, item);
            return id;
        }

        private void SynchronizeInitialItems()
        {
            bool changed = !_initialized || _initialItems.Count != _appliedInitialItems.Count;
            if (!changed)
            {
                for (int i = 0; i < _initialItems.Count; i++)
                    if (_initialItems[i] != _appliedInitialItems[i])
                    {
                        changed = true;
                        break;
                    }
            }
            if (!changed)
                return;

            // Editing the initial outfit replaces only its entries; runtime entries survive.
            foreach (int id in _initialHandles)
                _equippedItems.Remove(id);
                
            _initialHandles.Clear();
            _appliedInitialItems.Clear();
            
            foreach (WearableItemSO item in _initialItems)
            {
                _appliedInitialItems.Add(item);
                if (item != null)
                    _initialHandles.Add(AddEntry(item));
            }
            
            _initialized = true;
        }

        public void ApplyVisuals()
        {
            SynchronizeInitialItems();
            
            _previousPoints.Clear();
            _previousPoints.AddRange(_points.Values);
            _points.Clear();
            
            foreach (VisualAttachmentPoint point in _attachmentPoints)
            {
                if (point == null || point.Id == VisualAttachmentPointId.None)
                    continue;
                // First registration owns the identity if it is accidentally duplicated.
                _points.TryAdd(point.Id, point);
            }
            
            foreach (VisualAttachmentPoint previous in _previousPoints)
                if (previous != null && !_points.ContainsValue(previous))
                    previous.SetSprite(null);

            _winners.Clear();
            
            foreach (var entry in _equippedItems)
            {
                WearableVisualData visuals = entry.Value != null ? entry.Value.Visuals : null;
                
                if (visuals == null)
                    continue;

                _itemPoints.Clear();
                
                foreach (WearableVisualPiece piece in visuals.Pieces)
                {
                    if (piece == null || piece.Sprite == null || !_points.ContainsKey(piece.AttachmentPoint) || !_itemPoints.Add(piece.AttachmentPoint))
                        continue;

                    if (!_winners.TryGetValue(piece.AttachmentPoint, out WinningPiece winner)
                        || visuals.Priority > winner.Priority
                        || (visuals.Priority == winner.Priority && entry.Key > winner.EntryId))
                        _winners[piece.AttachmentPoint] = new WinningPiece
                        {
                            EntryId = entry.Key,
                            Priority = visuals.Priority,
                            Sprite = piece.Sprite
                        };
                }
            }
            
            foreach (var point in _points)
                point.Value.SetSprite(_winners.TryGetValue(point.Key, out WinningPiece winner) ? winner.Sprite : null);
                
            _visualDirty = false;
        }
    }
}
