// Run in a stopped Editor:
// unity command script run --file Tools/Validation/CurveRendererChecks.cs --entry CurveRendererChecks.Run --project-path <project> --format json
// This file lives outside Assets so the validation harness is not included in game builds.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;
using UnityEngine.U2D.IK;
using SketchLand;

public static class CurveRendererChecks
{
    private static int _assertions;
    private static void Check(bool condition, string message)
    {
        _assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private struct Pose
    {
        public Transform Bone;
        public Vector3 Position, Scale;
        public Quaternion Rotation;
        public Pose(Transform bone)
        { Bone = bone; Position = bone.localPosition; Scale = bone.localScale; Rotation = bone.localRotation; }
        public bool Unchanged => Bone.localPosition == Position && Bone.localRotation == Rotation && Bone.localScale == Scale;
    }

    private static MeshFilter[] Geometry(Transform root) => root.GetComponentsInChildren<MeshFilter>(true)
        .Where(m => m.sharedMesh != null && (m.sharedMesh.name == "Curved strokes" || m.sharedMesh.name == "Curved artwork")).ToArray();

    public static object Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run these isolated prefab checks outside Play mode.");
        _assertions = 0;
        var results = new List<object>();
        string[] paths = {
            "Assets/Game/Content/Prefabs/Characters/Player.prefab",
            "Assets/Game/Content/Prefabs/Characters/ZombieNpc.prefab",
            "Assets/Game/Content/Prefabs/Characters/DeerNpc.prefab"
        };
        string[] folders = { "StickFigure", "Zombie", "Deer" };
        string[][] clips = {
            new[] { "StickFigure_Idle", "StickFigure_Walk", "StickFigure_LightJog", "StickFigure_Sprint", "StickFigure_Airborne", "StickFigure_Damaged" },
            new[] { "Zombie_Idle", "Zombie_Walk", "Zombie_Airborne", "Zombie_Damaged" },
            new[] { "Deer_Idle", "Deer_Trot" }
        };
        for (int rig = 0; rig < paths.Length; rig++)
        {
            var root = PrefabUtility.LoadPrefabContents(paths[rig]);
            try
            {
                var skin = root.GetComponentInChildren<SpriteSkin>(true);
                var source = skin.GetComponent<SpriteRenderer>();
                var curve = skin.GetComponent<StickFigureCurveRenderer>();
                Check(curve != null, "Missing curve component: " + paths[rig]);
                // Neither live transform names nor imported bone names may determine chain setup.
                var originalSprite = source.sprite;
                var renamedSprite = UnityEngine.Object.Instantiate(originalSprite);
                try
                {
                    var metadata = UnityEngine.U2D.SpriteDataAccessExtensions.GetBones(renamedSprite);
                    for (int i = 0; i < metadata.Length; i++) metadata[i].name = "Unrelated bone " + i;
                    UnityEngine.U2D.SpriteDataAccessExtensions.SetBones(renamedSprite, metadata);
                    source.sprite = renamedSprite;
                    foreach (var bone in skin.boneTransforms) bone.name = "Arbitrary transform " + bone.GetInstanceID();
                    curve.Reinitialize();
                    Check(curve.SkinCount == 1 && curve.StrokeChainCount == (rig == 2 ? 4 : 5), "Name-independent discovery: " + curve.Status);
                    int poseCount = 0;
                    foreach (string name in clips[rig])
                    {
                        // Clip paths target the original names, so restore transform names for sampling only.
                        var originalBones = UnityEngine.U2D.SpriteDataAccessExtensions.GetBones(originalSprite);
                        for (int i = 0; i < skin.boneTransforms.Length; i++) skin.boneTransforms[i].name = originalBones[i].name;
                        // The deer has duplicate imported names; its actual hierarchy names are restored below.
                        if (rig == 2)
                        {
                            var assetSkin = AssetDatabase.LoadAssetAtPath<GameObject>(paths[rig]).GetComponentInChildren<SpriteSkin>(true);
                            for (int i = 0; i < skin.boneTransforms.Length; i++) skin.boneTransforms[i].name = assetSkin.boneTransforms[i].name;
                        }
                        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Game/Content/Art/Animations/" + folders[rig] + "/" + name + ".anim");
                        Check(clip != null, "Missing clip " + name);
                        foreach (float phase in new[] { 0f, .2f, .5f, .9f })
                        {
                            clip.SampleAnimation(skin.gameObject, clip.length * phase);
                            skin.GetComponent<IKManager2D>().UpdateManager();
                            var poses = skin.boneTransforms.Select(b => new Pose(b)).ToArray();
                            curve.RefreshNow();
                            Check(poses.All(p => p.Unchanged), "Rendering changed a bone pose.");
                            Check(source.forceRenderingOff, "Source not replaced.");
                            foreach (var filter in Geometry(skin.transform))
                            {
                                var vertices = filter.sharedMesh.vertices;
                                Check(vertices.All(p => !float.IsNaN(p.x) && !float.IsInfinity(p.x)
                                    && !float.IsNaN(p.y) && !float.IsInfinity(p.y)), "Invalid curve geometry.");
                                Check(filter.sharedMesh.triangles.All(i => i >= 0 && i < vertices.Length), "Invalid triangle index.");
                            }
                            poseCount++;
                        }
                    }
                    var geometry = Geometry(skin.transform);
                    int[] meshIds = geometry.Select(m => m.sharedMesh.GetInstanceID()).ToArray();
                    var beforeFlip = geometry.Select(m => m.sharedMesh.vertices).ToArray();
                    source.flipX = true;
                    curve.RefreshNow();
                    for (int mesh = 0; mesh < geometry.Length; mesh++)
                    {
                        var after = geometry[mesh].sharedMesh.vertices;
                        for (int v = 0; v < after.Length; v++)
                            Check(Vector3.Distance(after[v], new Vector3(-beforeFlip[mesh][v].x, beforeFlip[mesh][v].y, beforeFlip[mesh][v].z)) < .00001f, "Sprite flip mismatch.");
                    }
                    source.flipX = false;
                    source.color = new Color(.2f, .7f, .4f, .8f);
                    source.sortingOrder = 17;
                    curve.RefreshNow();
                    var properties = new MaterialPropertyBlock();
                    foreach (var filter in geometry)
                    {
                        var renderer = filter.GetComponent<MeshRenderer>();
                        renderer.GetPropertyBlock(properties);
                        Check(properties.GetColor("_RendererColor") == source.color, "Tint was not forwarded.");
                        Check(renderer.sortingOrder == source.sortingOrder && renderer.sharedMaterial == source.sharedMaterial, "Render state mismatch.");
                    }
                    for (int i = 0; i < 25; i++)
                    {
                        curve.UseCurvedLimbs = false;
                        Check(!source.forceRenderingOff && geometry.All(m => !m.gameObject.activeInHierarchy), "Original appearance was not restored.");
                        curve.UseCurvedLimbs = true;
                    }
                    Check(Geometry(skin.transform).Select(m => m.sharedMesh.GetInstanceID()).SequenceEqual(meshIds), "Toggle rebuilt mesh resources.");
                    for (int i = 0; i < 10; i++) curve.Reinitialize();
                    Check(Geometry(skin.transform).Length == geometry.Length, "Reinitialization leaked generated geometry.");
                    var validBones = (Transform[])skin.boneTransforms.Clone();
                    var invalidBones = (Transform[])validBones.Clone(); invalidBones[0] = null;
                    skin.SetBoneTransforms(invalidBones); curve.RefreshNow();
                    Check(!source.forceRenderingOff && curve.SkinCount == 0, "Invalid bindings hid the original sprite.");
                    skin.SetBoneTransforms(validBones); curve.RefreshNow();
                    Check(curve.SkinCount == 1 && source.forceRenderingOff, "Late binding did not recover automatically.");
                    curve.enabled = false;
                    Check(!source.forceRenderingOff && Geometry(skin.transform).Length == 0, "Disabling did not release resources.");
                    curve.enabled = true; curve.RefreshNow();
                    Check(curve.SkinCount == 1 && source.forceRenderingOff, "Re-enabling failed.");
                    results.Add(new { rig = folders[rig], poses = poseCount, chains = curve.StrokeChainCount });
                }
                finally { source.sprite = originalSprite; UnityEngine.Object.DestroyImmediate(renamedSprite); }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        CheckMultipleSkins();
        float baselineError = CheckPlayerBaseline();
        return new { assertions = _assertions, rigs = results, multipleSkins = true, playerBaselineMaxError = baselineError };
    }

    private static void CheckMultipleSkins()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Game/Content/Prefabs/StickFigureRig.prefab");
        GameObject combined = null;
        try
        {
            root.GetComponent<StickFigureCurveRenderer>().enabled = false;
            combined = new GameObject("Multi-skin validation");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(combined, root.scene);
            for (int i = 0; i < 2; i++)
            {
                var copy = UnityEngine.Object.Instantiate(root, combined.transform);
                UnityEngine.Object.DestroyImmediate(copy.GetComponent<StickFigureCurveRenderer>());
            }
            var curve = combined.AddComponent<StickFigureCurveRenderer>(); curve.RefreshNow();
            Check(curve.SkinCount == 2 && curve.StrokeChainCount == 10, "Multi-skin root discovery failed.");
            curve.UseCurvedLimbs = false;
            Check(combined.GetComponentsInChildren<SpriteSkin>().All(s => !s.GetComponent<SpriteRenderer>().forceRenderingOff), "Multi-skin toggle failed.");
        }
        finally { if (combined != null) UnityEngine.Object.DestroyImmediate(combined); PrefabUtility.UnloadPrefabContents(root); }
    }

    private static object Field(object target, string name) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);

