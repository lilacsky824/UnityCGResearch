using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using static ToonMapCommon;

public class GradientMapGeneratorWindow : EditorWindow
{
    private GradientMapGenerator _generator;
    private ToonMapGeneratorData _data;

    private Texture2D _previewTexture = default;
    private RenderTexture _gradientMapPreview;

    private int _currentSelectedIndex = 0;
    private ReorderableList _gradientReorderableList = null;
    private ReorderableList _hdrGradientReorderableList = null;
    private VisualElement _gradientPreview, _applyPreview;

    private SerializedObject _serializedObject;
    private SerializedProperty _gradientListProperty;
    private SerializedProperty _hdrGradientListProperty;

    [MenuItem("Tools/GradientMap Generator")]
    static void Init()
    {
        GradientMapGeneratorWindow window = (GradientMapGeneratorWindow)GetWindow(typeof(GradientMapGeneratorWindow), false, "GradientMap Generator");
        window.Show();
    }

    void Awake()
    {
        _data = GetToonMapData();

        _serializedObject = new SerializedObject(_data);
        _generator = new GradientMapGenerator(_data);

        _gradientListProperty = _serializedObject.FindProperty("GradientList");
        _hdrGradientListProperty = _serializedObject.FindProperty("HDRGradientList");

        _gradientReorderableList = new ReorderableList(_serializedObject, _gradientListProperty, true, false, true, true);
        _gradientReorderableList.drawElementCallback = (rect, index, isActive, isFocused) => DrawListElement(rect, index, isActive, isFocused, _gradientListProperty);

        _hdrGradientReorderableList = new ReorderableList(_serializedObject, _hdrGradientListProperty, true, false, true, true);
        _hdrGradientReorderableList.drawElementCallback = (rect, index, isActive, isFocused) => DrawListElement(rect, index, isActive, isFocused, _hdrGradientListProperty);
    }

    void OnDestroy()
    {
        if (_gradientMapPreview != null)
        {
            _gradientMapPreview.Release();
        }
    }

    public void CreateGUI()
    {
        // Load and clone the UXML.
        string[] guids = AssetDatabase.FindAssets($"GradientMapGenerator t:{nameof(VisualTreeAsset)}");
        if (guids.Length == 0)
        {
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
        VisualElement root = visualTree.CloneTree();
        rootVisualElement.Add(root);

        // Find UI elements in the tree
        Button saveButton = root.Q<Button>("saveButton");
        Button previewButton = root.Q<Button>("previewButton");
        IntegerField resolutionField = root.Q<IntegerField>("resolutionField");
        resolutionField.bindingPath = nameof(_data.Resolution);
        Slider hardnessSlider = root.Q<Slider>("hardnessSlider");
        hardnessSlider.bindingPath = nameof(_data.Hardness);
        IMGUIContainer gradientListContainer = root.Q<IMGUIContainer>("gradientListContainer");
        ObjectField previewTextureField = root.Q<ObjectField>("previewTextureField");
        Toggle hdrToggle = root.Q<Toggle>("hdrToggle");
        hdrToggle.bindingPath = nameof(_data.HDR);
        Toggle showAlphaToggle = root.Q<Toggle>("showAlphaToggle");
        showAlphaToggle.bindingPath = nameof(_data.ShowAlpha);
        EnumField sourceTypeField = root.Q<EnumField>("sourceTypeField");
        sourceTypeField.bindingPath = nameof(_data.SourceRemapType);
        HelpBox errorBox = root.Q<HelpBox>("errorBox");
        ScrollView previewContainer = root.Q<ScrollView>("previewContainer");
        VisualElement gradientPreview = root.Q<VisualElement>("gradientPreview");
        VisualElement applyGradientPreview = root.Q<VisualElement>("applyGradientPreview");

        _gradientPreview = gradientPreview;
        _applyPreview = applyGradientPreview;

        // Initialize fields
        resolutionField.value = _data.Resolution;
        hardnessSlider.value = _data.Hardness;
        previewTextureField.value = _previewTexture;
        //hdrToggle.value = _gradientMapGeneratorData.HDR;
        showAlphaToggle.value = _data.ShowAlpha;
        errorBox.style.display = DisplayStyle.None;
        sourceTypeField.Init(_data.SourceRemapType);

        // Add event listeners
        _gradientReorderableList.onSelectCallback = (ReorderableList list) =>
        {
            _currentSelectedIndex = list.index;
            _generator.GenerateGradientMapByComputeShader();
            GenerateGradientMapPreviewByComputeShader(_currentSelectedIndex);
            DrawPreviewTexture();
        };

        _hdrGradientReorderableList.onSelectCallback = (ReorderableList list) =>
        {
            _currentSelectedIndex = list.index;
            _generator.GenerateGradientMapByComputeShader();
            GenerateGradientMapPreviewByComputeShader(_currentSelectedIndex);
            DrawPreviewTexture();
        };

        saveButton.clicked += () =>
        {
            string path = EditorUtility.SaveFilePanel("Choose Path To Save GradientMap", Application.dataPath, "GradientMap", _data.HDR ? "exr" : "png");
            if (!string.IsNullOrEmpty(path))
            {
                _generator.GenerateGradientMapAndSave(path);
            }
        };

        previewButton.clicked += () =>
        {
            if (GradientMapGenerator.ComputeShader.HasKernel("GradientMapGenerator"))
            {
                _generator.GenerateGradientMapByComputeShader();
                GenerateGradientMapPreviewByComputeShader(_currentSelectedIndex);
                DrawPreviewTexture();
            }
        };

        resolutionField.RegisterValueChangedCallback(evt =>
        {
            _generator.GenerateGradientMapByComputeShader();
            GenerateGradientMapPreviewByComputeShader(_currentSelectedIndex);
            DrawPreviewTexture();
            SaveGradientMapGeneratorData();
        });

        hardnessSlider.RegisterValueChangedCallback(evt =>
        {
            _generator.GenerateGradientMapByComputeShader();
            GenerateGradientMapPreviewByComputeShader(_currentSelectedIndex);
            DrawPreviewTexture();
            SaveGradientMapGeneratorData();
        });

        gradientListContainer.onGUIHandler = () =>
        {
            if (_data.HDR)
            {
                DrawGradientList(_gradientListProperty, _hdrGradientReorderableList);
            }
            else
            {
                DrawGradientList(_gradientListProperty, _gradientReorderableList);
            }
        };

        previewTextureField.RegisterValueChangedCallback(evt =>
        {
            _previewTexture = (Texture2D)evt.newValue;
            _generator.GenerateGradientMapByComputeShader();
            GenerateGradientMapPreviewByComputeShader(_currentSelectedIndex);
            DrawPreviewTexture();
        });

        hdrToggle.RegisterValueChangedCallback(evt =>
        {
            _generator.GenerateGradientMapByComputeShader();
            GenerateGradientMapPreviewByComputeShader(_currentSelectedIndex);
            DrawPreviewTexture();
            SaveGradientMapGeneratorData();
        });
        hdrToggle.value = _data.HDR;


        showAlphaToggle.RegisterValueChangedCallback(evt =>
        {
            _generator.GenerateGradientMapByComputeShader();
            GenerateGradientMapPreviewByComputeShader(_currentSelectedIndex);
            DrawPreviewTexture();
            SaveGradientMapGeneratorData();
        });

        sourceTypeField.RegisterValueChangedCallback(evt =>
        {
            _generator.GenerateGradientMapByComputeShader();
            GenerateGradientMapPreviewByComputeShader(_currentSelectedIndex);
            DrawPreviewTexture();
            SaveGradientMapGeneratorData();
        });

        // Validate compute shader
        if (!GradientMapGenerator.ComputeShader || !GradientMapGenerator.ComputeShader.HasKernel("GradientMapGenerator"))
        {
            errorBox.style.display = DisplayStyle.Flex;
        }

        rootVisualElement.Bind(_serializedObject);
    }

    /// <summary>
    /// To update the preview in real-time when selecting a gradient, we can only use the ReorderableList to listen for the currently selected gradient index.
    /// </summary>
    void DrawGradientList(SerializedProperty property, ReorderableList list)
    {
        EditorGUI.BeginChangeCheck();

        property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, property.displayName);
        if (property.isExpanded)
        {
            list.DoLayoutList();
        }
        if (EditorGUI.EndChangeCheck())
        {
            if (GradientMapGenerator.ComputeShader.HasKernel("GradientMapGenerator"))
            {
                _generator.GenerateGradientMapByComputeShader();
                GenerateGradientMapPreviewByComputeShader(_currentSelectedIndex);
                DrawPreviewTexture();
            }

            SaveGradientMapGeneratorData();
        }
    }

