using System;
using UnityEngine;

namespace SketchLand
{
    public enum PlayerAnimationStyle { Swing, Bow, Clip, Eating }

    /// <summary>Presentation data only. Editing this asset never changes damage, charge, or cooldown.</summary>
    [CreateAssetMenu(menuName = "SketchLand/Animation/Player Animation", fileName = "PlayerAnimation")]
    public sealed class PlayerAnimationProfile : ScriptableObject
    {
        public PlayerAnimationStyle Style;
        [Tooltip("Default keeps action poses off the hips and legs. Disable for the original body sway.")]
        public bool UpperBodyOnly = true;
        [Min(0f)] public float Intensity = 1f;
        public SwingVisualSettings Swing = new();
        public BowVisualSettings Bow = new();
        public EatingVisualSettings Eating = new();
        public AnimationClip Clip;
        [Tooltip("Use the supplied UpperBody mask for gestures while moving. Null allows a full-body clip.")]
        public AvatarMask Mask;
        public bool Additive;
        [Min(0.001f)] public float Speed = 1f;
        [Min(0f)] public float BlendIn = 0.08f;
        [Min(0f)] public float BlendOut = 0.12f;
    }

    [Serializable]
    public sealed class EatingVisualSettings
    {
        [Tooltip("Mouth position relative to the Animator root, aligned to the head bone.")]
        public Vector2 MouthOffset = new(0.19f, 0.15f);
        [Min(0.01f)] public float RaiseDuration = 0.18f;
        [Min(0f)] public float BiteFrequency = 4.5f;
        [Min(0f)] public float HandBob = 0.025f;
        [Range(0f, 20f)] public float HeadNod = 4f;
    }

    [Serializable]
    public sealed class SwingVisualSettings
    {
        [Tooltip("Normalized gameplay progress → original performance time. Identity preserves the old timing.")]
        public AnimationCurve TimeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public AnimationCurve BodyCurve = new(new Keyframe(0f, 0f), new Keyframe(0.34f, 1f),
            new Keyframe(0.39f, 1f), new Keyframe(0.56f, -1f), new Keyframe(0.62f, -1f), new Keyframe(1f, 0f));
        [Range(0f, 2f)] public float Exaggeration = 0.612f;
        [Range(0f, 90f)] public float TorsoRotation = 43f;
        [Range(0f, 90f)] public float HeadCounterRotation = 25f;
        public Vector2 FreeHandTravel = new(0.44f, 0.24f);
        [Range(0.01f, 0.8f)] public float WindUpEnd = 0.42f;
        [Range(0.01f, 0.95f)] public float StrikeEnd = 0.52f;
        [Range(0.01f, 0.99f)] public float ReturnStart = 0.82f;
    }

    [Serializable]
    public sealed class BowVisualSettings
    {
        public Vector2 BowPosition = new(0.55f, 0.03f);
        public Vector2 ReadyHandPosition = new(0.39f, 0.03f);
        public Vector2 DrawHandControlPosition = new(0.14f, 0.16f);
        public Vector2 DrawnHandPosition = new(-0.12f, 0.13f);
        public AnimationCurve DrawCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Min(0.01f)] public float RaiseDuration = 0.12f;
        [Min(0.01f)] public float ReleaseDuration = 0.12f;
        [Min(0.01f)] public float ReturnDuration = 0.12f;
    }
}
