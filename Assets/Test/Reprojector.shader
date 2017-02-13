Shader "Hidden/Reprojector"
{
    Properties
    {
        _MainTex("", 2D) = "white" {}
    }

    CGINCLUDE

    #include "UnityCG.cginc"

    sampler2D_float _CameraDepthTexture;
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

    fixed4 frag_reprojection(v2f_img i) : SV_Target
    {
        half2 movec = tex2D(_CameraMotionVectorsTexture, i.uv).rg;

        float2 uv0 = i.uv - movec;
        float2 uv1 = i.uv;

        float d0 = LinearizeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv0));
        float d1 = LinearizeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv1));

        fixed4 c0 = tex2D(_HistoryTex, uv0);

        half rej = abs(d1 - d0) * 100;
        rej += any(uv0 < 0) + any(uv1 > 1 - _MainTex_TexelSize.xy);

        return c0 * saturate(1 - rej);
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
