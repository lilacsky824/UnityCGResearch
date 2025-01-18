using Spine;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bakes Spine animations into a Vertex Animation Texture (VAT).
/// The X-axis of the VAT represents the vertex ID,
/// and the Y-axis represents the frame.
/// Previously attempted to separate data for each frame by the Y-axis, but encountered precision issues.
/// </summary>
public class SpineVATBaker : MonoBehaviour
{
    [SerializeField]
    private SkeletonAnimation _skeletonAnimation;
    private MeshFilter _meshFilter;
    private string _path = "Assets/VAT.exr";
    private const float _fps = 60.0f;
    private bool _saveFloatFormat = false;

    public void GenerateVAT()
    {
        _path = EditorUtility.SaveFilePanel("Select saving path", "Assets/", "VAT", "exr");
        if (string.IsNullOrEmpty(_path))
        {
            Debug.LogWarning("No path selected. Aborting VAT generation.");
            return;
        }

        try
        {
            GenerateVATProcess();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"VAT generation failed: {ex.Message}");
        }
    }


    private void GenerateVATProcess()
    {
        InitializeComponents();
        Mesh mesh = _meshFilter.sharedMesh;
        Spine.Animation animation = GetAnimation();

        //Width
        int vertexCount = mesh.vertexCount;
        //Height
        int frameCount = Mathf.CeilToInt(animation.Duration * _fps);

        Texture2D vatTexture = CreateTexture(vertexCount, frameCount);
        Vector4[] vatPixels = new Vector4[vertexCount * frameCount];

        Vector3[] initialVertices = mesh.vertices;
        float deltaTime = 1.0f / _fps;

        float time = 0.0f;
        for (int frame = 0; frame < frameCount; frame++)
        {
            ApplyAnimation(animation, ref time, deltaTime);

            Vector3[] currentVertices = mesh.vertices;
            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 offset = currentVertices[i] - initialVertices[i];
                int pixelIndex = frame * vertexCount + i;
                vatPixels[pixelIndex] = new Vector4(offset.x, offset.y, offset.z, 1.0f);
            }
        }

        SaveVAT(vatTexture, vatPixels);
        SetTextureImportSettings(_path, vertexCount, frameCount);
    }

    private void InitializeComponents()
    {
        if(_skeletonAnimation == null)
            _skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();

        if (_skeletonAnimation == null)
        {
            throw new System.Exception("Required components are missing: SkeletonAnimation.");
        }

        _meshFilter = _skeletonAnimation.GetComponent<MeshFilter>();

        if (_meshFilter == null)
        {
            throw new System.Exception("Required components are missing: MeshFilter.");
        }
    }

    private Spine.Animation GetAnimation()
    {
        string animationName = _skeletonAnimation.AnimationName;
        var animation = _skeletonAnimation.SkeletonDataAsset.GetSkeletonData(true).FindAnimation(animationName);

        if (animation == null || animation.Duration <= 0)
        {
            throw new System.Exception($"Invalid animation: {animationName}");
        }

        return animation;
    }

    private Texture2D CreateTexture(int vertexCount, int frameCount)
    {
        // Uses TextureFormat.RGBAFloat because the data written to SetPixelData
        // consists of Vector4 values, which are four floats. Therefore, Half format cannot be used.
        return new Texture2D(vertexCount, frameCount, TextureFormat.RGBAFloat, false);
    }

    private void ApplyAnimation(Spine.Animation animation, ref float time, float deltaTime)
    {
        animation.Apply(_skeletonAnimation.Skeleton, time, time, false, null, 1.0f, MixBlend.Replace, MixDirection.In);
        _skeletonAnimation.Update(deltaTime);
        _skeletonAnimation.LateUpdateMesh();
        time += deltaTime;
    }

    private void SaveVAT(Texture2D vatTexture, Vector4[] vatPixels)
    {
        vatTexture.SetPixelData(vatPixels, 0);
        byte[] bytes = vatTexture.EncodeToEXR(_saveFloatFormat ? Texture2D.EXRFlags.OutputAsFloat : Texture2D.EXRFlags.None);

        using (var fileStream = new System.IO.FileStream(_path, System.IO.FileMode.Create))
        {
            fileStream.Write(bytes, 0, bytes.Length);
        }

        AssetDatabase.Refresh();
        Debug.Log($"VAT texture saved to {_path}");
    }

    private void SetTextureImportSettings(string assetPath, int textureWidth, int textureHeight)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = Mathf.Max(textureWidth, textureHeight);
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = false; // Not needed for VAT

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"Import settings applied to {assetPath}");
        }
        else
        {
            Debug.LogWarning($"Failed to get TextureImporter for {assetPath}");
        }
    }
}
