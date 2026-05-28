Shader "VectorKit/Blend"
{
    // Composites a source RenderTexture onto the destination using a blend mode.
    // Used by VectorGroup for isolation-group blend modes.

    Properties
    {
        _SrcTex   ("Source RT", 2D) = "clear" {}
        _BlendMode("Blend Mode", Int) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off Lighting Off ZWrite Off ZTest Always
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert_full_quad
            #pragma fragment frag
            #pragma target   3.0

            #include "UnityCG.cginc"
            #include "Include/BlendLibrary.hlsl"

            sampler2D _SrcTex;
            int       _BlendMode;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert_full_quad(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 src = tex2D(_SrcTex, i.uv);
                if (src.a <= 0.001) discard;
                // Blend against transparent black backdrop (the RT will be composited
                // over whatever was rendered below by the nested Canvas)
                float4 blended = ApplyBlendMode(float4(0,0,0,0), src, _BlendMode);
                return blended;
            }
            ENDCG
        }
    }
}
