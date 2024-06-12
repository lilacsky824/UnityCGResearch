using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

public class GradientMapGenerator : EditorWindow
{
    private int _gradientMapResolution = 128;
    private float _gradientHardness = 0.0f;
    private GradientMapGeneratorData _gradientMapGeneratorData;
    private Texture2D _previewTexture = default;
    private bool _showAlpha = false;
    private GradientMapGeneratorData.GreyscaleSourceType _sourceType;
    private static ComputeShader _gradientMapComputeShader = default;
    private ComputeBuffer _gradientColorKeysBuffer;
    private RenderTexture _gradientMap;
    private RenderTexture _gradientMapPreview;

    private bool _resolutionChanged = true;
    private int _currentSelectedIndex = 0;
    private ReorderableList _gradientReorderableList = null;
    private VisualElement _gradientPreview, _applyPreview;
    private SerializedObject _serializedObject;
    const string _settingDataDefaultPath = "Assets/Settings/GradientListData.asset";

    [MenuItem("Tools/GradientMap Generator")]
    static void Init()
    {
        GradientMapGenerator window = (GradientMapGenerator)EditorWindow.GetWindow(typeof(GradientMapGenerator), false, "GradientMap Generator");
        window.Show();
    }

    void Awake()
    {
        _gradientMapComputeShader = ToonMapCommon.LoadComputeShader();

        string[] guids = AssetDatabase.FindAssets($"t:{nameof(GradientMapGeneratorData)}");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            _gradientMapGeneratorData = AssetDatabase.LoadAssetAtPath<GradientMapGeneratorData>(path);
        }

