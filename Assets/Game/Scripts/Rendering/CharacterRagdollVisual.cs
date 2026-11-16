using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace SketchLand
{
    /// <summary>
    /// Cosmetic position-based ragdoll. Owns only transforms, sprite renderers and sprite skins.
    /// Reads the foreground grid; never registers an Entity or changes tiles/network state.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class CharacterRagdollVisual : MonoBehaviour
    {
        private const int Capacity = 16;
        private const int SolverIterations = 16;
        private static readonly List<CharacterRagdollVisual> Active = new();
        private static readonly System.Random Random = new();

        private sealed class Bone
        {
            public Transform Transform;
            public Bone Parent;
            public Vector2 Start, End, PreviousStart, PreviousEnd;
            public Vector2 StepStart, StepEnd, StartContact, EndContact, RenderStart, RenderEnd;
            public float Length, Radius, BendLimit, RestAngle, InitialAngle, Z;
            public Quaternion InitialRotation;
            public Vector2 Attachment;
        }

        private readonly List<Bone> _bones = new();
        private SpriteRenderer[] _renderers;
        private Color[] _colors;
        private CharacterDeathEffect.Settings _settings;
        private TileGrid _terrain;
        private float _age;
        private float _quietTime;
        private bool _sleeping;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        public static bool TryCreate(Transform source, CharacterDeathEffect.Settings settings,
            CharacterDeathEffect.BoneOverride[] overrides, Vector2 velocity)
        {
            var skins = source.GetComponentsInChildren<SpriteSkin>(false);
            if (skins.Length == 0 || WorldManager.Instance == null) return false;

            var root = new GameObject(source.name + " corpse (visual)");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, source.gameObject.scene);
            root.transform.SetPositionAndRotation(source.position, source.rotation);
            root.transform.localScale = source.lossyScale;
            var visual = root.AddComponent<CharacterRagdollVisual>();
            visual._settings = settings;
            visual._terrain = WorldManager.Instance.FgGrid;
            var transforms = new Dictionary<Transform, Transform>();
            CopyTransforms(source, root.transform, transforms);
            CopyRenderers(transforms);

            var bones = new Dictionary<Transform, Bone>();
            foreach (SpriteSkin skin in skins)
            {
                var renderer = skin.GetComponent<SpriteRenderer>();
                if (renderer.sprite == null || skin.boneTransforms == null) continue;
                var spriteBones = renderer.sprite.GetBones();
                var mapped = new Transform[skin.boneTransforms.Length];
                bool valid = skin.rootBone != null && transforms.ContainsKey(skin.rootBone)
                    && spriteBones.Length == mapped.Length;
                for (int i = 0; i < mapped.Length && valid; i++)
                    valid = skin.boneTransforms[i] != null && transforms.TryGetValue(skin.boneTransforms[i], out mapped[i]);
                if (!valid) continue;

                var copy = transforms[skin.transform].gameObject.AddComponent<SpriteSkin>();
                copy.autoRebind = false;
                copy.SetRootBone(transforms[skin.rootBone]);
                copy.SetBoneTransforms(mapped);
                copy.alwaysUpdate = true;
                for (int i = 0; i < mapped.Length; i++)
                {
                    Transform original = skin.boneTransforms[i];
                    if (bones.ContainsKey(original)) continue;
                    // Skinning markers at hand/foot tips are almost zero length. Giving them an
                    // independent physics body amplifies tiny contact errors into large rotations.
                    // Keep them bound to the skin, following their parent's physical bone instead.
                    if (spriteBones[i].length < 0.02f && !HasBoneChild(original, skin.boneTransforms)) continue;
                    Vector2 start = original.position;
                    Vector2 offset = original.TransformVector(Vector3.right * Mathf.Max(0.05f, spriteBones[i].length));
                    var bone = new Bone
                    {
                        Transform = mapped[i], Start = start, End = start + offset,
                        Length = offset.magnitude, Radius = settings.BoneRadius,
                        BendLimit = 125f, InitialAngle = Angle(offset),
                        InitialRotation = original.rotation, Z = original.position.z
                    };
                    foreach (var tuning in overrides)
                        if (tuning.Bone == original)
                        { bone.Radius = tuning.Radius; bone.BendLimit = tuning.BendLimit; break; }
                    bones.Add(original, bone);
                }
            }

            if (bones.Count == 0) { Destroy(root); return false; }
            foreach (var curves in source.GetComponentsInChildren<StickFigureCurveRenderer>(true))
                curves.CopyTo(transforms);
            // Sort parents before children so writing world poses never drags an already posed child.
            foreach (Transform original in source.GetComponentsInChildren<Transform>(true))
                if (bones.TryGetValue(original, out Bone bone))
                {
                    if (bones.TryGetValue(original.parent, out Bone parent))
                    {
                        bone.Parent = parent;
                        Vector2 axis = (parent.End - parent.Start) / parent.Length;
                        Vector2 delta = bone.Start - parent.Start;
                        bone.Attachment = new Vector2(Vector2.Dot(delta, axis), Vector2.Dot(delta, Perpendicular(axis))) / parent.Length;
                        bone.RestAngle = Mathf.DeltaAngle(parent.InitialAngle, bone.InitialAngle);
                    }
                    visual._bones.Add(bone);
                }

            float direction = Mathf.Abs(velocity.x) > 0.1f ? Mathf.Sign(velocity.x) : (Random.Next(2) == 0 ? -1f : 1f);
            velocity = Vector2.ClampMagnitude(velocity, 40f) + new Vector2(direction * settings.HorizontalKick, settings.UpwardKick);
            Vector2 center = Vector2.zero;
            foreach (Bone bone in visual._bones) center += bone.Start + bone.End;
            center /= visual._bones.Count * 2f;
            float step = Time.fixedDeltaTime / Mathf.Max(1, Mathf.CeilToInt(Time.fixedDeltaTime * 120f));
            foreach (Bone bone in visual._bones)
            {
                bone.RenderStart = bone.Start;
                bone.RenderEnd = bone.End;
                bone.PreviousStart = bone.Start - (velocity + Perpendicular(bone.Start - center) * (-direction * settings.Tumble)) * step;
                bone.PreviousEnd = bone.End - (velocity + Perpendicular(bone.End - center) * (-direction * settings.Tumble)) * step;
            }
            visual._renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            visual._colors = new Color[visual._renderers.Length];
            for (int i = 0; i < visual._renderers.Length; i++) visual._colors[i] = visual._renderers[i].color;
            // The oldest corpse is removed under load; never let cosmetics grow without bound.
            if (Active.Count >= Capacity)
            {
                CharacterRagdollVisual oldest = Active[0];
                Active.RemoveAt(0);
                if (oldest != null) { oldest.gameObject.SetActive(false); Destroy(oldest.gameObject); }
            }
            Active.Add(visual);
            visual._terrain.OnTileDataChanged += visual.Wake;
            root.SetActive(true);
            return true;
        }

        private static void CopyTransforms(Transform source, Transform target, Dictionary<Transform, Transform> map)
        {
            map.Add(source, target);
            target.gameObject.layer = source.gameObject.layer;
            foreach (Transform child in source)
            {
                // Runtime render caches rebuild from the copied skin; do not clone their temporary objects.
                if ((child.gameObject.hideFlags & HideFlags.DontSave) != 0) continue;
                var copy = new GameObject(child.name).transform;
                copy.SetParent(target, false);
                copy.localPosition = child.localPosition;
                copy.localRotation = child.localRotation;
                copy.localScale = child.localScale;
                CopyTransforms(child, copy, map);
                copy.gameObject.SetActive(child.gameObject.activeSelf);
            }
        }

        private static bool HasBoneChild(Transform parent, Transform[] bones)
        {
            foreach (Transform bone in bones)
                if (bone != null && bone.parent == parent) return true;
            return false;
        }

        private static void CopyRenderers(Dictionary<Transform, Transform> map)
        {
            var properties = new MaterialPropertyBlock();
            foreach (var pair in map)
            {
                if (pair.Key.TryGetComponent(out SpriteRenderer source))
                {
                    var copy = pair.Value.gameObject.AddComponent<SpriteRenderer>();
                    copy.sprite = source.sprite;
                    copy.sharedMaterials = source.sharedMaterials;
                    copy.color = source.color;
                    copy.flipX = source.flipX;
                    copy.flipY = source.flipY;
                    copy.drawMode = source.drawMode;
                    copy.size = source.size;
                    copy.maskInteraction = source.maskInteraction;
                    copy.spriteSortPoint = source.spriteSortPoint;
                    copy.sortingLayerID = source.sortingLayerID;
                    copy.sortingOrder = source.sortingOrder;
                    copy.enabled = source.enabled;
                    source.GetPropertyBlock(properties);
                    copy.SetPropertyBlock(properties);
                    properties.Clear();
                }
                if (pair.Key.TryGetComponent(out SortingGroup group))
                {
                    var copy = pair.Value.gameObject.AddComponent<SortingGroup>();
                    copy.sortingLayerID = group.sortingLayerID;
                    copy.sortingOrder = group.sortingOrder;
                    copy.sortAtRoot = group.sortAtRoot;
                }
            }
        }

        private void FixedUpdate()
        {
            if (WorldManager.Instance == null || !WorldManager.Instance.IsWorldReady || WorldManager.Instance.FgGrid != _terrain)
            { Destroy(gameObject); return; }
            foreach (Bone bone in _bones) { bone.RenderStart = bone.Start; bone.RenderEnd = bone.End; }
            if (_sleeping) return;
            // Substeps at least 120 Hz: stable and independent of rendering frame rate.
            int steps = Mathf.Max(1, Mathf.CeilToInt(Time.fixedDeltaTime * 120f));
            float step = Time.fixedDeltaTime / steps;
            for (int s = 0; s < steps; s++) Simulate(step);
        }

        private void LateUpdate() => ApplyPose(Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime));

        private void Wake(Vector2Int position)
        {
            _sleeping = false;
            _quietTime = 0f;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _settings.Lifetime) { Destroy(gameObject); return; }
            float alpha = _settings.FadeDuration > 0f ? Mathf.Clamp01((_settings.Lifetime - _age) / _settings.FadeDuration) : 1f;
            for (int i = 0; i < _renderers.Length; i++)
            {
                Color color = _colors[i];
                color.a *= alpha;
                _renderers[i].color = color;
            }
        }

        private void Simulate(float dt)
        {
            float drag = Mathf.Exp(-_settings.AirDrag * dt);
            float availableEnergy = 0f;
            foreach (Bone bone in _bones)
            {
                bone.StepStart = bone.Start;
                bone.StepEnd = bone.End;
                bone.StartContact = bone.EndContact = Vector2.zero;
                Integrate(ref bone.Start, ref bone.PreviousStart, dt, drag);
                Integrate(ref bone.End, ref bone.PreviousEnd, dt, drag);
                availableEnergy += (bone.Start - bone.StepStart).sqrMagnitude + (bone.End - bone.StepEnd).sqrMagnitude;
            }
            for (int iteration = 0; iteration < SolverIterations; iteration++)
            {
                foreach (Bone bone in _bones)
                {
                    Bone parent = bone.Parent;
                    if (parent != null)
                    {
                        Vector2 axis = parent.End - parent.Start;
                        Vector2 target = parent.Start + axis * bone.Attachment.x + Perpendicular(axis) * bone.Attachment.y;
                        Vector2 error = bone.Start - target;
                        float a = 1f - bone.Attachment.x, b = bone.Attachment.x, v = bone.Attachment.y;
                        float weight = 1f / (1f + a * a + b * b + 2f * v * v);
                        // Include the offset socket's torque (important for the deer's shoulders/hips).
                        bone.Start -= error * weight;
                        parent.Start += (error * a + Perpendicular(error) * v) * weight;
                        parent.End += (error * b - Perpendicular(error) * v) * weight;

                        ConstrainBend(bone, parent);
                    }
                    Vector2 difference = bone.End - bone.Start;
                    float length = difference.magnitude;
                    if (length > 0.00001f)
                    {
                        Vector2 correction = difference * ((length - bone.Length) / (2f * length));
                        bone.Start += correction;
                        bone.End -= correction;
                    }
                }
                foreach (Bone bone in _bones) Collide(bone);
            }
            // Contacts are velocity constraints, applied once after positional convergence.
            // Position corrections must not become a fresh source of kinetic energy next step.
            float solvedEnergy = 0f, maxMotion = 0f;
            bool touching = false;
            foreach (Bone bone in _bones)
            {
                Vector2 startVelocity = ContactVelocity(bone.Start - bone.StepStart, bone.StartContact);
                Vector2 endVelocity = ContactVelocity(bone.End - bone.StepEnd, bone.EndContact);
                bone.PreviousStart = bone.Start - startVelocity;
                bone.PreviousEnd = bone.End - endVelocity;
                solvedEnergy += startVelocity.sqrMagnitude + endVelocity.sqrMagnitude;
                maxMotion = Mathf.Max(maxMotion, (bone.Start - bone.StepStart).magnitude, (bone.End - bone.StepEnd).magnitude);
                touching |= bone.StartContact.sqrMagnitude > 0f || bone.EndContact.sqrMagnitude > 0f;
            }
            if (solvedEnergy > availableEnergy && solvedEnergy > 0.00000001f)
            {
                float scale = Mathf.Sqrt(availableEnergy / solvedEnergy);
                foreach (Bone bone in _bones)
                {
                    bone.PreviousStart = Vector2.Lerp(bone.Start, bone.PreviousStart, scale);
                    bone.PreviousEnd = Vector2.Lerp(bone.End, bone.PreviousEnd, scale);
                }
            }
            _quietTime = touching && maxMotion < 0.2f * dt ? _quietTime + dt : 0f;
            if (_quietTime >= 0.4f) _sleeping = true;
        }

        private static void ConstrainBend(Bone bone, Bone parent)
        {
            Vector2 childAxis = bone.End - bone.Start, parentAxis = parent.End - parent.Start;
            if (childAxis.sqrMagnitude < 0.000001f || parentAxis.sqrMagnitude < 0.000001f) return;
            float relative = Mathf.DeltaAngle(Angle(parentAxis) + bone.RestAngle, Angle(childAxis));
            float error = relative - Mathf.Clamp(relative, -bone.BendLimit, bone.BendLimit);
            if (Mathf.Abs(error) < 0.001f) return;
            Vector2 childGradient = Perpendicular(childAxis) / childAxis.sqrMagnitude;
            Vector2 parentGradient = Perpendicular(parentAxis) / parentAxis.sqrMagnitude;
            float impulse = Mathf.Clamp(error, -12f, 12f) * Mathf.Deg2Rad * 0.7f
                / (2f * (childGradient.sqrMagnitude + parentGradient.sqrMagnitude));
            // Share the correction across both bodies instead of teleporting the child's endpoint.
            bone.Start += childGradient * impulse;
            bone.End -= childGradient * impulse;
            parent.Start -= parentGradient * impulse;
            parent.End += parentGradient * impulse;
        }

        private void Integrate(ref Vector2 position, ref Vector2 previous, float dt, float drag)
        {
            Vector2 next = position + Vector2.ClampMagnitude((position - previous) * drag, 80f * dt)
                + Vector2.down * (_settings.Gravity * dt * dt);
            previous = position;
            position = next;
        }

        private void Collide(Bone bone)
        {
            int samples = Mathf.Clamp(Mathf.CeilToInt(bone.Length / 0.12f), 1, 12);
            for (int sample = 0; sample <= samples; sample++)
            {
                float t = (float)sample / samples;
                Vector2 point = Vector2.Lerp(bone.Start, bone.End, t);
                Vector2 correction = TerrainCorrection(point, bone.Radius);
                if (correction.sqrMagnitude < 0.00000001f) continue;
                float a = 1f - t, b = t, denominator = a * a + b * b;
                bone.Start += correction * (a / denominator);
                bone.End += correction * (b / denominator);
                Vector2 normal = correction.normalized;
                if (a > 0f) bone.StartContact += normal * a;
                if (b > 0f) bone.EndContact += normal * b;
            }
        }

        private Vector2 ContactVelocity(Vector2 velocity, Vector2 contact)
        {
            if (contact.sqrMagnitude < 0.000001f) return velocity;
            Vector2 normal = contact.normalized;
            float inward = Vector2.Dot(velocity, normal);
            if (inward < 0f) velocity -= normal * inward;
            return velocity * (1f - _settings.GroundFriction);
        }

        private Vector2 TerrainCorrection(Vector2 point, float radius)
        {
            Vector2 corrected = point;
            int minX = Mathf.FloorToInt(point.x - radius), maxX = Mathf.FloorToInt(point.x + radius);
            int minY = Mathf.FloorToInt(point.y - radius), maxY = Mathf.FloorToInt(point.y + radius);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    TileData tile = _terrain.GetTileData(x, y);
                    if (!(tile.HasTile && tile.IsSolid) && !tile.IsOutOfBounds) continue;
                    if ((corrected.x < x && Solid(x - 1, y)) || (corrected.x > x + 1f && Solid(x + 1, y))
                        || (corrected.y < y && Solid(x, y - 1)) || (corrected.y > y + 1f && Solid(x, y + 1))) continue;
                    Vector2 nearest = new(Mathf.Clamp(corrected.x, x, x + 1f), Mathf.Clamp(corrected.y, y, y + 1f));
                    Vector2 delta = corrected - nearest;
                    float distance = delta.magnitude;
                    if (distance >= radius) continue;
                    if (distance > 0.00001f) corrected += delta * ((radius - distance) / distance);
                    else
                    {
                        // Internal tile seams are not surfaces. They must never eject a joint sideways.
                        float left = Solid(x - 1, y) ? float.PositiveInfinity : corrected.x - x;
                        float right = Solid(x + 1, y) ? float.PositiveInfinity : x + 1f - corrected.x;
                        float bottom = Solid(x, y - 1) ? float.PositiveInfinity : corrected.y - y;
                        float top = Solid(x, y + 1) ? float.PositiveInfinity : y + 1f - corrected.y;
                        float closest = Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top));
                        if (float.IsPositiveInfinity(closest)) continue;
                        if (closest == left) corrected.x = x - radius;
                        else if (closest == right) corrected.x = x + 1f + radius;
                        else if (closest == bottom) corrected.y = y - radius;
                        else corrected.y = y + 1f + radius;
                    }
                }
            return corrected - point;
        }

        private bool Solid(int x, int y)
        {
            TileData tile = _terrain.GetTileData(x, y);
            return (tile.HasTile && tile.IsSolid) || tile.IsOutOfBounds;
        }

        private void ApplyPose(float blend)
        {
            foreach (Bone bone in _bones)
            {
                Vector2 position = Vector2.Lerp(bone.RenderStart, bone.Start, blend);
                float angle = Mathf.DeltaAngle(bone.InitialAngle, Mathf.LerpAngle(Angle(bone.RenderEnd - bone.RenderStart), Angle(bone.End - bone.Start), blend));
                bone.Transform.SetPositionAndRotation(new Vector3(position.x, position.y, bone.Z),
                    Quaternion.AngleAxis(angle, Vector3.forward) * bone.InitialRotation);
            }
        }

        private static Vector2 Perpendicular(Vector2 value) => new(-value.y, value.x);
        private static float Angle(Vector2 value) => Mathf.Atan2(value.y, value.x) * Mathf.Rad2Deg;
        private void OnDestroy()
        {
            if (_terrain != null) _terrain.OnTileDataChanged -= Wake;
            Active.Remove(this);
        }
    }
}
