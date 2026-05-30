using System.Collections.Generic;
using UnityEngine;

namespace VectorKit.Runtime
{
    // Triangulates a set of closed polyline contours (from merged SVG sub-paths)
    // using ear-clipping, with automatic outer/hole classification and hole bridging.
    internal static class VectorTessellator
    {
        // Input:  list of closed polylines (one per sub-path); each polyline already closed
        //         (last point == first point optional; we handle both).
        // Output: flat list of triangle vertices, 3 per triangle, in CCW order.
        public static List<Vector2> Tessellate(List<List<Vector2>> contours)
        {
            var result = new List<Vector2>();
            if (contours == null || contours.Count == 0) return result;

            // Filter degenerate contours
            var valid = new List<List<Vector2>>(contours.Count);
            foreach (var c in contours)
            {
                if (c == null || c.Count < 3) continue;
                // Strip duplicate closing point if present
                var pts = c;
                if (pts.Count >= 2 && (pts[pts.Count - 1] - pts[0]).sqrMagnitude < 1e-6f)
                {
                    pts = c.GetRange(0, c.Count - 1);
                }
                if (pts.Count >= 3) valid.Add(pts);
            }
            if (valid.Count == 0) return result;

            if (valid.Count == 1)
            {
                EarClip(valid[0], result);
                return result;
            }

            // Classify contours: in VectorKit coords (Y-up after SVG flip),
            // outer contours have negative signed area, holes have positive area.
            var outers = new List<List<Vector2>>();
            var holes  = new List<List<Vector2>>();
            foreach (var c in valid)
            {
                if (SignedArea(c) <= 0f) outers.Add(c);
                else                     holes.Add(c);
            }

            if (outers.Count == 0)
            {
                // Degenerate: all same winding, just triangulate each independently
                foreach (var c in valid) EarClip(c, result);
                return result;
            }

            // Sort outers descending by area so the largest outer has first claim on holes
            // (prevents small nested outers from stealing holes that belong to the body).
            outers.Sort((a, b) => Mathf.Abs(SignedArea(b)).CompareTo(Mathf.Abs(SignedArea(a))));

            // Assign each hole to the first (largest) outer that contains it,
            // then bridge and ear-clip each outer+holes group.
            var assigned = new bool[holes.Count];
            foreach (var outer in outers)
            {
                var myHoles = new List<List<Vector2>>();
                for (int h = 0; h < holes.Count; h++)
                {
                    if (assigned[h]) continue;
                    if (ContourContains(outer, Centroid(holes[h])))
                    {
                        myHoles.Add(holes[h]);
                        assigned[h] = true;
                    }
                }

                var merged = myHoles.Count > 0 ? BridgeHoles(outer, myHoles) : outer;
                EarClip(merged, result);
            }

            // Any unassigned holes: triangulate individually (shouldn't happen for well-formed SVG)
            for (int h = 0; h < holes.Count; h++)
                if (!assigned[h]) EarClip(holes[h], result);

            return result;
        }

        // ── Geometry helpers ──────────────────────────────────────────────────────

        // Signed area: negative = CCW in VectorKit space (outer), positive = CW (hole).
        private static float SignedArea(List<Vector2> pts)
        {
            float area = 0f;
            int n = pts.Count;
            for (int i = 0; i < n; i++)
            {
                var a = pts[i]; var b = pts[(i + 1) % n];
                area += a.x * b.y - b.x * a.y;
            }
            return area * 0.5f;
        }

        private static Vector2 Centroid(List<Vector2> pts)
        {
            float x = 0f, y = 0f;
            foreach (var p in pts) { x += p.x; y += p.y; }
            return new Vector2(x / pts.Count, y / pts.Count);
        }

