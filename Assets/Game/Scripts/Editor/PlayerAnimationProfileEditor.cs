using UnityEditor;
using UnityEngine;

namespace SketchLand.EditorTools
{
    [CustomEditor(typeof(PlayerAnimationProfile))]
    public sealed class PlayerAnimationProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("Visual settings only. Damage, charge and attack duration live in gameplay/item settings.", MessageType.Info);
            Draw("Style");
            var style = (PlayerAnimationStyle)serializedObject.FindProperty("Style").enumValueIndex;
            if (style == PlayerAnimationStyle.Clip)
            {
                Draw("Clip"); Draw("Mask"); Draw("Additive");
                Draw("Speed"); Draw("BlendIn"); Draw("BlendOut");
                EditorGUILayout.HelpBox("Assign a clip authored relative to StickFigureRig. Use UpperBody.mask to leave locomotion in control of the hips and legs. Add a Cue Animations binding on PlayerAnimationPresenter.", MessageType.None);
            }
            else
            {
                Draw("UpperBodyOnly"); Draw("Intensity");
                Draw(style == PlayerAnimationStyle.Swing ? "Swing" : "Bow");
            }
            serializedObject.ApplyModifiedProperties();
        }
        private void Draw(string property) => EditorGUILayout.PropertyField(serializedObject.FindProperty(property), true);
    }
}
