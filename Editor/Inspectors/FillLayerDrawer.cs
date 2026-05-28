using UnityEditor;
using UnityEngine;
using VectorKit.Runtime;

namespace VectorKit.Editor
{
    // Draws a FillLayer inline: enabled toggle, blend mode, opacity, then the Fill (managed ref).
    [CustomPropertyDrawer(typeof(FillLayer))]
    public class FillLayerDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            var fill = property.FindPropertyRelative("Fill");
            if (fill != null) h += EditorGUI.GetPropertyHeight(fill, true);
            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float lh  = EditorGUIUtility.singleLineHeight;
            float sp  = EditorGUIUtility.standardVerticalSpacing;
            float y   = position.y;
            float x   = position.x;
            float w   = position.width;

            // Row 1: [enabled] [BlendMode popup, 110px] [Opacity slider, rest]
            var enabledProp  = property.FindPropertyRelative("Enabled");
            var blendProp    = property.FindPropertyRelative("BlendMode");
            var opacityProp  = property.FindPropertyRelative("Opacity");
            var fillProp     = property.FindPropertyRelative("Fill");

            float toggleW = 16;
            float blendW  = 110;
            float opW     = w - toggleW - blendW - 4;

            EditorGUI.PropertyField(new Rect(x, y, toggleW, lh), enabledProp, GUIContent.none);
            EditorGUI.PropertyField(new Rect(x + toggleW + 2, y, blendW, lh), blendProp, GUIContent.none);
            EditorGUI.PropertyField(new Rect(x + toggleW + blendW + 4, y, opW, lh), opacityProp, GUIContent.none);

            // Row 2: Fill (FillDefinition managed ref — handled by FillDefinitionDrawer)
            if (fillProp != null)
            {
                y += lh + sp;
                EditorGUI.indentLevel++;
                EditorGUI.PropertyField(new Rect(x, y, w, EditorGUI.GetPropertyHeight(fillProp, true)), fillProp, new GUIContent("Fill"), true);
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }
    }
}
