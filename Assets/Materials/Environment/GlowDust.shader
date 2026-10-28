Shader "WanderSoul/Environment/GlowDust"
{
    Properties
    {
        [MainTexture] _BaseMap("Dust Texture", 2D) = "white" {}
        [MainColor][HDR] _BaseColor("Glow Color", Color) = (1,1,1,1)
        [HideInInspector] _DustBounds("Viewport Bounds", Vector) = (0,0.1,1,0.84)
        [HideInInspector] _DustFade("Edge Fade", Vector) = (0.03,0.06,0,0)
        [HideInInspector] _DustCameraPosition("Camera Position", Vector) = (0,0,0,0)
        [HideInInspector] _DustCameraRight("Camera Right", Vector) = (1,0,0,0)
        [HideInInspector] _DustCameraUp("Camera Up", Vector) = (0,1,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "ForwardUnlit"
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _DustBounds;
                float4 _DustFade;
                float4 _DustCameraPosition;
                float4 _DustCameraRight;
                float4 _DustCameraUp;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Explicit camera basis avoids render-texture Y inversion and follows the actual output camera.
                float3 relative = input.positionWS - _DustCameraPosition.xyz;
                float2 viewport = float2(dot(relative, _DustCameraRight.xyz), dot(relative, _DustCameraUp.xyz)) + 0.5;
                float2 distanceInside = min(viewport - _DustBounds.xy, _DustBounds.zw - viewport);
                float2 fade = smoothstep(0.0, max(_DustFade.xy, float2(0.00001, 0.00001)), distanceInside);
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor * input.color;
                color.a *= fade.x * fade.y;
                return color;
            }
            ENDHLSL
        }
    }
}
