Shader "Hidden/Reprojector"
{
    Properties
    {
        _MainTex("", 2D) = "white" {}
    }

    CGINCLUDE

    #include "UnityCG.cginc"

    sampler2D_float _CameraDepthTexture;
    float4 _CameraDepthTexture_TexelSize;

    sampler2D_half _CameraMotionVectorsTexture;

    sampler2D _HistoryTex;
    sampler2D _MainTex;

    float4 _MainTex_TexelSize;

    const float sampleInterval = 1;

    // Z buffer depth to linear 0-1 depth
    // Handles orthographic projection correctly
    float LinearizeDepth(float z)
    {
        float isOrtho = unity_OrthoParams.w;
        float isPers = 1.0 - unity_OrthoParams.w;
        z *= _ZBufferParams.x;
        return (1.0 - isOrtho * z) / (isPers * z + _ZBufferParams.y);
    }

    float3 CompareDepth(float3 min_uvd, float2 uv)
    {
        float d = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
        return d < min_uvd ? float3(uv, d) : min_uvd;
    }

    float2 SearchClosest(float2 uv)
    {
        float4 duv = _CameraDepthTexture_TexelSize.xyxy * float4(1, 1, -1, 0);

        float3 min_uvd = float3(uv, SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));

        min_uvd = CompareDepth(min_uvd, uv - duv.xy);
        min_uvd = CompareDepth(min_uvd, uv - duv.wy);
        min_uvd = CompareDepth(min_uvd, uv - duv.zy);

        min_uvd = CompareDepth(min_uvd, uv + duv.zw);
        min_uvd = CompareDepth(min_uvd, uv + duv.xw);

        min_uvd = CompareDepth(min_uvd, uv + duv.zy);
        min_uvd = CompareDepth(min_uvd, uv + duv.wy);
        min_uvd = CompareDepth(min_uvd, uv + duv.xy);

        return min_uvd.xy;
    }

    fixed4 frag_reprojection(v2f_img i) : SV_Target
    {
        #if defined(_SEARCH_CLOSEST)
        half2 movec = tex2D(_CameraMotionVectorsTexture, SearchClosest(i.uv)).rg;
        #else
        half2 movec = tex2D(_CameraMotionVectorsTexture, i.uv).rg;
        #endif

        float2 uv0 = i.uv - movec;
        float2 uv1 = i.uv;

        float d0 = LinearizeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv0));
        float d1 = LinearizeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv1));

        fixed4 c0 = tex2D(_HistoryTex, uv0);

        half rej = abs(d1 - d0) * 100;
        rej += any(uv0 < 0) + any(uv1 > 1 - _MainTex_TexelSize.xy);

        return lerp(c0, fixed4(1, 0, 0, 0), saturate(rej));
    }

    fixed4 frag_composit(v2f_img i) : SV_Target
    {
        return tex2D(_HistoryTex, i.uv);
    }

    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma multi_compile _ _SEARCH_CLOSEST
            #pragma vertex vert_img
            #pragma fragment frag_reprojection
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_composit
            ENDCG
        }
    }
}