        private static bool ContourContains(List<Vector2> poly, Vector2 p)
        {
            int n = poly.Count;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var pi = poly[i]; var pj = poly[j];
                if ((pi.y > p.y) != (pj.y > p.y) &&
                    p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y) + pi.x)
                    inside = !inside;
            }
            return inside;
        }

        // ── Hole bridging ─────────────────────────────────────────────────────────

        private static List<Vector2> BridgeHoles(List<Vector2> outer, List<List<Vector2>> holes)
        {
            // Sort holes by rightmost vertex (process leftmost hole last for stability)
            holes.Sort((a, b) => RightmostX(b).CompareTo(RightmostX(a)));

            var result = new List<Vector2>(outer);
            foreach (var hole in holes)
            {
                if (hole.Count < 3) continue;
                result = Bridge(result, hole);
            }
            return result;
        }

        private static float RightmostX(List<Vector2> pts)
        {
            float mx = float.MinValue;
            foreach (var p in pts) if (p.x > mx) mx = p.x;
            return mx;
        }

        // Inserts bridge edges between outer contour and hole, returning merged polygon.
        // Uses the Mapbox earcut minimum-angle refinement: after finding the nearest outer
        // edge via horizontal ray, searches inside the candidate triangle for a vertex
        // with a smaller angle to horizontal — ensuring the bridge line is not blocked
        // by a concave pocket in the outer polygon.
        private static List<Vector2> Bridge(List<Vector2> outer, List<Vector2> hole)
        {
            // Find rightmost vertex of hole
            int hIdx = 0;
            for (int i = 1; i < hole.Count; i++)
                if (hole[i].x > hole[hIdx].x) hIdx = i;
            Vector2 hv = hole[hIdx];

            float nearestX = float.MaxValue;
            int   oIdx     = -1;
            int n = outer.Count;

            for (int i = 0; i < n; i++)
            {
                var a = outer[i]; var b = outer[(i + 1) % n];
                if ((a.y <= hv.y && b.y > hv.y) || (b.y <= hv.y && a.y > hv.y))
                {
                    float t  = (hv.y - a.y) / (b.y - a.y);
                    float ix = a.x + t * (b.x - a.x);
                    if (ix >= hv.x && ix < nearestX)
                    {
                        nearestX = ix;
                        oIdx = (a.x >= b.x) ? i : (i + 1) % n;
                    }
                }
            }

            // Mapbox earcut refinement: among outer vertices inside triangle(hv, M, outer[oIdx]),
            // choose the one with the smallest angle from horizontal (most direct line of sight).
            if (oIdx >= 0)
            {
                var M  = new Vector2(nearestX, hv.y);
                var ov = outer[oIdx];
                float dxBase = ov.x - hv.x;
                float tanMin = dxBase > 0.001f ? Mathf.Abs(ov.y - hv.y) / dxBase : float.MaxValue;

                for (int i = 0; i < n; i++)
                {
                    Vector2 v  = outer[i];
                    float   dx = v.x - hv.x;
                    if (dx <= 0f || v.x > ov.x + 0.001f) continue;
                    if (!PointInTriangle(v, hv, M, ov)) continue;

                    float tan = Mathf.Abs(v.y - hv.y) / dx;
                    if (tan < tanMin - 0.0001f ||
                        (tan <= tanMin + 0.0001f && v.x > outer[oIdx].x))
                    {
                        tanMin = tan;
                        oIdx   = i;
                        ov     = v;
                    }
                }
            }

            if (oIdx < 0)
            {
                // Fallback: nearest outer vertex to hole's rightmost point
                float best = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    float d = (outer[i] - hv).sqrMagnitude;
                    if (d < best) { best = d; oIdx = i; }
                }
            }
            if (oIdx < 0) return outer;

            // Stitch: outer[0..oIdx] → hole[hIdx..end] → hole[0..hIdx] → outer[oIdx..end] → outer[0]
            var merged = new List<Vector2>(outer.Count + hole.Count + 2);
            for (int i = 0; i <= oIdx; i++) merged.Add(outer[i]);
            for (int i = hIdx; i < hole.Count; i++) merged.Add(hole[i]);
            for (int i = 0; i <= hIdx; i++) merged.Add(hole[i]);
            for (int i = oIdx; i < outer.Count; i++) merged.Add(outer[i]);
            return merged;
        }

        // ── Ear-clipping triangulation ────────────────────────────────────────────

        private static void EarClip(List<Vector2> poly, List<Vector2> result)
        {
            if (poly.Count < 3) return;
            if (poly.Count == 3)
            {
                result.Add(poly[0]); result.Add(poly[1]); result.Add(poly[2]);
                return;
            }

            // Work on a mutable index list; ensure CCW orientation.
            // Standard Y-up math: SignedArea > 0 = CCW, < 0 = CW.
            var idx = new List<int>(poly.Count);
            for (int i = 0; i < poly.Count; i++) idx.Add(i);
            if (SignedArea(poly) < 0f) idx.Reverse(); // reverse CW → CCW

            int guard = idx.Count * idx.Count + 16;
            while (idx.Count > 3 && guard-- > 0)
            {
                int n = idx.Count;
                bool cut = false;
                for (int i = 0; i < n; i++)
                {
                    int ip = (i - 1 + n) % n, in_ = (i + 1) % n;
                    Vector2 A = poly[idx[ip]], B = poly[idx[i]], C = poly[idx[in_]];

                    if (Cross2D(A, B, C) <= 0f) continue; // concave or degenerate

                    bool isEar = true;
                    for (int j = 0; j < n; j++)
                    {
                        if (j == ip || j == i || j == in_) continue;
                        if (PointInTriangle(poly[idx[j]], A, B, C)) { isEar = false; break; }
                    }

                    if (isEar)
                    {
                        result.Add(A); result.Add(B); result.Add(C);
                        idx.RemoveAt(i);
                        cut = true;
                        break;
                    }
                }
                if (!cut) break;
            }

            if (idx.Count == 3)
            {
                result.Add(poly[idx[0]]); result.Add(poly[idx[1]]); result.Add(poly[idx[2]]);
            }
        }

        // Positive cross product → left turn (CCW).
        private static float Cross2D(Vector2 a, Vector2 b, Vector2 c)
            => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross2D(a, b, p), d2 = Cross2D(b, c, p), d3 = Cross2D(c, a, p);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }
    }
}
