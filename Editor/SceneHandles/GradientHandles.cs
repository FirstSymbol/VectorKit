using UnityEditor;
using UnityEngine;
using VectorKit.Runtime;

namespace VectorKit.Editor
{
    // Scene handles for editing gradient fill parameters (offset, angle, scale, center, radius).
    internal static class GradientHandles
    {
        public static void Draw(VectorShapeUI ui, SerializedObject so)
        {
            if (ui.Fills == null || ui.Fills.Count == 0) return;

            var rect     = ui.GetComponent<RectTransform>();
            var halfSize = new Vector2(rect.rect.width * 0.5f, rect.rect.height * 0.5f);
            var t        = ui.transform;

            for (int i = 0; i < ui.Fills.Count; i++)
            {
                var layer = ui.Fills[i];
                if (!layer.Enabled || layer.Fill == null) continue;

                switch (layer.Fill)
                {
                    case LinearGradientFill lin:  DrawLinear(ui, so, lin,  halfSize, t); break;
                    case RadialGradientFill rad:  DrawRadial(ui, so, rad,  halfSize, t); break;
                    case ConicGradientFill  con:  DrawConic (ui, so, con,  halfSize, t); break;
                }
            }
        }

        // ── Linear gradient: centre disc + end-point handle ───────────────────────

        private static void DrawLinear(VectorShapeUI ui, SerializedObject so,
            LinearGradientFill fill, Vector2 halfSize, Transform t)
        {
            float rad  = fill.Angle * Mathf.Deg2Rad;
            var   dir  = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            float len  = Mathf.Max(halfSize.x, halfSize.y) * fill.Scale;

            Vector2 centreLocal = fill.Offset * halfSize;
            Vector2 endLocal    = centreLocal + dir * len;

            using (new Handles.DrawingScope(new Color(1f, 0.8f, 0.2f, 0.7f)))
                VectorHandleUtility.DrawDottedLine(centreLocal, endLocal, t);

            // Centre drag
            EditorGUI.BeginChangeCheck();
            Vector2 newCentre = VectorHandleUtility.DragHandle(centreLocal, t);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ui, "Move Gradient Offset");
                fill.Offset = halfSize.x > 0 && halfSize.y > 0
                    ? new Vector2(newCentre.x / halfSize.x, newCentre.y / halfSize.y)
                    : Vector2.zero;
                MarkDirty(ui);
            }

            // End drag — adjusts angle and scale together
            EditorGUI.BeginChangeCheck();
            Vector2 newEnd = VectorHandleUtility.DragHandle(endLocal, t);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ui, "Adjust Gradient Angle/Scale");
                Vector2 delta = newEnd - centreLocal;
                fill.Angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                fill.Scale = Mathf.Max(0.001f, delta.magnitude / Mathf.Max(halfSize.x, halfSize.y));
                MarkDirty(ui);
            }
        }

        // ── Radial gradient: centre disc + radius handle ──────────────────────────

        private static void DrawRadial(VectorShapeUI ui, SerializedObject so,
            RadialGradientFill fill, Vector2 halfSize, Transform t)
        {
            Vector2 centreLocal  = fill.Center * halfSize;
            float   worldRadius  = fill.Radius * Mathf.Max(halfSize.x, halfSize.y);
            Vector2 radiusLocal  = centreLocal + new Vector2(worldRadius, 0f);

            // Draw circle outline in world space
            Vector3 wc = t.TransformPoint(centreLocal);
            Vector3 wr = t.TransformPoint(radiusLocal);
            using (new Handles.DrawingScope(new Color(1f, 0.8f, 0.2f, 0.7f)))
                Handles.DrawWireArc(wc, Vector3.back, (wr - wc).normalized, 360f, Vector3.Distance(wc, wr));

            // Centre drag
            EditorGUI.BeginChangeCheck();
            Vector2 newCentre = VectorHandleUtility.DragHandle(centreLocal, t);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ui, "Move Radial Gradient Center");
                fill.Center = halfSize.x > 0 && halfSize.y > 0
                    ? new Vector2(newCentre.x / halfSize.x, newCentre.y / halfSize.y)
                    : Vector2.zero;
                MarkDirty(ui);
            }

            // Radius drag
            EditorGUI.BeginChangeCheck();
            Vector2 newRadius = VectorHandleUtility.DragHandle(radiusLocal, t);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ui, "Adjust Radial Gradient Radius");
                Vector2 delta = newRadius - fill.Center * halfSize;
                fill.Radius = Mathf.Max(0.001f, delta.magnitude / Mathf.Max(halfSize.x, halfSize.y));
                MarkDirty(ui);
            }
        }

        // ── Conic gradient: centre disc + start-angle handle ─────────────────────

        private static void DrawConic(VectorShapeUI ui, SerializedObject so,
            ConicGradientFill fill, Vector2 halfSize, Transform t)
        {
            Vector2 centreLocal = fill.Center * halfSize;
            float   rad         = fill.StartAngle * Mathf.Deg2Rad;
            float   len         = Mathf.Max(halfSize.x, halfSize.y) * 0.8f;
            Vector2 angleLocal  = centreLocal + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * len;

            using (new Handles.DrawingScope(new Color(1f, 0.8f, 0.2f, 0.7f)))
                VectorHandleUtility.DrawDottedLine(centreLocal, angleLocal, t);

            // Centre drag
            EditorGUI.BeginChangeCheck();
            Vector2 newCentre = VectorHandleUtility.DragHandle(centreLocal, t);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ui, "Move Conic Gradient Center");
                fill.Center = halfSize.x > 0 && halfSize.y > 0
                    ? new Vector2(newCentre.x / halfSize.x, newCentre.y / halfSize.y)
                    : Vector2.zero;
                MarkDirty(ui);
            }

            // Angle drag
            EditorGUI.BeginChangeCheck();
            Vector2 newAngle = VectorHandleUtility.DragHandle(angleLocal, t);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ui, "Adjust Conic Start Angle");
                Vector2 delta = newAngle - fill.Center * halfSize;
                fill.StartAngle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                MarkDirty(ui);
            }
        }

        private static void MarkDirty(VectorShapeUI ui)
        {
            EditorUtility.SetDirty(ui);
            ui.SetVerticesDirty();
        }
    }
}
