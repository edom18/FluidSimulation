using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

public class SwapBuffer
{
    private RenderTexture[] _buffers = new RenderTexture[2];

    public RenderTexture Current => _buffers[0];
    public RenderTexture Other => _buffers[1];

    private int _width = 0;
    private int _height = 0;

    public int Width => _width;
    public int Height => _height;

    public SwapBuffer(int width, int height)
    {
        _width = width;
        _height = height;

        for (int i = 0; i < _buffers.Length; i++)
        {
            _buffers[i] = new RenderTexture(width, height, 0);
            _buffers[i].enableRandomWrite = true;
            _buffers[i].Create();
        }
    }

    public void Swap()
    {
        RenderTexture temp = _buffers[0];
        _buffers[0] = _buffers[1];
        _buffers[1] = temp;
    }

    public void Release()
    {
        foreach (var buf in _buffers)
        {
            buf.Release();
        }
    }
}

public class FluidSimulation : MonoBehaviour
{
    private struct KernelDef
    {
        public int UpdateAdvectionID;
        public int UpdateDivergenceID;
        public int UpdatePressureID;
        public int UpdateVelocityID;
        public int UpdateTextureID;
    }
    [SerializeField] private ComputeShader _shader = null;
    [SerializeField] private Texture2D _texture = null;
    [SerializeField] private RawImage _preview = null;
    [SerializeField] private RawImage _velocityPreview = null;
    [SerializeField] private float _alpha = 1.0f;
    [SerializeField] private float _beta = 0.25f;
    [SerializeField] private float _noiseScale = 100f;
    [SerializeField] private float _numCalcPressure = 20;

    private KernelDef _kernelDef;

    private RenderTexture _divergenceTexture = null;

    private SwapBuffer _velocityBuffer = null;
    private SwapBuffer _pressureBuffer = null;
    private SwapBuffer _previewBuffer = null;

    private void Start()
    {
        Initialize();
    }

    private void Update()
    {
        UpdateAdvection();
        UpdateDivergence();
        UpdatePressure();
        UpdateVelocity();
        UpdateTexture();
    }

    private void OnDestroy()
    {
        _divergenceTexture.Release();

        _velocityBuffer.Release();
        _pressureBuffer.Release();
        _previewBuffer.Release();
    }

    private void Initialize()
    {
        CreateBuffers();

        InitializeKernel();

        _velocityPreview.texture = _velocityBuffer.Current;
        _preview.texture = _previewBuffer.Current;
    }

    private void CreateBuffers()
    {
        _velocityBuffer = new SwapBuffer(_texture.width, _texture.height);
        _pressureBuffer = new SwapBuffer(_texture.width, _texture.height);
        _previewBuffer = new SwapBuffer(_texture.width, _texture.height);

        Graphics.CopyTexture(_texture, 0, 0, _previewBuffer.Current, 0, 0);

        _divergenceTexture = new RenderTexture(_texture.width, _texture.height, 0);
        _divergenceTexture.enableRandomWrite = true;
        _divergenceTexture.Create();

        float min = -0.1f;
        float max = 0.1f;
        Texture2D noiseTex = CreatePerlinNoiseTexture.Create(_texture.width, _texture.height, min, max, _noiseScale);
        Graphics.CopyTexture(noiseTex, 0, 0, _velocityBuffer.Current, 0, 0);
    }

    private void InitializeKernel()
    {
        _kernelDef.UpdateAdvectionID = _shader.FindKernel("UpdateAdvection");
        _kernelDef.UpdateDivergenceID = _shader.FindKernel("UpdateDivergence");
        _kernelDef.UpdatePressureID = _shader.FindKernel("UpdatePressure");
        _kernelDef.UpdateVelocityID = _shader.FindKernel("UpdateVelocity");
        _kernelDef.UpdateTextureID = _shader.FindKernel("UpdateTexture");
    }

    private void UpdateAdvection()
    {
        _shader.SetFloat("_DeltaTime", Time.deltaTime);
        _shader.SetFloat("_Scale", 1.0f);

        _shader.SetTexture(_kernelDef.UpdateAdvectionID, "_SourceVelocity", _velocityBuffer.Current);
        _shader.SetTexture(_kernelDef.UpdateAdvectionID, "_UpdateVelocity", _velocityBuffer.Current);
        _shader.SetTexture(_kernelDef.UpdateAdvectionID, "_ResultVelocity", _velocityBuffer.Other);

        _shader.Dispatch(_kernelDef.UpdateAdvectionID, _velocityBuffer.Width / 8, _velocityBuffer.Height / 8, 1);

        _velocityBuffer.Swap();

        _velocityPreview.texture = _velocityBuffer.Current;
    }

    private void UpdateDivergence()
    {
        _shader.SetTexture(_kernelDef.UpdateDivergenceID, "_SourceVelocity", _velocityBuffer.Current);
        _shader.SetTexture(_kernelDef.UpdateDivergenceID, "_ResultDivergence", _divergenceTexture);

        _shader.Dispatch(_kernelDef.UpdateAdvectionID, _divergenceTexture.width / 8, _divergenceTexture.height / 8, 1);
    }

    private void UpdatePressure()
    {
        _shader.SetFloat("_Alpha", _alpha);
        _shader.SetFloat("_Beta", _beta);

        for (int i = 0; i < _numCalcPressure; i++)
        {
            _shader.SetTexture(_kernelDef.UpdatePressureID, "_SourcePressure", _pressureBuffer.Current);
            _shader.SetTexture(_kernelDef.UpdatePressureID, "_ResultPressure", _pressureBuffer.Other);
            _shader.SetTexture(_kernelDef.UpdatePressureID, "_ResultDivergence", _divergenceTexture);

            _shader.Dispatch(_kernelDef.UpdatePressureID, _pressureBuffer.Width / 8, _pressureBuffer.Height / 8, 1);

            _pressureBuffer.Swap();
        }
    }

    private void UpdateVelocity()
    {
        _shader.SetFloat("_Scale", 1.0f);

        _shader.SetTexture(_kernelDef.UpdateVelocityID, "_SourceVelocity", _velocityBuffer.Current);
        _shader.SetTexture(_kernelDef.UpdateVelocityID, "_SourcePressure", _pressureBuffer.Current);
        _shader.SetTexture(_kernelDef.UpdateVelocityID, "_ResultVelocity", _velocityBuffer.Other);

        _shader.Dispatch(_kernelDef.UpdateVelocityID, _velocityBuffer.Width / 8, _velocityBuffer.Height / 8, 1);
    }

    private void UpdateTexture()
    {
        _shader.SetTexture(_kernelDef.UpdateTextureID, "_SourceTexture", _previewBuffer.Current);
        _shader.SetTexture(_kernelDef.UpdateTextureID, "_SourceVelocity", _velocityBuffer.Current);
        _shader.SetTexture(_kernelDef.UpdateTextureID, "_ResultTexture", _previewBuffer.Other);

        _shader.Dispatch(_kernelDef.UpdateTextureID, _previewBuffer.Width / 8, _previewBuffer.Height / 8, 1);

        _previewBuffer.Swap();

        _preview.texture = _previewBuffer.Current;
    }
}
