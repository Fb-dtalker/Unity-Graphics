Shader "Hidden/HDRP/CompositeUI"
{
    HLSLINCLUDE

        #pragma target 4.5
        #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch
        #pragma editor_sync_compilation
        #pragma multi_compile_local _ HDR_OUTPUT_REC2020 HDR_OUTPUT_SCRGB
        #pragma multi_compile_local_fragment _ APPLY_AFTER_POST

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/HDROutput.hlsl"

        TEXTURE2D_X(_InputTexture);
        TEXTURE2D_X(_UITexture);
        TEXTURE2D_X(_AfterPostProcessTexture);

        CBUFFER_START(cb)
            float4 _HDROutputParams;
        CBUFFER_END

        #define _MinNits    _HDROutputParams.x
        #define _MaxNits    _HDROutputParams.y
        #define _PaperWhite _HDROutputParams.z
        #define _IsRec2020  (int)(_HDROutputParams.w) == 0
        #define _IsRec709   (int)(_HDROutputParams.w) == 1
        #define _IsP3       (int)(_HDROutputParams.w) == 2

        struct Attributes
        {
            uint vertexID : SV_VertexID;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 texcoord   : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            output.texcoord = GetNormalizedFullScreenTriangleTexCoord(input.vertexID);
            return output;
        }

        float4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = input.texcoord;
            // We need to flip y
            uv.y = 1.0f - uv.y;

            float4 outColor = SAMPLE_TEXTURE2D_X(_InputTexture, s_point_clamp_sampler, uv);
            // Apply AfterPostProcess target
            #if APPLY_AFTER_POST
            float4 afterPostColor = SAMPLE_TEXTURE2D_X_LOD(_AfterPostProcessTexture, s_point_clamp_sampler, uv, 0);
            afterPostColor.rgb = ProcessUIForHDR(afterPostColor.rgb, _PaperWhite, _MaxNits);
            // After post objects are blended according to the method described here: https://developer.nvidia.com/gpugems/GPUGems3/gpugems3_ch23.html
            outColor.xyz = afterPostColor.a * outColor.xyz + afterPostColor.xyz;
            #endif

            float4 uiValue = SAMPLE_TEXTURE2D_X_LOD(_UITexture, s_point_clamp_sampler, uv, 0);
            outColor.rgb = SceneUIComposition(uiValue, outColor.rgb, _PaperWhite, _MaxNits);
            outColor.rgb = OETF(outColor.rgb);

            return outColor;
        }
    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }

        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Frag
            ENDHLSL
        }
    }
    Fallback Off
}
