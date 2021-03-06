﻿using System.Collections;
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
            _buffers[i] = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
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
        public int InteractionForceID;
        public int UpdateDivergenceID;
        public int UpdatePressureID;
        public int UpdateVelocityID;
        public int UpdateTextureID;
    }

    private struct PropertyDef
    {
        public int DeltaTimeID;
        public int ScaleID;
        public int SourceVelocityID;
        public int UpdateVelocityID;
        public int ResultVelocityID;
        public int SourcePressureID;
        public int ResultPressureID;
        public int ResultDivergenceID;
        public int SourceTextureID;
        public int ResultTextureID;
        public int CursorID;
        public int VelocityID;
        public int WidthID;
        public int HeightID;
        public int AttenuationID;
    }

    [SerializeField] private ComputeShader _shader = null;
    [SerializeField] private Texture2D _texture = null;
    [SerializeField] private RawImage _preview = null;
    [SerializeField] private RawImage _velocityPreview = null;
    [SerializeField] private RawImage _divergencePreview = null;
    [SerializeField] private RawImage _pressurePreview = null;
    [SerializeField] private float _attenuation = 0.99f;
    [SerializeField] private float _numCalcPressure = 20;

    private float _scale = 1.0f;

    private KernelDef _kernelDef = default;
    private PropertyDef _propertyDef = default;

    private RenderTexture _divergenceTexture = null;

    private SwapBuffer _velocityBuffer = null;
    private SwapBuffer _pressureBuffer = null;
    private SwapBuffer _previewBuffer = null;

    private Vector3 _mouseVelocity = Vector3.zero;
    private Vector3 _currentMouse = Vector3.zero;
    private Vector3 _prevMouse = Vector3.zero;

    private void Start()
    {
        Initialize();
        GetAllPropertyID();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _currentMouse = GetMousePosition();
            _prevMouse = _currentMouse;
        }

        if (Input.GetMouseButton(0))
        {
            _currentMouse = GetMousePosition();
            CalculateVelocity();
        }

        if (Input.GetMouseButtonUp(0))
        {
            _mouseVelocity = Vector3.zero;
        }

        BasicSetup();

        UpdateAdvection();
        InteractionForce();
        UpdateDivergence();
        UpdatePressure();
        UpdateVelocity();
        UpdateTexture();

        UpdatePreview();
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
        _scale = _texture.width / _preview.rectTransform.rect.width;

        CreateBuffers();

        InitializeKernel();
    }

    private Vector3 GetMousePosition()
    {
        Vector3 mPos = Input.mousePosition;
        Vector3[] corners = new Vector3[4];
        _preview.rectTransform.GetWorldCorners(corners);

        float x = mPos.x - corners[0].x;
        if (x < 0)
        {
            return default;
        }

        float y = mPos.y - corners[0].y;
        if (y < 0)
        {
            return default;
        }

        if (x > _preview.rectTransform.rect.width)
        {
            return default;
        }

        if (y > _preview.rectTransform.rect.height)
        {
            return default;
        }

        return new Vector3(x, y, 0);
    }

    private void CalculateVelocity()
    {
        Vector4 delta = _currentMouse - _prevMouse;
        _mouseVelocity = delta / Time.deltaTime;
        _prevMouse = _currentMouse;
    }

    private void UpdatePreview()
    {
        _velocityPreview.texture = _velocityBuffer.Current;
        _divergencePreview.texture = _divergenceTexture;
        _pressurePreview.texture = _pressureBuffer.Current;
        _preview.texture = _previewBuffer.Current;
    }

    private void CreateBuffers()
    {
        _velocityBuffer = new SwapBuffer(_texture.width, _texture.height);
        _pressureBuffer = new SwapBuffer(_texture.width, _texture.height);
        _previewBuffer = new SwapBuffer(_texture.width, _texture.height);

        Graphics.Blit(_texture, _previewBuffer.Current);

        _divergenceTexture = new RenderTexture(_texture.width, _texture.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        _divergenceTexture.enableRandomWrite = true;
        _divergenceTexture.Create();
    }

    private void BasicSetup()
    {
        _shader.SetFloat(_propertyDef.WidthID, _texture.width);
        _shader.SetFloat(_propertyDef.HeightID, _texture.height);
        _shader.SetFloat(_propertyDef.DeltaTimeID, Time.deltaTime);
        _shader.SetFloat(_propertyDef.ScaleID, _scale);
        _shader.SetVector(_propertyDef.CursorID, _currentMouse);
        _shader.SetVector(_propertyDef.VelocityID, _mouseVelocity);
        _shader.SetFloat(_propertyDef.AttenuationID, _attenuation);
    }

    private void InitializeKernel()
    {
        _kernelDef.UpdateAdvectionID = _shader.FindKernel("UpdateAdvection");
        _kernelDef.InteractionForceID = _shader.FindKernel("InteractionForce");
        _kernelDef.UpdateDivergenceID = _shader.FindKernel("UpdateDivergence");
        _kernelDef.UpdatePressureID = _shader.FindKernel("UpdatePressure");
        _kernelDef.UpdateVelocityID = _shader.FindKernel("UpdateVelocity");
        _kernelDef.UpdateTextureID = _shader.FindKernel("UpdateTexture");
    }

    private void GetAllPropertyID()
    {
        _propertyDef.DeltaTimeID = Shader.PropertyToID("_DeltaTime");
        _propertyDef.ScaleID = Shader.PropertyToID("_Scale");
        _propertyDef.SourceVelocityID = Shader.PropertyToID("_SourceVelocity");
        _propertyDef.UpdateVelocityID = Shader.PropertyToID("_UpdateVelocity");
        _propertyDef.ResultVelocityID = Shader.PropertyToID("_ResultVelocity");
        _propertyDef.SourcePressureID = Shader.PropertyToID("_SourcePressure");
        _propertyDef.ResultPressureID = Shader.PropertyToID("_ResultPressure");
        _propertyDef.ResultDivergenceID = Shader.PropertyToID("_ResultDivergence");
        _propertyDef.SourceTextureID = Shader.PropertyToID("_SourceTexture");
        _propertyDef.ResultTextureID = Shader.PropertyToID("_ResultTexture");
        _propertyDef.CursorID = Shader.PropertyToID("_Cursor");
        _propertyDef.VelocityID = Shader.PropertyToID("_Velocity");
        _propertyDef.WidthID = Shader.PropertyToID("_Width");
        _propertyDef.HeightID = Shader.PropertyToID("_Height");
        _propertyDef.AttenuationID = Shader.PropertyToID("_Attenuation");
    }

    private void UpdateAdvection()
    {
        _shader.SetTexture(_kernelDef.UpdateAdvectionID, _propertyDef.SourceVelocityID, _velocityBuffer.Current);
        _shader.SetTexture(_kernelDef.UpdateAdvectionID, _propertyDef.UpdateVelocityID, _velocityBuffer.Current);
        _shader.SetTexture(_kernelDef.UpdateAdvectionID, _propertyDef.ResultVelocityID, _velocityBuffer.Other);

        _shader.Dispatch(_kernelDef.UpdateAdvectionID, _velocityBuffer.Width / 8, _velocityBuffer.Height / 8, 1);

        _velocityBuffer.Swap();
    }

    private void InteractionForce()
    {
        _shader.SetTexture(_kernelDef.InteractionForceID, _propertyDef.SourceVelocityID, _velocityBuffer.Current);
        _shader.SetTexture(_kernelDef.InteractionForceID, _propertyDef.ResultVelocityID, _velocityBuffer.Other);

        _shader.Dispatch(_kernelDef.InteractionForceID, _velocityBuffer.Width / 8, _velocityBuffer.Height / 8, 1);

        _velocityBuffer.Swap();
    }

    private void UpdateDivergence()
    {
        _shader.SetTexture(_kernelDef.UpdateDivergenceID, _propertyDef.SourceVelocityID, _velocityBuffer.Current);
        _shader.SetTexture(_kernelDef.UpdateDivergenceID, _propertyDef.ResultDivergenceID, _divergenceTexture);

        _shader.Dispatch(_kernelDef.UpdateDivergenceID, _divergenceTexture.width / 8, _divergenceTexture.height / 8, 1);
    }

    private void UpdatePressure()
    {
        for (int i = 0; i < _numCalcPressure; i++)
        {
            _shader.SetTexture(_kernelDef.UpdatePressureID, _propertyDef.SourcePressureID, _pressureBuffer.Current);
            _shader.SetTexture(_kernelDef.UpdatePressureID, _propertyDef.ResultPressureID, _pressureBuffer.Other);
            _shader.SetTexture(_kernelDef.UpdatePressureID, _propertyDef.ResultDivergenceID, _divergenceTexture);

            _shader.Dispatch(_kernelDef.UpdatePressureID, _pressureBuffer.Width / 8, _pressureBuffer.Height / 8, 1);

            _pressureBuffer.Swap();
        }
    }

    private void UpdateVelocity()
    {
        _shader.SetTexture(_kernelDef.UpdateVelocityID, _propertyDef.SourceVelocityID, _velocityBuffer.Current);
        _shader.SetTexture(_kernelDef.UpdateVelocityID, _propertyDef.SourcePressureID, _pressureBuffer.Current);
        _shader.SetTexture(_kernelDef.UpdateVelocityID, _propertyDef.ResultVelocityID, _velocityBuffer.Other);

        _shader.Dispatch(_kernelDef.UpdateVelocityID, _velocityBuffer.Width / 8, _velocityBuffer.Height / 8, 1);

        _velocityBuffer.Swap();
    }

    private void UpdateTexture()
    {
        _shader.SetTexture(_kernelDef.UpdateTextureID, _propertyDef.SourceTextureID, _previewBuffer.Current);
        _shader.SetTexture(_kernelDef.UpdateTextureID, _propertyDef.SourceVelocityID, _velocityBuffer.Current);
        _shader.SetTexture(_kernelDef.UpdateTextureID, _propertyDef.ResultTextureID, _previewBuffer.Other);

        _shader.Dispatch(_kernelDef.UpdateTextureID, _previewBuffer.Width / 8, _previewBuffer.Height / 8, 1);

        _previewBuffer.Swap();
    }
}

