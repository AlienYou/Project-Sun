Shader "Project Sun/Scope Lens Composite"
{
    Properties
    {
        _MainTex ("Scope View", 2D) = "black" {}
        _MaskTex ("Lens Mask", 2D) = "white" {}
        _ReticleTex ("Reticle", 2D) = "white" {}
        [HDR] _ReticleColor ("Reticle Color", Color) = (0.1, 1, 0.2, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _EdgeSoftness ("Edge Softness", Range(0.5, 4)) = 1.25
        _UseMaskTex ("Use Mask", Float) = 0
        _UseReticleTex ("Use Reticle Texture", Float) = 0
        _ReticleStyle ("Fallback Reticle Style", Float) = 0
        _ReticleDotRadius ("Reticle Dot Radius", Float) = 0.005
        _ReticleHalfFrame ("Reticle Half Frame", Float) = 0.05
        _ReticleHalfThickness ("Reticle Half Thickness", Float) = 0.002
        _ReticleGap ("Reticle Gap", Float) = 0.01
        _EyeboxOffset ("Eyebox Offset", Vector) = (0, 0, 0, 0)
        _EyeboxSeverity ("Eyebox Severity", Range(0, 1)) = 0
        _EyeboxMaxOcclusion ("Eyebox Maximum Occlusion", Range(0, 1)) = 0.92
        _EyeboxContraction ("Eyebox Pupil Contraction", Range(0, 0.75)) = 0.28
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+50"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "ScopeLensComposite"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            TEXTURE2D(_ReticleTex);
            SAMPLER(sampler_ReticleTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _ReticleColor;
                float _Opacity;
                float _EdgeSoftness;
                float _UseMaskTex;
                float _UseReticleTex;
                float _ReticleStyle;
                float _ReticleDotRadius;
                float _ReticleHalfFrame;
                float _ReticleHalfThickness;
                float _ReticleGap;
                float2 _EyeboxOffset;
                float _EyeboxSeverity;
                float _EyeboxMaxOcclusion;
                float _EyeboxContraction;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            float SoftBand(float value, float halfWidth, float antialias)
            {
                return 1.0 - smoothstep(halfWidth - antialias, halfWidth + antialias, abs(value));
            }

            float SoftRange(float value, float minimum, float maximum, float antialias)
            {
                return smoothstep(minimum - antialias, minimum + antialias, value) *
                    (1.0 - smoothstep(maximum - antialias, maximum + antialias, value));
            }

            float TextureMaskValue(half4 sampleValue)
            {
                return min(sampleValue.a, max(sampleValue.r, max(sampleValue.g, sampleValue.b)));
            }

            float ProceduralReticle(float2 delta, float antialias)
            {
                float radius = length(delta);
                float dot = 1.0 - smoothstep(_ReticleDotRadius - antialias,
                    _ReticleDotRadius + antialias, radius);
                float horizontal = SoftBand(delta.y, _ReticleHalfThickness, antialias) *
                    SoftRange(abs(delta.x), _ReticleGap, _ReticleHalfFrame, antialias);
                float vertical = SoftBand(delta.x, _ReticleHalfThickness, antialias) *
                    SoftRange(abs(delta.y), _ReticleGap, _ReticleHalfFrame, antialias);
                float ring = 1.0 - smoothstep(_ReticleHalfThickness - antialias,
                    _ReticleHalfThickness + antialias, abs(radius - _ReticleHalfFrame));

                if (_ReticleStyle < 0.5) return 0.0;
                if (_ReticleStyle < 1.5) return dot;
                if (_ReticleStyle < 2.5) return saturate(ring + dot);
                return saturate(horizontal + vertical);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 delta = input.uv - 0.5;
                float apertureDistance = length(delta);
                float apertureAA = max(fwidth(apertureDistance) * _EdgeSoftness, 0.00025);
                float circularMask = 1.0 - smoothstep(0.5 - apertureAA, 0.5 + apertureAA,
                    apertureDistance);
                half4 authoredMaskSample = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv);
                float authoredMask = lerp(1.0, TextureMaskValue(authoredMaskSample),
                    saturate(_UseMaskTex));
                float lensAlpha = circularMask * authoredMask * saturate(_Opacity);

                half4 scopeColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // Eyebox 强度同时收缩并平移可见出瞳；只压暗镜片 RGB，不改变物理口径 Alpha，
                // 因此黑边会遮住镜内画面而不是错误露出主相机的未放大背景。
                float pupilRadius = 0.5 * (1.0 - saturate(_EyeboxSeverity) * _EyeboxContraction);
                float pupilDistance = length(delta - _EyeboxOffset);
                float pupilAA = max(fwidth(pupilDistance) * _EdgeSoftness, 0.00025);
                float pupilMask = 1.0 - smoothstep(pupilRadius - pupilAA, pupilRadius + pupilAA,
                    pupilDistance);
                float eyeboxOcclusion = (1.0 - pupilMask) * saturate(_EyeboxSeverity) *
                    saturate(_EyeboxMaxOcclusion);
                float eyeboxVisibility = 1.0 - eyeboxOcclusion;
                scopeColor.rgb *= eyeboxVisibility;
                float reticleAA = max(max(fwidth(delta.x), fwidth(delta.y)) * 1.25, 0.0002);
                float reticleAlpha = ProceduralReticle(delta, reticleAA);
                if (_UseReticleTex > 0.5)
                {
                    float fullFrame = max(_ReticleHalfFrame * 2.0, 0.0001);
                    float2 reticleUv = delta / fullFrame + 0.5;
                    float inside = step(0.0, reticleUv.x) * step(reticleUv.x, 1.0) *
                        step(0.0, reticleUv.y) * step(reticleUv.y, 1.0);
                    half4 reticleSample = SAMPLE_TEXTURE2D(_ReticleTex, sampler_ReticleTex, reticleUv);
                    reticleAlpha = TextureMaskValue(reticleSample) * inside;
                }

                float reticleBlend = saturate(reticleAlpha * _ReticleColor.a) * lensAlpha *
                    eyeboxVisibility;
                scopeColor.rgb = lerp(scopeColor.rgb, _ReticleColor.rgb, reticleBlend);
                scopeColor.a = lensAlpha;
                return scopeColor;
            }
            ENDHLSL
        }

        // 镜外合成使用该专用 Pass 把同一个运行时镜片网格写入口径 Mask。
        // 自定义 LightMode 可防止 URP 在常规前向渲染中重复绘制；Renderer Feature 会按 Pass 名显式调用。
        Pass
        {
            Name "ScopeApertureMask"
            Tags { "LightMode" = "ProjectSunScopeMask" }
            Blend One Zero
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment MaskFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _ReticleColor;
                float _Opacity;
                float _EdgeSoftness;
                float _UseMaskTex;
                float _UseReticleTex;
                float _ReticleStyle;
                float _ReticleDotRadius;
                float _ReticleHalfFrame;
                float _ReticleHalfThickness;
                float _ReticleGap;
                float2 _EyeboxOffset;
                float _EyeboxSeverity;
                float _EyeboxMaxOcclusion;
                float _EyeboxContraction;
            CBUFFER_END

            struct MaskAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct MaskVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            MaskVaryings MaskVert(MaskAttributes input)
            {
                MaskVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            float MaskTextureValue(half4 sampleValue)
            {
                return min(sampleValue.a, max(sampleValue.r, max(sampleValue.g, sampleValue.b)));
            }

            half4 MaskFrag(MaskVaryings input) : SV_Target
            {
                float apertureDistance = length(input.uv - 0.5);
                float apertureAA = max(fwidth(apertureDistance) * _EdgeSoftness, 0.00025);
                float circularMask = 1.0 - smoothstep(0.5 - apertureAA, 0.5 + apertureAA,
                    apertureDistance);
                half4 authoredMaskSample = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv);
                float authoredMask = lerp(1.0, MaskTextureValue(authoredMaskSample),
                    saturate(_UseMaskTex));
                float apertureCoverage = circularMask * authoredMask;
                return half4(apertureCoverage, apertureCoverage, apertureCoverage, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
