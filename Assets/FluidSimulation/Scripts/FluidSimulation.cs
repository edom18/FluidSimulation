﻿using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

    private int _kernelUpdateId = 0;
    private int _kernelVelocityId = 0;
    private RenderTexture[] _buffers = new RenderTexture[2];
    private RenderTexture InTex { get { return _buffers[0]; } }
    private RenderTexture OutTex { get { return _buffers[1]; } }

    private RenderTexture _noiseRenderTex = null;
    private RenderTexture _velocityRenderTex = null;

    private void Start()
    {
        Initialize();
    }

    private void Update()
    {
        UpdateTexture();
        UpdateVelocity();
    }

    private void OnDestroy()
    {
        _noiseRenderTex.Release();
        _velocityRenderTex.Release();
        
        foreach (var buf in _buffers)
        {
            buf.Release();
        }
    }

    private void Initialize()
    {
        for (int i = 0; i < _buffers.Length; i++)
        {
            _buffers[i] = new RenderTexture(_texture.width, _texture.height, 0);
            _buffers[i].enableRandomWrite = true;
            _buffers[i].Create();
        }

        _noiseRenderTex = new RenderTexture(_texture.width, _texture.height, 0);
        _noiseRenderTex.enableRandomWrite = true;
        _noiseRenderTex.Create();

        _velocityRenderTex = new RenderTexture(_texture.width, _texture.height, 0);
        _velocityRenderTex.enableRandomWrite = true;
        _velocityRenderTex.Create();

        _kernelUpdateId = _shader.FindKernel("Update");
        _kernelVelocityId = _shader.FindKernel("UpdateVelocity");

        int kernel = _shader.FindKernel("CopyTex");
        _shader.SetTexture(kernel, "inTex", _texture);
        _shader.SetTexture(kernel, "outTex", OutTex);
        _shader.Dispatch(kernel, _texture.width / 8, _texture.height / 8, 1);

        float min = -0.01f;
        float max =  0.01f;

        Texture2D noiseTex = CreatePerlinNoiseTexture.Create(_texture.width, _texture.height, min, max, _noiseScale);
        _shader.SetTexture(kernel, "inTex", noiseTex);
        _shader.SetTexture(kernel, "outTex", _noiseRenderTex);
        _shader.Dispatch(kernel, noiseTex.width / 8, noiseTex.height / 8, 1);

        Texture2D velocityTex = CreatePerlinNoiseTexture.Create(_texture.width, _texture.height, min, max, _noiseScale * 125f);
        _shader.SetTexture(kernel, "inTex", velocityTex);
        _shader.SetTexture(kernel, "outTex", _velocityRenderTex);
        _shader.Dispatch(kernel, velocityTex.width / 8, velocityTex.height / 8, 1);

        _noisePreview.texture = _noiseRenderTex;
        _rawImage.texture = OutTex;

        SwapBuffer();
    }

    private void SwapBuffer()
    {
        RenderTexture tmp = _buffers[1];
        _buffers[1] = _buffers[0];
        _buffers[0] = tmp;
    }

    private void UpdateVelocity()
    {
        _shader.SetTexture(_kernelVelocityId, "inNoiseTex", _noiseRenderTex);
        _shader.SetTexture(_kernelVelocityId, "inVelocityTex", _velocityRenderTex);
        _shader.SetTexture(_kernelVelocityId, "outNoiseTex", _noiseRenderTex);

        _shader.Dispatch(_kernelVelocityId, _texture.width / 8, _texture.height / 8, 1);
    }

    private void UpdateTexture()
    {
        _shader.SetTexture(_kernelUpdateId, "inNoiseTex", _noiseRenderTex);
        _shader.SetTexture(_kernelUpdateId, "inTex", InTex);
        _shader.SetTexture(_kernelUpdateId, "outTex", OutTex);

        _shader.Dispatch(_kernelUpdateId, _texture.width / 8, _texture.height / 8, 1);

        SwapBuffer();

        _rawImage.texture = OutTex;
    }
}
