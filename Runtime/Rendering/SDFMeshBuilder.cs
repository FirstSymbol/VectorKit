using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VectorKit.Runtime
{
    // Generates quad geometry for VectorShapeUI (VertexHelper) and VectorShapeWorld (Mesh).
    //
    // Vertex channel layout (matches VectorShape.shader appdata_vk):
    //   uv0  (x,y,a,b)  — local SDF position (x,y) — INCLUDES shadow offset for shadow quads
    //                    — zw: (dashSize, dashGap) for strokes; (paramA, paramB) for other
    //   uv1  (x,y,z,w)  — primary shape params (PackShaderParams)
    //   uv2  (x,y,z,w)  — (halfW, halfH, edgeSoftness, effectType)
    //   uv3  (x,y,z,w)  — (atlasRow, fillKind, gradAngle, gradScale)
    //   normal (x,y,z)  — shadow: (offsetX, offsetY, blur)
    //                    — fill/stroke: (internalPadding, AA=1, 0)
    //                    — bevel: (0, 0, bevelDistance)
    //   tangent(x,y,z,w)— shadow: (spread, AA, gradOffX, gradOffY)
    //                    — stroke: (strokeHalfWidth, 0, 0, alignment)
    //                    — bevel:  (highlightAlpha, 0, bevelAngle, shadowAlpha)
    //                    — fill:   (0, 0, gradOffX, gradOffY)
    //   color            — vertex color (fill RGBA * tint * opacity, alpha carries opacity)
    //
    // Draw order: DropShadow → OuterGlow → Fills → InnerShadow → InnerGlow → Strokes → Bevel
    internal static class SDFMeshBuilder
    {
        private const int ET_Fill   = 0;
        private const int ET_Shadow = 1;
        private const int ET_Stroke = 2;
        private const int ET_Inner  = 3;
        private const int ET_Bevel  = 5;

        // Canvas (uGUI) path
        public static void Populate(
            VertexHelper vh, Vector2 size, Color tint,
            ShapeDefinition shape, IList<FillLayer> fills,
            IList<StrokeLayer> strokes, IList<VectorEffect> effects,
            VectorShaderState outState, List<int> outAtlasRows)
        {
            vh.Clear();
            outAtlasRows?.Clear();
            if (shape == null) return;

            float hw       = size.x * 0.5f * shape.Scale.x;
            float hh       = size.y * 0.5f * shape.Scale.y;
            var   halfSize = new Vector2(hw, hh);

            BuildShaderState(outState, shape, fills, halfSize);
            GenerateGeometry(vh, hw, hh, tint, shape, fills, strokes, effects, outAtlasRows);
        }

        // World-Space path
        public static void RebuildMesh(
            Mesh mesh, Vector2 size, Color tint,
            ShapeDefinition shape, IList<FillLayer> fills,
            IList<StrokeLayer> strokes, IList<VectorEffect> effects,
            VectorShaderState outState, List<int> outAtlasRows)
        {
            var vh = new VertexHelper();
            Populate(vh, size, tint, shape, fills, strokes, effects, outState, outAtlasRows);
            vh.FillMesh(mesh);
            vh.Dispose();
        }

        // ── Shader State ─────────────────────────────────────────────────────────

        private static void BuildShaderState(
            VectorShaderState state, ShapeDefinition shape,
            IList<FillLayer> fills, Vector2 halfSize)
        {
            state.Clear();
            state.AtlasTex = GradientAtlas.Texture;

            if (shape is PathShape ps)
            {
                state.ShapeKind = ShapeKind.Path;
                var pts = new List<Vector2>();
                PathFlattener.Flatten(ps, pts);
                if (state.PathData == null) state.PathData = new Vector4[256];
                state.PathPointCount = PathFlattener.PackIntoShaderArray(pts, state.PathData);
            }
            else if (shape is BooleanShape bs)
            {
                state.ShapeKind = ShapeKind.Rectangle;
                if (bs.Operations != null) PackBoolOps(state, bs.Operations, halfSize);
            }
            else
            {
                state.ShapeKind = shape.Kind;
            }

            if (fills != null)
                foreach (var layer in fills)
                    if (layer?.Fill is ImageFill imf && imf.Texture != null)
                    { state.PatternTex = imf.Texture; break; }
        }

        private static void PackBoolOps(
            VectorShaderState state, List<BooleanOperationData> ops, Vector2 halfSize)
        {
            int n = 0;
            for (int i = 0; i < ops.Count && n < 16; i++)
            {
                var op = ops[i];
                if (op?.Shape == null || op.Operation == BoolOp.None) continue;

                if (state.BoolOpType == null)
                {
                    state.BoolOpType      = new Vector4[16];
                    state.BoolShapeParams = new Vector4[16];
                    state.BoolTransform   = new Vector4[16];
                    state.BoolSize        = new Vector4[16];
                }

                state.BoolOpType[n]      = new Vector4((float)op.Operation, (float)op.Shape.Kind, op.Smoothness, op.Smoothness);
                state.BoolShapeParams[n] = op.Shape.PackShaderParams();
                state.BoolTransform[n]   = new Vector4(0, 0, 0, 1); // xy=offset, z=rotation, w=unused
                state.BoolSize[n]        = new Vector4(halfSize.x, halfSize.y, 0, 0);

                if (op.Shape is PathShape bps)
                {
                    var bpts = new List<Vector2>();
                    PathFlattener.Flatten(bps, bpts);
                    if (state.BoolPathData == null) state.BoolPathData = new Vector4[256];
                    state.BoolPathPointCount = PathFlattener.PackIntoShaderArray(bpts, state.BoolPathData);
                }
                n++;
            }
            state.BoolCount = n;
        }

        // ── Geometry Generation ──────────────────────────────────────────────────

        private static void GenerateGeometry(
            VertexHelper vh, float hw, float hh, Color tint,
            ShapeDefinition shape, IList<FillLayer> fills,
            IList<StrokeLayer> strokes, IList<VectorEffect> effects,
            List<int> outAtlasRows)
        {
            Vector4 sp1 = shape.PackShaderParams();
            float   pad = shape.InternalPadding;

            // uv2.z: shape-specific smoothing (corner style for rect, width for line)
            float soft = shape is RectangleShape rs ? rs.CornerSmoothing
                       : shape is LineShape ls ? ls.Width
                       : 0f;

            // edge AA: controls smoothstep transition width at shape boundary
            float aa = Mathf.Max(0.001f, shape.EdgeSoftness);

            // 1. Drop shadows (behind everything)
            if (effects != null)
                foreach (var e in effects)
                    if (e is DropShadowEffect ds && ds.Enabled)
                    {
                        float exp = ds.Blur + Mathf.Max(0f, ds.Spread);
                        int   row = AcquireRow(ds.Fill, outAtlasRows);
                        AddShadowQuad(vh, hw, hh, exp, ds.Offset, sp1, soft, ET_Shadow, pad,
                            FillParams(ds.Fill, row), VertColor(ds.Fill, tint, ds.Opacity),
                            aa, ds.Blur, ds.Spread);
                    }

            // 2. Outer glows
            if (effects != null)
                foreach (var e in effects)
                    if (e is OuterGlowEffect og && og.Enabled)
                    {
                        float exp = og.Blur + Mathf.Max(0f, og.Spread);
                        int   row = AcquireRow(og.Fill, outAtlasRows);
                        AddShadowQuad(vh, hw, hh, exp, Vector2.zero, sp1, soft, ET_Shadow, pad,
                            FillParams(og.Fill, row), VertColor(og.Fill, tint, og.Opacity),
                            aa, og.Blur, og.Spread);
                    }

            // 3. Fill layers (back to front)
            if (fills != null)
                foreach (var f in fills)
                    if (f is { Enabled: true, Fill: not null })
                    {
                        int row = AcquireRow(f.Fill, outAtlasRows);
                        AddFillQuad(vh, hw, hh, sp1, soft, ET_Fill, pad,
                            FillParams(f.Fill, row), VertColor(f.Fill, tint, f.Opacity), aa);
                    }

            // 4. Inner shadows — quad stays at origin so all shape pixels are covered
            if (effects != null)
                foreach (var e in effects)
                    if (e is InnerShadowEffect ins && ins.Enabled)
                    {
                        int row = AcquireRow(ins.Fill, outAtlasRows);
                        AddInnerEffectQuad(vh, hw, hh, ins.Offset, sp1, soft, ET_Inner, pad,
                            FillParams(ins.Fill, row), VertColor(ins.Fill, tint, ins.Opacity),
                            aa, ins.Blur, ins.Spread);
                    }

            // 5. Inner glows
            if (effects != null)
                foreach (var e in effects)
                    if (e is InnerGlowEffect ing && ing.Enabled)
                    {
                        int row = AcquireRow(ing.Fill, outAtlasRows);
                        AddInnerEffectQuad(vh, hw, hh, Vector2.zero, sp1, soft, ET_Inner, pad,
                            FillParams(ing.Fill, row), VertColor(ing.Fill, tint, ing.Opacity),
                            aa, ing.Blur, ing.Spread);
                    }

            // 6. Stroke layers
            if (strokes != null)
                foreach (var s in strokes)
                    if (s is { Enabled: true, Fill: not null })
                    {
                        float exp = s.Alignment == StrokeAlignment.Outside ? s.Width
                                  : s.Alignment == StrokeAlignment.Center  ? s.Width * 0.5f
                                  : 0f;
                        int row = AcquireRow(s.Fill, outAtlasRows);
                        AddStrokeQuad(vh, hw, hh, exp, sp1, soft, pad,
                            FillParams(s.Fill, row), VertColor(s.Fill, tint, s.Opacity),
                            aa, s.Width * 0.5f, (float)s.Alignment, s.Dash, s.Gap);
                    }

            // 7. Bevel
            if (effects != null)
                foreach (var e in effects)
                    if (e is BevelEffect bv && bv.Enabled)
                        AddBevelQuad(vh, hw, hh, sp1, soft, pad, bv, aa);
        }

        // ── Quad Specialisations ─────────────────────────────────────────────────

        // Shadow/glow quads: UV0.xy includes the offset, normal.xy = offset for shader
        private static void AddShadowQuad(
            VertexHelper vh, float hw, float hh, float expand,
            Vector2 offset, Vector4 sp1, float soft, int effectType, float pad,
            Vector4 fillParams, Color32 vertColor, float aa, float blur, float spread)
        {
            float qw = hw + expand;
            float qh = hh + expand;
            AddQuad(vh, qw, qh, offset, sp1,
                new Vector4(hw, hh, soft, effectType),
                fillParams,
                new Vector3(offset.x, offset.y, blur),
                new Vector4(spread, aa, 0, 0),
                vertColor, 0, 0,
                includeOffsetInUV: true);
        }

        // Inner shadow/glow quads: quad stays at origin so every shape pixel is covered.
        // The shadow offset is encoded in normal.xy (negated so shader's p-=normal gives p+offset).
        private static void AddInnerEffectQuad(
            VertexHelper vh, float hw, float hh,
            Vector2 shadowOffset, Vector4 sp1, float soft, int effectType, float pad,
            Vector4 fillParams, Color32 vertColor, float aa, float blur, float spread)
        {
            AddQuad(vh, hw, hh, Vector2.zero, sp1,
                new Vector4(hw, hh, soft, effectType),
                fillParams,
                new Vector3(-shadowOffset.x, -shadowOffset.y, blur),
                new Vector4(spread, aa, 0, 0),
                vertColor, 0, 0,
                includeOffsetInUV: false);
        }

        // Fill quads: normal.y = aa (edge softness)
        private static void AddFillQuad(
            VertexHelper vh, float hw, float hh,
            Vector4 sp1, float soft, int effectType, float pad,
            Vector4 fillParams, Color32 vertColor, float aa)
        {
            AddQuad(vh, hw, hh, Vector2.zero, sp1,
                new Vector4(hw, hh, soft, effectType),
                fillParams,
                new Vector3(pad, aa, 0),
                new Vector4(0, 0, 0, 0),
                vertColor, 0, 0,
                includeOffsetInUV: false);
        }

        // Stroke quads: tangent.x = strokeHalfWidth, tangent.w = alignment
        private static void AddStrokeQuad(
            VertexHelper vh, float hw, float hh, float expand,
            Vector4 sp1, float soft, float pad,
            Vector4 fillParams, Color32 vertColor, float aa,
            float strokeHW, float alignment, float dash, float gap)
        {
            float qw = hw + expand;
            float qh = hh + expand;
            AddQuad(vh, qw, qh, Vector2.zero, sp1,
                new Vector4(hw, hh, soft, ET_Stroke),
                fillParams,
                new Vector3(pad, aa, 0),
                new Vector4(strokeHW, 0, 0, alignment),
                vertColor, dash, gap,
                includeOffsetInUV: false);
        }

        // Bevel quads: normal.y = aa, normal.z = bevel distance
        private static void AddBevelQuad(
            VertexHelper vh, float hw, float hh,
            Vector4 sp1, float soft, float pad, BevelEffect bv, float aa)
        {
            Color32 bvc = new Color32(255, 255, 255, 255);
            AddQuad(vh, hw, hh, Vector2.zero, sp1,
                new Vector4(hw, hh, soft, ET_Bevel),
                Vector4.zero,
                new Vector3(0, aa, bv.Distance),
                new Vector4(bv.HighlightAlpha, 0,
                            bv.Angle * Mathf.Deg2Rad,
                            bv.ShadowAlpha),
                bvc, 0, 0,
                includeOffsetInUV: false);
        }

        // ── Core Quad Emitter ────────────────────────────────────────────────────

        private static void AddQuad(
            VertexHelper vh,
            float qw, float qh,           // quad half extents
            Vector2 offset,               // screen position offset (vertex positions)
            Vector4 sp1,                  // uv1 = primary shape params
            Vector4 baseData,             // uv2 = (hw, hh, soft, effectType)
            Vector4 fillParams,           // uv3 = (atlasRow, fillKind, gradAngle, gradScale)
            Vector3 normal,               // see layout comment above
            Vector4 tangent,              // see layout comment above
            Color32 vertColor,
            float   dashParam,            // uv0.z
            float   gapParam,             // uv0.w
            bool    includeOffsetInUV)    // true for shadow quads
        {
            int bi = vh.currentVertCount;

            var v = new UIVertex
            {
                color   = vertColor,
                uv1     = sp1,
                uv2     = baseData,
                uv3     = fillParams,
                normal  = normal,
                tangent = tangent,
            };

            // BL
            float bx = -qw, by = -qh;
            v.position = new Vector3(bx + offset.x, by + offset.y, 0);
            v.uv0 = new Vector4(includeOffsetInUV ? bx + offset.x : bx,
                                includeOffsetInUV ? by + offset.y : by,
                                dashParam, gapParam);
            vh.AddVert(v);

            // BR
            float rx = qw;
            v.position = new Vector3(rx + offset.x, by + offset.y, 0);
            v.uv0 = new Vector4(includeOffsetInUV ? rx + offset.x : rx,
                                includeOffsetInUV ? by + offset.y : by,
                                dashParam, gapParam);
            vh.AddVert(v);

            // TR
            float ty = qh;
            v.position = new Vector3(rx + offset.x, ty + offset.y, 0);
            v.uv0 = new Vector4(includeOffsetInUV ? rx + offset.x : rx,
                                includeOffsetInUV ? ty + offset.y : ty,
                                dashParam, gapParam);
            vh.AddVert(v);

            // TL
            v.position = new Vector3(bx + offset.x, ty + offset.y, 0);
            v.uv0 = new Vector4(includeOffsetInUV ? bx + offset.x : bx,
                                includeOffsetInUV ? ty + offset.y : ty,
                                dashParam, gapParam);
            vh.AddVert(v);

            vh.AddTriangle(bi, bi + 1, bi + 2);
            vh.AddTriangle(bi, bi + 2, bi + 3);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static int AcquireRow(FillDefinition fill, List<int> outAtlasRows)
        {
            if (fill == null || fill is SolidFill) return 0;
            int row = GradientAtlas.Acquire(fill);
            outAtlasRows?.Add(row);
            return row;
        }

        private static Color32 VertColor(FillDefinition fill, Color tint, float opacity)
        {
            Color c;
            if (fill is SolidFill sf)
                c = new Color(sf.Color.r * tint.r, sf.Color.g * tint.g, sf.Color.b * tint.b,
                              sf.Color.a * tint.a * opacity);
            else
                c = new Color(tint.r, tint.g, tint.b, tint.a * opacity);
            return c;
        }

        private static Vector4 FillParams(FillDefinition fill, int atlasRow) => fill switch
        {
            SolidFill              => new Vector4(0,        (int)FillKind.Solid,          0,    1),
            LinearGradientFill lgf => new Vector4(atlasRow, (int)FillKind.LinearGradient, lgf.Angle, lgf.Scale),
            RadialGradientFill rgf => new Vector4(atlasRow, (int)FillKind.RadialGradient, 0,    rgf.Radius),
            ConicGradientFill  cgf => new Vector4(atlasRow, (int)FillKind.ConicGradient,  cgf.StartAngle, 1),
            ImageFill          imf => new Vector4(atlasRow, (int)FillKind.Image,           (float)imf.FitMode, 1),
            _                      => Vector4.zero,
        };
    }
}
