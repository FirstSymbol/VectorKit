#if UNITY_EDITOR
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.UI;

namespace VectorKit.Runtime
{
    // ScriptedImporter for .vksvg files (VectorKit SVG).
    // Rename any .svg to .vksvg to have it processed by VectorKit instead of Unity's
    // built-in VectorGraphics importer (both cannot claim the same extension).
    // Creates a prefab with VectorShapeUI hierarchy under a root Canvas (if present)
    // or plain GameObjects for world-space use.
    [ScriptedImporter(1, "vksvg")]
    public class SVGImporter : ScriptedImporter
    {
        public float  PixelsPerUnit = 1f;
        public bool   UseUICanvas   = true;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            string text;
            try { text = File.ReadAllText(ctx.assetPath); }
            catch { ctx.LogImportError("SVGImporter: cannot read file."); return; }

            var doc = SVGParser.Parse(text);

            var root = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));

            if (UseUICanvas)
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var scaler = root.AddComponent<CanvasScaler>();
                scaler.referencePixelsPerUnit = PixelsPerUnit;
                root.AddComponent<GraphicRaycaster>();

                var rt = root.GetComponent<RectTransform>();
                rt.sizeDelta = doc.ViewBox;
            }

            // SVG coords origin is top-left; Unity canvas origin is center.
            // Offset each shape position to convert from SVG space to canvas-center space.
            var vbHalf = doc.ViewBox * 0.5f;

            int shapeIndex = 0;
            foreach (var shape in doc.Shapes)
            {
                string displayName = string.IsNullOrEmpty(shape.Id) ? $"Shape_{shapeIndex}" : shape.Id;
                var go = new GameObject(displayName);
                go.transform.SetParent(root.transform, false);

                // shape.Position is (svgCenterX, -svgCenterY); subtract viewBox half to center
                var unityPos = new Vector2(shape.Position.x - vbHalf.x, shape.Position.y + vbHalf.y);

                // Coordinate-based shapes store their native half-size so they scale
                // proportionally when the RectTransform is resized after import.
                if (shape.Shape is PathShape || shape.Shape is LineShape)
                    shape.Shape.NativeHalfSize = shape.Size * 0.5f;

                if (UseUICanvas)
                {
                    var rt    = go.AddComponent<RectTransform>();
                    rt.anchorMin = rt.anchorMax = Vector2.one * 0.5f;
                    rt.sizeDelta  = shape.Size;
                    rt.anchoredPosition = unityPos;

                    var ui = go.AddComponent<VectorShapeUI>();
                    ui.Shape   = shape.Shape;
                    ui.Fills   = shape.Fills;
                    ui.Strokes = shape.Strokes;
                }
                else
                {
                    go.transform.localPosition = new Vector3(
                        unityPos.x / PixelsPerUnit,
                        unityPos.y / PixelsPerUnit, 0);

                    var ws = go.AddComponent<VectorShapeWorld>();
                    ws.Shape   = shape.Shape;
                    ws.Size    = shape.Size;
                    ws.Fills   = shape.Fills;
                    ws.Strokes = shape.Strokes;
                }

                ctx.AddObjectToAsset($"shape_{shapeIndex}", go);
                shapeIndex++;
            }

            ctx.AddObjectToAsset("prefab", root);
            ctx.SetMainObject(root);
        }
    }
}
#endif
