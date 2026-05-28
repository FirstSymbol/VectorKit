using UnityEditor;
using UnityEngine;
using VectorKit.Runtime;

namespace VectorKit.Editor
{
    internal static class RectangleHandles
    {
        public static void Draw(VectorShapeUI ui, RectangleShape rect)
        {
            var rt = ui.rectTransform;
            float hw = rt.rect.width  * 0.5f;
            float hh = rt.rect.height * 0.5f;

            Undo.RecordObject(ui, "Edit Rectangle Corner Radius");

            Handles.color = VectorHandleUtility.HandleColor;

            // Four corner radius handles — drag inward from each corner
            var corners = new Vector2[]
            {
                new( hw, hh),   // TR → controls corners.y (top-right)
                new(-hw, hh),   // TL → controls corners.x (top-left)
                new(-hw, -hh),  // BL → controls corners.w (bottom-left)
                new( hw, -hh),  // BR → controls corners.z (bottom-right)
            };

            float[] radii = { rect.CornerRadius.y, rect.CornerRadius.x, rect.CornerRadius.w, rect.CornerRadius.z };
            float maxR    = Mathf.Min(hw, hh);

            for (int i = 0; i < 4; i++)
            {
                Vector2 dir      = -corners[i].normalized;
                Vector2 handlePos = corners[i] + dir * radii[i];
                Vector2 newPos   = VectorHandleUtility.DragHandle(handlePos, rt.transform);
                float   newR     = Mathf.Clamp(Vector2.Distance(newPos, corners[i]), 0f, maxR);

                if (!Mathf.Approximately(newR, radii[i]))
                {
                    var cr = rect.CornerRadius;
                    switch (i)
                    {
                        case 0: cr.y = newR; break;
                        case 1: cr.x = newR; break;
                        case 2: cr.w = newR; break;
                        case 3: cr.z = newR; break;
                    }
                    rect.CornerRadius = cr;
                    EditorUtility.SetDirty(ui);
                    ui.SetVerticesDirty();
                }
            }
        }

        public static void DrawWorld(VectorShapeWorld ws, RectangleShape rect)
        {
            float hw = ws.Size.x * 0.5f;
            float hh = ws.Size.y * 0.5f;

            Undo.RecordObject(ws, "Edit Rectangle Corner Radius");
            Handles.color = VectorHandleUtility.HandleColor;

            var corner = new Vector2(hw, hh);
            var dir    = -corner.normalized;
            float r    = Mathf.Min(rect.CornerRadius.x, Mathf.Min(hw, hh));
            var handle = corner + dir * r;
            var newPos = VectorHandleUtility.DragHandle(handle, ws.transform);
            float newR = Mathf.Clamp(Vector2.Distance(newPos, corner), 0f, Mathf.Min(hw, hh));
            if (!Mathf.Approximately(newR, r))
            {
                rect.CornerRadius = Vector4.one * newR;
                EditorUtility.SetDirty(ws);
                ws.Rebuild();
            }
        }
    }
}
