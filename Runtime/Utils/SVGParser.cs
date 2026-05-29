using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using UnityEngine;

namespace VectorKit.Runtime
{
    // Parses SVG files into a flat list of VectorShapeData records.
    // Handles <path>, <rect>, <circle>, <ellipse>, <line>, <polyline>, <polygon>.
    // SVG Y-axis is flipped to match Unity's coordinate system.
    internal static class SVGParser
    {
        public class ShapeData
        {
            public string           Id;
            public ShapeDefinition  Shape;
            public List<FillLayer>  Fills   = new List<FillLayer>();
            public List<StrokeLayer> Strokes = new List<StrokeLayer>();
            public Vector2          Size;
            public Vector2          Position;
        }

        public class Document
        {
            public Vector2           ViewBox;
            public List<ShapeData>   Shapes = new List<ShapeData>();
        }

        private static readonly XNamespace SVG = "http://www.w3.org/2000/svg";

        public static Document Parse(string svgText)
        {
            var doc = new Document();
            XDocument xdoc;
            try { xdoc = XDocument.Parse(svgText); }
            catch (Exception e) { Debug.LogWarning($"[VectorKit] SVG parse error: {e.Message}"); return doc; }

            var root = xdoc.Root;
            if (root == null) return doc;

            doc.ViewBox = ParseViewBox(root.Attribute("viewBox")?.Value
                       ?? root.Attribute("width")?.Value + " " + root.Attribute("height")?.Value);

            // Seed the inherited presentation context from root <svg> attributes
            var rootCtx = PresentContext.Default.Inherit(root);
            ParseChildren(root, doc, Matrix2x3.Identity, rootCtx);
            return doc;
        }

        // Inheritable presentation attributes that cascade through the SVG tree
        private struct PresentContext
        {
            public string Fill;         // "none", "#rrggbb", named color, or null=inherit
            public string Stroke;       // same
            public float  FillOpacity;
            public float  StrokeOpacity;
            public float  Opacity;
            public float  StrokeWidth;

            public static readonly PresentContext Default = new PresentContext
            { Fill = "black", Stroke = "none", FillOpacity = 1f, StrokeOpacity = 1f, Opacity = 1f, StrokeWidth = 1f };

            public PresentContext Inherit(XElement el)
            {
                var r = this;
                var style = el.Attribute("style")?.Value;
                r.Fill         = Attrs(el, style, "fill",           r.Fill);
                r.Stroke       = Attrs(el, style, "stroke",         r.Stroke);
                r.FillOpacity  = FA2(el, style, "fill-opacity",     r.FillOpacity);
                r.StrokeOpacity= FA2(el, style, "stroke-opacity",   r.StrokeOpacity);
                r.Opacity      = FA2(el, style, "opacity",          r.Opacity);
                r.StrokeWidth  = FA2(el, style, "stroke-width",     r.StrokeWidth);
                return r;
            }

            static string Attrs(XElement el, string style, string name, string fallback)
            {
                var v = el.Attribute(name)?.Value;
                if (v != null) return v;
                if (style != null) { var s = ExtractStyleStatic(style, name); if (s != null) return s; }
                return fallback;
            }
            static float FA2(XElement el, string style, string name, float fallback)
            {
                var v = el.Attribute(name)?.Value;
                if (v == null && style != null) v = ExtractStyleStatic(style, name);
                return v != null && float.TryParse(v, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float r) ? r : fallback;
            }
            static string ExtractStyleStatic(string style, string prop)
            {
                foreach (var part in style.Split(';'))
                {
                    var kv = part.Trim().Split(':');
                    if (kv.Length == 2 && kv[0].Trim() == prop) return kv[1].Trim();
                }
                return null;
            }
        }

        private static void ParseChildren(XElement parent, Document doc, Matrix2x3 xf)
            => ParseChildren(parent, doc, xf, PresentContext.Default);

