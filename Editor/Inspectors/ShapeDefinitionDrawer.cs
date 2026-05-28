using System;
using UnityEditor;
using UnityEngine;
using VectorKit.Runtime;

namespace VectorKit.Editor
{
    // Draws a [SerializeReference] ShapeDefinition field: type dropdown + child fields.
    // NEVER calls PropertyField on the managed-reference property itself.
    // Children are drawn via NextVisible() since they are all concrete value types.
    [CustomPropertyDrawer(typeof(ShapeDefinition), true)]
    public class ShapeDefinitionDrawer : PropertyDrawer
    {
        private static readonly Type[] s_Types =
        {
            typeof(RectangleShape), typeof(EllipseShape), typeof(PolygonShape),
            typeof(StarShape), typeof(LineShape), typeof(ArcShape),
            typeof(CapsuleShape), typeof(TriangleShape), typeof(HeartShape),
            typeof(PathShape), typeof(BooleanShape),
        };

        private static readonly GUIContent[] s_Names =
        {
            new("Rectangle"), new("Ellipse"), new("Polygon"),
            new("Star"),      new("Line"),    new("Arc"),
            new("Capsule"),   new("Triangle"),new("Heart"),
            new("Path"),      new("Boolean"),
        };

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            if (!property.isExpanded) return h;
            h += ChildrenHeight(property);
            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Foldout + type dropdown on the same line
            var foldRect  = new Rect(position.x, position.y, position.width - 130, EditorGUIUtility.singleLineHeight);
            var popupRect = new Rect(position.x + position.width - 128, position.y, 128, EditorGUIUtility.singleLineHeight);

            property.isExpanded = EditorGUI.Foldout(foldRect, property.isExpanded, label, true);

            int cur = Array.FindIndex(s_Types, t => t == property.managedReferenceValue?.GetType());
            int next = EditorGUI.Popup(popupRect, cur < 0 ? 0 : cur, s_Names);
            if (next != cur || property.managedReferenceValue == null)
            {
                property.managedReferenceValue = Activator.CreateInstance(s_Types[Mathf.Max(0, next)]);
            }

            if (property.isExpanded)
            {
                position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                DrawChildren(property, ref position);
            }

            EditorGUI.EndProperty();
        }

        internal static void DrawChildren(SerializedProperty prop, ref Rect pos)
        {
            var it    = prop.Copy();
            var end   = it.GetEndProperty();
            bool first = true;

            EditorGUI.indentLevel++;
            while (it.NextVisible(first))
            {
                first = false;
                if (SerializedProperty.EqualContents(it, end)) break;

                float h = EditorGUI.GetPropertyHeight(it, true);
                var r   = new Rect(pos.x, pos.y, pos.width, h);

                // Nested managed refs (e.g. BooleanShape.Operations[i].Shape) are handled
                // by this same drawer being called recursively via PropertyField, which is
                // valid here because 'it' is a concrete list element or value-type field.
                EditorGUI.PropertyField(r, it, true);
                pos.y += h + EditorGUIUtility.standardVerticalSpacing;
            }
            EditorGUI.indentLevel--;
        }

        internal static float ChildrenHeight(SerializedProperty prop)
        {
            float h   = 0;
            var   it  = prop.Copy();
            var   end = it.GetEndProperty();
            bool first = true;
            while (it.NextVisible(first))
            {
                first = false;
                if (SerializedProperty.EqualContents(it, end)) break;
                h += EditorGUI.GetPropertyHeight(it, true) + EditorGUIUtility.standardVerticalSpacing;
            }
            return h;
        }
    }
}
