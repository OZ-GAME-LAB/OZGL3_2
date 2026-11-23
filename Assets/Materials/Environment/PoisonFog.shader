Shader "OZGL/Environment/Poison Fog"
{
    Properties
    {
        [MainColor] _FogColor ("Fog Color", Color) = (0.46, 0.38, 0.52, 1)
        _Density ("Density", Range(0,1)) = 0.75
        [HideInInspector] _FogMotion ("Speed / Swirl / Scale", Vector) = (0.3,0.4,1,0)
        [HideInInspector] _FogTime ("Local Time", Float) = 0
        [HideInInspector] _Layer ("Base / Wisps", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "PoisonFog"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float4 _FogMotion;
                float _Density;
                float _FogTime;
                float _Layer;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                // UV.xy + Custom1.xy: 입자 생성 시 정한 두 난수로 무늬가 겹쳐 맞물리지 않게 합니다.
                float4 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }
            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), f.x),
                    lerp(Hash(cell + float2(0,1)), Hash(cell + float2(1,1)), f.x), f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv.xy * 2.0 - 1.0;
                float2 phase = input.uv.zw;
                float t = _FogTime * _FogMotion.x * (0.35 + frac(phase.x) * 0.3);
                float bend = (Noise(float2(uv.x * 1.8 - t, phase.y)) - 0.5) * _FogMotion.y;
                float2 envelopeUV = float2(uv.x, uv.y + bend * 0.6);
                float envelope = pow(saturate(1.0 - dot(envelopeUV, envelopeUV)), 2.3);
                // 입자 사각형의 끝에서 반드시 0이 되도록 별도 감쇠합니다.
                float2 cardFade = smoothstep(0.0, 0.22, 1.0 - abs(uv));
                float2 p = float2(uv.x * 2.0, (uv.y + bend) * lerp(2.3, 3.0, _Layer));
                p *= _FogMotion.z;
                p += phase + float2(-t, t * 0.22);
                float distortion = Noise(p * 0.55 + float2(t * 0.2, 5.7)) - 0.5;
                p.y += distortion * _FogMotion.y * 2.0;
                float cloud = Noise(p) * 0.57 + Noise(p * 2.07 + 17.2) * 0.29
                    + Noise(p * 4.13 - t * 0.4) * 0.14;
                float textureFade = smoothstep(lerp(0.12, 0.28, _Layer), 0.8, cloud);
                half alpha = envelope * cardFade.x * cardFade.y * textureFade
                    * lerp(0.28, 0.21, _Layer) * _Density * _FogColor.a * input.color.a;
                half3 color = _FogColor.rgb * lerp(0.9, 1.1, cloud) * input.color.rgb;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
