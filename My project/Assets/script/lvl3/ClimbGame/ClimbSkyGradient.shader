// Sky of the climbing mini-game: a vertical gradient (zenith → horizon → ground).
// The colors are driven by ClimbCamera from the weather and the altitude.
Shader "Skybox/ClimbGradient"
{
    Properties
    {
        _TopColor ("Zenith", Color) = (0.2, 0.42, 0.8, 1)
        _HorizonColor ("Horizon", Color) = (0.55, 0.78, 0.98, 1)
        _BottomColor ("Below horizon", Color) = (0.47, 0.66, 0.83, 1)
        _Exponent ("Gradient spread", Range(0.1, 4)) = 0.6
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor, _HorizonColor, _BottomColor;
            half _Exponent;

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float y = normalize(i.dir).y;
                fixed4 up = lerp(_HorizonColor, _TopColor, pow(saturate(y), _Exponent));
                fixed4 down = lerp(_HorizonColor, _BottomColor, saturate(-y * 4));
                return y >= 0 ? up : down;
            }
            ENDCG
        }
    }
    Fallback Off
}
