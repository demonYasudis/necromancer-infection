Shader "NecromancerInfection/ProtectionArea"
{
    Properties { _Color ("Tint", Color) = (0.65, 0.32, 1, 0.85) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1, -1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct v2f { float4 position : SV_POSITION; fixed4 color : COLOR; };
            fixed4 _Color;
            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                return output;
            }
            fixed4 frag(v2f input) : SV_Target { return input.color; }
            ENDCG
        }
    }
}
