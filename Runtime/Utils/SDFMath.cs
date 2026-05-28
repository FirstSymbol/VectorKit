using System.Collections.Generic;
using UnityEngine;

namespace VectorKit.Runtime
{
    // CPU-side SDF evaluation — mirrors the HLSL SDFLibrary for use in raycasting.
    internal static class SDFMath
    {
        public static float Evaluate(ShapeDefinition shape, Vector2 p, Vector2 halfSize)
        {
            if (shape == null) return float.MaxValue;
            switch (shape)
            {
                case RectangleShape r: return Rectangle(p, halfSize, r.CornerSmoothing, r.CornerRadius);
                case EllipseShape:     return Ellipse(p, halfSize);
                case PolygonShape pg:  return Polygon(p, halfSize, pg.Sides, pg.Rounding);
                case StarShape st:     return Star(p, halfSize, st.Points, st.Ratio, st.OuterRounding, st.InnerRounding);
                case CapsuleShape cp:  return Capsule(p, halfSize, cp.Rounding);
                case LineShape ln:     return Line(p, ln.Start, ln.End, ln.Width);
                case ArcShape arc:     return Arc(p, halfSize, arc.InnerRadius, arc.StartAngle * Mathf.Deg2Rad, arc.EndAngle * Mathf.Deg2Rad);
                case TriangleShape:    return Triangle(p, halfSize);
                case HeartShape:       return Heart(p, halfSize);
                case PathShape ps:     return PathSDF(p, ps);
                default:               return float.MaxValue;
            }
        }

        public static float EvaluateWithBooleans(ShapeDefinition shape, Vector2 p, Vector2 halfSize, IList<BooleanOperationData> boolOps, Matrix4x4 worldToLocal)
        {
            float d = Evaluate(shape, p, halfSize);
            if (boolOps == null) return d;
            foreach (var op in boolOps)
            {
                if (op == null || op.Operation == BoolOp.None || op.Shape == null) continue;
                // Transform p into the operand's local space (no RectTransform in world-space booleans)
                float d2 = Evaluate(op.Shape, p, halfSize);
                float k  = op.Smoothness;
                if (k > 0.001f) d = SmoothBoolOp(d, d2, op.Operation, k);
                else             d = HardBoolOp(d, d2, op.Operation);
            }
            return d;
        }

        private static float Rectangle(Vector2 p, Vector2 hs, float smoothing, Vector4 corners)
        {
            Vector2 s   = new Vector2(p.x >= 0 ? 1 : 0, p.y >= 0 ? 1 : 0);
            float topR  = Mathf.Lerp(corners.x, corners.y, s.x);
            float botR  = Mathf.Lerp(corners.w, corners.z, s.x);
            float r     = Mathf.Min(Mathf.Lerp(botR, topR, s.y), Mathf.Min(hs.x, hs.y));
            Vector2 q   = new Vector2(Mathf.Abs(p.x) - hs.x + r, Mathf.Abs(p.y) - hs.y + r);
            if (smoothing > 0.01f && r > 0.01f)
            {
                float n  = Mathf.Lerp(2f, 4.5f, smoothing);
                var q0   = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
                float cd = Mathf.Pow(Mathf.Pow(Mathf.Abs(q0.x), n) + Mathf.Pow(Mathf.Abs(q0.y), n), 1f / n);
                return Mathf.Min(Mathf.Max(q.x, q.y), 0f) + cd - r;
            }
            return Mathf.Min(Mathf.Max(q.x, q.y), 0f) + new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude - r;
        }

        private static float Ellipse(Vector2 p, Vector2 hs)
            => (new Vector2(p.x / hs.x, p.y / hs.y).magnitude - 1f) * Mathf.Min(hs.x, hs.y);

