using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VectorKit.Runtime;

namespace VectorKit.Editor
{
    public class VectorEditorWindow : EditorWindow
    {
        private VectorShapeUI _selected;
        private UnityEditor.Editor _embeddedInspector;

        public static void Open()
        {
            var wnd = GetWindow<VectorEditorWindow>("Vector Kit");
            wnd.minSize = new Vector2(280, 400);
            wnd.Show();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            DestroyEmbeddedInspector();
        }

        private void OnSelectionChanged()
        {
            _selected = Selection.activeGameObject?.GetComponent<VectorShapeUI>();
            DestroyEmbeddedInspector();

            if (_selected != null)
                _embeddedInspector = UnityEditor.Editor.CreateEditor(_selected);

            Repaint();
        }

        private void OnGUI()
        {
            if (_selected == null)
            {
                EditorGUILayout.HelpBox("Select a VectorShapeUI in the scene.", MessageType.Info);
                return;
            }

            GUILayout.Label(_selected.gameObject.name, EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (_embeddedInspector != null)
            {
                _embeddedInspector.OnInspectorGUI();
            }
        }

        private void DestroyEmbeddedInspector()
        {
            if (_embeddedInspector != null)
            {
                DestroyImmediate(_embeddedInspector);
                _embeddedInspector = null;
            }
        }
    }
}
