Shader "WanderSoul/Interaction/LoggingTree"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _LoggingWorldOffset("World Offset", Vector) = (0,0,0,0)
        [HideInInspector] _LoggingClipPlane("Clip Plane", Vector) = (0,1,0,0)
        [HideInInspector] _LoggingClipEnabled("Clip Enabled", Float) = 0
        [HideInInspector] _LoggingVisible("Visible", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; float clipHeight : TEXCOORD4; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _LoggingWorldOffset;
                float4 _LoggingClipPlane;
                float _LoggingClipEnabled;
                float _LoggingVisible;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                float3 world = TransformObjectToWorld(input.positionOS) + _LoggingWorldOffset.xyz;
                output.positionCS = TransformWorldToHClip(world);
                output.clipHeight = dot(world, _LoggingClipPlane.xyz) - _LoggingClipPlane.w;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                clip(_LoggingVisible - 0.5);
                if (_LoggingClipEnabled > 0.5) clip(input.clipHeight);
                return CommonUnlitFragment(input, input.color);
            }
            ENDHLSL
        }
    }
}
