using UnityEditor;
using UnityEngine;

namespace VectorKit.Editor
{
    internal static class VectorHandleUtility
    {
        private static readonly Color s_HandleColor  = new Color(0.2f, 0.6f, 1f, 1f);
        private static readonly Color s_HandleColorH = new Color(1f, 0.8f, 0.2f, 1f);
        private const float HandleSize = 6f;

        // Draws a circular drag handle in scene-view local space.
        // Returns the new position if dragged, or the original if not.
        internal static Vector2 DragHandle(Vector2 localPos, Transform transform, float screenSize = HandleSize)
        {
            var worldPos = transform.TransformPoint(localPos);
            float sz = HandleUtility.GetHandleSize(worldPos) * screenSize / 64f;

            Handles.color = Handles.color == Color.clear ? s_HandleColor : Handles.color;

            EditorGUI.BeginChangeCheck();
            var newWorld = Handles.FreeMoveHandle(worldPos, sz, Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
                return transform.InverseTransformPoint(newWorld);
            return localPos;
        }

        internal static void DrawDottedLine(Vector2 a, Vector2 b, Transform transform, float gap = 4f)
        {
            var wa = transform.TransformPoint(a);
            var wb = transform.TransformPoint(b);
            Handles.color = new Color(1f, 1f, 1f, 0.3f);
            Handles.DrawDottedLine(wa, wb, gap);
        }

        internal static Color HandleColor   => s_HandleColor;
        internal static Color HandleColorHi => s_HandleColorH;
    }
}