        if (_gradientMapGeneratorData == null)
        {
            _gradientMapGeneratorData = CreateInstance<GradientMapGeneratorData>();
            AssetDatabase.CreateAsset(_gradientMapGeneratorData, _settingDataDefaultPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        _resolutionChanged = true;
        _serializedObject = new SerializedObject(_gradientMapGeneratorData);
        _gradientReorderableList = new ReorderableList(_serializedObject, _serializedObject.FindProperty("GradientList"), true, false, true, true);
        _gradientReorderableList.drawElementCallback = DrawListElement;
    }

    void OnDestroy()
    {
        if (_gradientColorKeysBuffer != null)
        {
            _gradientColorKeysBuffer.Release();
        }
        if (_gradientMapPreview != null)
        {
            _gradientMapPreview.Release();
        }
        if (_gradientMap != null)
        {
            _gradientMap.Release();
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
        Slider hardnessSlider = root.Q<Slider>("hardnessSlider");
        IMGUIContainer gradientListContainer = root.Q<IMGUIContainer>("gradientListContainer");
        ObjectField previewTextureField = root.Q<ObjectField>("previewTextureField");
        Toggle showAlphaToggle = root.Q<Toggle>("showAlphaToggle");
        EnumField sourceTypeField = root.Q<EnumField>("sourceTypeField");
        HelpBox errorBox = root.Q<HelpBox>("errorBox");
        ScrollView previewContainer = root.Q<ScrollView>("previewContainer");
        VisualElement gradientPreview = root.Q<VisualElement>("gradientPreview");
        VisualElement applyGradientPreview = root.Q<VisualElement>("applyGradientPreview");

        _gradientPreview = gradientPreview;
        _applyPreview = applyGradientPreview;

        // Initialize fields
        resolutionField.value = _gradientMapResolution;
        hardnessSlider.value = _gradientHardness;
        previewTextureField.value = _previewTexture;
        showAlphaToggle.value = _showAlpha;
        errorBox.style.display = DisplayStyle.None;
        sourceTypeField.Init(_sourceType);

        // Add event listeners
        _gradientReorderableList.onSelectCallback = (ReorderableList list) =>
        {
            _currentSelectedIndex = list.index;
            GenerateGradientMapByComputeShader();
            DrawPreviewTexture();
        };

        saveButton.clicked += () =>
        {
            string path = EditorUtility.SaveFilePanel("Choose Path To Save GradientMap", Application.dataPath, "GradientMap", "png");
            if (!string.IsNullOrEmpty(path))
            {
                GenerateGradientMapAndSave(path);
            }
        };

        previewButton.clicked += () =>
        {
            if (_gradientMapComputeShader && _gradientMapComputeShader.HasKernel("GradientMapGenerator"))
            {
                GenerateGradientMapByComputeShader();
                DrawPreviewTexture();
            }
        };

        resolutionField.RegisterValueChangedCallback(evt =>
        {
            _gradientMapResolution = evt.newValue;
            GenerateGradientMapByComputeShader();
            DrawPreviewTexture();
        });

        hardnessSlider.RegisterValueChangedCallback(evt =>
        {
            _gradientHardness = evt.newValue;
            GenerateGradientMapByComputeShader();
            DrawPreviewTexture();
        });

        gradientListContainer.onGUIHandler = DrawGradientList;

        previewTextureField.RegisterValueChangedCallback(evt =>
        {
            _previewTexture = (Texture2D)evt.newValue;
            GenerateGradientMapByComputeShader();
            DrawPreviewTexture();
        });

        showAlphaToggle.RegisterValueChangedCallback(evt =>
        {
            _showAlpha = evt.newValue;
            GenerateGradientMapByComputeShader();
            DrawPreviewTexture();
        });

        sourceTypeField.RegisterValueChangedCallback(evt =>
        {
            _sourceType = (GradientMapGeneratorData.GreyscaleSourceType)evt.newValue;
            GenerateGradientMapByComputeShader();
            DrawPreviewTexture();
        });

        // Validate compute shader
        if (!_gradientMapComputeShader || !_gradientMapComputeShader.HasKernel("GradientMapGenerator"))
        {
            errorBox.style.display = DisplayStyle.Flex;
        }
    }

    void SaveGradientMapGeneratorData()
    {
        _gradientMapGeneratorData.GradientMapResolution = _gradientMapResolution;
        _gradientMapGeneratorData.GradientHardness = _gradientHardness;
        _gradientMapGeneratorData.ShowAlpha = _showAlpha;
        _gradientMapGeneratorData.SourceRemapType = _sourceType;
        EditorUtility.SetDirty(_gradientMapGeneratorData);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public void GenerateGradientMapAndSave(string path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            GenerateGradientMapByComputeShader();
            ToonMapCommon.DumpRenderTextureToScreenThenSave(path, _gradientMapResolution, _gradientMapGeneratorData.GradientList.Length, _gradientMap);
            _gradientColorKeysBuffer.Release();
        }
    }

    void GenerateGradientMapByComputeShader()
    {
        if (_gradientMapComputeShader)
        {
            if (!_gradientMapComputeShader.HasKernel("GradientMapGenerator"))
            {
                return;
            }

            if(_resolutionChanged)
            {
                if(_gradientMap != null)
                    _gradientMap.Release();

                _gradientMap = new RenderTexture(_gradientMapResolution, _gradientMapGeneratorData.GradientList.Length, 0, RenderTextureFormat.ARGB32);
                _gradientMap.filterMode = FilterMode.Point;
                _gradientMap.wrapModeV = TextureWrapMode.Repeat;
                _gradientMap.enableRandomWrite = true;
                _gradientMap.Create();
            }
            _resolutionChanged = false;

            for (int i = 0; i < _gradientMapGeneratorData.GradientList.Length; i++)
            {
                if (_gradientMapGeneratorData.GradientList[i] == null)
                {
                    return;
                }

                GradientColorKey[] tempGradientColorKeys = _gradientMapGeneratorData.GradientList[i].colorKeys;
                Vector4[] gradientColorKeys = new Vector4[tempGradientColorKeys.Length];
                for (int j = 0; j < tempGradientColorKeys.Length; j++)
                {
                    gradientColorKeys[j] = tempGradientColorKeys[j].color;
                    gradientColorKeys[j].w = tempGradientColorKeys[j].time;
                }

                _gradientColorKeysBuffer = new ComputeBuffer(gradientColorKeys.Length, 16);
                _gradientColorKeysBuffer.SetData(gradientColorKeys);

                int kernel = _gradientMapComputeShader.FindKernel("GradientMapGenerator");
                int threadGroupAmount = Mathf.CeilToInt(_gradientMapResolution / 8.0f);

                _gradientMapComputeShader.SetInt("_GradientIndex", i);
                _gradientMapComputeShader.SetTexture(kernel, "_GradientMap", _gradientMap);
                _gradientMapComputeShader.SetBuffer(kernel, "_GradientColorKeys", _gradientColorKeysBuffer);
                _gradientMapComputeShader.SetFloat("_GradientColorKeysAmount", gradientColorKeys.Length);
                _gradientMapComputeShader.SetFloat("_GradientHardness", _gradientHardness);
                _gradientMapComputeShader.Dispatch(kernel, threadGroupAmount, 1, 1);

                Debug.Log("Generate GradientMap");
            }
            _gradientColorKeysBuffer.Release();
        }

        GenerateGradientMapPreviewByComputeShader(_currentSelectedIndex);
    }

    void GenerateGradientMapPreviewByComputeShader(int index)
    {
        if (_gradientMapComputeShader && _gradientMap != null && _previewTexture != null)
        {
            _gradientMapPreview = new RenderTexture(_previewTexture.width, _previewTexture.height, 0, RenderTextureFormat.ARGB32);
            _gradientMapPreview.enableRandomWrite = true;
            _gradientMapPreview.Create();

            int kernel = _gradientMapComputeShader.FindKernel("GradientMapPreviewGenerator");
            int threadGroupAmountX = Mathf.CeilToInt(_previewTexture.width / 8.0f);
            int threadGroupAmountY = Mathf.CeilToInt(_previewTexture.height / 8.0f);

            _gradientMapComputeShader.SetInt("_GradientIndex", index);
            _gradientMapComputeShader.SetBool("_ShowAlpha", _showAlpha);
            _gradientMapComputeShader.SetInt("_RemapType", (int)_sourceType);
            _gradientMapComputeShader.SetTexture(kernel, "_GradientMap", _gradientMap);
            _gradientMapComputeShader.SetTexture(kernel, "_PreviewTexture", _previewTexture);
            _gradientMapComputeShader.SetTexture(kernel, "_GradientMapPreview", _gradientMapPreview);
            _gradientMapComputeShader.Dispatch(kernel, threadGroupAmountX, threadGroupAmountY, 1);
        }
    }

    private void DrawPreviewTexture()
    {
        int height = _gradientMapGeneratorData.GradientList.Length * 8;
        _gradientPreview.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_gradientMap));
        _gradientPreview.style.height = height;

        _applyPreview.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_gradientMapPreview));
    }

    /// <summary>
    /// To update the preview in real-time when selecting a gradient, we can only use the ReorderableList to listen for the currently selected gradient index.
    /// </summary>
    void DrawGradientList()
    {
        EditorGUI.BeginChangeCheck();
        SerializedProperty property = _gradientReorderableList.serializedProperty;
        property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, property.displayName);
        if (property.isExpanded)
        {
            _gradientReorderableList.DoLayoutList();
        }
        if (EditorGUI.EndChangeCheck())
        {
            if (_gradientMapComputeShader && _gradientMapComputeShader.HasKernel("GradientMapGenerator"))
            {
                GenerateGradientMapByComputeShader();
                DrawPreviewTexture();
            }

            _serializedObject.ApplyModifiedProperties();
            SaveGradientMapGeneratorData();
        }
    }

    void DrawListElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        var arect = rect;
        var serElem = _gradientReorderableList.serializedProperty.GetArrayElementAtIndex(index);
        arect.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(arect, serElem, GUIContent.none);
    }
}
