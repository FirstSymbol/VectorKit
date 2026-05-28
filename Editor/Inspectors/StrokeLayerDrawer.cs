using UnityEditor;
using UnityEngine;
using VectorKit.Runtime;

namespace VectorKit.Editor
{
    [CustomPropertyDrawer(typeof(StrokeLayer))]
    public class StrokeLayerDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 2;
            var fill = property.FindPropertyRelative("Fill");
            if (fill != null) h += EditorGUI.GetPropertyHeight(fill, true);
            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float lh = EditorGUIUtility.singleLineHeight;
            float sp = EditorGUIUtility.standardVerticalSpacing;
            float y  = position.y, x = position.x, w = position.width;

            var enabledProp    = property.FindPropertyRelative("Enabled");
            var blendProp      = property.FindPropertyRelative("BlendMode");
            var opacityProp    = property.FindPropertyRelative("Opacity");
            var widthProp      = property.FindPropertyRelative("Width");
            var alignmentProp  = property.FindPropertyRelative("Alignment");
            var fillProp       = property.FindPropertyRelative("Fill");

            // Row 1: enabled | blend mode | opacity
            float toggleW = 16, blendW = 110, opW = w - toggleW - blendW - 4;
            EditorGUI.PropertyField(new Rect(x, y, toggleW, lh), enabledProp, GUIContent.none);
            EditorGUI.PropertyField(new Rect(x + toggleW + 2, y, blendW, lh), blendProp, GUIContent.none);
            EditorGUI.PropertyField(new Rect(x + toggleW + blendW + 4, y, opW, lh), opacityProp, GUIContent.none);

            // Row 2: Width | Alignment
            y += lh + sp;
            float halfW = (w - 4) * 0.5f;
            EditorGUI.LabelField(new Rect(x, y, 50, lh), "Width");
            EditorGUI.PropertyField(new Rect(x + 52, y, halfW - 52, lh), widthProp, GUIContent.none);
            EditorGUI.LabelField(new Rect(x + halfW + 4, y, 60, lh), "Align");
            EditorGUI.PropertyField(new Rect(x + halfW + 66, y, w - halfW - 70, lh), alignmentProp, GUIContent.none);

            // Fill
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
