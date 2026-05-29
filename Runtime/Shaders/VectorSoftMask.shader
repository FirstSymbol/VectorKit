Shader "VectorKit/SoftMaskedImage"
{
    // Applies a VectorShape soft mask to a standard Unity UI Image.
    // Attach VectorSoftMaskable component to any child Graphic.

    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp      ("Stencil Comparison", Float) = 8
        _Stencil          ("Stencil ID", Float) = 0
        _StencilOp        ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask  ("Stencil Read Mask", Float) = 255
        _ColorMask        ("Color Mask", Float) = 15

        _MaskMatrixX  ("Mask Matrix X", Vector) = (1,0,0,0)
        _MaskMatrixY  ("Mask Matrix Y", Vector) = (0,1,0,0)
        _MaskMatrixZ  ("Mask Matrix Z", Vector) = (0,0,1,0)
        _MaskMatrixW  ("Mask Matrix W", Vector) = (0,0,0,1)
        _MaskParams   ("Mask Params",   Vector) = (0,0,0,0)
        _MaskSize     ("Mask Size",     Vector) = (0,0,0,0)
        _MaskShape    ("Mask Shape",    Vector) = (0,0,0,0)
        _MaskTex      ("Mask Gradient", 2D)     = "white" {}
        _MaskFillParams ("Mask Fill Params", Vector) = (0,0,0,0)
        _MaskFillOffset ("Mask Fill Offset", Vector) = (0,0,0,0)
        _MaskBoolParams ("Mask Bool Count", Int) = 0
        _AtlasHeightInv ("Atlas Height Inv", Float) = 0.00390625
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "Include/SDFLibrary.hlsl"

            sampler2D _MainTex;
            float4    _ClipRect;
            float     _AtlasHeightInv;

            float4    _MaskMatrixX, _MaskMatrixY, _MaskMatrixZ, _MaskMatrixW;
            float4    _MaskParams, _MaskSize, _MaskShape;
            sampler2D _MaskTex;
            float4    _MaskFillParams, _MaskFillOffset;
            int       _MaskBoolParams;
            float4 _MaskBoolOpType    [16];
            float4 _MaskBoolShapeParams[16];
            float4 _MaskBoolTransform [16];
            float4 _MaskBoolSize      [16];

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f     { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 worldPos : TEXCOORD1; };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex   = UnityObjectToClipPos(v.vertex);
                o.color    = v.color;
                o.uv       = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;

                if (_MaskParams.x > 0.5)
                {
                    float4x4 lm = float4x4(_MaskMatrixX, _MaskMatrixY, _MaskMatrixZ, _MaskMatrixW);
                    float2 mp = mul(lm, i.worldPos).xy;
                    float mD = GetBasicSDF(mp, _MaskSize.xy * 0.5, _MaskParams.y, _MaskParams.z, _MaskShape, false);

                    int mbc = _MaskBoolParams;
                    for (int mk = 0; mk < 16; mk++)
                    {
                        if (mk >= mbc) break;
                        float4 mbt = _MaskBoolTransform[mk];
                        float2 mp2 = mp - mbt.xy;
                        if (abs(mbt.z) > 0.0001) { float sc=cos(-mbt.z),ss=sin(-mbt.z); mp2=float2(mp2.x*sc-mp2.y*ss, mp2.x*ss+mp2.y*sc); }
                        float md2 = GetBasicSDF(mp2, _MaskBoolSize[mk].xy * 0.5, _MaskBoolOpType[mk].y, _MaskBoolOpType[mk].z, _MaskBoolShapeParams[mk], false);
                        if (_MaskBoolOpType[mk].w > 0.001) mD = smin_op(mD, md2, _MaskBoolOpType[mk].x, _MaskBoolOpType[mk].w);
                        else                               mD = hard_op(mD, md2, _MaskBoolOpType[mk].x);
                    }

                    float feather = _MaskParams.w;
                    float mAlpha  = smoothstep(max(0.001, feather), -max(0.001, feather), mD);
                    float mRow    = _MaskFillParams.w;
                    float mVCoord = (mRow * 3.0 + 1.5) * _AtlasHeightInv;
                    float mFillA  = tex2D(_MaskTex, float2(0.5, mVCoord)).a;
                    c.a  *= mAlpha * mFillA * _MaskFillOffset.z;
                    c.rgb = c.rgb * c.a;
                }
                else
                {
                    c.rgb *= c.a;
                }

                if (c.a <= 0.001) discard;
#ifdef UNITY_UI_CLIP_RECT
                c *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);
#endif
                return c;
            }
            ENDCG
        }
    }
}
