// Analytic animation, no bitmap or AI-generated imagery. Native particles supply
// position/lifetime; the shader supplies a tapered, moving flame silhouette.
Shader "CastleOfIdeas/AnalyticFlame"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float y = i.uv.y;
                float sway = sin(y * 13 - _Time.y * 6) * 0.07 * y;
                float x = abs(i.uv.x - 0.5 + sway);
                float edge = (1 - y) * 0.42;
                float alpha = (1 - smoothstep(edge * 0.35, edge + 0.02, x)) * sin(y * 3.14159);
                alpha *= 0.75 + 0.25 * sin(_Time.y * 11 + y * 16);
                float3 color = lerp(float3(1, 0.82, 0.18), float3(1, 0.07, 0.005), y);
                return fixed4(color * 1.8 * i.color.rgb, alpha * i.color.a);
            }
            ENDCG
        }
    }
}
