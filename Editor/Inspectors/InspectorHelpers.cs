using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VectorKit.Runtime;

namespace VectorKit.Editor
{
    internal static class InspectorHelpers
    {
        internal static ReorderableList BuildLayerList(
            SerializedProperty prop, string header,
            ReorderableList.AddCallbackDelegate onAdd)
        {
            var list = new ReorderableList(prop.serializedObject, prop, true, true, true, true);

            list.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, header, EditorStyles.boldLabel);

            list.drawElementCallback = (rect, index, active, focused) =>
            {
                var el = prop.GetArrayElementAtIndex(index);
                rect.y += 2; rect.height -= 4;
                EditorGUI.PropertyField(rect, el, GUIContent.none, true);
            };

            list.elementHeightCallback = index =>
            {
                var el = prop.GetArrayElementAtIndex(index);
                return EditorGUI.GetPropertyHeight(el, true) + 4;
            };

            list.onAddCallback = onAdd;
            return list;
        }

        internal static void AddFillLayer(ReorderableList list)
        {
            var prop = list.serializedProperty;
            prop.InsertArrayElementAtIndex(prop.arraySize);
            prop.GetArrayElementAtIndex(prop.arraySize - 1).boxedValue = new FillLayer();
        }

        internal static void AddStrokeLayer(ReorderableList list)
        {
            var prop = list.serializedProperty;
            prop.InsertArrayElementAtIndex(prop.arraySize);
            prop.GetArrayElementAtIndex(prop.arraySize - 1).boxedValue = new StrokeLayer();
        }

        internal static void AddEffect(ReorderableList list)
        {
            var prop = list.serializedProperty;
            prop.InsertArrayElementAtIndex(prop.arraySize);
            prop.GetArrayElementAtIndex(prop.arraySize - 1).managedReferenceValue = new DropShadowEffect();
        }
    }
}
