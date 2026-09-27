Shader "TurnLimbo/Legacy/Sparks"
{
    Properties
    {
        _MainTex ("Original ray texture", 2D) = "white" {}
        _Color ("HDR ray color", Color) = (29.857058,29.857058,29.857058,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            struct input { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct output { float4 vertex : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            output vert(input v)
            {
                output o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            float4 frag(output i) : SV_Target
            {
                float ray = tex2D(_MainTex, i.uv).r;
                return float4(i.color.rgb * _Color.rgb, ray * i.color.a);
            }
            ENDCG
        }
    }
}
