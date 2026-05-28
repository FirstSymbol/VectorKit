using System;
using UnityEditor;
using UnityEngine;
using VectorKit.Runtime;

namespace VectorKit.Editor
{
    // Draws a [SerializeReference] FillDefinition with type dropdown + child fields.
    [CustomPropertyDrawer(typeof(FillDefinition), true)]
    public class FillDefinitionDrawer : PropertyDrawer
    {
        private static readonly Type[] s_Types =
        {
            typeof(SolidFill), typeof(LinearGradientFill), typeof(RadialGradientFill),
            typeof(ConicGradientFill), typeof(ImageFill),
        };

        private static readonly GUIContent[] s_Names =
        {
            new("Solid"), new("Linear Gradient"), new("Radial Gradient"),
            new("Conic Gradient"), new("Image"),
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

            var foldRect  = new Rect(position.x, position.y, position.width - 130, EditorGUIUtility.singleLineHeight);
            var popupRect = new Rect(position.x + position.width - 128, position.y, 128, EditorGUIUtility.singleLineHeight);

            property.isExpanded = EditorGUI.Foldout(foldRect, property.isExpanded, label, true);

            int cur  = Array.FindIndex(s_Types, t => t == property.managedReferenceValue?.GetType());
            int next = EditorGUI.Popup(popupRect, cur < 0 ? 0 : cur, s_Names);
            if (next != cur || property.managedReferenceValue == null)
                property.managedReferenceValue = Activator.CreateInstance(s_Types[Mathf.Max(0, next)]);

            if (property.isExpanded)
            {
                position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                ShapeDefinitionDrawer.DrawChildren(property, ref position);
            }

            EditorGUI.EndProperty();
        }
    }
}
