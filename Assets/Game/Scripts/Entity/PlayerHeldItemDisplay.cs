using UnityEngine;

namespace SketchLand
{
    // Position the tool after animation, before PlayerHandGripIK and IKManager2D.
    [DefaultExecutionOrder(-30)]
    public class PlayerHeldItemDisplay : MonoBehaviour
    {
        [SerializeField] private Transform _itemRoot;
        [SerializeField] private SpriteRenderer _heldItemSprite;
        [SerializeField] private Transform _handMount;

        private enum ItemPose { Held, Raising, Swinging, Returning }

        private PlayerArmHandler _armHandler;
        private PlayerHandGripIK _handGripIK;
        private ServerCharacter _character;
        private ItemSO _selectedItem;
        private ItemPose _pose;
        private Transform _swingParent;
        private Vector3 _swingPosition;
        private Quaternion _swingRotation;
        private Vector3 _swingScale;
        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Vector3 _startScale;
        private float _transitionProgress;

        private void Awake()
        {
            _armHandler = GetComponent<PlayerArmHandler>();
            _handGripIK = GetComponent<PlayerHandGripIK>();
            _character = GetComponent<ServerCharacter>();

            // The prefab keeps the authored swing pose; the hand mount owns
            // the resting pose. Neither needs per-item offset settings.
            _swingParent = _itemRoot.parent;
            _swingPosition = _itemRoot.localPosition;
            _swingRotation = _itemRoot.localRotation;
            _swingScale = _itemRoot.localScale;
            EndSwing();
        }

        private void Update()
        {
            ItemSO selected = null;
            if (Player.Instance != null && Player.Instance.Character == _character
                && InventoryInputManager.Instance != null
                && InventoryManager.Instance != null
                && InventoryManager.Instance.PlayerInventory != null
                && InventoryCursorManager.Instance != null
                && GameDataRegistry.Instance != null
                && _character.Health != null
                && _character.Health.CurrentLifeState.Value != LifeState.Dead)
            {
                // Use the same selection as item actions, including cursor items.
                var slot = InventoryInputManager.Instance.GetActiveInvSlot();
                if (slot != null && !slot.IsEmpty)
                    selected = slot.GetItemSO();
            }

            SetSelectedItem(selected);
        }

        private void SetSelectedItem(ItemSO item)
        {
            if (_selectedItem != item)
            {
                _selectedItem = item;
                if (_armHandler.IsSwinging)
                    _armHandler.CancelSwing();
            }

            if (_pose == ItemPose.Held)
                ShowItem(_selectedItem);
        }

        private void ShowItem(ItemSO item)
        {
            _heldItemSprite.sprite = item != null ? item.DisplayIcon : null;
            _heldItemSprite.enabled = _heldItemSprite.sprite != null;
        }

        public void BeginSwing(ToolItemSO tool)
        {
            // Detach BEFORE enabling grip IK: a target beneath R_Hand would
            // make the hand chase its own descendant.
            Matrix4x4 localPose = _swingParent.worldToLocalMatrix * _itemRoot.localToWorldMatrix;
            _itemRoot.SetParent(_swingParent, false);
            ReadLocalPose(localPose, out _startPosition, out _startRotation, out _startScale);
            _itemRoot.SetLocalPositionAndRotation(_startPosition, _startRotation);
            _itemRoot.localScale = _startScale;
            _transitionProgress = 0f;
            _pose = ItemPose.Raising;
            ShowItem(tool);
        }

        public void SetTransitionProgress(float progress)
        {
            _transitionProgress = progress;
        }

        public void FinishRaise()
        {
            _pose = ItemPose.Swinging;
            _itemRoot.SetLocalPositionAndRotation(_swingPosition, _swingRotation);
            _itemRoot.localScale = _swingScale;
        }

        public void BeginReturn()
        {
            _startPosition = _itemRoot.localPosition;
            _startRotation = _itemRoot.localRotation;
            _startScale = _itemRoot.localScale;
            _transitionProgress = 0f;
            _pose = ItemPose.Returning;
        }

        public void EndSwing()
        {
            _pose = ItemPose.Held;
            _itemRoot.SetParent(_handMount, false);
            _itemRoot.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            _itemRoot.localScale = Vector3.one;
            ShowItem(_selectedItem);
        }

        private void LateUpdate()
        {
            if (_pose != ItemPose.Raising && _pose != ItemPose.Returning)
                return;

            Vector3 position = _swingPosition;
            Quaternion rotation = _swingRotation;
            Vector3 scale = _swingScale;

            if (_pose == ItemPose.Returning)
            {
                // Follow the independent animated hand target during release,
                // rather than feeding the currently IK-driven hand back into IK.
                Transform target = _handGripIK.AnimatedTarget;
                Matrix4x4 restPose = _swingParent.worldToLocalMatrix
                    * target.localToWorldMatrix
                    * Matrix4x4.TRS(_handMount.localPosition, _handMount.localRotation, _handMount.localScale);
                ReadLocalPose(restPose, out position, out rotation, out scale);
            }

            float blend = Mathf.SmoothStep(0f, 1f, _transitionProgress);
            _itemRoot.SetLocalPositionAndRotation(
                Vector3.Lerp(_startPosition, position, blend),
                Quaternion.Slerp(_startRotation, rotation, blend));
            _itemRoot.localScale = Vector3.Lerp(_startScale, scale, blend);
        }

        private static void ReadLocalPose(Matrix4x4 matrix, out Vector3 position,
            out Quaternion rotation, out Vector3 scale)
        {
            // Decompose in the XY plane, retaining reflections without introducing
            // 180-degree X/Y rotations when a parent has negative X scale.
            position = matrix.GetColumn(3);
            float xScale = new Vector2(matrix.m00, matrix.m10).magnitude;
            rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(matrix.m10, matrix.m00) * Mathf.Rad2Deg);
            float determinant = matrix.m00 * matrix.m11 - matrix.m01 * matrix.m10;
            scale = new Vector3(xScale, xScale > 0.0001f ? determinant / xScale : 0f, matrix.m22);
        }

        private void OnDisable()
        {
            _heldItemSprite.enabled = false;
        }
    }
}
