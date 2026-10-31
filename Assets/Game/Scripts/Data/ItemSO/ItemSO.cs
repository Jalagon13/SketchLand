using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New ItemSO", menuName = "OceanGame/Item/ItemSO")]
    public class ItemSO : ScriptableObject
    {
        [field: Header("Base Item")]
        [field: SerializeField] public string ItemName { get; private set; }
        [field: SerializeField, TextArea] public string Description { get; private set; }
        [field: SerializeField] public Sprite DisplayIcon { get; private set; }
        [field: SerializeField] public bool IsStackable { get; private set; } = true;

        [field: Header("Held Item Pose")]
        [field: Tooltip("HeldItemSprite local position relative to ItemGrip, copied directly from its Transform.")]
        [field: SerializeField] public Vector3 HeldPosition { get; private set; } = Vector3.zero;
        [field: Tooltip("HeldItemSprite local Z rotation in degrees, copied directly from its Transform.")]
        [field: SerializeField] public float HeldRotation { get; private set; } = 0f;
        [field: Tooltip("HeldItemSprite local scale, copied directly from its Transform.")]
        [field: SerializeField] public Vector3 HeldScale { get; private set; } = Vector3.one;
        
        public ushort GetId()
        {
            return GameDataRegistry.Instance.GetItemIdFromItemSO(this);
        }

        public virtual string GetDescription()
        {
            return Description;
        }

        public virtual void OnPrimaryActionStarted(Player player) { }
        public virtual void OnPrimaryActionHeld(Player player) { }
        public virtual void OnPrimaryActionRelease(Player player) { }

        public virtual void OnSecondaryActionStarted(Player player) { }
        public virtual void OnSecondaryActionHeld(Player player) { }
        public virtual void OnSecondaryActionRelease(Player player) { }
    }
}
