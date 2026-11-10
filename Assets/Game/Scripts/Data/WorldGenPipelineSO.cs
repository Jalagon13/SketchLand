using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SketchLand
{
    [CreateAssetMenu(fileName = "New Gen Pipeline", menuName = "OceanGame/WorldGenPipelineSO")]
    public class WorldGenPipelineSO : ScriptableObject
    {
        public bool UseRandomSeed = true;
        public int Seed = 0;
        public int GenColumnsPerFrame = 50;
        
        [Header("World")]
        public int Width = 100;
        public int Height = 100;
        public int RoughSurfaceHeight = 350;
        public int UndergroundBottomLevel = 275;

        [Header("World Zones")]
        [Tooltip("Always present above the highest lower-zone boundary. Only Surface generates natural daylight.")]
        public WorldZoneAppearance SurfaceZone = new();
        [Tooltip("Horizontal layers, resolved from highest Starts Below Y to lowest. Each can use its own repeating terrain background. All lower zones disable natural daylight sources.")]
        public List<WorldZoneLayer> LowerZones = new() { new WorldZoneLayer() };

        [Header("Steps (Sequential Execution)")]
        [SerializeReference]
        public List<WorldGenStep> Steps = new();

        public IEnumerator RunGenPipelineRoutine(Action<WorldGenContext> onComplete, Action<float, string> onProgress = null)
        {
            Debug.Log($"[WorldGen] Generation Started");
        
            int actualSeed = UseRandomSeed ? UnityEngine.Random.Range(0, 100000) : Seed;
            var context = new WorldGenContext(Width, Height, actualSeed, UndergroundBottomLevel, GenColumnsPerFrame, RoughSurfaceHeight, ResolveZones());

            for (int i = 0; i < Steps.Count; i++)
            {
                var step = Steps[i];
                
                if (step != null && step.RunStep)
                {
                    float progress = (float)i / Steps.Count;
                    onProgress?.Invoke(progress, step.GetType().Name);

                    // Track duration of this step
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                    yield return step.Execute(context);

                    stopwatch.Stop();
                    Debug.Log($"[WorldGen] Step '{step.GetType().Name}' finished in {stopwatch.Elapsed}");
                }
            }

            onProgress?.Invoke(1f, "Complete!");
            onComplete?.Invoke(context);
        }

        public WorldZone[] ResolveZones()
        {
            int height = Mathf.Max(1, Height);
            var layers = LowerZones == null ? new List<WorldZoneLayer>()
                : LowerZones.FindAll(layer => layer != null);
            // Stable ordering makes duplicate boundaries deterministic: the first entry wins.
            layers = layers.OrderByDescending(layer => layer.StartsBelowY).ToList();
            var zones = new List<WorldZone>();
            WorldZoneAppearance appearance = SurfaceZone ?? new WorldZoneAppearance();
            bool surface = true;
            int top = height;
            foreach (WorldZoneLayer layer in layers)
            {
                // Keep at least one row of Surface, and ignore empty/duplicate layers.
                int boundary = Mathf.Clamp(layer.StartsBelowY, 0, height - 1);
                if (boundary <= 0 || boundary >= top) continue;
                zones.Add(new WorldZone(appearance, boundary, top, surface));
                appearance = layer;
                surface = false;
                top = boundary;
            }
            zones.Add(new WorldZone(appearance, 0, top, surface));
            return zones.ToArray();
        }
    }
}
