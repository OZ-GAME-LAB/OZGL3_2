Shader "OZGL/Spring Water Test"
{
    Properties
    {
        [Header(Source Images)]
        [PerRendererData] _MainTex ("Water Mask (Sprite)", 2D) = "white" {}
        _TerrainTex ("Matching Dry Terrain", 2D) = "black" {}
        [Header(Pond Interior)]
        _InteriorColor ("Pond Interior Color", Color) = (0.045, 0.13, 0.14, 1)
        _BackgroundBlock ("Background Blocking", Range(0,1)) = 1
        [Header(Water Color)]
        _ShallowColor ("Shallow Color", Color) = (0.20, 0.53, 0.49, 1)
        _DeepColor ("Deep Color", Color) = (0.035, 0.16, 0.21, 1)
        _Opacity ("Water Tint Opacity", Range(0,1)) = 0.28
        _DeepOpacity ("Deep Tint Opacity", Range(0,1)) = 0.60
        _DepthRange ("Depth Range (Image UV)", Range(0.01,1)) = 0.48
        [Header(Surface)]
        _SurfaceY ("Surface Height (Image UV)", Range(0,1)) = 0.600
        _WaveAmplitude ("Wave Height (Image UV)", Range(0,0.02)) = 0.0026
        _WaveSpeed ("Wave Speed", Range(0,2)) = 0.35
        _WaveFrequency ("Wave Frequency", Range(1,60)) = 18
        _SurfaceWidth ("Surface Line Width", Range(0.0001,0.02)) = 0.002
        _SurfaceColor ("Surface Color", Color) = (0.53,0.78,0.71,1)
        _SurfaceStrength ("Surface Strength", Range(0,1)) = 0.585
        [Header(Gentle Breeze)]
        _BreezeStrength ("Breeze Strength", Range(0,1)) = 0.55
        _BreezeSpeed ("Breeze Change Speed", Range(0,1)) = 0.25
        [Header(Surface Ripples)]
        _SurfaceRippleDepth ("Ripple Band Depth (Image UV)", Range(0.005,0.1)) = 0.035
        _SurfaceRippleStrength ("Ripple Brightness", Range(0,0.5)) = 0.208
        _SurfaceRippleSpeed ("Ripple Drift Speed", Range(0,2)) = 0.4
        [Header(Reflected Light)]
        _ReflectionColor ("Reflection Light Color", Color) = (0.78,0.90,0.83,1)
        _ReflectionStrength ("Reflection Light Strength", Range(0,1)) = 0.30
        _ReflectionDepth ("Reflection Band Depth (Image UV)", Range(0.005,0.15)) = 0.045
        [Header(Underwater Texture)]
        _Refraction ("Terrain Refraction (Image UV)", Range(0,0.01)) = 0.0015
        _TextureScale ("Ripple Scale", Range(1,60)) = 24
        _TextureSpeed ("Ripple Speed", Range(0,1)) = 0.10
        _GlintStrength ("Soft Glints", Range(0,0.3)) = 0.035
        _TerrainAmount ("Terrain Reconstruction (0 For Moving Objects)", Range(0,1)) = 1
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="False" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "SpringWater"
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_TerrainTex); SAMPLER(sampler_TerrainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor, _DeepColor, _SurfaceColor, _Color, _InteriorColor;
                float _BackgroundBlock;
                float _Opacity, _DeepOpacity, _DepthRange, _SurfaceY;
                float _WaveAmplitude, _WaveSpeed, _WaveFrequency, _SurfaceWidth;
                float _SurfaceStrength, _Refraction, _TextureScale, _TextureSpeed;
                float _GlintStrength, _TerrainAmount;
                float _SurfaceRippleDepth, _SurfaceRippleStrength, _SurfaceRippleSpeed;
                float4 _ReflectionColor;
                float _ReflectionStrength, _ReflectionDepth;
                float _BreezeStrength, _BreezeSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            // Smooth overlapping waves: no extra noise textures required.
            float Ripple(float2 p, float t)
            {
                return sin(p.x * 1.13 + p.y * 0.77 + t) * 0.50
                     + sin(p.x * -0.67 + p.y * 1.37 - t * 0.73) * 0.30
                     + sin(p.x * 1.91 - p.y * 0.43 + t * 0.41) * 0.20;
            }

            // Traveling gust envelope and bounded phase offset, shared by surface and light.
            // Do not multiply elapsed time by the changing gust: that accelerates over time.
            float2 Breeze(float horizontalUV)
            {
                float breezeTime = _Time.y * _BreezeSpeed;
                float gust = 0.5 + 0.3 * sin(horizontalUV * 9.0 - breezeTime)
                                 + 0.2 * sin(horizontalUV * 15.7 - breezeTime * 0.47 + 1.3);
                float phaseOffset = _BreezeStrength * 0.4
                                  * sin(horizontalUV * 6.0 - breezeTime * 0.73);
                return float2(saturate(gust), phaseOffset);
            }

            // Two broken highlights follow the surface; the terrain mask stays fixed.
            float SurfaceRipples(float2 uv, float below, float2 breeze)
            {
                float bandDepth = max(_SurfaceRippleDepth, 0.005);
                float drift = _Time.y * _SurfaceRippleSpeed + breeze.y;
                float bandPosition = below / bandDepth;
                float crestA = 0.28 + 0.055 * sin(uv.x * 53.0 - drift);
                float crestB = 0.62 + 0.075 * sin(uv.x * 39.0 + drift * 0.71 + 2.1);
                float distanceA = abs(bandPosition - crestA);
                float distanceB = abs(bandPosition - crestB);
                float filterA = max(fwidth(bandPosition - crestA), 0.001);
                float filterB = max(fwidth(bandPosition - crestB), 0.001);
                float strokeA = 1.0 - smoothstep(0.025, 0.025 + filterA, distanceA);
                float strokeB = 1.0 - smoothstep(0.018, 0.018 + filterB, distanceB);
                // Moving envelopes break the highlights into short, softly appearing streaks.
                float segmentsA = smoothstep(0.2, 0.85,
                    sin(uv.x * 119.0 - drift * 0.8) * sin(uv.x * 47.0 + drift * 0.37));
                float segmentsB = smoothstep(0.15, 0.8,
                    sin(uv.x * 97.0 + drift * 0.63 + 1.7) * sin(uv.x * 61.0 - drift * 0.29));
                float bandFade = smoothstep(0.0, 0.12, bandPosition)
                               * (1.0 - smoothstep(0.35, 1.0, bandPosition));
                return saturate(strokeA * segmentsA + strokeB * segmentsB * 0.65)
                     * bandFade * _SurfaceRippleStrength
                     * lerp(1.0, 0.7 + 0.3 * breeze.x, _BreezeStrength);
            }

            // Stylized reflected light, not a captured scene reflection. Reuses ripple motion.
            float ReflectedLight(float2 uv, float below, float2 breeze)
            {
                float drift = _Time.y * _SurfaceRippleSpeed + breeze.y;
                float bandPosition = below / max(_ReflectionDepth, 0.005);
                float bend = sin(uv.x * 53.0 - drift);
                float otherBend = sin(uv.x * 39.0 + drift * 0.71 + 2.1);
                float crestA = bandPosition - (0.20 + 0.045 * bend);
                float crestB = bandPosition - (0.53 + 0.065 * otherBend);
                float filterA = max(fwidth(crestA), 0.001);
                float filterB = max(fwidth(crestB), 0.001);
                float streakA = 1.0 - smoothstep(0.018, 0.018 + filterA, abs(crestA));
                float streakB = 1.0 - smoothstep(0.025, 0.025 + filterB, abs(crestB));
                // Broad moving patches produce long highlights with plenty of dark gaps.
                float patchA = smoothstep(0.35, 0.9,
                    sin(uv.x * 71.0 - drift * 0.8) * sin(uv.x * 29.0 + drift * 0.37));
                float patchB = smoothstep(0.35, 0.9,
                    sin(uv.x * 59.0 + drift * 0.63 + 1.7) * sin(uv.x * 37.0 - drift * 0.29));
                float bandFade = smoothstep(0.0, 0.08, bandPosition)
                               * (1.0 - smoothstep(0.25, 1.0, bandPosition));
                return saturate(streakA * patchA + streakB * patchB * 0.65)
                     * bandFade * _ReflectionStrength
                     * lerp(1.0, 0.65 + 0.35 * breeze.x, _BreezeStrength);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
                float2 breeze = Breeze(uv.x);
                float t = _Time.y * _WaveSpeed + breeze.y;
                float broadWave =
                    sin(uv.x * _WaveFrequency + t) * 0.65 +
                    sin(uv.x * _WaveFrequency * 1.73 - t * 0.61) * 0.35;
                float fineWave = sin(uv.x * _WaveFrequency * 3.7 - t * 1.4) * 0.7
                               + sin(uv.x * _WaveFrequency * 5.1 - t * 1.13 + 1.9) * 0.3;
                float fineWeight = _BreezeStrength * breeze.x * 0.22;
                float calmEnvelope = lerp(1.0, 0.45 + 0.55 * breeze.x, _BreezeStrength);
                // Convex blend keeps the displacement within the existing Wave Height limit.
                float surface = _SurfaceY + _WaveAmplitude * calmEnvelope
                              * lerp(broadWave, fineWave, fineWeight);
                float below = surface - uv.y;
                float aa = max(fwidth(below), 0.00001);
                float coverage = mask * smoothstep(-aa, aa, below);
                float depth = saturate(max(below, 0.0) / _DepthRange);
                float tintAlpha = lerp(_Opacity, _DeepOpacity, depth);
                float3 tint = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth);

                // Source images share dimensions, pivot and placement. UVs must not be atlased.
                float2 p = uv * float2(2.2348, 1.0) * _TextureScale;
                float flowTime = _Time.y * _TextureSpeed;
                float2 ripple = float2(Ripple(p, flowTime), Ripple(p + 7.31, flowTime * 0.83));
                float edgeFade = smoothstep(0.0, 0.018, below);
                float2 offset = ripple * float2(0.44747, 1.0) * _Refraction * edgeFade;
                float2 terrainUV = saturate(uv + offset);
                float4 terrain = SAMPLE_TEXTURE2D(_TerrainTex, sampler_TerrainTex, terrainUV);
                float4 originalTerrain = SAMPLE_TEXTURE2D(_TerrainTex, sampler_TerrainTex, uv);
                // Fade reconstruction at transparent terrain edges to reduce doubled silhouettes.
                float terrainAlpha = terrain.a * originalTerrain.a * _TerrainAmount;
                // Composite terrain over a separate pond interior, then tint the water.
                // Blocking = 1 hides distant scenery without increasing water tint opacity.
                float3 interiorRGB = _InteriorColor.rgb * _BackgroundBlock;
                float3 underwaterRGB = terrain.rgb * terrainAlpha + interiorRGB * (1.0 - terrainAlpha);
                float underwaterAlpha = terrainAlpha + _BackgroundBlock * (1.0 - terrainAlpha);
                float3 rgb = underwaterRGB * (1.0 - tintAlpha) + tint * tintAlpha;
                float alpha = underwaterAlpha * (1.0 - tintAlpha) + tintAlpha;

                float surfaceLine = (1.0 - smoothstep(_SurfaceWidth, _SurfaceWidth + aa, below))
                           * _SurfaceStrength;
                float glint = pow(saturate(Ripple(p * float2(0.55, 2.8), flowTime * 0.6)), 10.0);
                glint *= _GlintStrength * exp2(-depth * 5.0);
                float surfaceRipples = SurfaceRipples(uv, below, breeze);
                float lightAmount = saturate(surfaceLine + glint + surfaceRipples);
                rgb = rgb * (1.0 - lightAmount) + _SurfaceColor.rgb * lightAmount;
                alpha = alpha * (1.0 - lightAmount) + lightAmount;

                float reflectedLight = ReflectedLight(uv, below, breeze);
                rgb = rgb * (1.0 - reflectedLight) + _ReflectionColor.rgb * reflectedLight;
                alpha = alpha * (1.0 - reflectedLight) + reflectedLight;

                // Premultiplied output keeps transparent mask edges clean.
                float fade = coverage * input.color.a;
                return half4(rgb * input.color.rgb * fade, alpha * fade);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
