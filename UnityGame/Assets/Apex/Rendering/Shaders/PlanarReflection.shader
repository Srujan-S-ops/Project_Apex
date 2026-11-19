// PROJECT APEX — planar reflection layer for the showroom floor.
// Drawn additively over a dark glossy URP Lit floor. _ReflectionTex is rendered by PlanarReflection.cs
// from a mirrored camera (horizontally mirrored image, hence the 1-u sample).
Shader "Apex/PlanarReflection"
{
    Properties
    {
        _ReflectionTex ("Reflection", 2D) = "black" {}
        _Strength ("Strength", Range(0, 2)) = 0.55
        _Fresnel ("Fresnel Power", Range(0.5, 8)) = 3
        _FadeRadius ("Fade Radius (m)", Float) = 9
        _Blur ("Blur (texels)", Range(0, 4)) = 1.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "PlanarReflection"
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);
            float4 _ReflectionTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float _Strength;
                float _Fresnel;
                float _FadeRadius;
                float _Blur;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.screenPos = ComputeScreenPos(p.positionCS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.screenPos.xy / i.screenPos.w;
                uv.x = 1.0 - uv.x;
                float2 t = _ReflectionTex_TexelSize.xy * _Blur;
                half3 c = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, uv).rgb * 0.4;
                c += SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, uv + float2(t.x, 0)).rgb * 0.15;
                c += SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, uv - float2(t.x, 0)).rgb * 0.15;
                c += SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, uv + float2(0, t.y)).rgb * 0.15;
                c += SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, uv - float2(0, t.y)).rgb * 0.15;
                float3 viewDir = normalize(GetCameraPositionWS() - i.positionWS);
                float ndv = saturate(dot(normalize(i.normalWS), viewDir));
                float fresnel = 0.25 + 0.75 * pow(1.0 - ndv, _Fresnel);
                float fade = saturate(1.0 - length(i.positionWS.xz) / _FadeRadius);
                return half4(c * _Strength * fresnel * fade * fade, 1);
            }
            ENDHLSL
        }
    }
}
