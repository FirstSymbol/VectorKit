using System;
using UnityEditor;
using UnityEngine;
using VectorKit.Runtime;

namespace VectorKit.Editor
{
    // Draws a [SerializeReference] VectorEffect element with type selector.
    [CustomPropertyDrawer(typeof(VectorEffect), true)]
    public class VectorEffectDrawer : PropertyDrawer
    {
        private static readonly Type[] s_Types =
        {
            typeof(DropShadowEffect), typeof(InnerShadowEffect),
            typeof(OuterGlowEffect),  typeof(InnerGlowEffect),
            typeof(GaussianBlurEffect), typeof(BevelEffect),
        };

        private static readonly GUIContent[] s_Names =
        {
            new("Drop Shadow"),   new("Inner Shadow"),
            new("Outer Glow"),    new("Inner Glow"),
            new("Gaussian Blur"), new("Bevel"),
        };

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            if (property.isExpanded) h += ShapeDefinitionDrawer.ChildrenHeight(property);
            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float lh = EditorGUIUtility.singleLineHeight;
            var topRow = new Rect(position.x, position.y, position.width, lh);

            var foldRect  = new Rect(topRow.x, topRow.y, topRow.width - 130, lh);
            var popupRect = new Rect(topRow.x + topRow.width - 128, topRow.y, 128, lh);

            property.isExpanded = EditorGUI.Foldout(foldRect, property.isExpanded, label, true);

            int cur  = Array.FindIndex(s_Types, t => t == property.managedReferenceValue?.GetType());
            int next = EditorGUI.Popup(popupRect, cur < 0 ? 0 : cur, s_Names);
            if (next != cur || property.managedReferenceValue == null)
                property.managedReferenceValue = Activator.CreateInstance(s_Types[Mathf.Max(0, next)]);

            if (property.isExpanded)
            {
                position.y += lh + EditorGUIUtility.standardVerticalSpacing;
                ShapeDefinitionDrawer.DrawChildren(property, ref position);
            }

            EditorGUI.EndProperty();
        }
    }
}
