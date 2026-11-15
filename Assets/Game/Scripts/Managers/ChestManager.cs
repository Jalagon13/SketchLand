using System;
using System.Collections.Generic;
using UnityEngine;

namespace SketchLand
{
    public class ChestManager : MonoBehaviour
    {
        public static ChestManager Instance { get; private set; }

        public event Action<ChestData> OnChestOpened;
        public event Action OnChestClosed;
        public event Action OnChestInventoryChanged;

        [field: SerializeField] public int DefaultChestSize { get; private set; } = 20;

        private readonly Dictionary<Vector2Int, ChestData> _chests = new();
        public ChestData CurrentOpenChest { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        public ChestData GetOrCreateChest(Vector2Int rootPos)
        {
            if (!_chests.TryGetValue(rootPos, out var chest))
            {
                chest = new ChestData(rootPos, DefaultChestSize);
                _chests[rootPos] = chest;
            }
            
            return chest;
        }

        public ChestData GetChest(Vector2Int rootPos)
        {
            _chests.TryGetValue(rootPos, out var chest);
            return chest;
        }

        public void OpenChest(Vector2Int rootPos)
        {
            CurrentOpenChest = GetOrCreateChest(rootPos);

            // Automatically open player inventory UI so items can be transferred
            if (!InventoryInputManager.Instance.IsInventoryOpen)
            {
                InventoryInputManager.Instance.OnToggleInventory();
            }
            else
            {
                AudioManager.Instance.PlayOneShot(FMODEvents.Instance.InventoryOpenSFX, default);
            }
            
            OnChestOpened?.Invoke(CurrentOpenChest);
        }

        public void CloseChest()
        {
            if (CurrentOpenChest == null) return;
            CurrentOpenChest = null;
            OnChestClosed?.Invoke();
        }

        public void NotifyChestChanged()
        {
            OnChestInventoryChanged?.Invoke();
        }

        public void DestroyChest(Vector2Int rootPos)
        {
            if (_chests.TryGetValue(rootPos, out var chest))
            {
                // Drop all items into the world at chest position
                Vector2 dropPos = new Vector2(rootPos.x + 0.5f, rootPos.y + 0.5f);
                foreach (var slot in chest.Slots)
                {
                    if (!slot.IsEmpty)
                    {
                        var itemSO = GameDataRegistry.Instance.GetItemSOFromItemId(slot.ItemId);
                        GameManager.Instance.SpawnItem(itemSO, slot.Amount, dropPos);
                        slot.Clear();
                    }
                }

                if (CurrentOpenChest == chest)
                {
                    CloseChest();
                }

                _chests.Remove(rootPos);
            }
        }
    }
}
