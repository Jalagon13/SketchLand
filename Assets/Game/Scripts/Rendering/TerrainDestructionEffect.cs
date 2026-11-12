using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SketchLand
{
    /// <summary>Local render debris. No rigidbodies, colliders, drops or network state.</summary>
    [DisallowMultipleComponent]
    public sealed class TerrainDestructionEffect : MonoBehaviour
    {
        [SerializeField, Range(0.2f, 1.2f)] private float _lifetime = 0.725f;
        [SerializeField, Min(0f)] private float _burstSpeed = 2.1f;
        [SerializeField, Min(0f)] private float _gravity = 22f;
        [SerializeField] private bool _collideWithTerrain = true;
        private const int Capacity = 96;

        private sealed class Fragment
        {
            public GameObject Root;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public TileGrid Terrain;
            public Vector2 Position, Velocity;
            public float Age, Lifetime, Rotation, Spin, Radius, Z;
        }

        private readonly List<Fragment> _active = new();
        private readonly Stack<Fragment> _pool = new();
        // Never consume Unity's gameplay random sequence for visual variation.
        private readonly System.Random _random = new();
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector2> _uvs = new();
        private readonly List<Color> _colors = new();
        private readonly List<int> _fill = new(), _ink = new();
        private MaterialPropertyBlock _properties;

        private float Random(float min, float max) => Mathf.Lerp(min, max, (float)_random.NextDouble());

        public void Emit(Vector2[] silhouette, TerrainTileConfigSO config, Material fill, Material ink,
            TileGrid terrain, int sortingLayer, int sortingOrder, int objectLayer, float z, Color tint)
        {
            if (silhouette.Length < 3 || !Application.isPlaying) return;
            _properties ??= new MaterialPropertyBlock();
            Vector2 center = Vector2.zero;
            foreach (Vector2 point in silhouette) center += point;
            center /= silhouette.Length;
            int count = _random.Next(3, 5);
            var seeds = new Vector2[count];
            float phase = Random(0f, Mathf.PI * 2f);
            for (int i = 0; i < count; i++)
            {
                float angle = phase + i * Mathf.PI * 2f / count + Random(-0.25f, 0.25f);
                seeds[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random(0.22f, 0.37f);
            }
            for (int i = 0; i < count; i++)
            {
                var polygon = new List<Vector2>(silhouette);
                for (int j = 0; j < count && polygon.Count >= 3; j++)
                {
                    if (i == j) continue;
                    // Voronoi half-planes cut the actual tile silhouette into fitting shards.
                    Vector2 normal = seeds[j] - seeds[i];
                    float distance = (seeds[j].sqrMagnitude - seeds[i].sqrMagnitude) * 0.5f;
                    polygon = Clip(polygon, normal, distance);
                }
                if (polygon.Count < 3) continue;
                if (_active.Count >= Capacity) Recycle(0);
                Fragment fragment = _pool.Count > 0 ? _pool.Pop() : CreateFragment();
                BuildMesh(fragment, polygon, config);
                fragment.Terrain = terrain;
                fragment.Age = 0f;
                fragment.Lifetime = _lifetime * Random(0.85f, 1.15f);
                fragment.Rotation = 0f;
                fragment.Spin = Random(-220f, 220f);
                fragment.Z = z;
                Vector2 direction = (fragment.Position - center).normalized;
                fragment.Velocity = direction * _burstSpeed * Random(0.65f, 1.1f) + Vector2.up * Random(0.6f, 1.2f);
                fragment.Root.layer = objectLayer;
                fragment.Renderer.sortingLayerID = sortingLayer;
                fragment.Renderer.sortingOrder = sortingOrder;
                fragment.Renderer.sharedMaterials = new[] { fill, ink };
                _properties.Clear();
                _properties.SetColor("_LayerTint", tint);
                fragment.Renderer.SetPropertyBlock(_properties);
                fragment.Root.transform.SetPositionAndRotation(new Vector3(fragment.Position.x, fragment.Position.y, z), Quaternion.identity);
                fragment.Root.transform.localScale = Vector3.one * 0.78f;
                fragment.Root.SetActive(true);
                _active.Add(fragment);
            }
        }

        private static List<Vector2> Clip(List<Vector2> input, Vector2 normal, float distance)
        {
            var output = new List<Vector2>(input.Count + 2);
            Vector2 previous = input[input.Count - 1];
            float previousDistance = Vector2.Dot(previous, normal) - distance;
            foreach (Vector2 point in input)
            {
                float currentDistance = Vector2.Dot(point, normal) - distance;
                if ((currentDistance <= 0f) != (previousDistance <= 0f))
                    output.Add(Vector2.Lerp(previous, point, previousDistance / (previousDistance - currentDistance)));
                if (currentDistance <= 0f && (output.Count == 0 || (output[output.Count - 1] - point).sqrMagnitude > 0.0000001f))
                    output.Add(point);
                previous = point;
                previousDistance = currentDistance;
            }
            if (output.Count > 1 && (output[0] - output[output.Count - 1]).sqrMagnitude < 0.0000001f)
                output.RemoveAt(output.Count - 1);
            return output;
        }

        private Fragment CreateFragment()
        {
            var root = new GameObject("Terrain shard (visual)");
            root.transform.SetParent(transform, false);
            var mesh = new Mesh { name = "Dynamic terrain shard" };
            mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return new Fragment { Root = root, Mesh = mesh, Renderer = renderer };
        }

        private void BuildMesh(Fragment fragment, List<Vector2> polygon, TerrainTileConfigSO config)
        {
            Vector2 center = Vector2.zero;
            foreach (Vector2 point in polygon) center += point;
            center /= polygon.Count;
            fragment.Position = center;
            fragment.Radius = 0f;
            _vertices.Clear(); _uvs.Clear(); _colors.Clear(); _fill.Clear(); _ink.Clear();
            Vector2 textureSize = new Vector2(Mathf.Max(0.01f, config.TextureWorldSize.x), Mathf.Max(0.01f, config.TextureWorldSize.y));
            AddVertex(center, center, textureSize, config.TextureOffset, config.FillTint);
            foreach (Vector2 point in polygon)
            {
                fragment.Radius = Mathf.Max(fragment.Radius, Vector2.Distance(center, point));
                AddVertex(point, center, textureSize, config.TextureOffset, config.FillTint);
            }
            for (int i = 0; i < polygon.Count; i++)
            {
                _fill.Add(0); _fill.Add(i + 1); _fill.Add((i + 1) % polygon.Count + 1);
            }
            // Closed mitered ink ring, including all newly cut edges, with a soft outer edge.
            int ring = _vertices.Count;
            float halfWidth = Mathf.Clamp(config.OutlineWidth * 0.4f, 0.006f, 0.025f);
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 point = polygon[i];
                Vector2 incoming = (point - polygon[(i + polygon.Count - 1) % polygon.Count]).normalized;
                Vector2 outgoing = (polygon[(i + 1) % polygon.Count] - point).normalized;
                Vector2 n1 = new Vector2(incoming.y, -incoming.x);
                Vector2 n2 = new Vector2(outgoing.y, -outgoing.x);
                Vector2 miter = (n1 + n2).normalized;
                float denominator = Mathf.Max(0.3f, Vector2.Dot(miter, n2));
                for (int band = 0; band < 4; band++)
                {
                    float width = band switch { 0 => -halfWidth - 0.006f, 1 => -halfWidth, 2 => halfWidth, _ => halfWidth + 0.006f };
                    Color color = config.OutlineColor;
                    if (band == 0 || band == 3) color.a = 0f;
                    AddVertex(point + miter * (width / denominator), center, Vector2.one, Vector2.zero, color);
                }
            }
            for (int i = 0; i < polygon.Count; i++)
                for (int band = 0; band < 3; band++)
                {
                    int a = ring + i * 4 + band, b = ring + ((i + 1) % polygon.Count) * 4 + band;
                    _ink.Add(a); _ink.Add(b); _ink.Add(a + 1);
                    _ink.Add(a + 1); _ink.Add(b); _ink.Add(b + 1);
                }
            fragment.Mesh.Clear();
            fragment.Mesh.SetVertices(_vertices);
            fragment.Mesh.SetUVs(0, _uvs);
            fragment.Mesh.SetColors(_colors);
            fragment.Mesh.subMeshCount = 2;
            fragment.Mesh.SetTriangles(_fill, 0, false);
            fragment.Mesh.SetTriangles(_ink, 1, false);
            fragment.Mesh.RecalculateBounds();
        }

        private void AddVertex(Vector2 point, Vector2 center, Vector2 textureSize, Vector2 offset, Color color)
        {
            _vertices.Add(point - center);
            _uvs.Add(new Vector2(point.x / textureSize.x, point.y / textureSize.y) + offset);
            _colors.Add(color);
        }

        private void Update() => Simulate(Time.deltaTime);

        private void Simulate(float deltaTime)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Fragment fragment = _active[i];
                fragment.Age += deltaTime;
                float progress = fragment.Age / fragment.Lifetime;
                float scale = 0.78f * (1f - progress * progress);
                if (scale < 0.025f) { Recycle(i); continue; }
                float dt = Mathf.Min(deltaTime, 0.1f);
                int steps = Mathf.Clamp(Mathf.CeilToInt(fragment.Velocity.magnitude * dt / 0.06f), 1, 12);
                float step = dt / steps;
                for (int s = 0; s < steps; s++)
                {
                    fragment.Velocity.y -= _gravity * step;
                    // Small collision proxy keeps irregular chips from hovering above surfaces.
                    float radius = Mathf.Min(fragment.Radius * 0.4f, 0.16f) * scale;
                    Vector2 next = fragment.Position + Vector2.right * (fragment.Velocity.x * step);
                    if (_collideWithTerrain && Blocked(fragment.Terrain, next, radius))
                    { fragment.Velocity.x *= -0.3f; fragment.Spin *= 0.65f; }
                    else fragment.Position.x = next.x;
                    next = fragment.Position + Vector2.up * (fragment.Velocity.y * step);
                    if (_collideWithTerrain && Blocked(fragment.Terrain, next, radius))
                    { fragment.Velocity.y *= -0.25f; fragment.Velocity.x *= 0.7f; fragment.Spin *= 0.65f; }
                    else fragment.Position.y = next.y;
                }
                fragment.Rotation += fragment.Spin * dt;
                fragment.Root.transform.SetPositionAndRotation(new Vector3(fragment.Position.x, fragment.Position.y, fragment.Z), Quaternion.Euler(0f, 0f, fragment.Rotation));
                fragment.Root.transform.localScale = Vector3.one * scale;
            }
        }

        private static bool Blocked(TileGrid grid, Vector2 point, float radius)
        {
            if (grid == null) return false;
            int minX = Mathf.FloorToInt(point.x - radius), maxX = Mathf.FloorToInt(point.x + radius);
            int minY = Mathf.FloorToInt(point.y - radius), maxY = Mathf.FloorToInt(point.y + radius);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    if (grid.GetTileData(x, y).IsSolid) return true;
            return false;
        }

        private void Recycle(int index)
        {
            Fragment fragment = _active[index];
            fragment.Root.SetActive(false);
            fragment.Terrain = null;
            fragment.Renderer.sharedMaterials = System.Array.Empty<Material>();
            _pool.Push(fragment);
            _active.RemoveAt(index);
        }

        public void Clear()
        {
            for (int i = _active.Count - 1; i >= 0; i--) Recycle(i);
        }

        private void OnDisable() => Clear();
        private void OnDestroy()
        {
            Clear();
            foreach (Fragment fragment in _pool)
            {
                if (fragment.Root != null) Destroy(fragment.Root);
                if (fragment.Mesh != null) Destroy(fragment.Mesh);
            }
            _pool.Clear();
        }
    }
}