        private static void ParseChildren(XElement parent, Document doc, Matrix2x3 xf, PresentContext ctx)
        {
            foreach (var el in parent.Elements())
            {
                var localXf  = xf * ParseTransform(el.Attribute("transform")?.Value);
                var localCtx = ctx.Inherit(el);
                var ln = el.Name.LocalName;

                if (ln == "g")   { ParseChildren(el, doc, localXf, localCtx); continue; }
                if (ln == "defs" || ln == "symbol" || ln == "style") continue;

                // <path> may produce multiple sub-paths — handle separately
                if (ln == "path")
                {
                    string pathId = el.Attribute("id")?.Value ?? string.Empty;
                    foreach (var sd in ParsePathShapes(el))
                    {
                        sd.Id = pathId;
                        ApplyPresentation(localCtx, sd);
                        doc.Shapes.Add(sd);
                    }
                    continue;
                }

                var data = ln switch
                {
                    "rect"     => ParseRect(el),
                    "circle"   => ParseCircle(el),
                    "ellipse"  => ParseEllipseEl(el),
                    "line"     => ParseLine(el),
                    "polyline" => ParsePolyline(el, false),
                    "polygon"  => ParsePolyline(el, true),
                    _          => null,
                };

                if (data == null) continue;
                data.Id = el.Attribute("id")?.Value ?? string.Empty;
                ApplyPresentation(localCtx, data);
                doc.Shapes.Add(data);
            }
        }

        // ── Shape Parsers ────────────────────────────────────────────────────────

        // Returns one ShapeData per SVG sub-path (each M command starts a new sub-path).
        private static IEnumerable<ShapeData> ParsePathShapes(XElement el)
        {
            var d = el.Attribute("d")?.Value;
            if (string.IsNullOrEmpty(d)) yield break;

            foreach (var path in BuildSubPaths(d))
            {
                if (path.Points.Count == 0) continue;
                Vector2 center;
                Vector2 size = EstimatePathBoundsAndCenter(path, out center);
                for (int pi = 0; pi < path.Points.Count; pi++)
                {
                    var pt = path.Points[pi];
                    pt.Position      -= center;
                    pt.ControlPoint1 -= center;
                    pt.ControlPoint2 -= center;
                    path.Points[pi] = pt;
                }
                yield return new ShapeData { Shape = path, Size = size, Position = center };
            }
        }

        private static ShapeData ParseRect(XElement el)
        {
            float x  = F(el, "x"),  y  = F(el, "y");
            float w  = F(el, "width"), h = F(el, "height");
            float rx = F(el, "rx"), ry = F(el, "ry");
            float r  = Mathf.Max(rx, ry);

            var shape = new RectangleShape { CornerRadius = Vector4.one * r, EdgeSoftness = 0f };
            return new ShapeData { Shape = shape, Size = new Vector2(w, h),
                Position = new Vector2(x + w * 0.5f, -(y + h * 0.5f)) };
        }

        private static ShapeData ParseCircle(XElement el)
        {
            float cx = F(el, "cx"), cy = F(el, "cy"), r = F(el, "r");
            return new ShapeData { Shape = new EllipseShape { EdgeSoftness = 0f }, Size = Vector2.one * (r * 2f),
                Position = new Vector2(cx, -cy) };
        }

        private static ShapeData ParseEllipseEl(XElement el)
        {
            float cx = F(el, "cx"), cy = F(el, "cy");
            float rx = F(el, "rx"), ry = F(el, "ry");
            return new ShapeData { Shape = new EllipseShape { EdgeSoftness = 0f }, Size = new Vector2(rx * 2f, ry * 2f),
                Position = new Vector2(cx, -cy) };
        }

        private static ShapeData ParseLine(XElement el)
        {
            float x1 = F(el, "x1"), y1 = F(el, "y1");
            float x2 = F(el, "x2"), y2 = F(el, "y2");
            var shape = new LineShape
            {
                Start = new Vector2(x1 - (x1+x2)*0.5f, -(y1 - (y1+y2)*0.5f)),
                End   = new Vector2(x2 - (x1+x2)*0.5f, -(y2 - (y1+y2)*0.5f)),
                Width = 1f,
                EdgeSoftness = 0f,
            };
            float mx = (x1 + x2) * 0.5f, my = (y1 + y2) * 0.5f;
            float bw = Mathf.Abs(x2 - x1) + 4f, bh = Mathf.Abs(y2 - y1) + 4f;
            return new ShapeData { Shape = shape, Size = new Vector2(bw, bh), Position = new Vector2(mx, -my) };
        }

