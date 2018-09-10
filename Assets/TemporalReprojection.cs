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
        internal static readonly int DepthWeight     = Shader.PropertyToID("_DepthWeight");
        internal static readonly int MotionWeight    = Shader.PropertyToID("_MotionWeight");
        internal static readonly int ColorHistory    = Shader.PropertyToID("_ColorHistory");
        internal static readonly int PrevMotionDepth = Shader.PropertyToID("_PrevMotionDepth");
        internal static readonly int DeltaTime       = Shader.PropertyToID("_DeltaTime");
    }

    RenderTexture _colorHistory;
    RenderTexture _prevMotionDepth;

    RenderTargetIdentifier[] _mrt = new RenderTargetIdentifier[2];

    float _prevDeltaTime;
    int _frameCount;

    public override void Release()
    {
        if (_colorHistory != null)
        {
            RenderTexture.ReleaseTemporary(_colorHistory);
            _colorHistory = null;
        }

        if (_prevMotionDepth != null)
        {
            RenderTexture.ReleaseTemporary(_prevMotionDepth);
            _prevMotionDepth = null;
        }

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

        // First pass: Reprojection from the previous frame

        // Allocate RTs for storing the next frame state.
        var newColorHistory = RenderTexture.GetTemporary(context.width, context.height, 0, RenderTextureFormat.ARGBHalf);
        var newMotionDepth = RenderTexture.GetTemporary(context.width, context.height, 0, RenderTextureFormat.ARGBHalf);

        _mrt[0] = newColorHistory.colorBuffer;
        _mrt[1] = newMotionDepth.colorBuffer;

        // Set the shader uniforms.
        var sheet = context.propertySheets.Get(Shader.Find("Hidden/TemporalReprojection"));

        sheet.properties.SetFloat(ShaderIDs.DepthWeight, settings.depthWeight);
        sheet.properties.SetFloat(ShaderIDs.MotionWeight, settings.motionWeight);

        if (_colorHistory != null) sheet.properties.SetTexture(ShaderIDs.ColorHistory, _colorHistory);
        if (_prevMotionDepth != null) sheet.properties.SetTexture(ShaderIDs.PrevMotionDepth, _prevMotionDepth);

        sheet.properties.SetVector(ShaderIDs.DeltaTime, new Vector2(Time.deltaTime, _prevDeltaTime));

        // Apply the shader (0:init, 1:update)
        var setupPass = (_frameCount++ % Mathf.Max(1, settings.sampleInterval) == 0) ? 0 : 1;
        cmd.BlitFullscreenTriangle(context.source, _mrt, newColorHistory.depthBuffer, sheet, setupPass);

        // Second pass: Composition
        cmd.BlitFullscreenTriangle(newColorHistory, context.destination, sheet, 2);

        // Discard the previous frame state.
        if (_colorHistory != null) RenderTexture.ReleaseTemporary(_colorHistory);
        if (_prevMotionDepth != null) RenderTexture.ReleaseTemporary(_prevMotionDepth);

        // Update the internal state.
        _colorHistory = newColorHistory;
        _prevMotionDepth = newMotionDepth;
        _prevDeltaTime = Time.deltaTime;

        cmd.EndSample("TemporalReprojection");
    }
}

#endregion
