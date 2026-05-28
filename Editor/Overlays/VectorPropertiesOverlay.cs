using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;
using VectorKit.Runtime;

namespace VectorKit.Editor
{
    [Overlay(typeof(SceneView), "vk-properties", "VectorKit Properties")]
    public class VectorPropertiesOverlay : Overlay
    {
        private Label    _nameLabel;
        private Label    _shapeLabel;
        private Slider   _opacitySlider;

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement { name = "vk-props-root" };
            root.style.minWidth    = 200;
            root.style.paddingLeft = root.style.paddingRight  = 6;
            root.style.paddingTop  = root.style.paddingBottom = 6;

            _nameLabel  = new Label("(nothing selected)");
            _nameLabel.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            _shapeLabel = new Label();

            _opacitySlider = new Slider("Opacity", 0f, 1f) { showInputField = true };
            _opacitySlider.RegisterValueChangedCallback(evt =>
            {
                if (Selection.activeGameObject?.TryGetComponent<VectorShapeUI>(out var ui) == true)
                {
                    Undo.RecordObject(ui, "Change Opacity");
                    ui.ShapeOpacity = evt.newValue;
                    ui.SetVerticesDirty();
                }
            });

            root.Add(_nameLabel);
            root.Add(_shapeLabel);
            root.Add(_opacitySlider);

            Selection.selectionChanged += Refresh;
            return root;
        }

        private void Refresh()
        {
            if (_nameLabel == null) return;

            var ui = Selection.activeGameObject?.GetComponent<VectorShapeUI>();
            if (ui == null)
            {
                _nameLabel.text  = "(nothing selected)";
                _shapeLabel.text = string.Empty;
                _opacitySlider.SetEnabled(false);
                return;
            }

            _nameLabel.text  = ui.gameObject.name;
            _shapeLabel.text = ui.Shape?.GetType().Name.Replace("Shape", "") ?? "—";
            _opacitySlider.SetValueWithoutNotify(ui.ShapeOpacity);
            _opacitySlider.SetEnabled(true);
        }
    }
}
