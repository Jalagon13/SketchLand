using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SketchLand
{
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }
        
        public event Action OnPlayerInventoryChanged;

        [Header("Inventory Setup")]
        [field: SerializeField] public int InventorySize { get; private set; } = 40;
        [field: SerializeField] public int HotbarSize { get; private set; } = 10;
        [field: SerializeField] public int MaxStackSize { get; private set; } = 999;
        
        [SerializeField] private List<Ingredient> _startingItems = new();

        public InventorySlot[] PlayerInventory { get; private set; }
        
        private void Awake() 
        {
            Instance = this;

            // Initialize Inventory Slots
            PlayerInventory = new InventorySlot[InventorySize];
            for (int i = 0; i < PlayerInventory.Length; i++)
            {
                PlayerInventory[i] = new InventorySlot();
            }
        }
        
        private void Start() 
        {
            if(Player.Instance == null) return;
            Player.Instance.PlayerReady += OnPlayerReady;

            if (Player.Instance.Character != null)
                OnPlayerReady(Player.Instance.Character);
        }

        private void OnDestroy() 
        {
            if (Player.Instance == null) return;

            Player.Instance.PlayerReady -= OnPlayerReady;
        }

        private void OnPlayerReady(ServerCharacter character)
        {
            foreach (var startItem in _startingItems)
            {
                AddItem(startItem.Item.GetId(), startItem.Amount);
            }
        }

        public void RefreshInventory()
        {
            OnPlayerInventoryChanged?.Invoke();
            InventoryCursorManager.Instance.RefreshCursorSlot();
        }

        // Pickups and crafting share feedback for the amount actually added.
        public int CollectItem(ushort itemId, int amount)
        {
            int remainder = AddItem(itemId, amount);
            int collectedAmount = amount - remainder;
            if (collectedAmount > 0)
            {
                if (WorldTextPopupManager.Instance == null)
                    Debug.LogError("WorldTextPopupManager is missing from the scene.", this);
                else
                    WorldTextPopupManager.Instance.SpawnItemCollectPopup(GameDataRegistry.Instance.GetItemSOFromItemId(itemId), collectedAmount);
            }
            return remainder;
        }

        public int AddItem(ushort itemId, int amount) // Returns remainder 
        {
            if (GameDataRegistry.Instance.GetItemSOFromItemId(itemId) == null) return amount;
            if(Player.Instance.Character.Health.CurrentLifeState.Value == LifeState.Dead) return amount;
        
            // Search for existing matching stacks
            for (int i = 0; i < PlayerInventory.Length; i++)
            {
                if (!PlayerInventory[i].IsEmpty && PlayerInventory[i].ItemId == itemId)
                {
                    // Check if there is room remaining in this stack
                    int roomLeft = MaxStackSize - PlayerInventory[i].Amount;
                    if (roomLeft > 0)
                    {
                        int amountToAdd = Mathf.Min(amount, roomLeft);
                        PlayerInventory[i].AddToCurrentAmount(amountToAdd);
                        amount -= amountToAdd;

                        if (amount <= 0) 
                        {
                            OnPlayerInventoryChanged?.Invoke();
                            return 0;
                        }
                    }
                }
            }

            // Next find a fresh empty slot for leftover amounts
            for (int i = 0; i < PlayerInventory.Length; i++)
            {
                if (PlayerInventory[i].IsEmpty)
                {
                    int amountToAdd = Mathf.Min(amount, MaxStackSize);
                    PlayerInventory[i].AssignItem(itemId, amountToAdd);
                    amount -= amountToAdd;

                    if (amount <= 0) 
                    {
                        OnPlayerInventoryChanged?.Invoke();
                        return 0;
                    }
                }
            }

            // If amount is still greater than 0, inventory is completely full
            Debug.LogWarning($"No more space in inventory. Need to develop what happens when inventory is full");
            OnPlayerInventoryChanged?.Invoke();
            return amount;
        }
        
        public bool RemoveItem(ItemSO item, int amount)
        {
            var id = item.GetId();
            return RemoveItem(id, amount);
        }

        public bool RemoveItem(int itemId, int amount)
        {
            if (Player.Instance.Character.Health.CurrentLifeState.Value == LifeState.Dead) return false;

            // Verify the player actually has enough total items to remove
            if (GetTotalItemCount(itemId) < amount) 
            {
                OnPlayerInventoryChanged?.Invoke();
                return false;
            }

            // Remove items starting from the back of the inventory
            for (int i = PlayerInventory.Length - 1; i >= 0; i--)
            {
                if (!PlayerInventory[i].IsEmpty && PlayerInventory[i].ItemId == itemId)
                {
                    if (PlayerInventory[i].Amount >= amount)
                    {
                        PlayerInventory[i].RemoveFromCurrentAmount(amount);
                        OnPlayerInventoryChanged?.Invoke();
                        return true;
                    }
                    else
                    {
                        amount -= PlayerInventory[i].Amount;
                        PlayerInventory[i].Clear();
                    }
                }
            }
            OnPlayerInventoryChanged?.Invoke();
            return true;
        }
        
        public int GetTotalItemCount(ItemSO item)
        {
            var id = item.GetId();
            return GetTotalItemCount(id);
        }

        public int GetTotalItemCount(int itemId)
        {
            int total = 0;
            for (int i = 0; i < PlayerInventory.Length; i++)
            {
                if (!PlayerInventory[i].IsEmpty && PlayerInventory[i].ItemId == itemId)
                {
                    total += PlayerInventory[i].Amount;
                }
            }
            return total;
        }

        public bool CanAcceptItem(int itemId, int amount)
        {
            if (itemId <= 0 || itemId > ushort.MaxValue
                || GameDataRegistry.Instance.GetItemSOFromItemId((ushort)itemId) == null) return false;
            int remainingAmount = amount;

            for (int i = 0; i < PlayerInventory.Length; i++)
            {
                // If it's an empty slot, it can hold a full maximum stack size
                if (PlayerInventory[i].IsEmpty)
                {
                    remainingAmount -= MaxStackSize;
                }
                // If it matches, it can hold whatever room is left in this specific stack
                else if (PlayerInventory[i].ItemId == itemId)
                {
                    int roomLeft = MaxStackSize - PlayerInventory[i].Amount;
                    if (roomLeft > 0)
                    {
                        remainingAmount -= roomLeft;
                    }
                }

                // if remaining amount 0, it can accept it
                if (remainingAmount <= 0)
                {
                    return true;
                }
            }

            // If we finished checking every slot and there's still leftover amount, it cannot fully fit
            return false;
        }
        
        public bool CanCraftRecipe(RecipeSO recipeSO)
        {
            var recipe = recipeSO.Recipe;

            foreach (var ingredient in recipe)
            {
                var ingredientItem = ingredient.Item;
                var amountNeeded = ingredient.Amount;

                int iid = GameDataRegistry.Instance.GetItemIdFromItemSO(ingredientItem);
                if (GetTotalItemCount(iid) < amountNeeded)
                {
                    return false;
                }
            }
            
            return true;
        }
        
        public bool IsInventoryFull()
        {
            for (int i = 0; i < PlayerInventory.Length; i++)
            {
                var slot = PlayerInventory[i];
                
                if(slot.IsEmpty)
                {
                    return false;
                }
                else if(!slot.GetItemSO().IsStackable && slot.Amount < MaxStackSize)
                {
                    return false;
                }
            }
            
            return true;
        }
        
    }

    
}
