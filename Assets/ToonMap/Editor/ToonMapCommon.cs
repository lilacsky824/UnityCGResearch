using System.IO;
using UnityEngine;
using UnityEditor;
using System;
using Unity.Mathematics;

public static class ToonMapCommon
{
    public const int MaxKeys = 8;
    [Serializable]
    public struct GradientKeyData
    {
        public float4 color;  // w is Time
        public float2 alpha;  // w is Time
    }

    [Serializable]
    public struct GradientParameterData
    {
        public uint colorLength;
        public uint alphaLength;
        //public uint mode;
    }

    public static (GradientKeyData[] keys, GradientParameterData parameter) GetGradientData(Gradient gradient)
    {
        GradientKeyData[] keys = new GradientKeyData[MaxKeys];
        GradientParameterData parameters = new GradientParameterData
        {
            colorLength = (uint)gradient.colorKeys.Length,
            alphaLength = (uint)gradient.alphaKeys.Length,
            //mode = (uint)gradient.mode
        };

        for (int i = 0; i < MaxKeys; i++)
        {
            GradientKeyData key = new GradientKeyData();

            int colorIndex = i >= parameters.colorLength ? (int)parameters.colorLength - 1 : i;
            GradientColorKey colorKey = gradient.colorKeys[colorIndex];
            key.color = new float4(colorKey.color.r, colorKey.color.g, colorKey.color.b, colorKey.time);

            int alphaIndex = i >= parameters.alphaLength ? (int)parameters.alphaLength - 1 : i;
            GradientAlphaKey alphaKey = gradient.alphaKeys[alphaIndex];
            key.alpha = new float2(alphaKey.alpha, alphaKey.time);

            keys[i] = key;
        }

        return (keys, parameters);
    }

    const string _settingDataDefaultPath = "Assets/Settings/GradientListData.asset";
    public static ToonMapGeneratorData  GetToonMapData()
    {
        ToonMapGeneratorData data = null;
        string[] guids = AssetDatabase.FindAssets($"t:{nameof(ToonMapGeneratorData)}");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            data = AssetDatabase.LoadAssetAtPath<ToonMapGeneratorData>(path);
        }

        if (data == null)
        {
            data = ScriptableObject.CreateInstance<ToonMapGeneratorData>();
            AssetDatabase.CreateAsset(data, _settingDataDefaultPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        return data;
    }

    public static ComputeShader LoadComputeShader()
    {
        string[] guid = AssetDatabase.FindAssets($"ToonMapGeneratorCompute t:{nameof(ComputeShader)}");
        if (guid.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid[0]);
            return AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
        }
        else
        {
            Debug.LogError("Compute Shader not found.");
        }

        return null;
    }

    public static void DumpRenderTextureToScreenThenSave(string path, int width, int height, bool isHDR, RenderTexture src)
    {
        Texture2D temp = new Texture2D(width, height, isHDR ? TextureFormat.RGBAHalf : TextureFormat.ARGB32, false, true);
        ConvertRenderTextureToTexture2D.Convert(src, temp, false);
        byte[] pixelBytes = isHDR ? temp.EncodeToEXR() : temp.EncodeToPNG();
        File.WriteAllBytes(path, pixelBytes);

        UnityEditor.AssetDatabase.Refresh();

        if(path.StartsWith(Application.dataPath))
        {
            SetTextureImportSettings(path);
        }
    }

    public static void SetTextureImportSettings(string path)
    {
        // Ensure the path is relative to the Assets folder
        path = "Assets" + path.Substring(Application.dataPath.Length);

        // Get the TextureImporter for the given path
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer != null)
        {
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // Save the changes to the asset
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        else
        {
            Debug.LogError("Failed to get TextureImporter for path: " + path);
        }
    }
}