        private static ShapeData ParsePolyline(XElement el, bool closed)
        {
            var pts = ParsePointList(el.Attribute("points")?.Value);
            if (pts == null || pts.Count < 2) return null;
            var path = new PathShape { Closed = closed, EdgeSoftness = 0f };
            foreach (var p in pts)
                path.Points.Add(new PathPoint { Position = new Vector2(p.x, -p.y), Type = PathPointType.Line });
            // Center points so origin = bounding-box center
            Vector2 center;
            Vector2 size = EstimatePathBoundsAndCenter(path, out center);
            for (int pi = 0; pi < path.Points.Count; pi++)
            {
                var pt = path.Points[pi];
                pt.Position -= center;
                path.Points[pi] = pt;
            }
            return new ShapeData { Shape = path, Size = size, Position = center };
        }

        // ── SVG Path Commands ────────────────────────────────────────────────────

        // Splits an SVG path d-string into one PathShape per M sub-path.
        // PathPoint convention used here:
        //   ControlPoint2 = OUT handle (c1 of the outgoing bezier, stored on the START point of that bezier)
        //   ControlPoint1 = IN  handle (c2 of the incoming bezier, stored on the END   point of that bezier)
        // This matches PathFlattener which reads:
        //   SubdivideCubic(prev.Position, prev.ControlPoint2, curr.ControlPoint1, curr.Position)
        private static List<PathShape> BuildSubPaths(string d)
        {
            var result   = new List<PathShape>();
            var tokens   = TokenizePath(d);
            int i        = 0;
            Vector2 cur  = Vector2.zero, start = Vector2.zero;
            Vector2 lastCtrl = Vector2.zero;
            char cmd     = 'M';
            PathShape path = null;

            while (i < tokens.Count)
            {
                var t = tokens[i];
                if (char.IsLetter(t[0])) { cmd = t[0]; i++; }

                switch (cmd)
                {
                    case 'M': case 'm':
                    {
                        var p = ReadVec2(tokens, ref i, cur, cmd == 'm');
                        if (path != null && path.Points.Count > 0) result.Add(path);
                        path = new PathShape { EdgeSoftness = 0f };
                        path.Points.Add(MakeLine(p));
                        cur = start = p; lastCtrl = cur;
                        cmd = cmd == 'm' ? 'l' : 'L';
                        break;
                    }
                    case 'L': case 'l':
                    {
                        if (path == null) path = new PathShape();
                        var p = ReadVec2(tokens, ref i, cur, cmd == 'l');
                        path.Points.Add(MakeLine(p));
                        cur = p; lastCtrl = cur;
                        break;
                    }
                    case 'H': case 'h':
                    {
                        if (path == null) path = new PathShape();
                        float x = ReadF(tokens, ref i);
                        var p = cmd == 'h' ? new Vector2(cur.x + x, cur.y) : new Vector2(x, cur.y);
                        path.Points.Add(MakeLine(p));
                        cur = p; lastCtrl = cur;
                        break;
                    }
                    case 'V': case 'v':
                    {
                        if (path == null) path = new PathShape();
                        float y = ReadF(tokens, ref i);
                        var p = cmd == 'v' ? new Vector2(cur.x, cur.y - y) : new Vector2(cur.x, -y);
                        path.Points.Add(MakeLine(p));
                        cur = p; lastCtrl = cur;
                        break;
                    }
                    case 'C': case 'c':
                    {
                        if (path == null) path = new PathShape();
                        var c1 = ReadVec2(tokens, ref i, cur, cmd == 'c');
                        var c2 = ReadVec2(tokens, ref i, cur, cmd == 'c');
                        var p  = ReadVec2(tokens, ref i, cur, cmd == 'c');
                        SetPrevCP2(path, c1);   // c1 = OUT handle of the start point
                        AddBezierPoint(path, p, c2);  // c2 = IN handle of the end point
                        lastCtrl = c2; cur = p;
                        break;
                    }
                    case 'S': case 's':
                    {
                        if (path == null) path = new PathShape();
                        var c1 = ReflectCtrl(lastCtrl, cur);  // reflect IN handle of previous end = OUT handle of cur
                        var c2 = ReadVec2(tokens, ref i, cur, cmd == 's');
                        var p  = ReadVec2(tokens, ref i, cur, cmd == 's');
                        SetPrevCP2(path, c1);
                        AddBezierPoint(path, p, c2);
                        lastCtrl = c2; cur = p;
                        break;
                    }
                    case 'Q': case 'q':
                    {
                        if (path == null) path = new PathShape();
                        var qc = ReadVec2(tokens, ref i, cur, cmd == 'q');
                        var p  = ReadVec2(tokens, ref i, cur, cmd == 'q');
                        var c1 = cur + (qc - cur) * (2f / 3f);  // OUT handle of start
                        var c2 = p   + (qc - p)   * (2f / 3f);  // IN  handle of end
                        SetPrevCP2(path, c1);
                        AddBezierPoint(path, p, c2);
                        lastCtrl = qc; cur = p;
                        break;
                    }
                    case 'T': case 't':
                    {
                        if (path == null) path = new PathShape();
                        var qc = ReflectCtrl(lastCtrl, cur);
                        var p  = ReadVec2(tokens, ref i, cur, cmd == 't');
                        var c1 = cur + (qc - cur) * (2f / 3f);
                        var c2 = p   + (qc - p)   * (2f / 3f);
                        SetPrevCP2(path, c1);
                        AddBezierPoint(path, p, c2);
                        lastCtrl = qc; cur = p;
                        break;
                    }
                    case 'A': case 'a':
                        if (path == null) path = new PathShape();
                        ArcToBezier(tokens, ref i, ref cur, cmd == 'a', path);
                        lastCtrl = cur;
                        break;
                    case 'Z': case 'z':
                        if (path != null) path.Closed = true;
                        cur = start; lastCtrl = cur;
                        break;
                    default:
                        i++;
                        break;
                }
            }
            if (path != null && path.Points.Count > 0) result.Add(path);
            return result;
        }