        private static float Polygon(Vector2 p, Vector2 hs, int sides, float rounding)
        {
            float n  = Mathf.Max(3, sides);
            float an = Mathf.PI / n;
            float a  = Mathf.Atan2(p.x, p.y);
            float bn = Mathf.Floor(a / (2f * an));
            float f  = a - (bn + 0.5f) * 2f * an;
            float len = p.magnitude;
            Vector2 ps = new Vector2(Mathf.Abs(Mathf.Sin(f)) * len, Mathf.Cos(f) * len);
            float maxR  = Mathf.Min(hs.x, hs.y);
            float round = rounding * maxR * 0.5f;
            float rOuter = maxR - round;
            Vector2 cl = new Vector2(Mathf.Clamp(ps.x, -rOuter * Mathf.Sin(an), rOuter * Mathf.Sin(an)), rOuter * Mathf.Cos(an));
            return (ps - cl).magnitude * Mathf.Sign(ps.y - cl.y) - round;
        }

        private static float Star(Vector2 p, Vector2 hs, int points, float ratio, float outerR, float innerR)
        {
            float n   = Mathf.Max(3, points);
            float maxR = Mathf.Min(hs.x, hs.y);
            float ro  = outerR * maxR * 0.5f;
            float rOut = Mathf.Max(maxR - ro, 0.001f);
            float rIn  = Mathf.Max(ratio * maxR - ro, 0.001f);
            float an  = Mathf.PI / n;
            float a   = Mathf.Atan2(p.x, p.y);
            float f   = Mathf.Abs(a) % (2f * an);
            if (f > an) f = 2f * an - f;
            Vector2 q0 = new Vector2(Mathf.Sin(f), Mathf.Cos(f)) * p.magnitude;
            Vector2 q1 = new Vector2(Mathf.Sin(2f*an-f), Mathf.Cos(2f*an-f)) * p.magnitude;
            Vector2 p1 = new Vector2(0, rOut);
            Vector2 p2 = new Vector2(rIn * Mathf.Sin(an), rIn * Mathf.Cos(an));
            Vector2 ba = p2 - p1;
            float ba2  = Mathf.Max(Vector2.Dot(ba, ba), 0.00001f);
            Vector2 pa0 = q0 - p1;
            float dist0 = (pa0 - ba * Mathf.Clamp(Vector2.Dot(pa0, ba) / ba2, 0f, 1f)).magnitude *
                          (pa0.y * ba.x - pa0.x * ba.y >= 0 ? 1f : -1f);
            Vector2 pa1 = q1 - p1;
            float dist1 = (pa1 - ba * Mathf.Clamp(Vector2.Dot(pa1, ba) / ba2, 0f, 1f)).magnitude *
                          (pa1.y * ba.x - pa1.x * ba.y >= 0 ? 1f : -1f);
            float rInnerPx = innerR * maxR;
            float d = dist0;
            if (rInnerPx > 0.001f) d = SmoothMin(dist0, dist1, rInnerPx);
            return d - ro;
        }

        private static float Capsule(Vector2 p, Vector2 hs, float rounding)
        {
            float r  = rounding * Mathf.Min(hs.x, hs.y);
            Vector2 h = new Vector2(Mathf.Max(hs.x - r, 0f), Mathf.Max(hs.y - r, 0f));
            return (p - Vector2.Min(Vector2.Max(p, -h), h)).magnitude - r;
        }

        private static float Line(Vector2 p, Vector2 a, Vector2 b, float width)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp(Vector2.Dot(pa, ba) / Mathf.Max(Vector2.Dot(ba, ba), 0.0001f), 0f, 1f);
            return (pa - ba * h).magnitude - width * 0.5f;
        }

        private static float Arc(Vector2 p, Vector2 hs, float innerRadius, float startAngle, float endAngle)
        {
            float maxR = Mathf.Min(hs.x, hs.y);
            float midR = (maxR + innerRadius * maxR) * 0.5f;
            float thick = (maxR - innerRadius * maxR) * 0.5f;
            float d = Mathf.Abs(p.magnitude - midR) - thick;
            if (Mathf.Abs(endAngle - startAngle) < Mathf.PI * 2f)
            {
                float a  = Mathf.Atan2(p.x, p.y);
                float da = Mathf.Repeat((a - startAngle) / (Mathf.PI * 2f), 1f);
                float target = Mathf.Repeat((endAngle - startAngle) / (Mathf.PI * 2f), 1f);
                if (da > target)
                {
                    Vector2 p1 = new Vector2(Mathf.Sin(startAngle), Mathf.Cos(startAngle)) * midR;
                    Vector2 p2 = new Vector2(Mathf.Sin(endAngle),   Mathf.Cos(endAngle))   * midR;
                    d = Mathf.Max(d, Mathf.Min((p - p1).magnitude, (p - p2).magnitude) - thick);
                }
            }
            return d;
        }

