using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(50)]
public class MagnifiyingLens : MonoBehaviour
{
    [SerializeField] RenderTexture closeViewTexture;
    [SerializeField] float minimapCloseViewMinHeightMeters = 6f;

    [Header("Follow Target")]
    [SerializeField] Transform followTarget;
    [SerializeField] Vector3 offset = new Vector3(-0.9f, 0.2f, -1.3f);

    [Header("Constant Screen Size")]
    [SerializeField] float referenceOrthoSize = 12.12f;
    [SerializeField, Range(0.05f, 2f)] float overallSizeMultiplier = 0.35f;
    [SerializeField] CameraHeightZoom cameraHeightZoom;

    UIDocument _ui;
    VisualElement _minimapChrome;
    VisualElement _minimapCloseView;
    bool _minimapVisible;
    bool _gameStarted;

    public bool IsMinimapVisible => _minimapVisible;
    public float ScreenSizeScaleFactor { get; private set; } = 1f;
    public float OverallSizeMultiplier => overallSizeMultiplier;

    Vector3 _baseScale;
    Camera _cam;

    void Awake()
    {
        _ui = GetComponent<UIDocument>();
        _baseScale = transform.localScale;
    }

    void OnEnable()
    {
        GameplayEventBus.FlipHoldStarted += OnFlipHoldStarted;
        GameplayEventBus.FallenOffSurface += OnFallenOffSurface;
    }

    void OnDisable()
    {
        GameplayEventBus.FlipHoldStarted -= OnFlipHoldStarted;
        GameplayEventBus.FallenOffSurface -= OnFallenOffSurface;
    }

    void Start()
    {
        _cam = Camera.main;
        if (cameraHeightZoom == null)
            cameraHeightZoom = FindAnyObjectByType<CameraHeightZoom>();
        CacheRefs();
    }

    void LateUpdate()
    {
        transform.position = new Vector3(0, GameplayEventBus.HeightAbovePlaySurface, -6) + offset;
        UpdateMinimapVisibility();
        MaintainConstantScreenSize();
    }

    void MaintainConstantScreenSize()
    {
        if (_cam == null || !_cam.orthographic) return;

        float zoomBase = referenceOrthoSize;
        float zoomCurrent = _cam.orthographicSize;

        if (cameraHeightZoom != null)
        {
            zoomBase = cameraHeightZoom.BaseOrthoSize;
            zoomCurrent = cameraHeightZoom.CurrentOrthoSize;
        }

        if (zoomBase <= 0f) return;

        float scaleFactor = (zoomCurrent / zoomBase) * overallSizeMultiplier;
        ScreenSizeScaleFactor = scaleFactor;
        transform.localScale = _baseScale * scaleFactor;
    }

    void CacheRefs()
    {
        VisualElement root = _ui != null ? _ui.rootVisualElement : null;
        if (root == null) return;

        _minimapChrome = root.Q<VisualElement>("minimap-chrome");
        _minimapCloseView = root.Q<VisualElement>("minimap-close-view");

        if (_minimapCloseView != null && closeViewTexture != null)
            _minimapCloseView.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(closeViewTexture));

        if (_minimapChrome != null)
            _minimapChrome.style.display = DisplayStyle.None;
    }

    void OnFlipHoldStarted()
    {
        _gameStarted = true;
        HideLens();
    }

    void OnFallenOffSurface()
    {
        _gameStarted = false;
        HideLens();
    }

    void UpdateMinimapVisibility()
    {
        if (_minimapChrome == null) return;

        float heightAboveSurface = GameplayEventBus.HeightAbovePlaySurface;

        float hysteresis = _minimapVisible ? 9f : 0f;
        bool show = _gameStarted &&
                    heightAboveSurface >= (minimapCloseViewMinHeightMeters - hysteresis);

        if (show != _minimapVisible)
        {
            _minimapVisible = show;
            if (show)
                ShowLens();
            else
                HideLens();
        }
    }

    void ShowLens()
    {
        _minimapVisible = true;
        if (_minimapChrome == null) return;
        _minimapChrome.style.display = DisplayStyle.Flex;
        _minimapChrome.AddToClassList("minimap-visible");
    }

    void HideLens()
    {
        _minimapVisible = false;
        if (_minimapChrome == null) return;
        _minimapChrome.RemoveFromClassList("minimap-visible");
        _minimapChrome.style.display = DisplayStyle.None;
    }
}
