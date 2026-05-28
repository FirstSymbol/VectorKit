using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;
using VectorKit.Runtime;

namespace VectorKit.Editor
{
    [Overlay(typeof(SceneView), "vk-layers", "VectorKit Layers")]
    public class VectorLayersOverlay : Overlay
    {
        private ScrollView _scroll;

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement { name = "vk-layers-root" };
            root.style.minWidth = 200;

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            root.Add(_scroll);

            Refresh();
            return root;
        }

        public void Refresh()
        {
            if (_scroll == null) return;
            _scroll.Clear();

            var shapes = Object.FindObjectsByType<VectorShapeUI>(FindObjectsInactive.Exclude);
            foreach (var shape in shapes)
            {
                var row = BuildRow(shape);
                _scroll.Add(row);
            }
        }

        private static VisualElement BuildRow(VectorShapeUI shape)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems    = Align.Center;
            row.style.paddingLeft   = row.style.paddingRight = 4;
            row.style.paddingTop    = row.style.paddingBottom = 2;

            var eye = new Toggle { value = shape.gameObject.activeSelf };
            eye.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(shape.gameObject, "Toggle Visibility");
                shape.gameObject.SetActive(evt.newValue);
            });

            var label = new Label(shape.name) { pickingMode = PickingMode.Position };
            label.style.flexGrow = 1;
            label.RegisterCallback<ClickEvent>(_ =>
            {
                Selection.activeGameObject = shape.gameObject;
                SceneView.FrameLastActiveSceneView();
            });

            row.Add(eye);
            row.Add(label);
            return row;
        }
    }
}
