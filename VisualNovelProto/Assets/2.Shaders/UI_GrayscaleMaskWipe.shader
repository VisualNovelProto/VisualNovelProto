Shader "UI/GrayscaleMaskWipe"
{
    Properties
    {
        _Color("Color", Color) = (0,0,0,1)        // 덮을 색(대개 블랙)
        _MaskTex("Mask (R channel)", 2D) = "white" {}
        _Cutoff("Cutoff 0→1", Range(0,1)) = 0
        _Invert("Invert", Float) = 0              // 0=그림대로, 1=반전
        _Softness("Edge Softness", Range(0,0.2)) = 0.02
    }
        SubShader
        {
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "CanUseSpriteAtlas" = "True" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off ZWrite Off ZTest Always

            Pass
            {
                HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"

                sampler2D _MaskTex;
                float4 _MaskTex_ST;
                float4 _Color;
                float _Cutoff, _Invert, _Softness;

                struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
                v2f vert(appdata_full v) {
                    v2f o; o.pos = UnityObjectToClipPos(v.vertex);
                    o.uv = TRANSFORM_TEX(v.texcoord, _MaskTex);
                    return o;
                }
                fixed4 frag(v2f i) :SV_Target{
                    float m = tex2D(_MaskTex, i.uv).r;
                    if (_Invert > 0.5) m = 1.0 - m;
                    // m(밝기)와 컷오프 비교 → 부드러운 경계
                    float a = smoothstep(_Cutoff - _Softness, _Cutoff + _Softness, 1.0 - m);
                    // a가 1일수록 화면을 가림(검정 오버레이)
                    return float4(_Color.rgb, a * _Color.a);
                }
                ENDHLSL
            }
        }
}
