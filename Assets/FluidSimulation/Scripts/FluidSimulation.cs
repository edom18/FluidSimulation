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
    private Texture2D _texture2 = null;

    [SerializeField]
    private RawImage _rawImage = null;

    private int _kernelId = 0;
    private RenderTexture _outTex;

    void Start()
    {
        _outTex = new RenderTexture(_texture.width, _texture.height, 0);
        _outTex.enableRandomWrite = true;
        _outTex.Create();

        _rawImage.texture = _outTex;

        _kernelId = _shader.FindKernel("CSMain");

        _shader.SetTexture(_kernelId, "inTex", _texture);
        _shader.SetTexture(_kernelId, "inTex2", _texture2);
        _shader.SetTexture(_kernelId, "outTex", _outTex);

        _shader.Dispatch(_kernelId, _texture.width / 8, _texture.height / 8, 1);
    }
}
