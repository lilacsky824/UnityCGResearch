using System;
using UnityEngine;
using static ToonMapCommon;

/// <summary>
/// Generate and save a RampMap using a ComputeShader based on the input Gradient.
/// </summary>
public class RampMapGenerator : IDisposable
{
    public static ComputeShader ComputeShader { get => _computeShader; }
    public RenderTexture RampMap { get => _rampMap; }

    private static ComputeShader _computeShader = default;
    private ToonMapGeneratorData _data;
    private ComputeBuffer _keyBuffer;
    private ComputeBuffer _paramBuffer;
    private RenderTexture _rampMap;

    public RampMapGenerator(ToonMapGeneratorData data)
    {
        _computeShader = LoadComputeShader();
        _data = data;
    }

    public void GenerateRampMapAndSave(string path)
    {
        if (path.Length != 0)
        {
            GenerateRampMapByComputeShader();
            DumpRenderTextureToScreenThenSave(path, _data.Resolution, _data.Resolution, false, _rampMap);
        }
    }

    public void GenerateRampMapByComputeShader()
    {
        if (_computeShader)
        {
            if (!_computeShader.HasKernel("RampMapGenerator"))
            {
                return;
            }

            bool needsRealloc = _rampMap == null ||
                                _rampMap.width != _rampMap.width ||
                                _rampMap.height != _rampMap.height;

            if (needsRealloc)
            {
                if (_rampMap != null)
                    _rampMap.Release();

                _rampMap = new RenderTexture(_data.Resolution, _data.Resolution, 0, RenderTextureFormat.ARGB32);
                _rampMap.enableRandomWrite = true;
                _rampMap.Create();
            }

            _keyBuffer = new ComputeBuffer(MaxKeys, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GradientKeyData)));
            _paramBuffer = new ComputeBuffer(1, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GradientParameterData)));

            var data = GetGradientData(_data.RampGradient);

            _keyBuffer.SetData(data.keys);
            _paramBuffer.SetData(new GradientParameterData[] { data.parameter });

            int kernel = _computeShader.FindKernel("RampMapGenerator");

            _computeShader.SetTexture(kernel, "_RampMap", _rampMap);
            _computeShader.SetBuffer(kernel, "_GradientKeyData", _keyBuffer);
            _computeShader.SetBuffer(kernel, "_GradientParameterData", _paramBuffer);
            _computeShader.SetFloat("_GradientHardness", _data.Hardness);

            int threadGroupAmount = Mathf.CeilToInt(_data.Resolution / 8.0f);
            _computeShader.Dispatch(kernel, threadGroupAmount, threadGroupAmount, 1);
            Debug.Log("Generate RampMap");

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
        if (_rampMap != null)
        {
            _rampMap.Release();
        }
    }
}
