Shader "Story/Believer Glitch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Glitch ("Local distortion", Range(0,0.1)) = 0
        _SpriteUVRect ("Sprite UV rectangle", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="False" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _SpriteUVRect;
                float _Glitch;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv; output.color = input.color * _Color;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float2 localUV = (input.uv - _SpriteUVRect.xy) / max(_SpriteUVRect.zw, .00001);
                float stripe = floor(localUV.y * 32);
                float jitter = frac(sin(stripe * 47.3 + floor(_Time.y * 18)) * 43758.5) - .5;
                float mask = step(.34, localUV.y) * step(localUV.y, .72);
                localUV.x += jitter * _Glitch * mask;
                float2 uv = _SpriteUVRect.xy + clamp(localUV, .001, .999) * _SpriteUVRect.zw;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * input.color;
            }
            ENDHLSL
        }
    }
}
