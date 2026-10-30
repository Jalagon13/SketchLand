using System.Collections;
using UnityEngine;

namespace SketchLand
{
    [DefaultExecutionOrder(100)]
    public class ParallaxBackground : MonoBehaviour
    {
        [SerializeField] private GameObject _camera;
        [SerializeField] private float _parallaxEffect;

        private float _length, _startPos, _startPosY;
        private float _heightAboveOrigin;
        private Camera _viewCamera;
        private bool _initialized;

        private void LateUpdate()
        {
            if (!_initialized || _length <= 0f) return;

            float distance = _camera.transform.position.x * _parallaxEffect;
            float distanceY = _camera.transform.position.y * _parallaxEffect;
            float positionY = _startPosY + distanceY;

            // Stop the image's top edge at the viewport top when climbing.
            // Below that limit, retain the normal vertical parallax.
            float minimumY = _camera.transform.position.y + _viewCamera.orthographicSize
                - _heightAboveOrigin + 0.02f;
            positionY = Mathf.Max(positionY, minimumY);

            // Keep the middle copy nearest the camera, including after teleports.
            // Wrap before positioning so the seam never spends a frame uncovered.
            float relativeX = _camera.transform.position.x - distance - _startPos;
            float wrapOffset = Mathf.Floor(relativeX / _length + 0.5f) * _length;
            transform.position = new Vector3(_startPos + distance + wrapOffset, positionY, transform.position.z);
        }

        public void Initialize()
        {
            StartCoroutine(DelayInitialization());
        }

        private IEnumerator DelayInitialization()
        {
            yield return new WaitForSeconds(1f);

            // Compute start position relative to the camera so we don't double-count
            // the camera's world position when applying the parallax distance.
            _startPos = transform.position.x - _camera.transform.position.x * _parallaxEffect;
            _startPosY = transform.position.y - _camera.transform.position.y * _parallaxEffect;
            var spriteRenderer = GetComponent<SpriteRenderer>();
            _length = spriteRenderer.bounds.size.x;
            _heightAboveOrigin = spriteRenderer.bounds.max.y - transform.position.y;
            _viewCamera = _camera.GetComponent<Camera>();

            _initialized = true;
        }
    }
}
