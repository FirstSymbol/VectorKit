Shader "VectorKit/ShapeWorld"
{
    // World-space variant of VectorShape - no UI stencil, standard depth testing.
    // Used by VectorShapeWorld component (MeshRenderer-based).

    Properties
    {
        [HideInInspector] _MainTex ("Gradient Atlas", 2D) = "white" {}
        _PatternTex ("Pattern Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _AtlasHeightInv ("Atlas Height Inv", Float) = 0.00390625
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off Lighting Off ZWrite Off ZTest LEqual
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.5

            #pragma multi_compile_local SHAPE_RECTANGLE SHAPE_ELLIPSE SHAPE_POLYGON SHAPE_STAR SHAPE_CAPSULE SHAPE_LINE SHAPE_ARC SHAPE_PATH SHAPE_TRIANGLE SHAPE_HEART _
            #pragma multi_compile_local _ HAS_BOOLEANS
            #pragma multi_compile_local _ HAS_NOISE

            #include "UnityCG.cginc"
            #include "Include/SDFLibrary.hlsl"
            #include "Include/FillLibrary.hlsl"
            #include "Include/EffectsLibrary.hlsl"

            sampler2D _MainTex;
            sampler2D _PatternTex;
            float     _AtlasHeightInv;

            int    _BoolParams1;
            float4 _BoolData_OpType    [16];
            float4 _BoolData_ShapeParams[16];
            float4 _BoolData_Transform [16];
            float4 _BoolData_Size      [16];

            struct appdata_vk
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float3 normal   : NORMAL;
                float4 tangent  : TANGENT;
                float4 texcoord0: TEXCOORD0;
                float4 texcoord1: TEXCOORD1;
                float4 texcoord2: TEXCOORD2;
                float4 texcoord3: TEXCOORD3;
            };

            struct v2f
            {
                float4 vertex      : SV_POSITION;
                fixed4 color       : COLOR;
                float4 baseData    : TEXCOORD2;
                float4 shapeParams : TEXCOORD1;
                float4 fillParams  : TEXCOORD3;
                float4 uv0         : TEXCOORD0;
                float4 effectData  : TEXCOORD5;
                float4 precalc1    : TEXCOORD6;
                float4 precalc2    : TEXCOORD7;
                float4 extraData   : TANGENT;
            };

            v2f vert(appdata_vk v)
            {
                v2f o;
                o.vertex      = UnityObjectToClipPos(v.vertex);
                o.color       = v.color;
                o.shapeParams = v.texcoord1;
                o.baseData    = v.texcoord2;
                o.fillParams  = v.texcoord3;

                float effectType    = v.texcoord2.w;
                float2 p_orig       = v.texcoord0.xy;
                float2 p            = p_orig;
                bool isShadow       = (effectType == 1.0 || effectType == 3.0);

                float blur = 0, aa = 1, padding = 0, spread = v.tangent.x;
                if (isShadow) { p -= v.normal.xy; blur = v.normal.z; aa = max(v.tangent.y, 0.001); }
                else          { padding = v.normal.x; aa = max(v.normal.y, 0.001); blur = v.normal.z; }

                o.uv0      = float4(p.x, p.y, p_orig.x, p_orig.y);
                o.effectData = float4(blur, aa, padding, spread);
                o.precalc1 = float4(0,0,0,0);
                o.precalc2 = float4(v.texcoord0.z, v.texcoord0.w, 0, 0);
                o.extraData = float4(0, 0, v.tangent.z, v.tangent.w);

                float2 halfSize = v.texcoord2.xy;
                float4 params   = v.texcoord1;

#if defined(SHAPE_POLYGON)
                float n=max(3.0,params.x); float an=3.14159265/n; float maxR=min(halfSize.x,halfSize.y);
                float rounding=params.y*maxR*0.5; float rOuter=maxR-rounding;
                o.precalc1=float4(2.0*an,rOuter*sin(an),rOuter*cos(an),rounding);
#elif defined(SHAPE_STAR)
                float ns=max(3.0,params.x); float maxRs=min(halfSize.x,halfSize.y);
                float ros=params.z*maxRs*0.5; float rOuts=max(maxRs-ros,0.001); float rIns=max(params.y*maxRs-ros,0.001);
                float ans=3.1415926535/ns; float2 p1s=float2(0,rOuts); float2 p2s=float2(rIns*sin(ans),rIns*cos(ans));
                float2 bas=p2s-p1s; float ba2s=max(dot(bas,bas),0.00001);
                o.precalc1=float4(2.0*ans,rOuts,ba2s,ros); o.precalc2.zw=p2s; o.extraData.xy=float2(params.w*maxRs,0);
#elif defined(SHAPE_CAPSULE)
                float rc=params.x*min(halfSize.x,halfSize.y); float2 hc=max(halfSize-rc,0);
                o.precalc1=float4(hc.x,hc.y,rc,0);
#elif defined(SHAPE_ARC)
                float maxRr=min(halfSize.x,halfSize.y); float innerR=params.x*maxRr;
                float thick=(maxRr-innerR)*0.5; float midR=(maxRr+innerR)*0.5;
                float2 p1r=midR*float2(sin(params.y),cos(params.y)); float2 p2r=midR*float2(sin(params.z),cos(params.z));
                float tda=frac((params.z-params.y)/6.28318);
                o.precalc1=float4(midR,thick,tda,0); o.precalc2.zw=p1r; o.extraData.xy=p2r;
#endif
                return o;
            }

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

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv0.xy, p_orig = i.uv0.zw;
                float2 halfSize = i.baseData.xy;
                float  smoothing = i.baseData.z, effectType = i.baseData.w;
                float  blur = i.effectData.x, aa = i.effectData.y, padding = i.effectData.z, spread = i.effectData.w;

                float2 noiseOff = 0;
#if defined(HAS_NOISE)
                float noiseAmt = frac(i.fillParams.z)*100; float noiseScale = i.fillParams.w*0.1;
                if (noiseAmt > 0.001) { float nv = vk_noise(p_orig*noiseScale); noiseOff = (nv*2-1)*noiseAmt; }
#endif

                // Tessellated polygon fill: vertex colour already premultiplied; skip SDF.
                if (effectType > 5.5 && effectType < 6.5)
                {
                    fixed4 tc = i.color;
                    if (tc.a <= 0.001) discard;
                    return tc;
                }

                bool needShadow = (effectType==1||effectType==3), needOrig = (effectType!=1);
                float d_orig=0, d=0;
                if (needOrig)   d_orig = GetMainSDF_Opt(p_orig+noiseOff, halfSize, smoothing, i.shapeParams, i);
                if (needShadow) d      = GetMainSDF_Opt(p+noiseOff,      halfSize, smoothing, i.shapeParams, i);

#if defined(HAS_BOOLEANS)
                int bc = _BoolParams1;
                for (int k=0; k<16; k++) {
                    if (k>=bc) break;
                    float bop=_BoolData_OpType[k].x, bt=_BoolData_OpType[k].y, bsm=_BoolData_OpType[k].z, bsb=_BoolData_OpType[k].w;
                    float4 btr=_BoolData_Transform[k]; float2 bsz=_BoolData_Size[k].xy; float4 bpa=_BoolData_ShapeParams[k];
                    bool isPath = (bt>5.5&&bt<6.5);
                    if (needOrig)   { float2 bp=p_orig-btr.xy; if(abs(btr.z)>0.0001||abs(btr.w-1)>0.0001) bp=float2(bp.x*btr.w-bp.y*btr.z,bp.x*btr.z+bp.y*btr.w); float d2=GetBasicSDF(bp+noiseOff,bsz,bt,bsm,bpa,isPath); if(bsb>0.001) d_orig=smin_op(d_orig,d2,bop,bsb); else d_orig=hard_op(d_orig,d2,bop); }
                    if (needShadow) { float2 bp=p-btr.xy;      if(abs(btr.z)>0.0001||abs(btr.w-1)>0.0001) bp=float2(bp.x*btr.w-bp.y*btr.z,bp.x*btr.z+bp.y*btr.w); float d2=GetBasicSDF(bp+noiseOff,bsz,bt,bsm,bpa,isPath); if(bsb>0.001) d=smin_op(d,d2,bop,bsb);      else d=hard_op(d,d2,bop); }
                }
#endif
                if (needOrig)   d_orig += padding;
                if (needShadow) d      += padding;

                float mask = ComputeLayerMask(effectType, d, d_orig, blur, aa, spread, i.extraData.w, i.precalc2.x, i.precalc2.y);
                if (mask <= 0.001) discard;

                float atlasRow = floor(i.fillParams.x+0.5), fillKind=i.fillParams.y, gradAngle=i.fillParams.z, gradScale=i.fillParams.w;
                float2 gradOffset = i.extraData.zw;
                float4 colorSample;
                if (fillKind > 3.5) {
                    float2 uv = (p_orig/halfSize*0.5+0.5)*gradOffset+gradOffset;
                    colorSample = tex2D(_PatternTex, uv);
                } else {
                    float t=0.5;
                    if (fillKind>0.5) {
                        float2 gp=(p_orig-(halfSize*gradOffset))/max(gradScale,0.001);
                        if (fillKind<1.5) { float rad=gradAngle*0.0174533; float2 dir=float2(cos(rad),sin(rad)); t=(dot(gp,dir)/max(abs(dir.x*halfSize.x)+abs(dir.y*halfSize.y),0.001))*0.5+0.5; }
                        else if (fillKind<2.5) t=length(gp)/max(max(halfSize.x,halfSize.y),0.001);
                        else if (fillKind<3.5) t=frac((atan2(gp.y,gp.x)-gradAngle*0.0174533)/6.28318+0.5);
                    }
                    float vCoord=(atlasRow*3+1.5)*_AtlasHeightInv;
                    colorSample=tex2D(_MainTex,float2(saturate(t),vCoord));
                }

                float4 fc = colorSample * i.color;
                if (effectType>4.5&&effectType<5.5) {
                    float bDist=blur; float2 bDir=float2(cos(i.extraData.z),sin(i.extraData.z));
                    float diff=GetMainSDF_Opt(p_orig+noiseOff+bDir*bDist,halfSize,smoothing,i.shapeParams,i)-GetMainSDF_Opt(p_orig+noiseOff-bDir*bDist,halfSize,smoothing,i.shapeParams,i);
                    float hi=saturate(diff/(bDist*2))*spread, sh=saturate(-diff/(bDist*2))*i.extraData.w;
                    float bm=smoothstep(aa,-aa,d_orig); if(bm<=0.001) discard;
                    fc=(sh>hi)?float4(0,0,0,sh):float4(1,1,1,hi); mask=bm;
                }

                fc.a *= mask;
                fc.rgb *= fc.a;
                if (fc.a <= 0.001) discard;
                return fc;
            }
            ENDCG
        }
    }
}
