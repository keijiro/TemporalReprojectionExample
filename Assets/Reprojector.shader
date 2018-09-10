Shader "Hidden/Reprojector"
{
    Properties
    {
        _MainTex("", 2D) = "white" {}
        _ColorHistory("", 2D) = "white" {}
        _PrevMotionDepth("", 2D) = "white" {}
    }

    CGINCLUDE

    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;

    sampler2D_float _CameraDepthTexture;
    float4 _CameraDepthTexture_TexelSize;

    sampler2D_half _CameraMotionVectorsTexture;
    float4 _CameraMotionVectorsTexture_TexelSize;

    sampler2D _ColorHistory;
    sampler2D_half _PrevMotionDepth;

    float _DepthWeight;
    float _MotionWeight;
    float2 _DeltaTime;

    struct FragmentOutput
    {
        half4 colorHistory : SV_Target0;
        half4 motionDepth : SV_Target1;
    };

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

    FragmentOutput FragmentInitialize(v2f_img i)
    {
        fixed4 c = tex2D(_MainTex, i.uv);
        half2 m = tex2D(_CameraMotionVectorsTexture, i.uv);
        half d = LinearizeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv));

        FragmentOutput o;
        o.colorHistory = fixed4(c.rgb, 1);
        o.motionDepth = half4(m, d, 0);
        return o;
    }

    FragmentOutput FragmentUpdate(v2f_img i)
    {
        float2 uv1 = i.uv.xy;
        half2 m1 = tex2D(_CameraMotionVectorsTexture, uv1);
        half d1 = LinearizeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv1));

        half2 mc1 = tex2D(_CameraMotionVectorsTexture, SearchClosest(uv1)).xy;

        float2 uv0 = uv1 - mc1;
        half3 md0 = tex2D(_PrevMotionDepth, uv0).xyz;
        fixed4 c0 = tex2D(_ColorHistory, uv0);

        // Disocclusion test
        float docc = abs(1 - d1 / md0.z) * _DepthWeight;

        // Velocity weighting
        float vw = distance(m1 * _DeltaTime.x, md0.xy * _DeltaTime.y) * _MotionWeight;

        // Out of screen test
        float oscr = any(uv0 < 0) + any(uv0 > 1);

        float alpha = 1 - saturate(docc + oscr + vw);

        FragmentOutput o;
        o.colorHistory = fixed4(c0.rgb, min(c0.a, alpha));
        o.motionDepth = half4(m1, d1, 0);
        return o;
    }

    fixed4 FragmentComposite(v2f_img i) : SV_Target
    {
        fixed4 co = tex2D(_MainTex, i.uv);
        fixed4 ch = tex2D(_ColorHistory, i.uv);
        return fixed4(lerp(fixed3(1, 0, 0), ch.rgb, ch.a), 1);
    }

    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragmentInitialize
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragmentUpdate
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragmentComposite
            #pragma target 3.0
            ENDCG
        }
    }
}
