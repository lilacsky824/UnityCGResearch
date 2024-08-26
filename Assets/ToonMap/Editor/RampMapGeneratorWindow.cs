using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static ToonMapCommon;

/// <summary>
/// Generate and save a RampMap using a ComputeShader based on the input Gradient.
/// </summary>
public class RampMapGeneratorWindow : EditorWindow
{
    private RampMapGenerator _generator;
    private ToonMapGeneratorData _data;

    private SerializedObject _serializedObject;

    [MenuItem("Tools/RampMap Generator")]
    static void Init()
    {
        RampMapGeneratorWindow window = (RampMapGeneratorWindow)GetWindow(typeof(RampMapGeneratorWindow), false, "RampMap Generator");
        window.Show();
    }

    void Awake()
    {
        _data = GetToonMapData();

        _serializedObject = new SerializedObject(_data);
        _generator = new RampMapGenerator(_data);
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
        resolutionField.bindingPath = nameof(_data.Resolution);
        Slider hardnessSlider = root.Q<Slider>("hardnessSlider");
        hardnessSlider.bindingPath = nameof(_data.Hardness);
        GradientField gradientField = root.Q<GradientField>("gradientField");
        gradientField.bindingPath = nameof(_data.RampGradient);
        HelpBox errorBox = root.Q<HelpBox>("errorBox");
        VisualElement previewContainer = root.Q<VisualElement>("previewContainer");

        // Initialize fields
        resolutionField.value = _data.Resolution;
        hardnessSlider.value = _data.Hardness;
        gradientField.value = _data.RampGradient;
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
            _generator.GenerateRampMapByComputeShader();
            DrawPreviewTexture(previewContainer);
        };

        resolutionField.RegisterValueChangedCallback(evt =>
        {
            _generator.GenerateRampMapByComputeShader();
            DrawPreviewTexture(previewContainer);
        });

        hardnessSlider.RegisterValueChangedCallback(evt =>
        {
            _generator.GenerateRampMapByComputeShader();
            DrawPreviewTexture(previewContainer);
        });

        gradientField.RegisterValueChangedCallback(evt =>
        {
            _generator.GenerateRampMapByComputeShader();
            DrawPreviewTexture(previewContainer);
        });

        // Validate compute shader
        if (!RampMapGenerator.ComputeShader || !RampMapGenerator.ComputeShader.HasKernel("RampMapGenerator"))
        {
            errorBox.style.display = DisplayStyle.Flex;
        }
        else
        {
            _generator.GenerateRampMapByComputeShader();
            DrawPreviewTexture(previewContainer);
        }

        rootVisualElement.Bind(_serializedObject);
    }

    public void GenerateRampMapAndSave(string path)
    {
        if (path.Length != 0)
        {
            _generator.GenerateRampMapByComputeShader();
            ToonMapCommon.DumpRenderTextureToScreenThenSave(path, _data.Resolution, _data.Resolution, false, _generator.RampMap);
        }
    }

    private void DrawPreviewTexture(VisualElement container)
    {
        container.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_generator.RampMap));
    }
}
