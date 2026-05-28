using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VectorKit.Runtime;

namespace VectorKit.Editor
{
    [CustomEditor(typeof(VectorShapeWorld))]
    [CanEditMultipleObjects]
    public class VectorShapeWorldInspector : UnityEditor.Editor
    {
        private SerializedProperty _shapeProp;
        private SerializedProperty _fillsProp;
        private SerializedProperty _strokesProp;
        private SerializedProperty _effectsProp;
        private SerializedProperty _sizeProp;
        private SerializedProperty _tintProp;

        private ReorderableList _fillsList;
        private ReorderableList _strokesList;
        private ReorderableList _effectsList;

        private static readonly Type[] s_ShapeTypes =
        {
            typeof(RectangleShape), typeof(EllipseShape), typeof(PolygonShape),
            typeof(StarShape), typeof(LineShape), typeof(ArcShape),
            typeof(CapsuleShape), typeof(TriangleShape), typeof(HeartShape),
            typeof(PathShape), typeof(BooleanShape),
        };

        private static readonly string[] s_ShapeNames =
        {
            "Rectangle", "Ellipse", "Polygon", "Star", "Line", "Arc",
            "Capsule",   "Triangle","Heart",  "Path", "Boolean",
        };

        private void OnEnable()
        {
            _shapeProp   = serializedObject.FindProperty("Shape");
            _fillsProp   = serializedObject.FindProperty("Fills");
            _strokesProp = serializedObject.FindProperty("Strokes");
            _effectsProp = serializedObject.FindProperty("Effects");
            _sizeProp    = serializedObject.FindProperty("Size");
            _tintProp    = serializedObject.FindProperty("Tint");

            _fillsList   = InspectorHelpers.BuildLayerList(_fillsProp,   "Fills",   InspectorHelpers.AddFillLayer);
            _strokesList = InspectorHelpers.BuildLayerList(_strokesProp, "Strokes", InspectorHelpers.AddStrokeLayer);
            _effectsList = InspectorHelpers.BuildLayerList(_effectsProp, "Effects", InspectorHelpers.AddEffect);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);
            DrawShapeDefinition(_shapeProp);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Appearance", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_sizeProp);
            EditorGUILayout.PropertyField(_tintProp);

            EditorGUILayout.Space(4);
            _fillsList.DoLayoutList();
            _strokesList.DoLayoutList();
            _effectsList.DoLayoutList();

            if (serializedObject.ApplyModifiedProperties())
                foreach (var t in targets)
                    if (t is VectorShapeWorld ws) ws.Rebuild();
        }

        private static void DrawShapeDefinition(SerializedProperty prop)
        {
            var currentType = prop.managedReferenceValue?.GetType();
            int cur  = Array.FindIndex(s_ShapeTypes, t => t == currentType);
            int next = EditorGUILayout.Popup("Type", cur < 0 ? 0 : cur, s_ShapeNames);
            if (next != cur || prop.managedReferenceValue == null)
                prop.managedReferenceValue = Activator.CreateInstance(s_ShapeTypes[Mathf.Max(0, next)]);

            var it    = prop.Copy();
            var end   = it.GetEndProperty();
            bool first = true;
            EditorGUI.indentLevel++;
            while (it.NextVisible(first))
            {
                first = false;
                if (SerializedProperty.EqualContents(it, end)) break;
                EditorGUILayout.PropertyField(it, true);
            }
            EditorGUI.indentLevel--;
        }

        private void OnSceneGUI()
        {
            var ws = (VectorShapeWorld)target;
            if (ws.Shape == null) return;
            switch (ws.Shape)
            {
                case RectangleShape r: RectangleHandles.DrawWorld(ws, r); break;
                case StarShape s:      StarHandles.DrawWorld(ws, s);      break;
            }
        }
    }
}
