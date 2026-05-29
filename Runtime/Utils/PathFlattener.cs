using System.Collections.Generic;
using UnityEngine;

namespace VectorKit.Runtime
{
    // Converts Bezier-based PathShape into a list of 2D polyline points.
    // Uses adaptive subdivision: segments are split until chord deviation < tolerance.
    internal static class PathFlattener
    {
        private const float DefaultTolerance = 0.1f;
        private const int   MaxDepth         = 8;

        public static void Flatten(PathShape path, List<Vector2> output, float tolerance = DefaultTolerance)
        {
            output.Clear();
            if (path == null || path.Points == null || path.Points.Count < 2) return;

            int count = path.Points.Count;
            output.Add(path.Points[0].Position);

            for (int i = 1; i < count; i++)
            {
                var prev = path.Points[i - 1];
                var curr = path.Points[i];

                if (curr.Type == PathPointType.Bezier)
                    SubdivideCubic(prev.Position, prev.ControlPoint2, curr.ControlPoint1, curr.Position, output, tolerance, 0);
                else
                    output.Add(curr.Position);
            }

            if (path.Closed && count >= 2)
            {
                var last  = path.Points[count - 1];
                var first = path.Points[0];
                if (first.Type == PathPointType.Bezier)
                    SubdivideCubic(last.Position, last.ControlPoint2, first.ControlPoint1, first.Position, output, tolerance, 0);
                else
                    output.Add(first.Position);
            }
        }

        private static void SubdivideCubic(
            Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
            List<Vector2> output, float tol, int depth)
        {
            // Flatness check: max deviation of control points from chord p0→p3
            float d1 = Mathf.Abs((p3.x - p0.x) * (p0.y - p1.y) - (p0.x - p1.x) * (p3.y - p0.y));
            float d2 = Mathf.Abs((p3.x - p0.x) * (p0.y - p2.y) - (p0.x - p2.x) * (p3.y - p0.y));

            if ((d1 + d2) * (d1 + d2) < tol * tol * ((p3 - p0).sqrMagnitude) || depth >= MaxDepth)
            {
                output.Add(p3);
                return;
            }

            // De Casteljau subdivision at t=0.5
            Vector2 m01 = (p0 + p1) * 0.5f;
            Vector2 m12 = (p1 + p2) * 0.5f;
            Vector2 m23 = (p2 + p3) * 0.5f;
            Vector2 m012 = (m01 + m12) * 0.5f;
            Vector2 m123 = (m12 + m23) * 0.5f;
            Vector2 mid  = (m012 + m123) * 0.5f;

            SubdivideCubic(p0, m01, m012, mid, output, tol, depth + 1);
            SubdivideCubic(mid, m123, m23, p3, output, tol, depth + 1);
        }

        // Packs flattened points into Vector4[] array for shader (2 points per Vector4)
        public static int PackIntoShaderArray(List<Vector2> points, Vector4[] dst, int maxPoints = 512)
        {
            int count = Mathf.Min(points.Count, maxPoints);
            int vec4Count = (count + 1) / 2;
            for (int i = 0; i < vec4Count; i++)
            {
                int idx0 = i * 2;
                int idx1 = idx0 + 1;
                float x0 = points[idx0].x, y0 = points[idx0].y;
                float x1 = (idx1 < count) ? points[idx1].x : 0f;
                float y1 = (idx1 < count) ? points[idx1].y : 0f;
                dst[i] = new Vector4(x0, y0, x1, y1);
            }
            return count;
        }
    }
}
