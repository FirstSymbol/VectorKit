using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VectorKit.Runtime;

namespace VectorKit.Editor
{
    public static class VectorKitMenus
    {
        // ── GameObject menu ───────────────────────────────────────────────────────

        [MenuItem("GameObject/Vector/Rectangle", false, 10)]
        public static void CreateRectangle() => CreateShape("Rectangle", new RectangleShape());

        [MenuItem("GameObject/Vector/Ellipse", false, 11)]
        public static void CreateEllipse() => CreateShape("Ellipse", new EllipseShape());

        [MenuItem("GameObject/Vector/Polygon", false, 12)]
        public static void CreatePolygon() => CreateShape("Polygon", new PolygonShape());

        [MenuItem("GameObject/Vector/Star", false, 13)]
        public static void CreateStar() => CreateShape("Star", new StarShape());

        [MenuItem("GameObject/Vector/Line", false, 14)]
        public static void CreateLine() => CreateShape("Line", new LineShape());

        [MenuItem("GameObject/Vector/Arc", false, 15)]
        public static void CreateArc() => CreateShape("Arc", new ArcShape());

        [MenuItem("GameObject/Vector/Path", false, 16)]
        public static void CreatePath()
        {
            var ps = new PathShape();
            ps.Points.Add(new PathPoint { Position = new Vector2(-50, 0), Type = PathPointType.Line });
            ps.Points.Add(new PathPoint { Position = new Vector2( 50, 0), Type = PathPointType.Line });
            CreateShape("Path", ps);
        }

        [MenuItem("GameObject/Vector/Group", false, 20)]
        public static void CreateGroup()
        {
            var go = CreateUIGameObject("VectorGroup");
            go.AddComponent<VectorGroup>();
            FinalizeObject(go);
        }

        [MenuItem("GameObject/Vector/Frame", false, 21)]
        public static void CreateFrame()
        {
            var go = CreateUIGameObject("Frame");
            go.AddComponent<VectorFrame>();
            go.AddComponent<Mask>();
            FinalizeObject(go);
        }

        // ── Tools menu ─────────────────────────────────────────────────────────────

        [MenuItem("Tools/Vector Kit/Open Editor", false, 100)]
        public static void OpenEditor() => VectorEditorWindow.Open();

        [MenuItem("Tools/Vector Kit/Clear Material Pool", false, 200)]
        public static void ClearMaterialPool()
        {
            VectorMaterialManager.ClearAll();
            GradientAtlas.Clear();
            BlendCompositor.ClearAll();
            Debug.Log("[VectorKit] Material pool, gradient atlas, and blend compositor cleared.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static void CreateShape(string name, ShapeDefinition shape)
        {
            var go = CreateUIGameObject(name);
            var ui = go.AddComponent<VectorShapeUI>();
            ui.Shape = shape;
            FinalizeObject(go);
        }

        private static GameObject CreateUIGameObject(string name)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100, 100);

            // Parent to selected Canvas or selected RectTransform
            var parent = Selection.activeGameObject;
            if (parent != null)
            {
                var canvas = parent.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    rt.SetParent(parent.transform, false);
                    return go;
                }
            }

            // Find or create a root Canvas
            var rootCanvas = Object.FindAnyObjectByType<Canvas>();
            if (rootCanvas == null)
            {
                var canvasGO = new GameObject("Canvas");
                rootCanvas = canvasGO.AddComponent<Canvas>();
                rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<GraphicRaycaster>();
                Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
            }

            rt.SetParent(rootCanvas.transform, false);
            return go;
        }

        private static void FinalizeObject(GameObject go)
        {
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeGameObject = go;
        }
    }
}
