using UnityEngine;
using UnityEngine.Serialization;
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

    [Header("Partner Lens")]
    [SerializeField] PlayerController trackedPlayer;
    [SerializeField] bool hideOnFlipHold = true;
    [SerializeField] bool destroyWhenTrackedPlayerFalls;
    [FormerlySerializedAs("alwaysVisibleInDuo")]
    [SerializeField] bool alwaysVisibleWithPartners;

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
    Color? _partnerAccentColor;

    void Awake()
    {
        _ui = GetComponent<UIDocument>();
        _baseScale = transform.localScale;
    }

    void OnEnable()
    {
        GameplayEventBus.FlipHoldStarted += OnFlipHoldStarted;
        GameplayEventBus.FallenOffSurface += OnFallenOffSurface;
        GameplayEventBus.PlayerFell += OnPlayerFell;
        GameplayEventBus.PartnersUnlocked += OnPartnersUnlocked;
    }

    void OnDisable()
    {
        GameplayEventBus.FlipHoldStarted -= OnFlipHoldStarted;
        GameplayEventBus.FallenOffSurface -= OnFallenOffSurface;
        GameplayEventBus.PlayerFell -= OnPlayerFell;
        GameplayEventBus.PartnersUnlocked -= OnPartnersUnlocked;
    }

    void Start()
    {
        _cam = Camera.main;
        if (cameraHeightZoom == null)
            cameraHeightZoom = FindAnyObjectByType<CameraHeightZoom>();
        CacheRefs();
    }

    public void InitializePartner(
        PlayerController partner,
        RenderTexture texture,
        Vector3 lensOffset,
        float minHeightMeters,
        Color accentColor)
    {
        trackedPlayer = partner;
        closeViewTexture = texture;
        offset = lensOffset;
        minimapCloseViewMinHeightMeters = minHeightMeters;
        hideOnFlipHold = false;
        alwaysVisibleWithPartners = true;
        destroyWhenTrackedPlayerFalls = true;
        _gameStarted = CrazyPanDogUIController.GameStarted;

        CacheRefs();
        _partnerAccentColor = accentColor;
        ApplyPartnerAccent();

        if (_gameStarted && partner != null && !partner.HasFallen)
            ShowLens();
    }

    void LateUpdate()
    {
        Transform trackTransform = ResolveTrackTransform();
        float trackX = trackTransform != null ? trackTransform.position.x : 0f;

        transform.position = new Vector3(trackX, GetTrackingHeight(), -6f) + offset;
        UpdateMinimapVisibility();
        MaintainConstantScreenSize();
    }

    Transform ResolveTrackTransform()
    {
        if (trackedPlayer != null && !trackedPlayer.HasFallen)
            return trackedPlayer.PlayerRoot;

        return followTarget;
    }

    float GetTrackingHeight()
    {
        if (trackedPlayer != null && !trackedPlayer.HasFallen)
            return trackedPlayer.HeightAbovePlaySurface;

        return GameplayEventBus.HeightAbovePlaySurface;
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

        if (_minimapChrome != null && !_minimapVisible)
            _minimapChrome.style.display = DisplayStyle.None;

        ApplyPartnerAccent();
    }

    void ApplyPartnerAccent()
    {
        if (!_partnerAccentColor.HasValue) return;

        Color accentColor = _partnerAccentColor.Value;
        if (_minimapChrome != null)
            _minimapChrome.style.borderTopColor =
                _minimapChrome.style.borderBottomColor =
                    _minimapChrome.style.borderLeftColor =
                        _minimapChrome.style.borderRightColor = accentColor;

        if (_minimapCloseView != null)
            _minimapCloseView.style.unityBackgroundImageTintColor = accentColor;
    }

    void OnPartnersUnlocked(int partnerCount)
    {
        if (partnerCount != 1 || destroyWhenTrackedPlayerFalls)
            return;

        alwaysVisibleWithPartners = true;
        hideOnFlipHold = false;
        minimapCloseViewMinHeightMeters = 0f;

        if (_gameStarted && (trackedPlayer == null || !trackedPlayer.HasFallen))
            ShowLens();
    }

    void OnFlipHoldStarted()
    {
        _gameStarted = true;
        if (hideOnFlipHold && !alwaysVisibleWithPartners)
            HideLens();
    }

    void OnFallenOffSurface()
    {
        _gameStarted = false;
        HideLens();
    }

    void OnPlayerFell(PlayerController player)
    {
        if (trackedPlayer == null || player != trackedPlayer)
            return;

        HideLens();
        if (destroyWhenTrackedPlayerFalls)
            Destroy(gameObject);
    }

    void UpdateMinimapVisibility()
    {
        if (_minimapChrome == null) return;

        if (alwaysVisibleWithPartners && GameplayEventBus.PartnersActive)
        {
            bool show = _gameStarted && (trackedPlayer == null || !trackedPlayer.HasFallen);
            SetMinimapVisible(show);
            return;
        }

        float heightAboveSurface = GetTrackingHeight();
        float hysteresis = _minimapVisible ? 9f : 0f;
        bool showNormal = _gameStarted &&
                          heightAboveSurface >= (minimapCloseViewMinHeightMeters - hysteresis);

        if (trackedPlayer != null)
            showNormal = _gameStarted && !trackedPlayer.HasFallen && showNormal;

        SetMinimapVisible(showNormal);
    }

    void SetMinimapVisible(bool show)
    {
        if (show == _minimapVisible) return;

        _minimapVisible = show;
        if (show)
            ShowLens();
        else
            HideLens();
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
