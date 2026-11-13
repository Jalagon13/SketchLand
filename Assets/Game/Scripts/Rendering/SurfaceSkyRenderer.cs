using UnityEngine;

namespace SketchLand
{
    [DefaultExecutionOrder(110)]
    public sealed class SurfaceSkyRenderer : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private SpriteRenderer _sun;
        [SerializeField] private SpriteRenderer _moon;

        private void LateUpdate()
        {
            var world = WorldManager.Instance;
            if (world == null || !world.IsWorldReady) return;

            WorldClock clock = world.Clock;
            bool night = clock.CurrentPhase == DayPhase.Night;
            float progress = night ? clock.PhaseProgress01 : clock.SunProgress01;
            float height = 0.55f + 0.33f * Mathf.Sin(Mathf.PI * progress);
            Vector3 position = _camera.ViewportToWorldPoint(new Vector3(
                Mathf.Lerp(-0.05f, 1.05f, progress), height, transform.position.z - _camera.transform.position.z));

            bool surface = world.GetZoneAtY(position.y).AllowsNaturalDaylight;
            _sun.enabled = surface && !night;
            _moon.enabled = surface && night;
            (night ? _moon : _sun).transform.position = position;
        }
    }
}
