using UnityEngine;

public class ToonMapGeneratorData : ScriptableObject
{
    public int Resolution = 128;
    public float Hardness = 0.0f;
    public Gradient RampGradient;
    public Gradient[] GradientList = new Gradient[1];
    [GradientUsage(true)]
    public Gradient[] HDRGradientList = new Gradient[1];
    public bool HDR;
    public bool ShowAlpha;
    public GreyscaleSourceType SourceRemapType;
    public enum GreyscaleSourceType { Greyscale, Hue, Saturation, Brightness, SaturationAndBrightness }

    public Gradient[] GetCurrentGradientList()
    {
        return HDR ? HDRGradientList : GradientList;
    }
}
