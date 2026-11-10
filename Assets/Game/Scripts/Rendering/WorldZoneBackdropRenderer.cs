using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SketchLand
{
    /// <summary>One camera-clipped quad per visible zone; no tile data or world-sized meshes.</summary>
    internal sealed class WorldZoneBackdropRenderer
    {
        private sealed class View
        {
            public GameObject Root;
            public Mesh Mesh;
            public MeshRenderer Renderer;
        }

        private readonly List<View> _views = new();
        private readonly Vector3[] _vertices = new Vector3[4];
        private readonly Vector2[] _uvs = new Vector2[4];
        private readonly Color[] _colors = new Color[4];
        private static readonly int[] Triangles = { 0, 1, 2, 0, 2, 3 };

        public void Render(WorldManager world, Camera camera, Transform parent, int padding,
            int gameObjectLayer, int sortingLayer, int sortingOrder, float z, Color tint,
            Func<TerrainTileConfigSO, Material> getFillMaterial, Material fallbackMaterial)
        {
            Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);
            for (int y = 0; y <= 1; y++)
                for (int x = 0; x <= 1; x++)
                {
                    Vector2 point = camera.ViewportToWorldPoint(new Vector3(x, y, camera.nearClipPlane));
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
            min = Vector2.Max(Vector2.zero, min - Vector2.one * padding);
            max = Vector2.Min(new Vector2(world.FgGrid.Width, world.FgGrid.Height), max + Vector2.one * padding);

            int used = 0;
            foreach (WorldZone zone in world.Zones)
            {
                TerrainTileConfigSO config = zone.Appearance.BackgroundTerrain;
                // A missing lower-zone texture stays black, so removing a selection never reveals sky.
                if (config == null && zone.AllowsNaturalDaylight) continue;
                float bottom = Mathf.Max(min.y, zone.MinY);
                float top = Mathf.Min(max.y, zone.MaxY);
                if (max.x <= min.x || top <= bottom) continue;

                View view = GetView(used++, parent);
                view.Root.name = zone.Appearance.Name + " zone backdrop";
                view.Root.layer = gameObjectLayer;
                view.Renderer.sortingLayerID = sortingLayer;
                view.Renderer.sortingOrder = sortingOrder;
                view.Renderer.sharedMaterial = config != null ? getFillMaterial(config) : fallbackMaterial;

                _vertices[0] = new Vector3(min.x, bottom, z);
                _vertices[1] = new Vector3(min.x, top, z);
                _vertices[2] = new Vector3(max.x, top, z);
                _vertices[3] = new Vector3(max.x, bottom, z);
                Vector2 textureSize = config != null ? config.TextureWorldSize : Vector2.one;
                textureSize.x = Mathf.Max(0.01f, textureSize.x);
                textureSize.y = Mathf.Max(0.01f, textureSize.y);
                Vector2 offset = config != null ? config.TextureOffset : Vector2.zero;
                Color color = config != null ? config.FillTint * tint : Color.black;
                for (int i = 0; i < 4; i++)
                {
                    _uvs[i] = new Vector2(_vertices[i].x / textureSize.x, _vertices[i].y / textureSize.y) + offset;
                    _colors[i] = color;
                }
                view.Mesh.vertices = _vertices;
                view.Mesh.uv = _uvs;
                view.Mesh.colors = _colors;
                view.Mesh.triangles = Triangles;
                view.Mesh.RecalculateBounds();
                view.Root.SetActive(true);
            }
            for (int i = used; i < _views.Count; i++)
                _views[i].Root.SetActive(false);
        }

        private View GetView(int index, Transform parent)
        {
            if (index < _views.Count) return _views[index];
            var root = new GameObject("Zone backdrop");
            root.transform.SetParent(parent, true);
            var mesh = new Mesh { name = "Visible zone backdrop" };
            mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var view = new View { Root = root, Mesh = mesh, Renderer = renderer };
            _views.Add(view);
            return view;
        }

        public void Clear()
        {
            foreach (View view in _views)
            {
                if (view.Root != null)
                {
                    view.Root.SetActive(false);
                    UnityEngine.Object.Destroy(view.Root);
                }
                if (view.Mesh != null) UnityEngine.Object.Destroy(view.Mesh);
            }
            _views.Clear();
        }
    }
}
