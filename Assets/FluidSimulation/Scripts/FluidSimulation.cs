using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FluidSimulation : MonoBehaviour
{
    [SerializeField]
    private ComputeShader _shader = null;

    [SerializeField]
    private Texture2D _texture = null;

    [SerializeField]
    private RawImage _rawImage = null;

    [SerializeField]
    private RawImage _noisePreview = null;

    [SerializeField]
    private float _noiseScale = 100f;

    private int _kernelId = 0;
    private RenderTexture InTex { get { return _buffers[0]; } }
    private RenderTexture OutTex { get { return _buffers[1]; } }
    private RenderTexture[] _buffers = new RenderTexture[2];
    private Texture2D _noiseTex = null;

    private void Start()
    {
        Initialize();

        _rawImage.texture = OutTex;
        _noisePreview.texture = _noiseTex;
    }

    private void Update()
    {
        UpdateTexture();
    }

    private void Initialize()
    {
        _noiseTex = CreatePerlinNoiseTexture.Create(_texture.width, _texture.height, _noiseScale);

        for (int i = 0; i < _buffers.Length; i++)
        {
            _buffers[i] = new RenderTexture(_texture.width, _texture.height, 0);
            _buffers[i].enableRandomWrite = true;
            _buffers[i].Create();
        }

        _kernelId = _shader.FindKernel("Update");

        int initKernel = _shader.FindKernel("Init");
        _shader.SetTexture(initKernel, "inTex", _texture);
        _shader.SetTexture(initKernel, "outTex", OutTex);
        _shader.Dispatch(initKernel, _texture.width / 8, _texture.height / 8, 1);

        SwapBuffer();
    }

    private void SwapBuffer()
    {
        RenderTexture tmp = _buffers[1];
        _buffers[1] = _buffers[0];
        _buffers[0] = tmp;
    }

    private void UpdateTexture()
    {
        _shader.SetTexture(_kernelId, "noiseTex", _noiseTex);
        _shader.SetTexture(_kernelId, "inTex", InTex);
        _shader.SetTexture(_kernelId, "outTex", OutTex);

        _shader.Dispatch(_kernelId, _texture.width / 8, _texture.height / 8, 1);

        SwapBuffer();

        _rawImage.texture = OutTex;
    }
}
