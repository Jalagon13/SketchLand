using UnityEngine;

namespace SketchLand
{
    public enum VisualAttachmentPointId
    {
        None = 0,
        Head = 10,
        Torso = 20,
        LeftShoulder = 30,
        RightShoulder = 40,
        LeftFoot = 50,
        RightFoot = 60
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class VisualAttachmentPoint : MonoBehaviour
    {
        [field: SerializeField] public VisualAttachmentPointId Id { get; private set; } = VisualAttachmentPointId.Head;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        private void Reset()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void SetSprite(Sprite sprite)
        {
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponent<SpriteRenderer>();

            if (_spriteRenderer.sprite != sprite)
                _spriteRenderer.sprite = sprite;
            if (_spriteRenderer.enabled != (sprite != null))
                _spriteRenderer.enabled = sprite != null;
        }
    }
}
