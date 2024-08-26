using System;
using System.Collections.Generic;
using UnityEngine;
using static ToonMapCommon;

public class GradientMapGenerator : IDisposable
{
    public static ComputeShader ComputeShader { get => _computeShader; }
    public RenderTexture GradientMap { get => _gradientMap; }

    private static ComputeShader _computeShader = default;

    private Vector2Int _resolution
    {
        get
        {
            return new Vector2Int(_data.Resolution, _data.GetCurrentGradientList().Length);
        }
    }
    private ToonMapGeneratorData _data;
    private ComputeBuffer _keyBuffer;
    private ComputeBuffer _paramBuffer;
    private RenderTexture _gradientMap;

    public GradientMapGenerator(ToonMapGeneratorData data)
    {
        _computeShader = LoadComputeShader();
        _data = data;
    }

    public void GenerateGradientMapAndSave(string path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            GenerateGradientMapByComputeShader();
            DumpRenderTextureToScreenThenSave(path, _data.Resolution, _resolution.y, _data.HDR, _gradientMap);
            _keyBuffer.Release();
        }
    }

    void ReallocGradientMap()
    {
        if (_gradientMap != null)
            _gradientMap.Release();

        _gradientMap = new RenderTexture(_data.Resolution, _resolution.y, 0, _data.HDR ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32);
        _gradientMap.filterMode = FilterMode.Point;
        _gradientMap.wrapModeV = TextureWrapMode.Repeat;
        _gradientMap.enableRandomWrite = true;
        _gradientMap.Create();
    }

    public void GenerateGradientMapByComputeShader()
    {
        if (_computeShader)
        {
            if (!_computeShader.HasKernel("GradientMapGenerator"))
            {
                return;
            }

            bool needRealloc = _gradientMap == null || _resolution != new Vector2Int(_gradientMap.width, _gradientMap.height);
            if (needRealloc)
            {
                ReallocGradientMap();
            }

            Gradient[] gradients = _data.GetCurrentGradientList();

            int length = gradients.Length;
            int kernel = _computeShader.FindKernel("GradientMapGenerator");
            List<GradientKeyData> keyData = new List<GradientKeyData>(length);
            GradientParameterData[] paramData = new GradientParameterData[length];

            _keyBuffer = new ComputeBuffer(length * MaxKeys, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GradientKeyData)));
            _paramBuffer = new ComputeBuffer(length, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GradientParameterData)));
            for (int i = 0; i < length; i++)
            {
                var data = GetGradientData(gradients[i]);

                keyData.AddRange(data.keys);
                paramData[i] = data.parameter;
            }

            _keyBuffer.SetData(keyData);
            _paramBuffer.SetData(paramData);

            _computeShader.SetTexture(kernel, "_GradientMap", _gradientMap);
            _computeShader.SetBuffer(kernel, "_GradientKeyData", _keyBuffer);
            _computeShader.SetBuffer(kernel, "_GradientParameterData", _paramBuffer);
            _computeShader.SetFloat("_GradientHardness", _data.Hardness);

            int threadGroupAmount = Mathf.CeilToInt(_data.Resolution / 8.0f);
            _computeShader.Dispatch(kernel, threadGroupAmount, length, 1);
            Debug.Log("Generate GradientMap");

            _keyBuffer.Release();
            _paramBuffer.Release();
        }
    }

    public void Dispose()
    {
        if (_keyBuffer != null)
        {
            _keyBuffer.Release();
        }
        if (_paramBuffer != null)
        {
            _paramBuffer.Release();
        }
        if (_gradientMap != null)
        {
            _gradientMap.Release();
        }
    }
}