        // Sets the ControlPoint2 (OUT handle) of the last point in the path.
        private static void SetPrevCP2(PathShape path, Vector2 cp2)
        {
            if (path.Points.Count == 0) return;
            var pt = path.Points[path.Points.Count - 1];
            pt.ControlPoint2 = cp2;
            path.Points[path.Points.Count - 1] = pt;
        }

        // Adds a bezier end-point: ControlPoint1 = IN handle (c2), ControlPoint2 = placeholder.
        private static void AddBezierPoint(PathShape path, Vector2 pos, Vector2 inHandle)
        {
            path.Points.Add(new PathPoint
            {
                Position      = pos,
                ControlPoint1 = inHandle,
                ControlPoint2 = pos,   // placeholder; overwritten by next bezier segment's SetPrevCP2
                Type          = PathPointType.Bezier,
            });
        }

        // Converts SVG arc to 1-4 cubic bezier segments (endpoint → center parameterization).
        private static void ArcToBezier(
            List<string> tokens, ref int i, ref Vector2 cur, bool rel, PathShape path)
        {
            float rx   = ReadF(tokens, ref i);
            float ry   = ReadF(tokens, ref i);
            float xRot = ReadF(tokens, ref i) * Mathf.Deg2Rad;
            bool  large = ReadF(tokens, ref i) != 0;
            bool  sweep = ReadF(tokens, ref i) != 0;
            var   p2   = ReadVec2(tokens, ref i, cur, rel);   // Unity space (y-up)

            if (Vector2.Distance(cur, p2) < 0.001f) { cur = p2; return; }

            rx = Mathf.Abs(rx); ry = Mathf.Abs(ry);
            if (rx < 0.001f || ry < 0.001f) { path.Points.Add(MakeLine(p2)); cur = p2; return; }

            // SVG arc parameterization is defined for y-down coordinates.
            // cur and p2 are Unity-space (y-up) — flip Y for the computation.
            float x1 = cur.x, y1 = -cur.y;
            float x2 = p2.x,  y2 = -p2.y;

            float cos = Mathf.Cos(xRot), sin = Mathf.Sin(xRot);
            float dx2 = (x1 - x2) * 0.5f, dy2 = (y1 - y2) * 0.5f;
            float x1p =  cos * dx2 + sin * dy2;
            float y1p = -sin * dx2 + cos * dy2;

            float x1p2 = x1p * x1p, y1p2 = y1p * y1p;
            float rx2   = rx  * rx,  ry2  = ry  * ry;

            float lambda = x1p2 / rx2 + y1p2 / ry2;
            if (lambda > 1f) { float ls = Mathf.Sqrt(lambda); rx *= ls; ry *= ls; rx2 = rx*rx; ry2 = ry*ry; }

            float num = Mathf.Max(0f, rx2 * ry2 - rx2 * y1p2 - ry2 * x1p2);
            float den = rx2 * y1p2 + ry2 * x1p2;
            float sq  = (large == sweep ? -1f : 1f) * Mathf.Sqrt(num / Mathf.Max(den, 1e-6f));

            float cxp =  sq * rx * y1p / ry;
            float cyp = -sq * ry * x1p / rx;
            float cx  = cos * cxp - sin * cyp + (x1 + x2) * 0.5f;
            float cy  = sin * cxp + cos * cyp + (y1 + y2) * 0.5f;

            float ux = (x1p - cxp) / rx, uy = (y1p - cyp) / ry;
            float vx = -(x1p + cxp) / rx, vy = -(y1p + cyp) / ry;
            float theta1 = Mathf.Atan2(uy, ux);
            float dTheta  = Mathf.Atan2(vy, vx) - theta1;
            if (!sweep && dTheta > 0) dTheta -= Mathf.PI * 2f;
            if ( sweep && dTheta < 0) dTheta += Mathf.PI * 2f;

            int  segs   = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(dTheta) / (Mathf.PI * 0.5f)));
            float dSeg  = dTheta / segs;
            float alpha = Mathf.Sin(dSeg) * (Mathf.Sqrt(4f + 3f * Mathf.Tan(dSeg * 0.5f) * Mathf.Tan(dSeg * 0.5f)) - 1f) / 3f;

