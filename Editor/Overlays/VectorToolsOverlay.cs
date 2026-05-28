using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace VectorKit.Editor
{
    [Overlay(typeof(SceneView), "vk-tools", "VectorKit Tools")]
    [Icon("Assets/VectorKit/Editor/UI/Icons/vk_tools.png")]
    public class VectorToolsOverlay : Overlay
    {
        private static readonly string[] s_ToolLabels = { "V", "R", "E", "P", "S", "A" };
        private static readonly string[] s_ToolTips   =
        {
            "Select", "Rectangle", "Ellipse", "Path (Pen)", "Star", "Arc"
        };

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement { name = "vk-tools-root" };
            root.style.flexDirection = FlexDirection.Column;
            root.style.paddingTop    = root.style.paddingBottom = 4;
            root.style.paddingLeft   = root.style.paddingRight  = 4;

            for (int i = 0; i < s_ToolLabels.Length; i++)
            {
                int idx = i;
                var btn = new Button(() => OnToolSelected(idx))
                {
                    text    = s_ToolLabels[i],
                    tooltip = s_ToolTips[i],
                };
                btn.style.marginBottom = 2;
                btn.style.width        = 28;
                btn.style.height       = 28;
                root.Add(btn);
            }

            return root;
        }

        private static void OnToolSelected(int idx)
        {
            switch (idx)
            {
                case 0: Tools.current = Tool.Move; break;
                case 1: VectorKitMenus.CreateRectangle();  break;
                case 2: VectorKitMenus.CreateEllipse();    break;
                case 3: VectorKitMenus.CreatePath();       break;
                case 4: VectorKitMenus.CreateStar();       break;
                case 5: VectorKitMenus.CreateArc();        break;
            }
        }
    }
}
