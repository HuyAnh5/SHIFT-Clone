Shader "Custom/URP/Unlit/MarchingAntsLine2Color"
{
    Properties
    {
        _DashLength ("Dash Length (UV)", Range(0.05, 2)) = 0.25
        _Offset ("Offset", Range(0,1)) = 0
        _ColorA ("Color A", Color) = (1,1,1,1)
        _ColorB ("Color B", Color) = (0,0,0,0) // alpha 0 = “0” (trong suốt)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalRenderPipeline" }
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "SRPUnlit"
            Tags { "LightMode"="SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;      // LineRenderer vertex color
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 vcol        : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float _DashLength;
                float _Offset;
                float4 _ColorA;
                float4 _ColorB;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.vcol = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float dashLen = max(_DashLength, 1e-4);
                float phase = (IN.uv.x / dashLen) + _Offset;
                float t = frac(phase);

                half4 c = (t < 0.5) ? (half4)_ColorA : (half4)_ColorB;
                c *= IN.vcol; // giữ alpha theo LineRenderer nếu bạn cần
                return c;
            }
            ENDHLSL
        }
    }
}
