using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SketchLand
{
    public class TooltipHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action OnHoverEnter;  // caller populates this

        private bool _isPointerOver;
        private readonly List<RaycastResult> _hits = new();

        private void Start()
        {
            if (InventoryCursorManager.Instance != null)
            {
                InventoryCursorManager.Instance.OnCursorSlotChanged += RefreshTooltip;
            }
        }

        private void OnDestroy()
        {
            if (InventoryCursorManager.Instance != null)
            {
                InventoryCursorManager.Instance.OnCursorSlotChanged -= RefreshTooltip;
            }
        }

        public void OnPointerEnter(PointerEventData _)
        {
            _isPointerOver = true;
            RefreshTooltip();
        }

        public void OnPointerExit(PointerEventData _)
        {
            ClearHover();
        }

        private void OnDisable() => ClearHover();

        private void ClearHover()
        {
            if (!_isPointerOver) return;
            _isPointerOver = false;
            if (Tooltip.ReferenceHolder != null) Tooltip.HideUI();
        }

        public void RefreshTooltip()
        {
            if (!_isPointerOver || !isActiveAndEnabled) return;

            var events = EventSystem.current;
            var pointer = Pointer.current;
            if (events == null || pointer == null)
            {
                ClearHover();
                return;
            }

            var data = new PointerEventData(events) { position = pointer.position.ReadValue() };
            events.RaycastAll(data, _hits);
            if (_hits.Count == 0 || !_hits[0].gameObject.transform.IsChildOf(transform))
            {
                ClearHover();
                return;
            }

            if (InventoryCursorManager.Instance != null && !InventoryCursorManager.Instance.CursorSlot.IsEmpty)
            {
                Tooltip.HideUI();
                return;
            }

            OnHoverEnter?.Invoke();
        }
    }
}