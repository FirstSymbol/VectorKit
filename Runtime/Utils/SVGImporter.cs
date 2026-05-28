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

            foreach (var shape in doc.Shapes)
            {
                string name = string.IsNullOrEmpty(shape.Id) ? "Shape" : shape.Id;
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);

                if (UseUICanvas)
                {
                    var rt    = go.AddComponent<RectTransform>();
                    rt.anchorMin = rt.anchorMax = Vector2.one * 0.5f;
                    rt.sizeDelta  = shape.Size;
                    rt.anchoredPosition = shape.Position;

                    var ui = go.AddComponent<VectorShapeUI>();
                    ui.Shape   = shape.Shape;
                    ui.Fills   = shape.Fills;
                    ui.Strokes = shape.Strokes;
                }
                else
                {
                    go.transform.localPosition = new Vector3(
                        shape.Position.x / PixelsPerUnit,
                        shape.Position.y / PixelsPerUnit, 0);

                    var ws = go.AddComponent<VectorShapeWorld>();
                    ws.Shape   = shape.Shape;
                    ws.Size    = shape.Size;
                    ws.Fills   = shape.Fills;
                    ws.Strokes = shape.Strokes;
                }

                ctx.AddObjectToAsset(name, go);
            }

            ctx.AddObjectToAsset("prefab", root);
            ctx.SetMainObject(root);
        }
    }
}
#endif
