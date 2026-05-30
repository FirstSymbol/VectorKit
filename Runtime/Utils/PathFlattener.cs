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

        // Returns true when the PathShape contains Move markers (merged multi-sub-path from SVG import).
        public static bool HasSubPaths(PathShape path)
        {
            if (path?.Points == null) return false;
            foreach (var pt in path.Points)
                if (pt.Type == PathPointType.Move) return true;
            return false;
        }

        // Splits a merged PathShape (with Move markers) into separate polylines, one per sub-path.
        // Each sub-path is closed by connecting the last point back to the first.
        public static List<List<Vector2>> FlattenSubPaths(PathShape path, float tolerance = DefaultTolerance)
        {
            var result = new List<List<Vector2>>();
            if (path?.Points == null || path.Points.Count == 0) return result;

            var current = new List<Vector2>();
            Vector2 subStart = path.Points[0].Type != PathPointType.Move
                ? path.Points[0].Position
                : Vector2.zero;

            int count = path.Points.Count;
            int segStart = 0; // index of first non-Move point in current sub-path

            for (int i = 0; i < count; i++)
            {
                var pt = path.Points[i];

                if (pt.Type == PathPointType.Move)
                {
                    // Finalize current sub-path
                    if (current.Count >= 2)
                    {
                        // Close if indicated (ControlPoint1.x == 1)
                        if (pt.ControlPoint1.x > 0.5f && current.Count >= 2)
                            current.Add(current[0]);
                        result.Add(current);
                    }
                    current  = new List<Vector2>();
                    subStart = pt.Position; // first point of next sub-path
                    segStart = i + 1;
                    continue;
                }

                if (current.Count == 0)
                {
                    current.Add(pt.Position);
                    subStart = pt.Position;
                }
                else
                {
                    var prev = path.Points[i - 1];
                    // Skip over Move markers when looking for prev segment point
                    int prevIdx = i - 1;
                    while (prevIdx > segStart && path.Points[prevIdx].Type == PathPointType.Move)
                        prevIdx--;
                    prev = path.Points[prevIdx];

                    if (pt.Type == PathPointType.Bezier)
                        SubdivideCubic(prev.Position, prev.ControlPoint2, pt.ControlPoint1, pt.Position, current, tolerance, 0);
                    else
                        current.Add(pt.Position);
                }
            }

            // Finalize last sub-path
            if (current.Count >= 2)
            {
                if (path.Closed)
                    current.Add(current[0]);
                result.Add(current);
            }

            return result;
        }

        // Flatten a single-contour PathShape (no Move markers) into a flat point list.
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

                if (curr.Type == PathPointType.Move) continue; // skip markers in single-path context
                if (prev.Type == PathPointType.Move)
                {
                    output.Add(curr.Position);
                    continue;
                }

                if (curr.Type == PathPointType.Bezier)
                    SubdivideCubic(prev.Position, prev.ControlPoint2, curr.ControlPoint1, curr.Position, output, tolerance, 0);
                else
                    output.Add(curr.Position);
            }

            if (path.Closed && count >= 2)
            {
                var last  = path.Points[count - 1];
                var first = path.Points[0];
                if (last.Type != PathPointType.Move && first.Type != PathPointType.Move)
                {
                    if (first.Type == PathPointType.Bezier)
                        SubdivideCubic(last.Position, last.ControlPoint2, first.ControlPoint1, first.Position, output, tolerance, 0);
                    else
                        output.Add(first.Position);
                }
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
