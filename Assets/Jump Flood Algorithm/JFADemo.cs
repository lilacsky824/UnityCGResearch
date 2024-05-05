using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class JFADemo : MonoBehaviour
{
    [SerializeField, Range(0, 1.0f)]
    private float _speed;
    [SerializeField, Range(0, 16)]
    private int _pass = 8;
    [SerializeField, Range(0, 4.0f)]
    private float _shapeWidth = 0.1f;
    [SerializeField]
    private bool _showDF;
    [SerializeField]
    private UIDocument _ui;
    [SerializeField]
    private Texture2D _source;
    [SerializeField]
    private RenderTexture _uv, _jfa1, _jfa2, _df, _result;
    [SerializeField]
    private ComputeShader _shader;

    private int _uvBufferFillKernel, _jfaKernel, _dfKernel, _resultKernel;

    private VisualElement _resultView;
    private const string _tabStyle = "ResultMenuTab";
    private float _shapeWidthSliderValue = 0.0f;

    void OnEnable()
    {
        CacheShaderKernels();
        CreateRTs();
        CreateUI();
        SetCommonShaderParameters();
    }

    void Update()
    {
        Dispatch();
    }

    void CacheShaderKernels()
    {
        _uvBufferFillKernel = _shader.FindKernel("UVBufferFill");
        _jfaKernel = _shader.FindKernel("JFAKernel");
        _dfKernel = _shader.FindKernel("DFKernel");
        _resultKernel = _shader.FindKernel("ResultKernel");
    }

    void SetCommonShaderParameters()
    {
        _shader.SetTexture(_resultKernel, "Source", _source);
        _shader.SetTexture(_resultKernel, "JFA", _jfa1);
        _shader.SetTexture(_resultKernel, "DF", _df);
        _shader.SetTexture(_resultKernel, "Result", _result);
    }

    void Dispatch()
    {
        _shader.SetVector("TexelSize", new Vector4(_source.width, _source.height, 1.0f / _source.width, 1.0f / _source.height));

        UVInit(_uvBufferFillKernel);
        JFA(_jfaKernel);
        DF(_dfKernel);

        _shader.SetFloat("ShapeWidth", _shapeWidth * _shapeWidthSliderValue);
        _shader.SetBool("ShowDF", _showDF);
        DispatchSingleKernel(_resultKernel);
    }

    void UVInit(int kernel)
    {
        _shader.SetTexture(kernel, "Source", _source);
        _shader.SetTexture(kernel, "UV", _uv);
        DispatchSingleKernel(kernel);
    }

    void JFA(int kernel)
    {
        SetJFAPass(kernel, _pass, _uv, _jfa1);

        for (int pass = _pass - 1; pass >= 0; pass--)
        {
            bool useSecondBuffer = (pass % 2 == 1);
            RenderTexture sourceTexture = useSecondBuffer ? _jfa1 : _jfa2;
            RenderTexture targetTexture = useSecondBuffer ? _jfa2 : _jfa1;

            SetJFAPass(kernel, pass, sourceTexture, targetTexture);
        }

        EnsureCorrectOutputTexture(kernel, _pass, _jfa1, _jfa2);
    }

    void SetJFAPass(int kernel, int pass, RenderTexture source, RenderTexture target)
    {
        _shader.SetInt("JumpSize", 1 << pass);
        _shader.SetTexture(kernel, "UV", source);
        _shader.SetTexture(kernel, "JFA", target);
        DispatchSingleKernel(kernel);
    }

    void EnsureCorrectOutputTexture(int kernel, int numberOfPasses, RenderTexture finalTarget, RenderTexture tempTarget)
    {
        if (numberOfPasses % 2 == 1)
        {
            SetJFAPass(kernel, 0, tempTarget, finalTarget);
        }
    }

    void DF(int kernel)
    {
        _shader.SetTexture(kernel, "JFA", _jfa1);
        _shader.SetTexture(kernel, "DF", _df);
        DispatchSingleKernel(kernel);
    }

    void CreateRTs()
    {
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor
        {
            width = _source.width,
            height = _source.height,
            graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16_SFloat,
            depthBufferBits = 0,
            volumeDepth = 1,
            msaaSamples = 1,
            dimension = TextureDimension.Tex2D,
            enableRandomWrite = true,
            sRGB = false
        };

        RenderTexture rt = RenderTexture.active;

        descriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
        _uv = new RenderTexture(descriptor);
        _uv.filterMode = FilterMode.Point;
        _uv.wrapMode = TextureWrapMode.Clamp;
        RenderTexture.active = _uv;
        GL.Clear(true, true, Color.red);

        _jfa1 = new RenderTexture(descriptor);
        _jfa1.filterMode = FilterMode.Point;
        _jfa1.wrapMode = TextureWrapMode.Clamp;
        RenderTexture.active = _jfa1;
        GL.Clear(true, true, Color.blue);

        _jfa2 = new RenderTexture(descriptor);
        _jfa2.filterMode = FilterMode.Point;
        _jfa2.wrapMode = TextureWrapMode.Clamp;
        RenderTexture.active = _jfa2;
        GL.Clear(true, true, Color.blue);

        descriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat;
        _df = new RenderTexture(descriptor);
        _df.wrapMode = TextureWrapMode.Clamp;
        RenderTexture.active = _df;
        GL.Clear(true, true, Color.green);

        descriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
        _result = new RenderTexture(descriptor);
        RenderTexture.active = _result;
        GL.Clear(true, true, Color.green);

        RenderTexture.active = rt;
    }

    void CreateUI()
    {
        VisualElement root = _ui.rootVisualElement;
        _resultView = root.Q<VisualElement>("Result");
        ToggleButtonGroup menu = _resultView.Q<ToggleButtonGroup>("ResultMenu");

        Button uvBtn = new Button();
        uvBtn.text = "UV";
        uvBtn.AddToClassList(_tabStyle);
        BindViewRTChange(_resultView, uvBtn, _uv);

        Button jfaBtn = new Button();
        jfaBtn.text = "JFA";
        jfaBtn.AddToClassList(_tabStyle);
        BindViewRTChange(_resultView, jfaBtn, _jfa1);

        Button DFBtn = new Button();
        DFBtn.text = "DF";
        DFBtn.AddToClassList(_tabStyle);
        BindViewRTChange(_resultView, DFBtn, _df);

        Button resultBtn = new Button();
        resultBtn.text = "Result";
        resultBtn.AddToClassList(_tabStyle);
        BindViewRTChange(_resultView, resultBtn, _result);

        int resultIndex = 0;
        menu.Add(uvBtn);
        resultIndex++;
        menu.Add(jfaBtn);
        resultIndex++;
        menu.Add(DFBtn);
        resultIndex++;
        menu.Add(resultBtn);

        //Show uv as default selection.
        using (var evt = ClickEvent.GetPooled())
        {
            evt.target = uvBtn;
            jfaBtn.SendEvent(evt);
        }

        void BindViewRTChange(VisualElement view, Button btn, RenderTexture rt)
        {
            btn.RegisterCallback<ClickEvent>((e) =>
            {
                view.style.backgroundImage = Background.FromRenderTexture(rt);
            });
        }
        
        TweakMenu();
        void TweakMenu()
        {
            VisualElement tweakMenu = _resultView.Q<VisualElement>("ResultTweakMenu");
            tweakMenu.style.display = DisplayStyle.None;
            menu.RegisterCallback<ChangeEvent<ToggleButtonGroupState>>((e) =>
            {
                var newVal = e.newValue.GetActiveOptions(stackalloc int[e.newValue.length]);
                tweakMenu.style.display = newVal[0] == resultIndex ? DisplayStyle.Flex : DisplayStyle.None;
            });

            Slider widthSlider = tweakMenu.Q<Slider>();
            _shapeWidthSliderValue = widthSlider.value;
            widthSlider.RegisterCallback<ChangeEvent<float>>((e) =>
            {
                _shapeWidthSliderValue = e.newValue;
            });

            Toggle showDFToggle = tweakMenu.Q<Toggle>();
            _showDF = showDFToggle.value;
            showDFToggle.RegisterCallback<ChangeEvent<bool>>((e) =>
            {
                _showDF = e.newValue;
            });
        }
    }

    void DispatchSingleKernel(int kernel)
    {
        int x = _source.width / 8, y = _source.height / 8, z = 1;
        _shader.Dispatch(kernel, x, y, z);
    }

    void OnDestroy()
    {
        _uv.Release();
        _jfa1.Release();
        _jfa2.Release();
        _df.Release();
        _result.Release();
    }
}
