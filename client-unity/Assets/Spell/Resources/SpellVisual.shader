Shader "Spell/Visual"
{
    Properties { _Tint ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Tint;
            struct Vertex { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct Vary { float4 pos : SV_POSITION; half shade : TEXCOORD0; };
            Vary vert(Vertex v)
            {
                Vary o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.shade = .70 + .30 * dot(UnityObjectToWorldNormal(v.normal), normalize(float3(.3,.8,-.4)));
                return o;
            }
            fixed4 frag(Vary v) : SV_Target { return fixed4(_Tint.rgb * v.shade, _Tint.a); }
            ENDCG
        }
    }
}
