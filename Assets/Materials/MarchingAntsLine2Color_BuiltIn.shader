Shader "Custom/BuiltIn/MarchingAntsLine2Color"
{
    Properties
    {
        _DashLength ("Dash Length (UV)", Range(0.05, 2)) = 0.25
        _Offset ("Offset", Range(0,1)) = 0
        _ColorA ("Color A", Color) = (1,1,1,1)
        _ColorB ("Color B", Color) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _DashLength;
            float _Offset;
            float4 _ColorA;
            float4 _ColorB;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos  : SV_POSITION;
                float2 uv   : TEXCOORD0;
                float4 col  : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.col = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float dashLen = max(_DashLength, 1e-4);
                float phase = (i.uv.x / dashLen) + _Offset;
                float t = frac(phase);

                fixed4 c = (t < 0.5) ? _ColorA : _ColorB;
                c *= i.col;
                return c;
            }
            ENDCG
        }
    }
}
