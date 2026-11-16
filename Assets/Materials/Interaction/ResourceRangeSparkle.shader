Shader "WanderSoul/Interaction/RangeSparkle"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "RangeSparkle"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = abs(input.uv * 2.0 - 1.0);
                float diamond = 1.0 - smoothstep(0.34, 0.66, p.x + p.y);
                float cross = max(1.0 - max(p.x * 4.5, p.y), 1.0 - max(p.x, p.y * 4.5));
                float sparkle = max(diamond, saturate(cross) * 0.85);
                half4 color = input.color;
                color.a *= sparkle;
                clip(color.a - 0.003);
                return color;
            }
            ENDHLSL
        }
    }
}
