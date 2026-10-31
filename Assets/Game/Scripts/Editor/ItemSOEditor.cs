using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace SketchLand
{
    [CustomEditor(typeof(ItemSO), true)]
    [CanEditMultipleObjects]
    public class ItemSOEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new HelpBox(
                "On HeldItemSprite, use the Transform menu > Copy Component, then paste here. " +
                "This saves the local Position, Z Rotation and Scale exactly as copied. Every selected item uses its own pose. " +
                "Tune item poses on a scene instance to keep the shared player prefab unchanged.", HelpBoxMessageType.Info));

            var status = new HelpBox("", HelpBoxMessageType.Info);
            status.visible = false;
            root.Add(new Button(() =>
            {
                bool success = TryPasteHeldPose(serializedObject, out string message);
                status.text = message;
                status.messageType = success ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
                status.visible = true;
            }) { text = "Paste Held Pose from Copied Transform" });
            root.Add(status);
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            return root;
        }

        internal static bool TryPasteHeldPose(SerializedObject items, out string message)
        {
            var scratch = new GameObject("Held Pose Clipboard", typeof(Transform))
            {
                hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave
            };
            try
            {
                if (!ComponentUtility.PasteComponentValues(scratch.transform))
                {
                    message = "Copy HeldItemSprite's Transform using Copy Component first. Copying individual values or another component will not work.";
                    return false;
                }

                Transform copied = scratch.transform;
                float rotation = copied.localEulerAngles.z;
                if (Quaternion.Angle(copied.localRotation, Quaternion.Euler(0f, 0f, rotation)) > 0.01f)
                {
                    message = "The copied rotation tilts the sprite out of the 2D plane. Adjust only its Z rotation.";
                    return false;
                }

                items.Update();
                items.FindProperty("<HeldPosition>k__BackingField").vector3Value = copied.localPosition;
                items.FindProperty("<HeldRotation>k__BackingField").floatValue = rotation;
                items.FindProperty("<HeldScale>k__BackingField").vector3Value = copied.localScale;
                items.ApplyModifiedProperties();
                foreach (Object item in items.targetObjects)
                    AssetDatabase.SaveAssetIfDirty(item);

                message = $"Saved custom held pose: Position {copied.localPosition:F4}, Z Rotation {rotation:F3}, Scale {copied.localScale:F4}.";
                return true;
            }
            finally
            {
                DestroyImmediate(scratch);
            }
        }
    }
}