    // Golden geometry was recorded from the approved prototype before replacing its name-based setup.
    private static float CheckPlayerBaseline()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Game/Content/Prefabs/StickFigureRig.prefab");
        try
        {
            var curve = root.GetComponent<StickFigureCurveRenderer>();
            curve.UseCurvedLimbs = true;
            var baseline = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText("Tools/Validation/Fixtures/player-curve-baseline.json"));
            float maxError = 0;
            foreach (var pose in baseline)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Game/Content/Art/Animations/StickFigure/StickFigure_" + (string)pose["name"] + ".anim");
                clip.SampleAnimation(root, clip.length * (float)pose["phase"]);
                root.GetComponent<IKManager2D>().UpdateManager();
                curve.RefreshNow();
                foreach (var view in (System.Collections.IEnumerable)Field(curve, "_views"))
                {
                    var bones = (Transform[])Field(view, "_bones");
                    var positions = (Vector3[])Field(view, "_strokePositions");
                    foreach (var expected in pose["chains"])
                    {
                        object found = null;
                        foreach (var chain in (System.Collections.IEnumerable)Field(view, "Strokes"))
                        {
                            var indices = (List<int>)Field(chain, "Bones");
                            if (AnimationUtility.CalculateTransformPath(bones[indices[0]], root.transform) == (string)expected["path"])
                            { found = chain; break; }
                        }
                        Check(found != null, "Approved player chain missing: " + (string)expected["path"]);
                        int offset = (int)Field(found, "VertexOffset");
                        int vertex = 0;
                        foreach (var xyz in expected["vertices"])
                        {
                            var wanted = new Vector3((float)xyz[0], (float)xyz[1], (float)xyz[2]);
                            maxError = Mathf.Max(maxError, Vector3.Distance(wanted, positions[offset + vertex++]));
                        }
                    }
                }
            }
            Check(maxError < .00001f, "Approved player silhouette changed: " + maxError);
            return maxError;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
