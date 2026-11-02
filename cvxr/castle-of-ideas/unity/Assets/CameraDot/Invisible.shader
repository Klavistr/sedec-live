Shader "CameraDot/Invisible"
{
    Properties { _Color ("Color", Color) = (0,0,0,0) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            // No colour, depth, or shadow contribution, even at close range.
            ColorMask 0
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct Input
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Output
            {
                float4 position : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Output vert(Input v)
            {
                Output o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position=UnityObjectToClipPos(v.vertex);
                return o;
            }
            fixed4 frag(Output i) : SV_Target { return fixed4(0,0,0,0); }
            ENDCG
        }
    }
    Fallback Off
}
