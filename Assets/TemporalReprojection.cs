// Temporal reprojection example
// https://github.com/keijiro/TemporalReprojectionTest

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

#region Effect settings

[System.Serializable]
[PostProcess(typeof(TemporalReprojectionRenderer), PostProcessEvent.BeforeStack, "Temporal Reprojection")]
public sealed class TemporalReprojection : PostProcessEffectSettings
{
    public FloatParameter motionWeight   = new FloatParameter { value = 1 };
    public FloatParameter depthWeight    = new FloatParameter { value = 20 };
    public IntParameter   sampleInterval = new IntParameter   { value = 60 };
}

#endregion

#region Effect renderer

sealed class TemporalReprojectionRenderer : PostProcessEffectRenderer<TemporalReprojection>
{
    static class ShaderIDs
    {
        internal static readonly int DepthWeight  = Shader.PropertyToID("_DepthWeight");
        internal static readonly int MotionWeight = Shader.PropertyToID("_MotionWeight");
        internal static readonly int UVRemap      = Shader.PropertyToID("_UVRemap");
        internal static readonly int PrevUVRemap  = Shader.PropertyToID("_PrevUVRemap");
        internal static readonly int PrevMoDepth  = Shader.PropertyToID("_PrevMoDepth");
        internal static readonly int DeltaTime    = Shader.PropertyToID("_DeltaTime");
    }

    RenderTexture _lastFrame;
    RenderTexture _prevUVRemap;
    RenderTexture _prevMoDepth;

    RenderTargetIdentifier[] _mrt = new RenderTargetIdentifier[2];

    float _prevDeltaTime;
    int _frameCount;

    public override void Release()
    {
        if (_lastFrame != null) RenderTexture.ReleaseTemporary(_lastFrame);
        if (_prevUVRemap != null) RenderTexture.ReleaseTemporary(_prevUVRemap);
        if (_prevMoDepth != null) RenderTexture.ReleaseTemporary(_prevMoDepth);
        base.Release();
    }

    public override DepthTextureMode GetCameraFlags()
    {
        return DepthTextureMode.MotionVectors | DepthTextureMode.Depth;
    }

    public override void Render(PostProcessRenderContext context)
    {
        var cmd = context.command;
        cmd.BeginSample("TemporalReprojection");

        // Allocate RTs for storing the next frame state.
        var uvRemap = RenderTexture.GetTemporary(context.width, context.height, 0, RenderTextureFormat.ARGBHalf);
        var moDepth = RenderTexture.GetTemporary(context.width, context.height, 0, RenderTextureFormat.ARGBHalf);
        _mrt[0] = uvRemap.colorBuffer;
        _mrt[1] = moDepth.colorBuffer;

        // Set the shader uniforms.
        var sheet = context.propertySheets.Get(Shader.Find("Hidden/TemporalReprojection"));
        sheet.properties.SetFloat(ShaderIDs.DepthWeight, settings.depthWeight);
        sheet.properties.SetFloat(ShaderIDs.MotionWeight, settings.motionWeight);
        if (_prevUVRemap != null) sheet.properties.SetTexture(ShaderIDs.PrevUVRemap, _prevUVRemap);
        if (_prevMoDepth != null) sheet.properties.SetTexture(ShaderIDs.PrevMoDepth, _prevMoDepth);
        sheet.properties.SetVector(ShaderIDs.DeltaTime, new Vector2(Time.deltaTime, _prevDeltaTime));

        // Detect frame interval.
        if (_frameCount++ % Mathf.Max(1, settings.sampleInterval) == 0)
        {
            // Update the last frame store.
            if (_lastFrame != null) RenderTexture.ReleaseTemporary(_lastFrame);
            _lastFrame = RenderTexture.GetTemporary(context.width, context.height, 0, RenderTextureFormat.ARGBHalf);
            cmd.BlitFullscreenTriangle(context.source, _lastFrame);

            // Reset pass
            cmd.BlitFullscreenTriangle(context.source, _mrt, uvRemap.depthBuffer, sheet, 0);
        }
        else
        {
            // Temporal reprojection pass
            cmd.BlitFullscreenTriangle(context.source, _mrt, uvRemap.depthBuffer, sheet, 1);
        }

        // Second pass: Composition
        sheet.properties.SetTexture(ShaderIDs.UVRemap, uvRemap);
        cmd.BlitFullscreenTriangle(_lastFrame, context.destination, sheet, 2);

        // Discard the previous frame state.
        if (_prevUVRemap != null) RenderTexture.ReleaseTemporary(_prevUVRemap);
        if (_prevMoDepth != null) RenderTexture.ReleaseTemporary(_prevMoDepth);

        // Update the internal state.
        _prevUVRemap = uvRemap;
        _prevMoDepth = moDepth;
        _prevDeltaTime = Time.deltaTime;

        cmd.EndSample("TemporalReprojection");
    }
}

#endregion
