using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SketchLand
{
    public class CraftingSlotUI : MonoBehaviour
    {
        [SerializeField] private Button _craftButton;
        [SerializeField] private Image _itemIcon;
        [SerializeField] private Image _greyOverlay;
        [SerializeField] private TextMeshProUGUI _stackText;
        
        private RecipeSO _recipe;
        private TooltipHover _hover;

        private void OnDestroy()
        {
            InventoryManager.Instance.OnPlayerInventoryChanged -= RefreshUI;
        }

        public void Initialize(RecipeSO recipe)
        {
            _recipe = recipe;

            InventoryManager.Instance.OnPlayerInventoryChanged += RefreshUI;

            _craftButton.onClick.AddListener(OnCraftButtonClicked);

            _itemIcon.sprite = recipe.OutputItem.DisplayIcon;
            _stackText.text = recipe.OutputAmonut > 1 ? recipe.OutputAmonut.ToString() : string.Empty;

            _hover = GetComponent<TooltipHover>();
            _hover.OnHoverEnter = () =>
            {
                if (InventoryCursorManager.Instance.CursorSlot.IsEmpty)
                {
                    Tooltip.ShowNew();
                    Tooltip.CraftingRecipeDisplay(_recipe, fontSize: 12f, iconScale: 0.6f);
                }
            };

            RefreshUI();
        }

        private void OnCraftButtonClicked()
        {
            var inventory = InventoryManager.Instance;
        
            if(inventory.CanCraftRecipe(_recipe))
            {
                // NTFS: Later on I need to make it so canacceptitem is true even if i do not have the space but if i craft the item and i consume the ingredients THEN I will have space
                if (!inventory.IsInventoryFull() && inventory.CanAcceptItem(_recipe.OutputItem.GetId(), _recipe.OutputAmonut)) 
                {
                    var outputId = GameDataRegistry.Instance.GetItemIdFromItemSO(_recipe.OutputItem);

                    foreach (var ingredient in _recipe.Recipe)
                    {
                        inventory.RemoveItem(ingredient.Item.GetId(), ingredient.Amount);
                    }

                    inventory.CollectItem(outputId, _recipe.OutputAmonut);
                    AudioManager.Instance.PlayOneShot(FMODEvents.Instance.SlotClickedSFX, default);
                }
            }
        }

        private void RefreshUI()
        {
            var inventory = InventoryManager.Instance;

            if (inventory.CanCraftRecipe(_recipe))
            {
                _greyOverlay.enabled = false;
            }
            else
            {
                _greyOverlay.enabled = true;
            }
        }
    }
}
