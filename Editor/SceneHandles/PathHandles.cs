using UnityEditor;
using UnityEngine;
using VectorKit.Runtime;

namespace VectorKit.Editor
{
    // Scene handles for PathShape: move path points + bezier control handles.
    internal static class PathHandles
    {
        public static void Draw(VectorShapeUI ui, PathShape path)
        {
            if (path.Points == null || path.Points.Count == 0) return;

            Undo.RecordObject(ui, "Edit Path");
            bool changed = false;

            Handles.color = VectorHandleUtility.HandleColor;

            for (int i = 0; i < path.Points.Count; i++)
            {
                var pt = path.Points[i];

                // Main point handle
                var newPos = VectorHandleUtility.DragHandle(pt.Position, ui.rectTransform.transform);
                if (newPos != pt.Position)
                {
                    Vector2 delta = newPos - pt.Position;
                    pt.Position    += delta;
                    pt.ControlPoint1 += delta;
                    pt.ControlPoint2 += delta;
                    path.Points[i]   = pt;
                    changed = true;
                }

                // Bezier control point handles
                if (pt.Type == PathPointType.Bezier)
                {
                    Handles.color = VectorHandleUtility.HandleColorHi;

                    VectorHandleUtility.DrawDottedLine(pt.ControlPoint1, pt.Position, ui.rectTransform.transform);
                    VectorHandleUtility.DrawDottedLine(pt.ControlPoint2, pt.Position, ui.rectTransform.transform);

                    var newCP1 = VectorHandleUtility.DragHandle(pt.ControlPoint1, ui.rectTransform.transform, 4f);
                    var newCP2 = VectorHandleUtility.DragHandle(pt.ControlPoint2, ui.rectTransform.transform, 4f);

                    if (newCP1 != pt.ControlPoint1 || newCP2 != pt.ControlPoint2)
                    {
                        pt.ControlPoint1 = newCP1;
                        pt.ControlPoint2 = newCP2;
                        path.Points[i]   = pt;
                        changed = true;
                    }

                    Handles.color = VectorHandleUtility.HandleColor;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(ui);
                ui.SetVerticesDirty();
            }
        }
    }
}