    void DrawListElement(Rect rect, int index, bool isActive, bool isFocused, SerializedProperty property)
    {
        var arect = rect;
        var serElem = property.GetArrayElementAtIndex(index);
        arect.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(arect, serElem, GUIContent.none);
    }

    void SaveGradientMapGeneratorData()
    {
        _serializedObject.ApplyModifiedProperties();

        EditorUtility.SetDirty(_data);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    void ReallocPreviewTexture()
    {
        if (_gradientMapPreview != null)
            _gradientMapPreview.Release();

        _gradientMapPreview = new RenderTexture(_previewTexture.width, _previewTexture.height, 0, RenderTextureFormat.ARGB32);
        _gradientMapPreview.enableRandomWrite = true;
        _gradientMapPreview.Create();
    }

    void GenerateGradientMapPreviewByComputeShader(int index)
    {
        if (_generator.GradientMap != null && _previewTexture != null)
        {
            bool needsRealloc = _gradientMapPreview == null ||
                                _gradientMapPreview.width != _previewTexture.width ||
                                _gradientMapPreview.height != _previewTexture.height;

            if (needsRealloc)
            {
                ReallocPreviewTexture();
            }

            int kernel = GradientMapGenerator.ComputeShader.FindKernel("GradientMapPreviewGenerator");
            int threadGroupAmountX = Mathf.CeilToInt(_previewTexture.width / 8.0f);
            int threadGroupAmountY = Mathf.CeilToInt(_previewTexture.height / 8.0f);

            GradientMapGenerator.ComputeShader.SetInt("_GradientIndex", index);
            GradientMapGenerator.ComputeShader.SetBool("_ShowAlpha", _data.ShowAlpha);
            GradientMapGenerator.ComputeShader.SetInt("_RemapType", (int)_data.SourceRemapType);
            GradientMapGenerator.ComputeShader.SetTexture(kernel, "_GradientMap", _generator.GradientMap);
            GradientMapGenerator.ComputeShader.SetTexture(kernel, "_PreviewTexture", _previewTexture);
            GradientMapGenerator.ComputeShader.SetTexture(kernel, "_GradientMapPreview", _gradientMapPreview);

            GradientMapGenerator.ComputeShader.Dispatch(kernel, threadGroupAmountX, threadGroupAmountY, 1);
        }
    }

    private void DrawPreviewTexture()
    {
        int height = _data.GetCurrentGradientList().Length * 32;
        _gradientPreview.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_generator.GradientMap));
        _gradientPreview.style.height = height;

        _applyPreview.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_gradientMapPreview));
    }
}