// Original analytic mist: no sampled images or third-party effects.
Shader "CastleOfIdeas/PortalMist"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v) {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o;
            }
            fixed4 frag(v2f i):SV_Target {
                float2 p=i.uv*2-1;
                float r=length(p);
                float wave=sin(p.x*10+sin(p.y*7+_Time.y*2)*2-_Time.y*3);
                float alpha=saturate(1-r)*saturate(1-r)*(0.45+0.3*wave);
                return fixed4(0.52+wave*0.1,0.08,1.1,alpha*i.color.a);
            }
            ENDCG
        }
    }
}
