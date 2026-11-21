Shader "OZGL/Environment/Blue Portal"
{
    Properties
    {
        [Header(Portal Colors)]
        [HDR] _RimColor ("Rim Color", Color) = (0.12, 0.72, 1.5, 1)
        [HDR] _CoreColor ("Bright Filament Color", Color) = (0.6, 1.3, 1.8, 1)
        _DeepColor ("Interior Color", Color) = (0.008, 0.025, 0.09, 1)
        [Header(Shape And Glow)]
        _Radius ("Radius", Range(0.4, 0.8)) = 0.7
        _RimWidth ("Rim Width", Range(0.008, 0.08)) = 0.028
        _Glow ("Glow Strength", Range(0, 2)) = 0.75
        _InteriorOpacity ("Interior Opacity", Range(0, 1)) = 0.92
        [Header(Motion)]
        _FlowSpeed ("Flow Speed", Range(0, 3)) = 0.65
        _Distortion ("Edge Distortion", Range(0, 0.05)) = 0.018
        _SwirlStrength ("Interior Swirl Strength", Range(0, 1)) = 0.4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "PortalUnlit"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RimColor, _CoreColor, _DeepColor;
                float _Radius, _RimWidth, _Glow, _InteriorOpacity;
                float _FlowSpeed, _Distortion, _SwirlStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 blend = frac(p);
                blend = blend * blend * (3.0 - 2.0 * blend);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), blend.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + 1.0), blend.x), blend.y);
            }

            float2 Rotate(float2 p, float angle)
            {
                float s, c;
                sincos(angle, s, c);
                return float2(c * p.x - s * p.y, s * p.x + c * p.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float radius = length(p);
                float time = _Time.y * _FlowSpeed;
                float2 flow = Rotate(p, -time);
                float noise = Noise(flow * 7.0 + time * 0.23);
                float detail = Noise(Rotate(p, time * 0.7) * 19.0 - time * 0.3);
                float edge = radius - _Radius + (noise - 0.5) * _Distortion * 2.0;
                float width = max(_RimWidth, 0.003);
                float aa = max(fwidth(radius), 0.001);

                float rim = exp2(-abs(edge) / width * 2.8);
                float filamentEdge = edge + (detail - 0.5) * width * 1.4;
                float filament = 1.0 - smoothstep(width * 0.1, width * 0.32 + aa, abs(filamentEdge));
                float halo = exp2(-abs(edge) * 27.0) * _Glow;
                float outerWisp = exp2(-abs(edge - width * 2.3) * 65.0)
                    * smoothstep(0.45, 0.8, noise) * 0.65;

                // Cartesian rotation keeps the flowing pattern continuous around the entire rim.
                float2 spiral = Rotate(p, time * 0.55 - radius * 7.0);
                float cloud = Noise(spiral * 6.0 + float2(time * 0.12, 0.0));
                float streak = pow(saturate(1.0 - abs(cloud - 0.5) * 5.0), 3.0);
                float inside = 1.0 - smoothstep(-aa, aa, edge);
                float radialLight = smoothstep(0.03, _Radius, radius);
                float swirl = streak * radialLight * _SwirlStrength * inside;
                float pulse = 0.88 + 0.12 * sin(time * 2.2 + noise * 4.0);

                float3 interior = _DeepColor.rgb + _RimColor.rgb
                    * (swirl * 0.36 + cloud * radialLight * 0.055);
                float alpha = saturate(inside * _InteriorOpacity + rim * 0.7);
                float3 emission = _RimColor.rgb * (rim * pulse + halo * 0.48 + outerWisp)
                    + _CoreColor.rgb * filament * (0.6 + detail * 0.4);
                // Fade before the edge of the quad, including at maximum radius/distortion.
                float canvasFade = 1.0 - smoothstep(0.90, 0.99, radius);
                return half4((interior * alpha + emission) * canvasFade, alpha * canvasFade);
            }
            ENDHLSL
        }
    }
}
