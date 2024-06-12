using System.IO;
using UnityEngine;
using UnityEditor;

public static class ToonMapCommon
{
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

    public static void DumpRenderTextureToScreenThenSave(string path, int width, int height, RenderTexture src)
    {
        Texture2D temp = new Texture2D(width, height, TextureFormat.ARGB32, false, true);
        ConvertRenderTextureToTexture2D.Convert(src, temp, false);
        byte[] pixelBytes = temp.EncodeToPNG();
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

            // Save the changes to the asset
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        else
        {
            Debug.LogError("Failed to get TextureImporter for path: " + path);
        }
    }
}
