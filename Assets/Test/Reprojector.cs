using UnityEngine;

[RequireComponent(typeof(Camera))]
public class Reprojector : MonoBehaviour
{
    [SerializeField] Shader _shader;
    [SerializeField] int _sampleInterval = 60;
    [SerializeField] bool _searchClosest;

    RenderTexture _historyTexture;
    Material _material;
    int _frameCount;

    void Start()
    {
        _material = new Material(_shader);
    }

    void OnDisable()
    {
        if (_historyTexture != null)
        {
            RenderTexture.ReleaseTemporary(_historyTexture);
            _historyTexture = null;
        }
    }

    void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }

    void OnPreCull()
    {
        var camera = GetComponent<Camera>();

        camera.depthTextureMode |=
            DepthTextureMode.MotionVectors | DepthTextureMode.Depth;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        var oldHistory = _historyTexture;
        var newHistory = RenderTexture.GetTemporary(source.width, source.height, 0, source.format);

        if (_frameCount++ % _sampleInterval == 0)
        {
            Graphics.Blit(source, newHistory);
            Graphics.Blit(source, destination);
        }
        else
        {
            if (_searchClosest)
                _material.EnableKeyword("_SEARCH_CLOSEST");
            else
                _material.DisableKeyword("_SEARCH_CLOSEST");

            _material.SetTexture("_HistoryTex", oldHistory);
            Graphics.Blit(source, newHistory, _material, 0);

            _material.SetTexture("_HistoryTex", newHistory);
            Graphics.Blit(source, destination, _material, 1);
        }

        if (oldHistory != null) RenderTexture.ReleaseTemporary(oldHistory);
        _historyTexture = newHistory;
    }
}
