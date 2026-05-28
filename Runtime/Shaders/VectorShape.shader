Shader "VectorKit/Shape"
{
    Properties
    {
        [HideInInspector] _MainTex ("Gradient Atlas", 2D) = "white" {}
        _PatternTex ("Pattern Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _AtlasHeightInv ("Atlas Height Inv", Float) = 0.00390625  // 1/256 default

        _StencilComp      ("Stencil Comparison", Float) = 8
        _Stencil          ("Stencil ID", Float) = 0
        _StencilOp        ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask  ("Stencil Read Mask", Float) = 255
        _ColorMask        ("Color Mask", Float) = 15

        _MaskMatrixX ("Mask Matrix X", Vector) = (1,0,0,0)
        _MaskMatrixY ("Mask Matrix Y", Vector) = (0,1,0,0)
        _MaskMatrixZ ("Mask Matrix Z", Vector) = (0,0,1,0)
        _MaskMatrixW ("Mask Matrix W", Vector) = (0,0,0,1)
        _MaskParams  ("Mask Params",   Vector) = (0,0,0,0)
        _MaskSize    ("Mask Size",     Vector) = (0,0,0,0)
        _MaskShape   ("Mask Shape",    Vector) = (0,0,0,0)
        _MaskTex     ("Mask Gradient", 2D) = "white" {}
        _MaskFillParams ("Mask Fill Params", Vector) = (0,0,0,0)
        _MaskFillOffset ("Mask Fill Offset", Vector) = (0,0,0,0)
        _MaskBoolParams ("Mask Bool Count", Int) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.5

            // Shape type - one keyword active at a time
            #pragma multi_compile_local SHAPE_RECTANGLE SHAPE_ELLIPSE SHAPE_POLYGON SHAPE_STAR SHAPE_CAPSULE SHAPE_LINE SHAPE_ARC SHAPE_PATH SHAPE_TRIANGLE SHAPE_HEART _

            // Feature flags
            #pragma multi_compile_local _ HAS_BOOLEANS
            #pragma multi_compile_local _ HAS_MASK
            #pragma multi_compile_local _ HAS_NOISE
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "Include/SDFLibrary.hlsl"
            #include "Include/FillLibrary.hlsl"
            #include "Include/EffectsLibrary.hlsl"

            // ── Uniforms ──────────────────────────────────────────────────────

            sampler2D _MainTex;
            sampler2D _PatternTex;
            float4    _ClipRect;
            float     _AtlasHeightInv;

            // Boolean operations - upgraded to 16
            int    _BoolParams1;
            float4 _BoolData_OpType    [16];
            float4 _BoolData_ShapeParams[16];
            float4 _BoolData_Transform [16];
            float4 _BoolData_Size      [16];

            // Soft mask
            float4    _MaskMatrixX, _MaskMatrixY, _MaskMatrixZ, _MaskMatrixW;
            float4    _MaskParams, _MaskSize, _MaskShape;
            sampler2D _MaskTex;
            float4    _MaskFillParams, _MaskFillOffset;
            int       _MaskBoolParams;
            float4 _MaskBoolOpType    [16];
            float4 _MaskBoolShapeParams[16];
            float4 _MaskBoolTransform [16];
            float4 _MaskBoolSize      [16];

            // ── Vertex I/O ────────────────────────────────────────────────────

            struct appdata_vk
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float3 normal   : NORMAL;    // xy=shadowOffset/internalPadding  z=blurRadius
                float4 tangent  : TANGENT;   // x=spread/strokeWidth  y=AA  z=gradOffsetX/bevelAngle  w=gradOffsetY/strokeAlignment/shadowAlpha
                float4 texcoord0: TEXCOORD0; // xy=localPos  zw=dashData
                float4 texcoord1: TEXCOORD1; // shape-specific packed params
                float4 texcoord2: TEXCOORD2; // xy=scaledSize  z=smoothing  w=effectType
                float4 texcoord3: TEXCOORD3; // x=atlasRow  y=fillKind  z=gradAngle  w=blendMode(float)
            };

            struct v2f
            {
                float4 vertex       : SV_POSITION;
                fixed4 color        : COLOR;
                float4 worldPosition: TEXCOORD4;

                float4 baseData     : TEXCOORD2;  // xy=halfSize  z=smoothing  w=effectType
                float4 shapeParams  : TEXCOORD1;
                float4 fillParams   : TEXCOORD3;  // x=atlasRow  y=fillKind  z=gradAngle  w=blendMode

                float4 uv0          : TEXCOORD0;  // xy=adjusted localPos  zw=origLocalPos (for shadow offset)
                float4 effectData   : TEXCOORD5;  // x=blur  y=aa  z=internalPadding  w=spread
                float4 precalc1     : TEXCOORD6;
                float4 precalc2     : TEXCOORD7;
                float4 extraData    : TANGENT;    // xy=p2 for ring/starInner  z=gradOffsetX/bevelAngle  w=gradOffsetY/strokeAlignment
            };

            // ── Vertex Shader ─────────────────────────────────────────────────

            v2f vert(appdata_vk v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex        = UnityObjectToClipPos(o.worldPosition);
                o.color         = v.color;

                o.shapeParams = v.texcoord1;
                o.baseData    = v.texcoord2;
                o.fillParams  = v.texcoord3;

                float effectType     = v.texcoord2.w;
                float2 p_orig        = v.texcoord0.xy;
                float2 p             = p_orig;

                float blur             = 0.0;
                float aa               = 1.0;
                float internalPadding  = 0.0;
                float spread           = v.tangent.x;

                bool isShadowEffect = (effectType == 1.0 || effectType == 3.0);

                if (isShadowEffect)
                {
                    p    -= v.normal.xy;  // apply shadow/glow offset
                    blur  = v.normal.z;
                    aa    = max(v.tangent.y, 0.001);
                }
                else
                {
                    internalPadding = v.normal.x;
                    aa              = max(v.normal.y, 0.001);
                    blur            = v.normal.z;
                }

                o.uv0       = float4(p.x, p.y, p_orig.x, p_orig.y);
                o.effectData = float4(blur, aa, internalPadding, spread);
                o.precalc1  = float4(0, 0, 0, 0);
                o.precalc2  = float4(v.texcoord0.z, v.texcoord0.w, 0, 0);  // zw = dashData
                o.extraData = float4(0, 0, v.tangent.z, v.tangent.w);

                float2 halfSize = v.texcoord2.xy * 0.5;
                float4 params   = v.texcoord1;

#if defined(SHAPE_POLYGON)
                float n_p  = max(3.0, params.x);
                float an_p = 3.14159265 / n_p;
                float maxR_p = min(halfSize.x, halfSize.y);
                float rounding_p = params.y * maxR_p * 0.5;
                float rOuter_p   = maxR_p - rounding_p;
                o.precalc1 = float4(2.0 * an_p, rOuter_p * sin(an_p), rOuter_p * cos(an_p), rounding_p);
#elif defined(SHAPE_STAR)
                float n_s    = max(3.0, params.x);
                float maxR_s = min(halfSize.x, halfSize.y);
                float ro_s   = params.z * maxR_s * 0.5;
                float rOut_s = max(maxR_s - ro_s, 0.001);
                float rIn_s  = max(params.y * maxR_s - ro_s, 0.001);
                float an_s   = 3.1415926535 / n_s;
                float2 p1_s  = float2(0.0, rOut_s);
                float2 p2_s  = float2(rIn_s * sin(an_s), rIn_s * cos(an_s));
                float2 ba_s  = p2_s - p1_s;
                float  ba2_s = max(dot(ba_s, ba_s), 0.00001);
                o.precalc1   = float4(2.0 * an_s, rOut_s, ba2_s, ro_s);
                o.precalc2.zw = p2_s;
                o.extraData.xy = float2(params.w * maxR_s, 0);
#elif defined(SHAPE_CAPSULE)
                float r_c  = params.x * min(halfSize.x, halfSize.y);
                float2 h_c = max(halfSize - r_c, 0.0);
                o.precalc1 = float4(h_c.x, h_c.y, r_c, 0);
#elif defined(SHAPE_ARC)
                float maxR_r  = min(halfSize.x, halfSize.y);
                float innerR  = params.x * maxR_r;
                float thick   = (maxR_r - innerR) * 0.5;
                float midR    = (maxR_r + innerR) * 0.5;
                float2 p1_r   = midR * float2(sin(params.y), cos(params.y));
                float2 p2_r   = midR * float2(sin(params.z), cos(params.z));
                float targetDa = frac((params.z - params.y) / 6.28318);
                o.precalc1    = float4(midR, thick, targetDa, 0);
                o.precalc2.zw = p1_r;
                o.extraData.xy = p2_r;
#endif
                return o;
            }

            // ── Fragment helpers ──────────────────────────────────────────────

            float GetMainSDF_Opt(float2 p, float2 halfSize, float smoothing, float4 params, v2f i)
            {
#if defined(SHAPE_RECTANGLE)
                return sdRectangle(p, halfSize, smoothing, params);
#elif defined(SHAPE_ELLIPSE)
                return sdEllipse(p, halfSize);
#elif defined(SHAPE_POLYGON)
                return sdPolygon_Precalc(p, i.precalc1);
#elif defined(SHAPE_STAR)
                return sdStar_Precalc(p, i.precalc1, i.precalc2, i.extraData.x);
#elif defined(SHAPE_CAPSULE)
                return sdCapsule_Precalc(p, i.precalc1);
#elif defined(SHAPE_LINE)
                return sdLine(p, smoothing, params);
#elif defined(SHAPE_ARC)
                return sdArc_Precalc(p, params.y, i.precalc1, i.precalc2, i.extraData.xy);
#elif defined(SHAPE_PATH)
                return sdPath(p, params, false);
#elif defined(SHAPE_TRIANGLE)
                return sdTriangle(p, halfSize);
#elif defined(SHAPE_HEART)
                return sdHeart(p, halfSize);
#else
                return 100000.0;
#endif
            }

            float GetMainPerimeterMapping(float2 p, float2 halfSize)
            {
#if defined(SHAPE_RECTANGLE)
                float w = halfSize.x, h = halfSize.y;
                float2 absP = abs(p);
                if (absP.x * h > absP.y * w)
                    return (p.x > 0) ? 2.0*w + (h - p.y) : 4.0*w + 2.0*h + (p.y + h);
                else
                    return (p.y > 0) ? p.x + w : 2.0*w + 2.0*h + (w - p.x);
#elif defined(SHAPE_LINE)
                return p.x;
#else
                return (atan2(p.y, p.x) + 3.14159265) * (halfSize.x + halfSize.y) * 0.5;
#endif
            }

            // ── Fragment Shader ───────────────────────────────────────────────

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p      = i.uv0.xy;
                float2 p_orig = i.uv0.zw;
                float2 halfSize      = i.baseData.xy * 0.5;
                float  customSmooth  = i.baseData.z;
                float  effectType    = i.baseData.w;

                float  blur           = i.effectData.x;
                float  aa             = i.effectData.y;
                float  internalPadding = i.effectData.z;
                float  spread         = i.effectData.w;

                // Edge noise
                float2 noiseOff = float2(0, 0);
#if defined(HAS_NOISE)
                float noiseAmount = frac(i.fillParams.z) * 100.0;
                float noiseScale  = i.fillParams.w * 0.1;
                if (noiseAmount > 0.001)
                {
                    float nv = vk_noise(p_orig * noiseScale);
                    noiseOff = (nv * 2.0 - 1.0) * noiseAmount;
                }
#endif

                // Compute SDFs
                bool needShadowD = (effectType == 1.0 || effectType == 3.0);
                bool needOrigD   = (effectType != 1.0);

                float d_orig = 0.0, d = 0.0;

                if (needOrigD) d_orig = GetMainSDF_Opt(p_orig + noiseOff, halfSize, customSmooth, i.shapeParams, i);
                if (needShadowD) d    = GetMainSDF_Opt(p      + noiseOff, halfSize, customSmooth, i.shapeParams, i);

                // Apply boolean operations
#if defined(HAS_BOOLEANS)
                int boolCount = _BoolParams1;
                for (int k = 0; k < 16; k++)
                {
                    if (k >= boolCount) break;
                    float boolOp    = _BoolData_OpType[k].x;
                    float boolType  = _BoolData_OpType[k].y;
                    float boolSmooth = _BoolData_OpType[k].z;
                    float smoothBlend = _BoolData_OpType[k].w;
                    float4 boolTrans = _BoolData_Transform[k];
                    float2 boolSize  = _BoolData_Size[k].xy;
                    float4 boolParams = _BoolData_ShapeParams[k];
                    bool isPathOp = (boolType > 5.5 && boolType < 6.5);

                    if (needOrigD)
                    {
                        float2 bp = p_orig - boolTrans.xy;
                        if (abs(boolTrans.z) > 0.0001 || abs(boolTrans.w - 1.0) > 0.0001)
                            bp = float2(bp.x * boolTrans.w - bp.y * boolTrans.z, bp.x * boolTrans.z + bp.y * boolTrans.w);
                        float d2 = GetBasicSDF(bp + noiseOff, boolSize * 0.5, boolType, boolSmooth, boolParams, isPathOp);
                        if (smoothBlend > 0.001) d_orig = smin_op(d_orig, d2, boolOp, smoothBlend);
                        else                     d_orig = hard_op(d_orig, d2, boolOp);
                    }
                    if (needShadowD)
                    {
                        float2 bp = p - boolTrans.xy;
                        if (abs(boolTrans.z) > 0.0001 || abs(boolTrans.w - 1.0) > 0.0001)
                            bp = float2(bp.x * boolTrans.w - bp.y * boolTrans.z, bp.x * boolTrans.z + bp.y * boolTrans.w);
                        float d2 = GetBasicSDF(bp + noiseOff, boolSize * 0.5, boolType, boolSmooth, boolParams, isPathOp);
                        if (smoothBlend > 0.001) d = smin_op(d, d2, boolOp, smoothBlend);
                        else                     d = hard_op(d, d2, boolOp);
                    }
                }
#endif

                if (needOrigD)   d_orig += internalPadding;
                if (needShadowD) d      += internalPadding;

                // Stroke dash test
                if (effectType > 1.5 && effectType < 2.5)
                {
                    float dashSize = i.precalc2.x;
                    float dashGap  = i.precalc2.y;
                    if (dashSize > 0.001 && (dashSize + dashGap) > 0.001)
                    {
                        float perim = GetMainPerimeterMapping(p_orig + noiseOff, halfSize);
                        float cycle = dashSize + dashGap;
                        if (frac(perim / cycle) > (dashSize / cycle)) discard;
                    }
                }

                // Compute layer mask
                float alignment = i.extraData.w;
                float mask = ComputeLayerMask(effectType, d, d_orig, blur, aa, spread, alignment, i.precalc2.x, i.precalc2.y);

                if (mask <= 0.001) discard;

                // Evaluate fill color
                float atlasRow  = floor(i.fillParams.x + 0.5);
                float fillKind  = i.fillParams.y;
                float gradAngle = i.fillParams.z;
                float gradScale = length(float2(i.fillParams.w, 0)); // w = scale (reused, see below)

                // gradScale and gradOffset are encoded differently per fill:
                // fillParams.w = gradScale
                // extraData.zw = gradOffset
                gradScale      = i.fillParams.w;
                float2 gradOffset = float2(i.extraData.z, i.extraData.w);

                float4 colorSample;
                if (fillKind > 3.5) // Image
                {
                    float2 patUV = (p_orig / halfSize * 0.5 + 0.5) * gradOffset + gradOffset; // tiling/offset
                    colorSample = tex2D(_PatternTex, patUV);
                }
                else
                {
                    float t = 0.5;
                    if (fillKind > 0.5)
                    {
                        float2 gp = p_orig - (halfSize * gradOffset);
                        gp /= max(gradScale, 0.001);
                        if (fillKind < 1.5) // Linear
                        {
                            float rad  = gradAngle * 0.0174533;
                            float2 dir = float2(cos(rad), sin(rad));
                            t = (dot(gp, dir) / max(abs(dir.x*halfSize.x)+abs(dir.y*halfSize.y), 0.001)) * 0.5 + 0.5;
                        }
                        else if (fillKind < 2.5) // Radial
                        {
                            t = length(gp) / max(max(halfSize.x, halfSize.y), 0.001);
                        }
                        else if (fillKind < 3.5) // Conic
                        {
                            t = frac((atan2(gp.y, gp.x) - gradAngle * 0.0174533) / 6.28318 + 0.5);
                        }
                    }
                    float vCoord = (atlasRow * 3.0 + 1.5) * _AtlasHeightInv;
                    colorSample  = tex2D(_MainTex, float2(saturate(t), vCoord));
                }

                float4 finalColor = colorSample * i.color;

                // Bevel effect override
                if (effectType > 4.5 && effectType < 5.5)
                {
                    float bevelDist = blur; // blur channel reused for bevel distance
                    float2 bDir = float2(cos(i.extraData.z), sin(i.extraData.z));
                    float diff = GetMainSDF_Opt(p_orig + noiseOff + bDir * bevelDist, halfSize, customSmooth, i.shapeParams, i)
                               - GetMainSDF_Opt(p_orig + noiseOff - bDir * bevelDist, halfSize, customSmooth, i.shapeParams, i);
                    float highlight = saturate( diff / (bevelDist * 2.0)) * spread;
                    float shadow    = saturate(-diff / (bevelDist * 2.0)) * i.extraData.w;
                    float baseMask  = smoothstep(aa, -aa, d_orig);
                    if (baseMask <= 0.001) discard;
                    finalColor = (shadow > highlight) ? float4(0,0,0,shadow) : float4(1,1,1,highlight);
                    mask = baseMask;
                }

                finalColor.a   *= mask;
                finalColor.rgb *= finalColor.a;  // premultiply

                // Soft mask
#if defined(HAS_MASK)
                if (_MaskParams.x > 0.5)
                {
                    float4x4 localToMask = float4x4(_MaskMatrixX, _MaskMatrixY, _MaskMatrixZ, _MaskMatrixW);
                    float2 maskP = mul(localToMask, float4(p_orig, 0.0, 1.0)).xy;

                    float maskType   = _MaskParams.y;
                    float maskSmooth = _MaskParams.z;
                    float maskFeather = _MaskParams.w;

                    float mD = GetBasicSDF(maskP, _MaskSize.xy * 0.5, maskType, maskSmooth, _MaskShape, false);

                    int mbc = _MaskBoolParams;
                    for (int mk = 0; mk < 16; mk++)
                    {
                        if (mk >= mbc) break;
                        float4 mbTrans  = _MaskBoolTransform[mk];
                        float2 mp2 = maskP - mbTrans.xy;
                        float  mbs = _MaskBoolOpType[mk].z;
                        float  mbsb = _MaskBoolOpType[mk].w;
                        if (abs(mbTrans.z) > 0.0001)
                        {
                            float sc = cos(-mbTrans.z), ss = sin(-mbTrans.z);
                            mp2 = float2(mp2.x * sc - mp2.y * ss, mp2.x * ss + mp2.y * sc);
                        }
                        float md2 = GetBasicSDF(mp2, _MaskBoolSize[mk].xy * 0.5, _MaskBoolOpType[mk].y, mbs, _MaskBoolShapeParams[mk], false);
                        if (mbsb > 0.001) mD = smin_op(mD, md2, _MaskBoolOpType[mk].x, mbsb);
                        else              mD = hard_op(mD, md2, _MaskBoolOpType[mk].x);
                    }

                    float mAlpha = smoothstep(max(0.001, maskFeather), -max(0.001, maskFeather), mD);
                    float mFillType = _MaskFillParams.x;
                    float mRow      = _MaskFillParams.w;
                    float mt        = 0.5;
                    float2 mHS      = _MaskSize.xy * 0.5;
                    if (mFillType > 0.5)
                    {
                        float2 mgp = (maskP - (mHS * _MaskFillOffset.xy)) / max(_MaskFillParams.z, 0.001);
                        if (mFillType < 1.5) { float rad=_MaskFillParams.y*0.0174533; float2 dir=float2(cos(rad),sin(rad)); mt=(dot(mgp,dir)/max(abs(dir.x*mHS.x)+abs(dir.y*mHS.y),0.001))*0.5+0.5; }
                        else if (mFillType < 2.5) mt = length(mgp)/max(max(mHS.x,mHS.y),0.001);
                        else if (mFillType < 3.5) mt = frac((atan2(mgp.y,mgp.x)-_MaskFillParams.y*0.0174533)/6.28318+0.5);
                    }
                    float mVCoord   = (mRow * 3.0 + 1.5) * _AtlasHeightInv;
                    float mFillAlpha = (mFillType > 0.5) ? tex2D(_MaskTex, float2(saturate(mt), mVCoord)).a
                                                         : tex2D(_MaskTex, float2(0.5, mVCoord)).a;
                    float mTotal   = mAlpha * mFillAlpha * _MaskFillOffset.z;
                    float oldA     = finalColor.a;
                    finalColor.a   = min(oldA, mTotal);
                    finalColor.rgb *= (finalColor.a / max(oldA, 0.0001));
                }
#endif

                if (finalColor.a <= 0.001) discard;

#ifdef UNITY_UI_CLIP_RECT
                finalColor *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
#endif
#ifdef UNITY_UI_ALPHACLIP
                clip(finalColor.a - 0.001);
#endif

                return finalColor;
            }
            ENDCG
        }
    }
}
