using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Generate and save a RampMap using a ComputeShader based on the input Gradient.
/// </summary>
public class RampMapGenerator : EditorWindow
{
    public static int _rampMapResolution = 128;
    private static float _gradientHardness = 0.0f;
    private static Gradient _gradient = new Gradient();
    private static ComputeShader _rampMapComputeShader = default;
    private Vector4[] _gradientColorKeys;
    private ComputeBuffer _gradientColorKeysBuffer;
    private RenderTexture _rampMap;

    private bool _resolutionChanged = true;

    [MenuItem("Tools/RampMap Generator")]
    static void Init()
    {
        RampMapGenerator window = (RampMapGenerator)EditorWindow.GetWindow(typeof(RampMapGenerator), false, "RampMap Generator");
        window.Show();
    }

    //Load ComputeShader when Awake
    void Awake()
    {
        if (!_rampMapComputeShader)
        {
            _rampMapComputeShader = ToonMapCommon.LoadComputeShader();
        }

        _resolutionChanged = true;
    }

    private void OnDestroy()
    {
        if (_gradientColorKeysBuffer != null)
        {
            _gradientColorKeysBuffer.Release();
        }
        if (_rampMap != null)
        {
            _rampMap.Release();
        }
    }

    public void CreateGUI()
    {
        // Load and clone the UXML.
        string[] guids = AssetDatabase.FindAssets($"RampMapGenerator t:{nameof(VisualTreeAsset)}");
        if (guids.Length == 0)
        {
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
        VisualElement root = visualTree.CloneTree();
        rootVisualElement.Add(root);

        // Bind UI elements
        Button saveButton = root.Q<Button>("saveButton");
        Button previewButton = root.Q<Button>("previewButton");
        IntegerField resolutionField = root.Q<IntegerField>("resolutionField");
        Slider hardnessSlider = root.Q<Slider>("hardnessSlider");
        GradientField gradientField = root.Q<GradientField>("gradientField");
        HelpBox errorBox = root.Q<HelpBox>("errorBox");
        VisualElement previewContainer = root.Q<VisualElement>("previewContainer");

        // Initialize fields
        resolutionField.value = _rampMapResolution;
        hardnessSlider.value = _gradientHardness;
        gradientField.value = _gradient;
        errorBox.style.display = DisplayStyle.None;

        // Add event listeners
        saveButton.clicked += () =>
        {
            string path = EditorUtility.SaveFilePanel("Choose Path To Save RampMap", Application.dataPath, "RampMap", "png");
            if (!string.IsNullOrEmpty(path))
            {
                GenerateRampMapAndSave(path);
            }
        };

        previewButton.clicked += () =>
        {
            GenerateRampMapByComputeShader();
            DrawPreviewTexture(previewContainer);
        };

        resolutionField.RegisterValueChangedCallback(evt =>
        {
            _rampMapResolution = evt.newValue;
            _resolutionChanged = true;
            GenerateRampMapByComputeShader();
            DrawPreviewTexture(previewContainer);
        });

        hardnessSlider.RegisterValueChangedCallback(evt =>
        {
            _gradientHardness = evt.newValue;
            GenerateRampMapByComputeShader();
            DrawPreviewTexture(previewContainer);
        });

        gradientField.RegisterValueChangedCallback(evt =>
        {
            _gradient = evt.newValue;
            GenerateRampMapByComputeShader();
            DrawPreviewTexture(previewContainer);
        });

        // Validate compute shader
        if (!_rampMapComputeShader || !_rampMapComputeShader.HasKernel("RampMapGenerator"))
        {
            errorBox.style.display = DisplayStyle.Flex;
        }
        else
        {
            GenerateRampMapByComputeShader();
            DrawPreviewTexture(previewContainer);
        }
    }

    public void GenerateRampMapAndSave(string path)
    {
        if (path.Length != 0)
        {
            GenerateRampMapByComputeShader();
            ToonMapCommon.DumpRenderTextureToScreenThenSave(path, _rampMapResolution, _rampMapResolution, _rampMap);
        }
    }

    void GenerateRampMapByComputeShader()
    {
        if (_rampMapComputeShader)
        {
            if (!_rampMapComputeShader.HasKernel("RampMapGenerator"))
            {
                return;
            }

            if (_resolutionChanged)
            {
                if (_rampMap != null)
                    _rampMap.Release();

                _rampMap = new RenderTexture(_rampMapResolution, _rampMapResolution, 0, RenderTextureFormat.ARGB32);
                _rampMap.enableRandomWrite = true;
                _rampMap.Create();
                _resolutionChanged = false;
            }

            GradientColorKey[] tempGradientColorKeys = _gradient.colorKeys;
            _gradientColorKeys = new Vector4[tempGradientColorKeys.Length];

            for (int i = 0; i < tempGradientColorKeys.Length; i++)
            {
                _gradientColorKeys[i] = tempGradientColorKeys[i].color;
                _gradientColorKeys[i].w = tempGradientColorKeys[i].time;
            }

            _gradientColorKeysBuffer = new ComputeBuffer(_gradientColorKeys.Length, 16);
            _gradientColorKeysBuffer.SetData(_gradientColorKeys);

            int kernal = _rampMapComputeShader.FindKernel("RampMapGenerator");
            int threadGroupAmount = Mathf.CeilToInt(_rampMapResolution / 8.0f);

            _rampMapComputeShader.SetTexture(kernal, "_RampMap", _rampMap);
            _rampMapComputeShader.SetBuffer(kernal, "_GradientColorKeys", _gradientColorKeysBuffer);
            _rampMapComputeShader.SetFloat("_GradientColorKeysAmount", _gradientColorKeys.Length);
            _rampMapComputeShader.SetFloat("_GradientHardness", _gradientHardness);
            _rampMapComputeShader.Dispatch(kernal, threadGroupAmount, threadGroupAmount, 1);
            _gradientColorKeysBuffer.Release();

            Debug.Log("Generate RampMap");
        }
    }

    private void DrawPreviewTexture(VisualElement container)
    {
        container.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_rampMap));
    }
}