        private static float Triangle(Vector2 p, Vector2 hs)
        {
            float r = Mathf.Min(hs.x, hs.y);
            p.y += r * 0.25f;
            const float k = 1.7320508f;
            p.x = Mathf.Abs(p.x) - r;
            p.y += r / k;
            if (p.x + k * p.y > 0f) p = new Vector2(p.x - k * p.y, -k * p.x - p.y) / 2f;
            p.x -= Mathf.Clamp(p.x, -2f * r, 0f);
            return -p.magnitude * Mathf.Sign(p.y);
        }

        private static float Heart(Vector2 p, Vector2 hs)
        {
            float r = Mathf.Min(hs.x, hs.y);
            p.x = Mathf.Abs(p.x);
            p.y += r * 0.5f;
            p /= r;
            float d;
            if (p.y + p.x > 1f)
                d = (p - new Vector2(0.25f, 0.75f)).magnitude - 0.3535534f;
            else
            {
                float d1 = (p - new Vector2(0f, 1f)).magnitude;
                float d2 = (p - Vector2.Max(p.x + p.y > 0 ? p * 0.5f : Vector2.zero, Vector2.zero)).magnitude;
                d = Mathf.Min(d1, d2) * Mathf.Sign(p.x - p.y);
            }
            return d * r;
        }

        private static float PathSDF(Vector2 p, PathShape ps)
        {
            var pts = new List<Vector2>();
            PathFlattener.Flatten(ps, pts);
            if (pts.Count < 2) return float.MaxValue;

            float d = float.MaxValue;
            float s = 1f;
            int   count = pts.Count;

            if (ps.Closed)
            {
                float sqD = float.MaxValue;
                for (int i = 0, j = count - 1; i < count; j = i, i++)
                {
                    Vector2 vi = pts[i], vj = pts[j];
                    Vector2 e  = vj - vi, w = p - vi;
                    float   h  = Mathf.Clamp(Vector2.Dot(w, e) / Mathf.Max(Vector2.Dot(e, e), 0.0001f), 0f, 1f);
                    Vector2 b  = w - e * h;
                    sqD = Mathf.Min(sqD, Vector2.Dot(b, b));
                    bool c1 = p.y >= vi.y, c2 = p.y < vj.y, c3 = e.x * w.y > e.y * w.x;
                    if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s *= -1f;
                }
                return s * Mathf.Sqrt(sqD);
            }
            else
            {
                for (int i = 0; i < count - 1; i++)
                {
                    Vector2 vi = pts[i], vj = pts[i + 1];
                    Vector2 e  = vj - vi, w = p - vi;
                    float   h  = Mathf.Clamp(Vector2.Dot(w, e) / Mathf.Max(Vector2.Dot(e, e), 0.0001f), 0f, 1f);
                    d = Mathf.Min(d, (w - e * h).sqrMagnitude);
                }
                return Mathf.Sqrt(d) - ps.Thickness * 0.5f;
            }
        }

        private static float SmoothMin(float a, float b, float k)
        {
            float h = Mathf.Clamp(0.5f + 0.5f * (b - a) / k, 0f, 1f);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        private static float SmoothBoolOp(float d1, float d2, BoolOp op, float k)
        {
            switch (op)
            {
                case BoolOp.Union:        return SmoothMin(d1, d2, k);
                case BoolOp.Subtraction:  return -SmoothMin(-d1, d2, k);
                case BoolOp.Intersection: return -SmoothMin(-d1, -d2, k);
                default: return d1;
            }
        }

        private static float HardBoolOp(float d1, float d2, BoolOp op)
        {
            switch (op)
            {
                case BoolOp.Union:        return Mathf.Min(d1, d2);
                case BoolOp.Subtraction:  return Mathf.Max(d1, -d2);
                case BoolOp.Intersection: return Mathf.Max(d1, d2);
                case BoolOp.Xor:          return Mathf.Max(Mathf.Min(d1, d2), -Mathf.Max(d1, d2));
                default: return d1;
            }
        }
    }
}
