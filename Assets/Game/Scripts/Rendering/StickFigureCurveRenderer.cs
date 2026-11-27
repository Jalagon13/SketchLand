using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace SketchLand
{
    /// <summary>
    /// Optional presentation for bound 2D Sprite Skin rigs. Discovers curves from topology,
    /// redraws uniform narrow regions as strokes, and preserves/deforms detailed artwork.
    /// Never writes bones, IK targets, animation clips, or imported sprite data.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(200)]
    public sealed class StickFigureCurveRenderer : MonoBehaviour
    {
        [Serializable]
        public sealed class WidthOverride
        {
            [Tooltip("First bone of an automatically discovered stroke chain.")]
            public Transform Start;
            [Min(0.001f)] public float Width = 0.1f;
        }

        [Header("Appearance")]
        [SerializeField] private bool _useCurvedLimbs = true;
        [SerializeField, Range(0f, 1f)] private float _softness = 0.9f;
        [SerializeField, Min(0.01f)] private float _widthScale = 0.85f;
        [SerializeField, Range(4, 24)] private int _samplesPerSegment = 12;
        [Header("Rig")]
        [Tooltip("Optional subtree. Defaults to this transform; can contain multiple Sprite Skins.")]
        [SerializeField] private Transform _rigRoot;
        [Tooltip("Optional art direction. Automatic setup does not require these references.")]
        [SerializeField] private List<WidthOverride> _widthOverrides = new();
        [Tooltip("Initialization-only subdivision of flexible textured triangles.")]
        [SerializeField, Range(1, 4)] private int _textureSubdivisions = 2;

        private const float Epsilon = 0.00001f;
        private const int CapSides = 16;
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int RendererColor = Shader.PropertyToID("_RendererColor");
        private static readonly int Flip = Shader.PropertyToID("_Flip");
        private static readonly int AlphaTex = Shader.PropertyToID("_AlphaTex");
        private static readonly int ExternalAlpha = Shader.PropertyToID("_EnableExternalAlpha");
        private readonly List<SkinView> _views = new();
        private bool _needsRebuild = true, _configurationDirty = true, _building, _hierarchyDirty;
        private SpriteSkin[] _observedSkins = Array.Empty<SpriteSkin>();
        private readonly List<BindingSnapshot> _pending = new();
        private int _settingsHash;
        private string _status = "Waiting for rig initialization.";

        public bool UseCurvedLimbs
        {
            get => _useCurvedLimbs;
            set { _useCurvedLimbs = value; if (isActiveAndEnabled) RefreshNow(); }
        }
        public float Softness { get => _softness; set => _softness = Mathf.Clamp01(value); }
        public int SkinCount => _views.Count;
        public int StrokeChainCount
        {
            get { int count = 0; foreach (var view in _views) count += view.Strokes.Count; return count; }
        }
        public string Status => _status;

        private void OnEnable()
        {
            _needsRebuild = true;
            _configurationDirty = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.projectChanged += MarkForRebuild;
#endif
        }
        private void OnValidate() => _configurationDirty = true;
        private void OnTransformChildrenChanged() { if (!_building) _hierarchyDirty = true; }
        private void LateUpdate() => RefreshNow();
        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.projectChanged -= MarkForRebuild;
#endif
            ReleaseViews();
        }
        private void OnDestroy() => ReleaseViews();
        private void MarkForRebuild() => _needsRebuild = true;

        [ContextMenu("Reinitialize From Rig")]
        public void Reinitialize()
        {
            _needsRebuild = true;
            if (isActiveAndEnabled) RefreshNow();
        }

        /// <summary>Copies appearance settings to a transform-only visual clone, such as a cosmetic ragdoll.</summary>
        public void CopyTo(IReadOnlyDictionary<Transform, Transform> transforms)
        {
            if (!transforms.TryGetValue(transform, out var target)) return;
            var copy = target.GetComponent<StickFigureCurveRenderer>();
            if (copy == null) copy = target.gameObject.AddComponent<StickFigureCurveRenderer>();
            copy._useCurvedLimbs = _useCurvedLimbs;
            copy._softness = _softness;
            copy._widthScale = _widthScale;
            copy._samplesPerSegment = _samplesPerSegment;
            copy._textureSubdivisions = _textureSubdivisions;
            copy._rigRoot = _rigRoot != null && transforms.TryGetValue(_rigRoot, out var root) ? root : null;
            copy._widthOverrides = new List<WidthOverride>(_widthOverrides.Count);
            foreach (var entry in _widthOverrides)
            {
                if (entry == null || entry.Start == null || !transforms.TryGetValue(entry.Start, out var start)) continue;
                copy._widthOverrides.Add(new WidthOverride { Start = start, Width = entry.Width });
            }
            copy.enabled = enabled;
            copy._needsRebuild = true;
            copy._configurationDirty = true;
        }

        public void RefreshNow()
        {
            if (!isActiveAndEnabled || _building) return;
            if (_configurationDirty)
            {
                int hash = ConfigurationHash();
                _needsRebuild |= hash != _settingsHash;
                _settingsHash = hash; _configurationDirty = false;
            }
            foreach (var view in _views)
                if (!view.IsCurrent()) { _needsRebuild = true; break; }
            foreach (var binding in _pending)
                if (!binding.IsCurrent()) { _needsRebuild = true; break; }
            if (_hierarchyDirty)
            {
                var root = _rigRoot != null ? _rigRoot : transform;
                var skins = root.GetComponentsInChildren<SpriteSkin>(true);
                _needsRebuild |= skins.Length != _observedSkins.Length;
                for (int i = 0; i < skins.Length && !_needsRebuild; i++)
                    _needsRebuild |= skins[i] != _observedSkins[i];
                _hierarchyDirty = false;
            }
            if (_needsRebuild) BuildViews();
            foreach (var view in _views) view.Render(this);
        }

        private int ConfigurationHash()
        {
            unchecked
            {
                int hash = _rigRoot != null ? _rigRoot.GetInstanceID() : 0;
                hash = hash * 31 + _samplesPerSegment;
                hash = hash * 31 + _textureSubdivisions;
                hash = hash * 31 + _widthScale.GetHashCode();
                foreach (var entry in _widthOverrides)
                {
                    if (entry == null) continue;
                    hash = hash * 31 + (entry.Start != null ? entry.Start.GetInstanceID() : 0);
                    hash = hash * 31 + entry.Width.GetHashCode();
                }
                return hash;
            }
        }

        private void BuildViews()
        {
            _building = true;
            try
            {
                ReleaseViews();
                var root = _rigRoot != null ? _rigRoot : transform;
                int skipped = 0;
                _observedSkins = root.GetComponentsInChildren<SpriteSkin>(true);
                foreach (var skin in _observedSkins)
                {
                    var owner = skin.GetComponentInParent<StickFigureCurveRenderer>(true);
                    if (owner != null && owner != this) continue;
                    if (!skin.TryGetComponent(out SpriteRenderer source) || source.sprite == null
                        || source.drawMode != SpriteDrawMode.Simple || source.maskInteraction != SpriteMaskInteraction.None)
                    { skipped++; _pending.Add(new BindingSnapshot(skin)); continue; }
                    var view = new SkinView(skin, source);
                    try
                    {
                        if (view.Build(this)) _views.Add(view);
                        else { view.Dispose(); skipped++; _pending.Add(new BindingSnapshot(skin)); }
                    }
                    catch (Exception error)
                    {
                        view.Dispose(); skipped++; _pending.Add(new BindingSnapshot(skin));
                        Debug.LogWarning($"Curve rendering kept the original sprite on {source.name}: {error.Message}", this);
                    }
                }
                _status = _views.Count == 0 ? "No supported bound Sprite Skins found. Original renderers remain visible."
                    : $"Rendering {_views.Count} skin(s), {StrokeChainCount} stroke chain(s).";
                if (skipped > 0) _status += $" {skipped} unsupported or unbound skin(s) retain their original rendering.";
                _needsRebuild = false;
            }
            finally { _building = false; }
        }

        private void ReleaseViews()
        {
            foreach (var view in _views) view.Dispose();
            _views.Clear();
            _pending.Clear();
        }
        private float WidthFor(Transform start, float inferred)
        {
            foreach (var entry in _widthOverrides)
                if (entry != null && entry.Start == start) return Mathf.Max(0.001f, entry.Width);
            return Mathf.Max(0.001f, inferred * _widthScale);
        }
        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        private static float WeightFor(BoneWeight weight, int bone)
        {
            float amount = 0f;
            if (weight.boneIndex0 == bone) amount += weight.weight0;
            if (weight.boneIndex1 == bone) amount += weight.weight1;
            if (weight.boneIndex2 == bone) amount += weight.weight2;
            if (weight.boneIndex3 == bone) amount += weight.weight3;
            return amount;
        }

        private struct Anchor
        {
            public Transform Transform;
            public Vector3 Offset;
            public Vector3 Position(Matrix4x4 inverse) => inverse.MultiplyPoint3x4(Transform.TransformPoint(Offset));
        }

        // Failed or not-yet-bound skins are retried when their binding changes, without rebuilding every frame.
        private sealed class BindingSnapshot
        {
            private readonly SpriteSkin _skin;
            private readonly SpriteRenderer _renderer;
            private readonly Sprite _sprite;
            private readonly Transform[] _bones;
            private readonly SpriteMaskInteraction _mask;
            private readonly SpriteDrawMode _drawMode;

            public BindingSnapshot(SpriteSkin skin)
            {
                _skin = skin;
                _renderer = skin.GetComponent<SpriteRenderer>();
                _sprite = _renderer != null ? _renderer.sprite : null;
                _mask = _renderer != null ? _renderer.maskInteraction : SpriteMaskInteraction.None;
                _drawMode = _renderer != null ? _renderer.drawMode : SpriteDrawMode.Simple;
                _bones = skin.boneTransforms != null ? (Transform[])skin.boneTransforms.Clone() : null;
            }

            public bool IsCurrent()
            {
                if (_skin == null) return false;
                if (_renderer != null && (_renderer.sprite != _sprite || _renderer.maskInteraction != _mask || _renderer.drawMode != _drawMode)) return false;
                var bones = _skin.boneTransforms;
                if (bones == null || _bones == null) return bones == null && _bones == null;
                if (bones.Length != _bones.Length) return false;
                for (int i = 0; i < bones.Length; i++) if (bones[i] != _bones[i]) return false;
                return true;
            }
        }

        private sealed class Chain
        {
            public readonly List<int> Bones = new();
            public readonly List<Anchor> Anchors = new();
            public Vector3[] Points, Tangents, Samples;
            public Vector2 UV;
            public float Width;
            public int VertexOffset;
            public void Allocate(int samples)
            {
                Points = new Vector3[Anchors.Count]; Tangents = new Vector3[Anchors.Count];
                Samples = new Vector3[(Anchors.Count - 1) * samples + 1];
            }
            public void Update(Matrix4x4 inverse)
            {
                for (int i = 0; i < Points.Length; i++) Points[i] = Anchors[i].Position(inverse);
                Tangents[0] = Points[1] - Points[0];
                int last = Points.Length - 1;
                Tangents[last] = Points[last] - Points[last - 1];
                for (int i = 1; i < last; i++)
                    Tangents[i] = (Points[i + 1] - Points[i - 1]).normalized *
                        Mathf.Min(Vector3.Distance(Points[i - 1], Points[i]), Vector3.Distance(Points[i], Points[i + 1]));
            }
            public void Controls(int segment, float softness, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d)
            {
                a = Points[segment]; d = Points[segment + 1]; var delta = d - a;
                if (Bones.Count == 1) { b = a + delta / 3f; c = d - delta / 3f; return; }
                b = segment == 0 ? Vector3.Lerp(d, a + delta / 3f, softness)
                    : Vector3.Lerp(a, a + Tangents[segment] / 3f, softness);
                c = segment == Bones.Count - 1 ? Vector3.Lerp(a, d - delta / 3f, softness)
                    : Vector3.Lerp(d, d - Tangents[segment + 1] / 3f, softness);
            }
            public static Vector3 Evaluate(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
            {
                float u = 1f - t;
                return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
            }
        }

        private struct ArtVertex
        {
            public Vector3 Position;
            public Vector2 UV;
            public BoneWeight Weights;
        }

        /// <summary>Owns temporary presentation and suppression of one source renderer.</summary>
        private sealed class SkinView : IDisposable
        {
            public readonly List<Chain> Strokes = new();
            private readonly SpriteSkin _skin;
            private readonly SpriteRenderer _source;
            private readonly Sprite _sprite;
            private readonly bool _originalForceOff;
            private readonly MaterialPropertyBlock _properties = new();
            private readonly List<Chain> _artChains = new();
            private Transform[] _bones;
            private SpriteBone[] _metadata;
            private Matrix4x4[] _bind, _matrices;
            private Chain[] _boneChains;
            private int[] _boneSegments, _continuations;
            private bool[] _strokeBones, _consumed;
            private List<int>[] _children;
            private float[] _widths;
            private Vector2[] _fillUV;
            private Mesh _strokeMesh, _artMesh;
            private MeshRenderer _strokes, _art;
            private GameObject _container;
            private Vector3[] _strokePositions, _artPositions, _restPositions;
            private ArtVertex[] _artVertices;
            private Vector2[] _restUV;
            private BoneWeight[] _restWeights;
            private ushort[] _restTriangles;
            private bool _suppressed;

            public SkinView(SpriteSkin skin, SpriteRenderer source)
            { _skin = skin; _source = source; _sprite = source.sprite; _originalForceOff = source.forceRenderingOff; }
            public bool IsCurrent()
            {
                if (_skin == null || _source == null || _source.sprite != _sprite || _bones == null
                    || _source.maskInteraction != SpriteMaskInteraction.None || _source.drawMode != SpriteDrawMode.Simple) return false;
                var bones = _skin.boneTransforms;
                if (bones == null || bones.Length != _bones.Length) return false;
                for (int i = 0; i < bones.Length; i++) if (bones[i] == null || bones[i] != _bones[i]) return false;
                return true;
            }
            public bool Build(StickFigureCurveRenderer owner)
            {
                _metadata = _sprite.GetBones(); var sourceBones = _skin.boneTransforms;
                if (_metadata.Length == 0 || sourceBones == null || sourceBones.Length != _metadata.Length) return false;
                foreach (var bone in sourceBones) if (bone == null) return false;
                _bones = (Transform[])sourceBones.Clone(); _bind = _sprite.GetBindPoses().ToArray();
                if (_bind.Length != _bones.Length) return false;
                var positions = _sprite.GetVertexAttribute<Vector3>(VertexAttribute.Position);
                var weights = _sprite.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight);
                if (weights.Length != positions.Length || positions.Length == 0) return false;
                _restPositions = new Vector3[positions.Length]; _restWeights = new BoneWeight[weights.Length];
                for (int i = 0; i < positions.Length; i++) { _restPositions[i] = positions[i]; _restWeights[i] = weights[i]; }
                _restUV = _sprite.uv; _restTriangles = _sprite.triangles;
                if (_restUV.Length != _restPositions.Length) return false;
                _matrices = new Matrix4x4[_bones.Length]; _boneChains = new Chain[_bones.Length];
                _boneSegments = new int[_bones.Length]; _children = new List<int>[_bones.Length];
                _continuations = new int[_bones.Length]; _strokeBones = new bool[_bones.Length];
                _consumed = new bool[_bones.Length]; _widths = new float[_bones.Length]; _fillUV = new Vector2[_bones.Length];
                for (int i = 0; i < _bones.Length; i++) _children[i] = new List<int>();
                for (int i = 0; i < _bones.Length; i++)
                {
                    int parent = _metadata[i].parentId;
                    if (parent >= _bones.Length || parent == i || parent < -1) return false;
                    if (parent >= 0) _children[parent].Add(i);
                    int ancestor = parent, depth = 0;
                    while (ancestor >= 0 && ancestor < _bones.Length && depth++ < _bones.Length) ancestor = _metadata[ancestor].parentId;
                    if (ancestor >= 0) return false;
                }
                using (var probe = new TextureProbe(_sprite.texture))
                    for (int i = 0; i < _bones.Length; i++) AnalyzeBone(i, probe);
                for (int i = 0; i < _bones.Length; i++) _continuations[i] = Continuation(i);
                for (int i = 0; i < _bones.Length; i++)
                {
                    if (!_strokeBones[i]) continue;
                    int parent = _metadata[i].parentId;
                    if (parent >= 0 && _strokeBones[parent] && _continuations[parent] == i) continue;
                    var chain = MakeChain(i, true, owner); Strokes.Add(chain);
                    foreach (int bone in chain.Bones) _consumed[bone] = true;
                    int tip = _continuations[chain.Bones[chain.Bones.Count - 1]];
                    if (tip >= 0 && IsMarker(tip)) _consumed[tip] = true;
                }
                for (int i = 0; i < _bones.Length; i++)
                {
                    if (_strokeBones[i] || IsMarker(i) || _children[i].Count == 0) continue;
                    int parent = _metadata[i].parentId;
                    if (parent >= 0 && !_strokeBones[parent] && _continuations[parent] == i) continue;
                    _artChains.Add(MakeChain(i, false, owner));
                }
                if (Strokes.Count == 0 && _artChains.Count == 0) return false;
                _container = new GameObject("Curve rendering (generated)")
                    { hideFlags = HideFlags.HideAndDontSave, layer = _source.gameObject.layer };
                _container.transform.SetParent(_source.transform, false);
                BuildStrokes(); BuildArtwork(owner._textureSubdivisions); return true;
            }
            private bool IsMarker(int index) => _metadata[index].length < _sprite.bounds.size.magnitude * 0.01f;
            private void AnalyzeBone(int bone, TextureProbe probe)
            {
                float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
                float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
                int count = 0;
                for (int v = 0; v < _restPositions.Length; v++)
                {
                    var weight = _restWeights[v];
                    if (WeightFor(weight, bone) < 0.5f) continue;
                    var p = _bind[bone].MultiplyPoint3x4(_restPositions[v]);
                    minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); count++;
                }
                _widths[bone] = count > 0 ? maxY - minY : 0f;
                float length = Mathf.Max(_metadata[bone].length, maxX - minX);
                if (IsMarker(bone) || count < 3 || length < Epsilon || _widths[bone] / length > 0.65f) return;
                _strokeBones[bone] = probe.TryUniformFill(bone, _restTriangles, _restWeights, _restUV,
                    _restPositions, _bind[bone], out _fillUV[bone], out float paintedWidth);
                // Mesh cells include transparent padding, especially on diagonal cut-out limbs.
                // Measure the opaque artwork so those cells do not inflate the generated stroke.
                if (_strokeBones[bone] && paintedWidth > Epsilon) _widths[bone] = paintedWidth;
            }
            private int Continuation(int bone)
            {
                var rest = _bind[bone].inverse; var start = rest.MultiplyPoint3x4(Vector3.zero);
                var direction = rest.MultiplyVector(Vector3.right).normalized;
                int best = -1; float score = -2f;
                foreach (int child in _children[bone])
                {
                    var offset = _bind[child].inverse.MultiplyPoint3x4(Vector3.zero) - start;
                    float candidate = offset.sqrMagnitude > Epsilon * Epsilon ? Vector3.Dot(offset.normalized, direction) : -1f;
                    if (candidate > score) { score = candidate; best = child; }
                }
                return best;
            }
            private Chain MakeChain(int start, bool stroke, StickFigureCurveRenderer owner)
            {
                var chain = new Chain(); int current = start;
                for (int iteration = 0; iteration < _bones.Length; iteration++)
                {
                    chain.Anchors.Add(new Anchor { Transform = _bones[current] });
                    _boneChains[current] = chain; _boneSegments[current] = chain.Bones.Count; chain.Bones.Add(current);
                    int next = _continuations[current];
                    bool more = next >= 0 && (stroke ? _strokeBones[next]
                        : !_strokeBones[next] && !IsMarker(next) && _children[next].Count > 0);
                    if (more && !chain.Bones.Contains(next)) { current = next; continue; }
                    chain.Anchors.Add(next >= 0 ? new Anchor { Transform = _bones[next] } : EndAnchor(current));
                    break;
                }
                float width = 0f; foreach (int bone in chain.Bones) width += _widths[bone];
                chain.Width = owner.WidthFor(_bones[start], width / chain.Bones.Count);
                chain.UV = _fillUV[start]; chain.Allocate(owner._samplesPerSegment); return chain;
            }
            private Anchor EndAnchor(int bone)
            {
                // Unweighted effectors are often omitted from SpriteSkin.boneTransforms.
                float length = _metadata[bone].length;
                Transform closest = null; float distance = Mathf.Max(length * 0.5f, Epsilon);
                foreach (Transform child in _bones[bone])
                {
                    if ((child.gameObject.hideFlags & HideFlags.DontSave) != 0) continue;
                    float error = Vector3.Distance(child.localPosition, Vector3.right * length);
                    if (error < distance) { closest = child; distance = error; }
                }
                return closest != null ? new Anchor { Transform = closest }
                    : new Anchor { Transform = _bones[bone], Offset = Vector3.right * length };
            }
            private MeshRenderer CreateMesh(string name, out Mesh mesh)
            {
                mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
                mesh.MarkDynamic();
                var child = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = _source.gameObject.layer };
                child.transform.SetParent(_container.transform, false); child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterial = _source.sharedMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return renderer;
            }
            private void BuildStrokes()
            {
                if (Strokes.Count == 0) return;
                var indices = new List<int>(); var uv = new List<Vector2>(); int count = 0;
                foreach (var chain in Strokes)
                {
                    chain.VertexOffset = count;
                    for (int i = 1; i < chain.Samples.Length; i++)
                    {
                        int n = count + i * 2;
                        indices.Add(n - 2); indices.Add(n - 1); indices.Add(n);
                        indices.Add(n); indices.Add(n - 1); indices.Add(n + 1);
                    }
                    count += chain.Samples.Length * 2;
                    for (int cap = 0; cap < 2; cap++)
                    {
                        for (int side = 1; side <= CapSides; side++)
                        { indices.Add(count); indices.Add(count + side); indices.Add(count + side + 1); }
                        count += CapSides + 2;
                    }
                    while (uv.Count < count) uv.Add(chain.UV);
                }
                _strokePositions = new Vector3[count]; _strokes = CreateMesh("Curved strokes", out _strokeMesh);
                _strokeMesh.vertices = _strokePositions; _strokeMesh.SetUVs(0, uv);
                _strokeMesh.colors = White(count); _strokeMesh.SetTriangles(indices, 0);
            }
            private static Color[] White(int count)
            { var colors = new Color[count]; for (int i = 0; i < count; i++) colors[i] = Color.white; return colors; }
            private bool Consumed(BoneWeight w) => (w.weight0 <= 0f || _consumed[w.boneIndex0])
                && (w.weight1 <= 0f || _consumed[w.boneIndex1]) && (w.weight2 <= 0f || _consumed[w.boneIndex2])
                && (w.weight3 <= 0f || _consumed[w.boneIndex3]);
            private bool Flexible(BoneWeight w) => (w.weight0 > 0f && _boneChains[w.boneIndex0] != null)
                || (w.weight1 > 0f && _boneChains[w.boneIndex1] != null) || (w.weight2 > 0f && _boneChains[w.boneIndex2] != null)
                || (w.weight3 > 0f && _boneChains[w.boneIndex3] != null);
            private void BuildArtwork(int subdivisions)
            {
                var vertices = new List<ArtVertex>(); var triangles = new List<int>(); var influences = new Dictionary<int, float>(12);
                for (int t = 0; t < _restTriangles.Length; t += 3)
                {
                    int a = _restTriangles[t], b = _restTriangles[t + 1], c = _restTriangles[t + 2];
                    if (Consumed(_restWeights[a]) && Consumed(_restWeights[b]) && Consumed(_restWeights[c])) continue;
                    int divisions = Flexible(_restWeights[a]) || Flexible(_restWeights[b]) || Flexible(_restWeights[c]) ? subdivisions : 1;
                    for (int i = 0; i < divisions; i++)
                        for (int j = 0; j < divisions - i; j++)
                        {
                            Add(i, j); Add(i + 1, j); Add(i, j + 1);
                            if (i + j + 1 < divisions) { Add(i + 1, j); Add(i + 1, j + 1); Add(i, j + 1); }
                        }
                    void Add(int x, int y)
                    {
                        float wb = x / (float)divisions, wc = y / (float)divisions, wa = 1f - wb - wc;
                        influences.Clear(); Accumulate(_restWeights[a], wa, influences);
                        Accumulate(_restWeights[b], wb, influences); Accumulate(_restWeights[c], wc, influences);
                        var weight = new BoneWeight();
                        for (int slot = 0; slot < 4; slot++)
                        {
                            int best = 0; float amount = 0;
                            foreach (var entry in influences) if (entry.Value > amount) { best = entry.Key; amount = entry.Value; }
                            if (slot == 0) { weight.boneIndex0 = best; weight.weight0 = amount; }
                            if (slot == 1) { weight.boneIndex1 = best; weight.weight1 = amount; }
                            if (slot == 2) { weight.boneIndex2 = best; weight.weight2 = amount; }
                            if (slot == 3) { weight.boneIndex3 = best; weight.weight3 = amount; }
                            influences.Remove(best);
                        }
                        float sum = weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3;
                        if (sum > Epsilon) { weight.weight0 /= sum; weight.weight1 /= sum; weight.weight2 /= sum; weight.weight3 /= sum; }
                        triangles.Add(vertices.Count);
                        vertices.Add(new ArtVertex { Position = _restPositions[a] * wa + _restPositions[b] * wb + _restPositions[c] * wc,
                            UV = _restUV[a] * wa + _restUV[b] * wb + _restUV[c] * wc, Weights = weight });
                    }
                }
                if (vertices.Count == 0) return;
                _artVertices = vertices.ToArray(); _artPositions = new Vector3[vertices.Count];
                var uv = new Vector2[vertices.Count]; for (int i = 0; i < uv.Length; i++) uv[i] = vertices[i].UV;
                _art = CreateMesh("Curved artwork", out _artMesh);
                _artMesh.vertices = _artPositions; _artMesh.uv = uv; _artMesh.colors = White(vertices.Count); _artMesh.SetTriangles(triangles, 0);
            }
            private static void Accumulate(BoneWeight w, float factor, Dictionary<int, float> target)
            {
                Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1); Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
                void Add(int bone, float value)
                { if (value * factor > 0f) { target.TryGetValue(bone, out float prior); target[bone] = prior + value * factor; } }
            }
            public void Render(StickFigureCurveRenderer owner)
            {
                if (_container == null || !IsCurrent()) { RestoreSource(); return; }
                bool visible = owner._useCurvedLimbs && _source.enabled && _skin.isActiveAndEnabled && !_originalForceOff;
                _container.SetActive(visible);
                if (!visible) { RestoreSource(); return; }
                var inverse = _source.transform.worldToLocalMatrix;
                for (int i = 0; i < _bones.Length; i++) _matrices[i] = inverse * _bones[i].localToWorldMatrix;
                foreach (var chain in Strokes) chain.Update(inverse);
                foreach (var chain in _artChains) chain.Update(inverse);
                if (_strokeMesh != null)
                {
                    foreach (var chain in Strokes) UpdateStroke(chain, owner);
                    _strokeMesh.vertices = _strokePositions; _strokeMesh.RecalculateBounds(); UpdateRenderer(_strokes);
                }
                if (_artMesh != null)
                {
                    for (int i = 0; i < _artVertices.Length; i++)
                    {
                        var v = _artVertices[i]; var w = v.Weights;
                        var p = Deform(v.Position, w.boneIndex0, owner._softness) * w.weight0;
                        if (w.weight1 > 0) p += Deform(v.Position, w.boneIndex1, owner._softness) * w.weight1;
                        if (w.weight2 > 0) p += Deform(v.Position, w.boneIndex2, owner._softness) * w.weight2;
                        if (w.weight3 > 0) p += Deform(v.Position, w.boneIndex3, owner._softness) * w.weight3;
                        _artPositions[i] = ApplyFlip(p);
                    }
                    _artMesh.vertices = _artPositions; _artMesh.RecalculateBounds(); UpdateRenderer(_art);
                }
                _source.forceRenderingOff = true; _suppressed = true;
            }
            private Vector3 Deform(Vector3 vertex, int bone, float softness)
            {
                var local = _bind[bone].MultiplyPoint3x4(vertex); var chain = _boneChains[bone];
                if (chain == null || _metadata[bone].length < Epsilon) return _matrices[bone].MultiplyPoint3x4(local);
                float t = local.x / _metadata[bone].length;
                // Extend the end frame outside the span. This keeps wide artwork continuous at the boundary.
                float clamped = Mathf.Clamp01(t);
                chain.Controls(_boneSegments[bone], softness, out var a, out var b, out var c, out var d);
                var p = Chain.Evaluate(a, b, c, d, clamped); float u = 1f - clamped;
                var direction = 3f * u * u * (b - a) + 6f * u * clamped * (c - b) + 3f * clamped * clamped * (d - c);
                var originalX = _matrices[bone].MultiplyVector(Vector3.right);
                if (direction.sqrMagnitude < Epsilon * Epsilon) direction = originalX;
                p += direction.normalized * originalX.magnitude * (local.x - clamped * _metadata[bone].length);
                var y = _matrices[bone].MultiplyVector(Vector3.up);
                float angle = Mathf.Atan2(direction.y, direction.x) - Mathf.Atan2(originalX.y, originalX.x);
                float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
                return p + new Vector3(cos * y.x - sin * y.y, sin * y.x + cos * y.y, y.z) * local.y
                    + _matrices[bone].MultiplyVector(Vector3.forward) * local.z;
            }
            private Vector3 ApplyFlip(Vector3 p)
            { if (_source.flipX) p.x = -p.x; if (_source.flipY) p.y = -p.y; return p; }
            private void UpdateStroke(Chain chain, StickFigureCurveRenderer owner)
            {
                int samples = owner._samplesPerSegment, point = 0;
                for (int segment = 0; segment < chain.Bones.Count; segment++)
                {
                    chain.Controls(segment, owner._softness, out var a, out var b, out var c, out var d);
                    for (int i = segment == 0 ? 0 : 1; i <= samples; i++) chain.Samples[point++] = Chain.Evaluate(a, b, c, d, i / (float)samples);
                }
                int offset = chain.VertexOffset;
                for (int i = 0; i < chain.Samples.Length; i++)
                {
                    var direction = chain.Samples[Mathf.Min(i + 1, chain.Samples.Length - 1)] - chain.Samples[Mathf.Max(i - 1, 0)];
                    var normal = new Vector3(-direction.y, direction.x).normalized * (chain.Width * 0.5f);
                    _strokePositions[offset++] = ApplyFlip(chain.Samples[i] + normal);
                    _strokePositions[offset++] = ApplyFlip(chain.Samples[i] - normal);
                }
                Cap(chain.Samples[0]); Cap(chain.Samples[chain.Samples.Length - 1]);
                void Cap(Vector3 center)
                {
                    _strokePositions[offset++] = ApplyFlip(center);
                    for (int i = 0; i <= CapSides; i++)
                    {
                        float angle = i * Mathf.PI * 2f / CapSides;
                        _strokePositions[offset++] = ApplyFlip(center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * (chain.Width * 0.5f));
                    }
                }
            }
            private void UpdateRenderer(MeshRenderer renderer)
            {
                renderer.gameObject.layer = _source.gameObject.layer;
                renderer.sortingLayerID = _source.sortingLayerID; renderer.sortingOrder = _source.sortingOrder;
                renderer.sharedMaterial = _source.sharedMaterial; _source.GetPropertyBlock(_properties);
                _properties.SetTexture(MainTex, _sprite.texture); _properties.SetColor(RendererColor, _source.color);
                _properties.SetVector(Flip, Vector4.one);
                var alpha = _sprite.associatedAlphaSplitTexture;
                _properties.SetFloat(ExternalAlpha, alpha != null ? 1f : 0f);
                if (alpha != null) _properties.SetTexture(AlphaTex, alpha);
                renderer.SetPropertyBlock(_properties);
            }
            private void RestoreSource()
            { if (_suppressed && _source != null) _source.forceRenderingOff = _originalForceOff; _suppressed = false; }
            public void Dispose()
            {
                RestoreSource(); if (_container != null) _container.SetActive(false);
                DestroyOwned(_container); DestroyOwned(_strokeMesh); DestroyOwned(_artMesh);
                _container = null; _strokeMesh = null; _artMesh = null;
            }
        }

        /// <summary>Initialization-only probe; detailed fills stay textured. No import settings are changed.</summary>
        private sealed class TextureProbe : IDisposable
        {
            private readonly Texture2D _source;
            private Texture2D _readable;
            private bool _attempted, _owned;
            public TextureProbe(Texture2D source) => _source = source;
            private bool Prepare()
            {
                if (_attempted) return _readable != null;
                _attempted = true;
                if (_source.isReadable) { _readable = _source; return true; }
                var previous = RenderTexture.active; RenderTexture temporary = null;
                try
                {
                    temporary = RenderTexture.GetTemporary(_source.width, _source.height, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(_source, temporary); RenderTexture.active = temporary;
                    _readable = new Texture2D(_source.width, _source.height, TextureFormat.RGBA32, false)
                        { hideFlags = HideFlags.HideAndDontSave };
                    _owned = true; _readable.ReadPixels(new Rect(0, 0, _source.width, _source.height), 0, 0);
                    _readable.Apply(); return true;
                }
                catch (UnityException) { if (_owned) DestroyOwned(_readable); _readable = null; _owned = false; return false; }
                finally { RenderTexture.active = previous; if (temporary != null) RenderTexture.ReleaseTemporary(temporary); }
            }
            public bool TryUniformFill(int bone, ushort[] triangles, BoneWeight[] weights, Vector2[] uv,
                Vector3[] positions, Matrix4x4 bind, out Vector2 fillUV, out float paintedWidth)
            {
                fillUV = Vector2.zero; paintedWidth = 0f; if (!Prepare()) return false;
                Color first = default; int samples = 0; bool hasSolidUV = false;
                float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    if (WeightFor(weights[a], bone) < 0.5f || WeightFor(weights[b], bone) < 0.5f
                        || WeightFor(weights[c], bone) < 0.5f) continue;
                    for (int point = 0; point < 4; point++)
                    {
                        var sample = point == 0 ? (uv[a] + uv[b] + uv[c]) / 3f : point == 1 ? (uv[a] * 2f + uv[b] + uv[c]) / 4f
                            : point == 2 ? (uv[a] + uv[b] * 2f + uv[c]) / 4f : (uv[a] + uv[b] + uv[c] * 2f) / 4f;
                        var color = _readable.GetPixelBilinear(sample.x, sample.y);
                        if (color.a < 0.98f) continue;
                        if (samples == 0) first = color;
                        else if (Mathf.Abs(color.r - first.r) > 0.025f || Mathf.Abs(color.g - first.g) > 0.025f
                            || Mathf.Abs(color.b - first.b) > 0.025f) return false;
                        // CPU bilinear reads and GPU texel-center sampling differ at sprite edges.
                        // Use a texel center surrounded by opaque matching pixels, so the constant
                        // UV used by a whole stroke cannot pick up transparent edge filtering.
                        if (!hasSolidUV)
                        {
                            int x = Mathf.FloorToInt(sample.x * _readable.width);
                            int yIndex = Mathf.FloorToInt(sample.y * _readable.height);
                            bool solid = x > 0 && x < _readable.width - 1
                                && yIndex > 0 && yIndex < _readable.height - 1;
                            for (int dy = -1; dy <= 1 && solid; dy++)
                                for (int dx = -1; dx <= 1 && solid; dx++)
                                {
                                    var pixel = _readable.GetPixel(x + dx, yIndex + dy);
                                    solid = pixel.a >= 0.98f && Mathf.Abs(pixel.r - first.r) <= 0.025f
                                        && Mathf.Abs(pixel.g - first.g) <= 0.025f && Mathf.Abs(pixel.b - first.b) <= 0.025f;
                                }
                            if (solid)
                            {
                                fillUV = new Vector2((x + 0.5f) / _readable.width, (yIndex + 0.5f) / _readable.height);
                                hasSolidUV = true;
                            }
                        }
                        var position = point == 0 ? (positions[a] + positions[b] + positions[c]) / 3f
                            : point == 1 ? (positions[a] * 2f + positions[b] + positions[c]) / 4f
                            : point == 2 ? (positions[a] + positions[b] * 2f + positions[c]) / 4f
                            : (positions[a] + positions[b] + positions[c] * 2f) / 4f;
                        float y = bind.MultiplyPoint3x4(position).y;
                        minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                        samples++;
                    }
                }
                if (samples < 3 || !hasSolidUV) return false;
                paintedWidth = maxY - minY;
                return true;
            }
            public void Dispose() { if (_owned) DestroyOwned(_readable); }
        }
    }
}
