using System;
using System.Collections.Generic;
using UnityEngine;

namespace SketchLand
{
    public class WorldTextPopupManager : MonoBehaviour
    {
        public static WorldTextPopupManager Instance { get; private set; }

        [SerializeField] private PlainTextPopup _plainTextPopupPrefab;

        [Header("Character Damage Number")]
        [SerializeField] private Color _damageNumberColor = Color.red;
        [SerializeField, Min(1f)] private float _damageNumberFontSize = 24f;

        [Header("Player Damage Number")]
        [SerializeField] private Color _playerNumberColor = Color.red;
        [SerializeField, Min(1f)] private float _playerNumberFontSize = 24f;

        [Header("Plain Text")]
        [SerializeField] private Color _plainTextColor = Color.white;
        [SerializeField, Min(1f)] private float _plainTextFontSize = 8f;

        [Header("Item Collect Popup")]
        [SerializeField] private ItemCollectWorldPopup _itemCollectPopupPrefab;
        [SerializeField] private Color _itemCollectTextColor = Color.white;
        [SerializeField, Min(1f)] private float _itemCollectFontSize = 20f;

        private readonly Dictionary<ushort, ItemCollectWorldPopup> _activeItemPopups = new();

        private void Awake()
        {
            Instance = this;
        }

        public void SpawnPlayerDamageNumbers(Vector3 worldPosition, int damage)
        {
            SpawnTextPopup(worldPosition, damage.ToString(), _playerNumberColor, _playerNumberFontSize);
        }

        public void SpawnCharacterDamageNumbers(Vector3 worldPosition, int damage)
        {
            SpawnTextPopup(worldPosition, damage.ToString(), _damageNumberColor, _damageNumberFontSize);
        }

        public void SpawnTextPopup(Vector3 worldPosition, string text)
        {
            SpawnTextPopup(worldPosition, text, _plainTextColor, _plainTextFontSize);
        }
        
        private void SpawnTextPopup(Vector3 worldPosition, string text, Color color, float fontSize)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogError("Cannot spawn a text popup with empty text.", this);
                return;
            }

            if (!CanSpawnAt(worldPosition))
            {
                return;
            }

            PlainTextPopup popup = Instantiate(_plainTextPopupPrefab, worldPosition, Quaternion.identity);

            popup.Initialize(text, color, fontSize);
            popup.Play();
        }

        public void SpawnItemCollectPopup(ItemSO item, int amount)
        {
            if (item == null)
            {
                Debug.LogError("Cannot spawn an item collect popup for a null item.", this);
                return;
            }

            if (amount <= 0)
            {
                Debug.LogError($"Cannot spawn an item collect popup for amount {amount}.", this);
                return;
            }

            Vector3 spawnPosition = Player.Instance.Character.transform.position;

            if (!CanSpawnAt(spawnPosition))
            {
                return;
            }

            ushort itemId = item.GetId();
            int totalAmount = amount;

            if (_activeItemPopups.TryGetValue(itemId, out ItemCollectWorldPopup existingPopup))
            {
                _activeItemPopups.Remove(itemId);

                if (existingPopup != null)
                {
                    totalAmount += existingPopup.DisplayAmount;
                    Destroy(existingPopup.gameObject);
                }
            }

            ItemCollectWorldPopup popup = Instantiate(_itemCollectPopupPrefab, spawnPosition, Quaternion.identity);

            popup.Initialize(item, totalAmount, _itemCollectTextColor, _itemCollectFontSize);
            popup.AnimationCompleted += completedPopup =>
            {
                if (_activeItemPopups.TryGetValue(itemId, out ItemCollectWorldPopup currentPopup) && currentPopup == completedPopup)
                {
                    _activeItemPopups.Remove(itemId);
                }
            };

            _activeItemPopups.Add(itemId, popup);
            popup.Play();
        }

        private bool CanSpawnAt(Vector3 worldPosition)
        {
            if (PlayerCamera.Instance == null)
            {
                Debug.LogError("World Renderer is not assigned on WorldTextPopupManager.", this);
                return false;
            }

            return PlayerCamera.Instance.IsPositionInBounds(worldPosition);
        }
    }
}