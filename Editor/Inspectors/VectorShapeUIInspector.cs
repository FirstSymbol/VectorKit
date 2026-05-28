using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VectorKit.Runtime;

namespace VectorKit.Editor
{
    [CustomEditor(typeof(VectorShapeUI))]
    [CanEditMultipleObjects]
    public class VectorShapeUIInspector : UnityEditor.Editor
    {
        private SerializedProperty _shapeProp;
        private SerializedProperty _fillsProp;
        private SerializedProperty _strokesProp;
        private SerializedProperty _effectsProp;
        private SerializedProperty _opacityProp;
        private SerializedProperty _colorProp;

        private ReorderableList _fillsList;
        private ReorderableList _strokesList;
        private ReorderableList _effectsList;

        private void OnEnable()
        {
            _shapeProp   = serializedObject.FindProperty("Shape");
            _fillsProp   = serializedObject.FindProperty("Fills");
            _strokesProp = serializedObject.FindProperty("Strokes");
            _effectsProp = serializedObject.FindProperty("Effects");
            _opacityProp = serializedObject.FindProperty("ShapeOpacity");
            _colorProp   = serializedObject.FindProperty("m_Color");

            _fillsList   = InspectorHelpers.BuildLayerList(_fillsProp,   "Fills",   InspectorHelpers.AddFillLayer);
            _strokesList = InspectorHelpers.BuildLayerList(_strokesProp, "Strokes", InspectorHelpers.AddStrokeLayer);
            _effectsList = InspectorHelpers.BuildLayerList(_effectsProp, "Effects", InspectorHelpers.AddEffect);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(2);

            // ── Shape ──────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);
            DrawShapeDefinition(_shapeProp);

            EditorGUILayout.Space(4);

            // ── Appearance ────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Appearance", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_colorProp, new GUIContent("Tint"));
            EditorGUILayout.PropertyField(_opacityProp, new GUIContent("Opacity"));

            EditorGUILayout.Space(4);

            // ── Fill Layers ────────────────────────────────────────────────────
            _fillsList.DoLayoutList();

            // ── Stroke Layers ──────────────────────────────────────────────────
            _strokesList.DoLayoutList();

            // ── Effects ────────────────────────────────────────────────────────
            _effectsList.DoLayoutList();

            if (serializedObject.ApplyModifiedProperties())
            {
                foreach (var t in targets)
                    if (t is VectorShapeUI ui) { ui.SetVerticesDirty(); ui.SetMaterialDirty(); }
            }
        }

        // ── Shape Definition ─────────────────────────────────────────────────────

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

        private static void DrawShapeDefinition(SerializedProperty prop)
        {
            // Type dropdown — do NOT call PropertyField on the managed-ref property
            var currentType = prop.managedReferenceValue?.GetType();
            int cur  = Array.FindIndex(s_ShapeTypes, t => t == currentType);
            int next = EditorGUILayout.Popup("Type", cur < 0 ? 0 : cur, s_ShapeNames);
            if (next != cur || prop.managedReferenceValue == null)
                prop.managedReferenceValue = Activator.CreateInstance(s_ShapeTypes[Mathf.Max(0, next)]);

            // Draw children via NextVisible — all concrete fields, safe to use PropertyField
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

        // ── Scene Handles ────────────────────────────────────────────────────────

        private void OnSceneGUI()
        {
            var ui = (VectorShapeUI)target;
            if (ui.Shape == null) return;

            switch (ui.Shape)
            {
                case RectangleShape r: RectangleHandles.Draw(ui, r); break;
                case StarShape s:      StarHandles.Draw(ui, s);      break;
                case PathShape p:      PathHandles.Draw(ui, p);      break;
            }

            GradientHandles.Draw(ui, serializedObject);
        }
    }
}
