using System;
using UnityEngine;

namespace SketchLand
{
    public enum DayPhase { Sunrise, Day, Sunset, Night }

    [Serializable]
    public sealed class WorldClock
    {
        [Tooltip("Advance the day/night cycle. Toggle during play to pause or resume at the current time.")]
        [SerializeField] private bool _runTimeOfDay;
        [SerializeField, Min(0.01f)] private float _sunriseMinutes = 2f;
        [SerializeField, Min(0.01f)] private float _dayMinutes = 10f;
        [SerializeField, Min(0.01f)] private float _sunsetMinutes = 2f;
        [SerializeField, Min(0.01f)] private float _nightMinutes = 8f;

        public double ElapsedMinutes { get; private set; }
        private double SunsetStart => (double)_sunriseMinutes + _dayMinutes;
        private double NightStart => SunsetStart + _sunsetMinutes;
        private double CycleMinutes => NightStart + _nightMinutes;
        private double CyclePosition => ElapsedMinutes % CycleMinutes;

        public long DayNumber => 1 + (long)(ElapsedMinutes / CycleMinutes);
        public float TimeOfDay01 => (float)(CyclePosition / CycleMinutes);
        public DayPhase CurrentPhase => CyclePosition switch
        {
            var t when t < _sunriseMinutes => DayPhase.Sunrise,
            var t when t < SunsetStart => DayPhase.Day,
            var t when t < NightStart => DayPhase.Sunset,
            _ => DayPhase.Night
        };

        public float PhaseProgress01 => CurrentPhase switch
        {
            DayPhase.Sunrise => (float)(CyclePosition / _sunriseMinutes),
            DayPhase.Day => (float)((CyclePosition - _sunriseMinutes) / _dayMinutes),
            DayPhase.Sunset => (float)((CyclePosition - SunsetStart) / _sunsetMinutes),
            _ => (float)((CyclePosition - NightStart) / _nightMinutes)
        };

        // Shared by world illumination and scenery so dawn and dusk stay in sync.
        public float Daylight01 => Mathf.SmoothStep(0f, 1f, CurrentPhase switch
        {
            DayPhase.Sunrise => PhaseProgress01,
            DayPhase.Day => 1f,
            DayPhase.Sunset => 1f - PhaseProgress01,
            _ => 0f
        });

        public float SunProgress01 => Mathf.Clamp01((float)(CyclePosition / NightStart));

        internal void StartNewWorld() => ElapsedMinutes = _sunriseMinutes;
        internal void Advance(float deltaSeconds)
        {
            if (_runTimeOfDay)
                ElapsedMinutes += deltaSeconds / 60.0;
        }
    }
}
