using UnityEngine;

namespace SketchLand
{
    [DisallowMultipleComponent]
    public sealed class SurfaceBackgroundTint : MonoBehaviour
    {
        [Header("Time of Day")]
        [Tooltip("Sprite tint at full daylight. Sunrise blends toward this color.")]
        [SerializeField] private Color _dayTint = Color.white;
        [Tooltip("Sprite tint at night. Sunset blends toward this color; torches do not affect it.")]
        [SerializeField] private Color _nightTint = new(0.08f, 0.12f, 0.25f, 1f);
        [Tooltip("All repeating copies belonging to this scenery layer.")]
        [SerializeField] private SpriteRenderer[] _renderers;

        private void LateUpdate()
        {
            var world = WorldManager.Instance;
            if (world == null || !world.IsWorldReady) return;

            Color tint = Color.Lerp(_nightTint, _dayTint, world.Clock.Daylight01);
            foreach (var renderer in _renderers)
                if (renderer != null) renderer.color = tint;
        }
    }
}