            // ArcPoint and ArcDeriv work in SVG space (y-down, using cx/cy from SVG-space computation).
            Vector2 ArcPoint(float t) {
                float xp = rx * Mathf.Cos(t), yp = ry * Mathf.Sin(t);
                return new Vector2(cos * xp - sin * yp + cx, sin * xp + cos * yp + cy);
            }
            Vector2 ArcDeriv(float t) {
                float xp = -rx * Mathf.Sin(t), yp = ry * Mathf.Cos(t);
                return new Vector2(cos * xp - sin * yp, sin * xp + cos * yp);
            }

            float t = theta1;
            Vector2 p1CurSVG = new Vector2(x1, y1);   // track current point in SVG space
            for (int s = 0; s < segs; s++)
            {
                float t2 = t + dSeg;
                var d1 = ArcDeriv(t);
                var d2 = ArcDeriv(t2);
                var ep  = ArcPoint(t2);            // SVG space end point
                var c1  = p1CurSVG + d1 * alpha;   // SVG space: OUT handle of start
                var c2  = ep        - d2 * alpha;   // SVG space: IN  handle of end
                // Convert SVG-space (y-down) to Unity-space (y-up) and store with correct convention
                SetPrevCP2(path, new Vector2(c1.x, -c1.y));        // c1 → OUT handle of previous point
                AddBezierPoint(path, new Vector2(ep.x, -ep.y), new Vector2(c2.x, -c2.y));
                t = t2;
                p1CurSVG = ep;
            }
            cur = p2;   // already Unity space
        }

        // ── Presentation (fill/stroke) ───────────────────────────────────────────

        private static void ApplyPresentation(PresentContext ctx, ShapeData data)
        {
            float totalOpacity = ctx.Opacity;
            float fillOp  = ctx.FillOpacity  * totalOpacity;
            float strkOp  = ctx.StrokeOpacity * totalOpacity;

            bool hasFill   = ctx.Fill   != "none" && !string.IsNullOrEmpty(ctx.Fill);
            bool hasStroke = ctx.Stroke != "none" && !string.IsNullOrEmpty(ctx.Stroke);

            if (hasFill)
            {
                var c = ParseColor(ctx.Fill);
                c.a *= fillOp;
                data.Fills.Add(new FillLayer { Fill = new SolidFill { Color = c } });
            }

            if (hasStroke)
            {
                var c = ParseColor(ctx.Stroke);
                c.a *= strkOp;
                float strkW = ctx.StrokeWidth;

                // For PathShape with no fill: encode the stroke as path thickness + fill layer.
                // This gives a correct band-around-path rendering via the SDF fill formula,
                // avoiding the open-path stroke formula artefacts.
                if (!hasFill && data.Shape is PathShape ps)
                {
                    ps.Thickness = strkW;
                    ps.EdgeSoftness = Mathf.Min(ps.EdgeSoftness, strkW * 0.5f);
                    data.Fills.Add(new FillLayer { Fill = new SolidFill { Color = c } });
                    // Pad size by stroke width so the rect mesh fully covers the stroke band.
                    data.Size = new Vector2(
                        Mathf.Max(data.Size.x, 0.01f) + strkW * 2f,
                        Mathf.Max(data.Size.y, 0.01f) + strkW * 2f);
                }
                else
                {
                    data.Strokes.Add(new StrokeLayer
                    {
                        Fill      = new SolidFill { Color = c },
                        Width     = strkW,
                        Alignment = StrokeAlignment.Center,
                    });
                }
            }
        }

        // ── Utilities ─────────────────────────────────────────────────────────────

        // ReadVec2 already converts to Unity Y-up space; store as-is.
        private static PathPoint MakeLine(Vector2 p) =>
            new PathPoint { Position = p, Type = PathPointType.Line };

        private static PathPoint MakeBezier(Vector2 pos, Vector2 cp1, Vector2 cp2) =>
            new PathPoint { Position = pos, ControlPoint1 = cp1, ControlPoint2 = cp2, Type = PathPointType.Bezier };

        private static Vector2 ReflectCtrl(Vector2 ctrl, Vector2 cur) => cur * 2f - ctrl;

        private static Vector2 EstimatePathBoundsAndCenter(PathShape ps, out Vector2 center)
        {
            if (ps.Points == null || ps.Points.Count == 0) { center = Vector2.zero; return Vector2.one * 100f; }
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            int count = ps.Points.Count;
            for (int i = 0; i < count; i++)
            {
                var pt = ps.Points[i];
                Expand(ref minX, ref maxX, ref minY, ref maxY, pt.Position);
                // Include IN handle for bezier endpoints
                if (pt.Type == PathPointType.Bezier)
                    Expand(ref minX, ref maxX, ref minY, ref maxY, pt.ControlPoint1);
                // Include OUT handle whenever the next segment is a bezier (point may be Line-typed start)
                bool nextIsBezier = (i + 1 < count && ps.Points[i + 1].Type == PathPointType.Bezier);
                if (pt.Type == PathPointType.Bezier || nextIsBezier)
                    Expand(ref minX, ref maxX, ref minY, ref maxY, pt.ControlPoint2);
            }
            // Closed path: include handles for the wrap-around segment (last → first)
            if (ps.Closed && count >= 2 && ps.Points[0].Type == PathPointType.Bezier)
            {
                Expand(ref minX, ref maxX, ref minY, ref maxY, ps.Points[count - 1].ControlPoint2);
                Expand(ref minX, ref maxX, ref minY, ref maxY, ps.Points[0].ControlPoint1);
            }
            center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            return new Vector2(maxX - minX, maxY - minY);
        }

        private static void Expand(ref float minX, ref float maxX, ref float minY, ref float maxY, Vector2 p)
        {
            if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
        }

        private static Vector2 EstimatePathBounds(PathShape ps)
        {
            Vector2 center;
            return EstimatePathBoundsAndCenter(ps, out center);
        }

        private static Vector2 ParseViewBox(string s)
        {
            if (string.IsNullOrEmpty(s)) return new Vector2(512, 512);
            var parts = s.Split(new[] {' ', ','}, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4)
                return new Vector2(Pf(parts[2]), Pf(parts[3]));
            if (parts.Length >= 2)
                return new Vector2(Pf(parts[0]), Pf(parts[1]));
            return new Vector2(512, 512);
        }

        private static Color ParseColor(string s)
        {
            if (string.IsNullOrEmpty(s) || s == "none" || s == "transparent") return Color.clear;
            if (s.StartsWith("#"))
            {
                s = s.Substring(1);
                if (s.Length == 3) s = $"{s[0]}{s[0]}{s[1]}{s[1]}{s[2]}{s[2]}";
                if (s.Length == 6 && uint.TryParse(s, NumberStyles.HexNumber, null, out uint rgb))
                    return new Color((rgb>>16&0xFF)/255f, (rgb>>8&0xFF)/255f, (rgb&0xFF)/255f);
            }
            if (s.StartsWith("rgb("))
            {
                var nums = s.Substring(4, s.Length - 5).Split(',');
                if (nums.Length == 3)
                    return new Color(Pf(nums[0])/255f, Pf(nums[1])/255f, Pf(nums[2])/255f);
            }
            // Named colors (common subset)
            return s switch
            {
                "black"   => Color.black,   "white"  => Color.white,
                "red"     => Color.red,     "green"  => Color.green,
                "blue"    => Color.blue,    "yellow" => Color.yellow,
                "cyan"    => Color.cyan,    "magenta"=> Color.magenta,
                "gray"    or "grey" => Color.gray,
                "transparent" => Color.clear,
                _ => Color.black,
            };
        }

        private static Matrix2x3 ParseTransform(string s)
        {
            if (string.IsNullOrEmpty(s)) return Matrix2x3.Identity;
            if (s.StartsWith("translate("))
            {
                var nums = s.Substring(10, s.Length-11).Split(new[]{',', ' '}, StringSplitOptions.RemoveEmptyEntries);
                float tx = nums.Length > 0 ? Pf(nums[0]) : 0f;
                float ty = nums.Length > 1 ? Pf(nums[1]) : 0f;
                return new Matrix2x3(1,0,tx, 0,1,-ty);
            }
            if (s.StartsWith("scale("))
            {
                var nums = s.Substring(6, s.Length-7).Split(new[]{',', ' '}, StringSplitOptions.RemoveEmptyEntries);
                float sx = nums.Length > 0 ? Pf(nums[0]) : 1f;
                float sy = nums.Length > 1 ? Pf(nums[1]) : sx;
                return new Matrix2x3(sx,0,0, 0,sy,0);
            }
            return Matrix2x3.Identity;
        }

        // ── Token / Number Helpers ────────────────────────────────────────────────

        private static List<string> TokenizePath(string d)
        {
            var result = new List<string>();
            int i = 0, n = d.Length;
            while (i < n)
            {
                char c = d[i];
                if (char.IsWhiteSpace(c) || c == ',') { i++; continue; }
                if (char.IsLetter(c)) { result.Add(c.ToString()); i++; continue; }
                int start = i;
                if (c == '-' || c == '+') i++;
                while (i < n && (char.IsDigit(d[i]) || d[i] == '.' || d[i] == 'e' || d[i] == 'E' ||
                       ((d[i] == '+' || d[i] == '-') && i > start && (d[i-1] == 'e' || d[i-1] == 'E')))) i++;
                if (i > start) result.Add(d.Substring(start, i - start));
            }
            return result;
        }

        private static List<Vector2> ParsePointList(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var parts = s.Split(new[]{' ', ',', '\t', '\n', '\r'}, StringSplitOptions.RemoveEmptyEntries);
            var pts = new List<Vector2>();
            for (int i = 0; i + 1 < parts.Length; i += 2)
                pts.Add(new Vector2(Pf(parts[i]), Pf(parts[i+1])));
            return pts;
        }

        private static float ReadF(List<string> tokens, ref int i) =>
            i < tokens.Count && float.TryParse(tokens[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

        private static Vector2 ReadVec2(List<string> tokens, ref int i, Vector2 origin, bool relative)
        {
            float x = ReadF(tokens, ref i);
            float y = ReadF(tokens, ref i);
            if (relative) return origin + new Vector2(x, -y);
            return new Vector2(x, -y);
        }

        private static float F(XElement el, string name) =>
            float.TryParse(el.Attribute(name)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

        private static float FA(XElement el, string name, float def)
        {
            var v = Attr(el, name);
            return v != null && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float r) ? r : def;
        }

        private static string Attr(XElement el, string name) =>
            el.Attribute(name)?.Value ?? el.Attribute("style")?.Value?.Let(style => ExtractStyle(style, name));

        private static string ExtractStyle(string style, string prop)
        {
            foreach (var part in style.Split(';'))
            {
                var kv = part.Trim().Split(':');
                if (kv.Length == 2 && kv[0].Trim() == prop) return kv[1].Trim();
            }
            return null;
        }

        private static float Pf(string s) =>
            float.TryParse(s?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

        // ── Minimal 2x3 transform matrix (for SVG transforms) ───────────────────

        private struct Matrix2x3
        {
            public float M00, M01, M02, M10, M11, M12;
            public static readonly Matrix2x3 Identity = new Matrix2x3(1,0,0, 0,1,0);

            public Matrix2x3(float m00, float m01, float m02, float m10, float m11, float m12)
            { M00=m00; M01=m01; M02=m02; M10=m10; M11=m11; M12=m12; }

            public static Matrix2x3 operator *(Matrix2x3 a, Matrix2x3 b) =>
                new Matrix2x3(
                    a.M00*b.M00+a.M01*b.M10, a.M00*b.M01+a.M01*b.M11, a.M00*b.M02+a.M01*b.M12+a.M02,
                    a.M10*b.M00+a.M11*b.M10, a.M10*b.M01+a.M11*b.M11, a.M10*b.M02+a.M11*b.M12+a.M12);
        }
    }

    internal static class StringExt
    {
        public static T Let<T>(this string s, Func<string, T> f) => f(s);
    }
}
