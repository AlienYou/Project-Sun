Shader "Project Sun/Scope Peripheral Composite"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "ScopePeripheralComposite"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            // Blit.hlsl 依赖 Core.hlsl 提供 TEXTURE2D_X、立体渲染和平台纹理采样宏。
            // 包含顺序不可颠倒，否则 D3D11 会在编译顶点阶段时找不到 TEXTURE2D_X。
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_LensMaskTex);
            float _OutsideDim;
            float _BlurRadiusPixels;
            float _BlurQuality;
            float _EdgeSoftness;
            float _Opacity;
            // xy 为单个源像素对应的 UV，zw 为源相机颜色目标的实际像素宽高。
            float4 _SourceTexelSize;

            half3 SamplePeripheralBlur(float2 uv)
            {
                half3 colour = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                if (_BlurQuality < 0.5 || _BlurRadiusPixels <= 0.01) return colour;

                // 低档仅返回中心像素；中档增加四个对角样本；高档再增加四个轴向样本。
                // 分支由整帧统一画质参数控制，不会在同一线程束内产生像素级分歧。
                float2 texel = _BlurRadiusPixels * _SourceTexelSize.xy;
                half3 sum = colour;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texel * float2(-1.0, -1.0)).rgb;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texel * float2(1.0, -1.0)).rgb;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texel * float2(-1.0, 1.0)).rgb;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texel * float2(1.0, 1.0)).rgb;
                if (_BlurQuality < 1.5) return sum * 0.2;

                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-texel.x, 0.0)).rgb;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(texel.x, 0.0)).rgb;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0.0, -texel.y)).rgb;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0.0, texel.y)).rgb;
                return sum / 9.0;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                // 口径 Mask 由同一运行时镜片网格、同一 Viewmodel 相机矩阵在当前 Pass 中生成。
                // 这里直接读取覆盖率，ADS 动画期间无需再猜测镜片中心或椭圆轴。
                float apertureCoverage = SAMPLE_TEXTURE2D_X(_LensMaskTex, sampler_LinearClamp, uv).r;
                float outsideLens = 1.0 - saturate(apertureCoverage);
                float effectWeight = outsideLens * saturate(_Opacity);
                half3 peripheral = SamplePeripheralBlur(uv) * (1.0 - saturate(_OutsideDim));
                source.rgb = lerp(source.rgb, peripheral, effectWeight);
                return source;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
