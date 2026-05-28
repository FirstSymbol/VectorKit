using UnityEditor;
using UnityEngine;
using VectorKit.Runtime;

namespace VectorKit.Editor
{
    internal static class StarHandles
    {
        public static void Draw(VectorShapeUI ui, StarShape star)
        {
            var rt = ui.rectTransform;
            float maxR = Mathf.Min(rt.rect.width, rt.rect.height) * 0.5f;
            DrawShared(star, rt.transform, maxR,
                () => { EditorUtility.SetDirty(ui); ui.SetVerticesDirty(); });
        }

        public static void DrawWorld(VectorShapeWorld ws, StarShape star)
        {
            float maxR = Mathf.Min(ws.Size.x, ws.Size.y) * 0.5f;
            DrawShared(star, ws.transform, maxR,
                () => { EditorUtility.SetDirty(ws); ws.Rebuild(); });
        }

        private static void DrawShared(StarShape star, Transform t, float maxR, System.Action onChanged)
        {
            Undo.RecordObject(t.gameObject, "Edit Star Ratio");
            Handles.color = VectorHandleUtility.HandleColor;

            float angle   = Mathf.PI / star.Points;
            float outerR  = maxR;
            float innerR  = star.Ratio * maxR;

            // Inner radius handle — on the first inner point direction
            var innerDir = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            var innerPos = innerDir * innerR;
            var newInner = VectorHandleUtility.DragHandle(innerPos, t);
            float newRatio = Mathf.Clamp01(newInner.magnitude / Mathf.Max(outerR, 0.001f));
            if (!Mathf.Approximately(newRatio, star.Ratio))
            {
                star.Ratio = newRatio;
                onChanged();
            }

            // Outer radius visual guide
            VectorHandleUtility.DrawDottedLine(Vector2.zero, new Vector2(0, outerR), t);
        }
    }
}
