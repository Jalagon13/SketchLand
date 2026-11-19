using System.Collections;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace SketchLand
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private EventInstance _surfaceAmbienceInstance;
        private Bus _masterBus;
        private float _masterVolume = 1f;
        private Coroutine _pendingAmbience;

        private void Awake() 
        {
            Instance = this;
        }

        private IEnumerator Start()
        {
            // Web builds download FMOD banks asynchronously.
            while (!RuntimeManager.HaveAllBanksLoaded) yield return null;
            _masterBus = RuntimeManager.GetBus("bus:/");
            _masterBus.setVolume(_masterVolume);
        }

        private void OnDestroy()
        {
            StopSurfaceAmbience();
        }

        // Play a sound one time at a specific world position
        public void PlayOneShot(EventReference sound, Vector3 worldPos)
        {
            RuntimeManager.PlayOneShot(sound, worldPos);
        }

        public void StartSurfaceAmbience(EventReference ambience)
        {
            if (_surfaceAmbienceInstance.isValid() || _pendingAmbience != null) return;

            _pendingAmbience = StartCoroutine(StartAmbienceWhenLoaded(ambience));
        }

        private IEnumerator StartAmbienceWhenLoaded(EventReference ambience)
        {
            // Always defer one frame so the coroutine handle is assigned before completion.
            yield return null;
            while (!RuntimeManager.HaveAllBanksLoaded) yield return null;

            _pendingAmbience = null;
            _surfaceAmbienceInstance = RuntimeManager.CreateInstance(ambience);
            var result = _surfaceAmbienceInstance.start();
            if (result != FMOD.RESULT.OK)
                Debug.LogError($"Could not start forest ambience: {result}", this);
        }

        public void StopSurfaceAmbience()
        {
            if (_pendingAmbience != null)
            {
                StopCoroutine(_pendingAmbience);
                _pendingAmbience = null;
            }
            if (!_surfaceAmbienceInstance.isValid()) return;

            _surfaceAmbienceInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _surfaceAmbienceInstance.release();
            _surfaceAmbienceInstance = default;
        }

        // connected to pause menu volume slider
        public void SetMasterVolume(float volume) 
        {
            _masterVolume = volume;
            if (_masterBus.isValid()) _masterBus.setVolume(volume);
        }
    }
}
