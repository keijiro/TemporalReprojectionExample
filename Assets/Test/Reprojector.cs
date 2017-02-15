using UnityEngine;

[RequireComponent(typeof(Camera))]
public class Reprojector : MonoBehaviour
{
    #region Editor properties

    [Space]
    [SerializeField] float _motionWeight = 10;
    [SerializeField] float _depthWeight = 2000;

    [Space]
    [SerializeField] int _sampleInterval = 60;

    #endregion

    #region Private members

    [SerializeField, HideInInspector] Shader _shader;

    Material _material;

    RenderTexture _colorHistory;
    RenderTexture _prevMotionDepth;
    RenderBuffer[] _mrt;

    float _prevDeltaTime;
    int _frameCount;

    #endregion

    #region MonoBehaviour functions

    void Start()
    {
        _material = new Material(_shader);
        _mrt = new RenderBuffer[2];
    }

    void OnDisable()
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
    }

    void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }

    void OnPreCull()
    {
        var camera = GetComponent<Camera>();
        camera.depthTextureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        var width = source.width;
        var height = source.height;

        var colorFormat = source.format;
        var motionDepthFormat = RenderTextureFormat.ARGBHalf;

        var newColorHistory = RenderTexture.GetTemporary(width, height, 0, colorFormat);
        var newMotionDepth = RenderTexture.GetTemporary(width, height, 0, motionDepthFormat);

        _material.SetFloat("_DepthWeight", _depthWeight);
        _material.SetFloat("_MotionWeight", _motionWeight);

        _material.SetTexture("_ColorHistory", _colorHistory);
        _material.SetTexture("_PrevMotionDepth", _prevMotionDepth);

        _material.SetVector("_DeltaTime", new Vector2(Time.deltaTime, _prevDeltaTime));

        _mrt[0] = newColorHistory.colorBuffer;
        _mrt[1] = newMotionDepth.colorBuffer;
        Graphics.SetRenderTarget(_mrt, Graphics.activeDepthBuffer);

        var setupPass = _frameCount++ % _sampleInterval == 0 ? 0 : 1;
        Graphics.Blit(source, _material, setupPass);

        _material.SetTexture("_ColorHistory", newColorHistory);
        Graphics.Blit(source, destination, _material, 2);

        if (_colorHistory != null) RenderTexture.ReleaseTemporary(_colorHistory);
        if (_prevMotionDepth != null) RenderTexture.ReleaseTemporary(_prevMotionDepth);

        _colorHistory = newColorHistory;
        _prevMotionDepth = newMotionDepth;
        _prevDeltaTime = Time.deltaTime;
    }

    #endregion
}
