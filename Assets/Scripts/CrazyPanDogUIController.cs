using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(100)]
public class CrazyPanDogUIController : MonoBehaviour
{
    [SerializeField] float introSeconds = 0.28f;
    [SerializeField] float holdSeconds = 0.42f;
    [SerializeField] float fadeOutSeconds = 0.22f;
    [SerializeField] float jitterPixels = 22f;
    [SerializeField] float liveCountPulseScale = 1.32f;
    [SerializeField] float liveCountPulseDuration = 0.22f;
    [SerializeField] float promptBlinkInterval = 0.8f;

    [Header("World Flip Combo")]
    [SerializeField] PanelSettings flipComboPanelSettings;
    [SerializeField] VisualTreeAsset flipComboWorldTree;
    [Tooltip("Added to (0, HeightAbovePlaySurface, 0). Z puts it behind the player.")]
    [SerializeField] Vector3 liveCounterWorldOffset = new Vector3(0f, 2.5f, -2.8f);
    [Tooltip("Target offset when the lens first appears; scales up with lens zoom after that.")]
    [SerializeField] Vector3 liveCounterLensWorldOffset = new Vector3(2f, 5f, -2.8f);
    [SerializeField] float liveCounterLensOffsetBlendSeconds = 0.3f;
    [SerializeField] MagnifiyingLens magnifyingLens;

    [Header("Flip Combo Constant Screen Size")]
    [FormerlySerializedAs("flipComboReferenceOrthoSize")]
    [SerializeField] float referenceOrthoSize = 12.12f;
    [FormerlySerializedAs("flipComboWorldScale")]
    [SerializeField, Range(0.05f, 2f)] float overallSizeMultiplier = 0.35f;
    [SerializeField] CameraHeightZoom cameraHeightZoom;

    const float FlipComboWorldPanelWidth = 600f;
    const float MagnifyingLensWorldPanelWidth = 300f;

    [Header("Altitude (optional)")]
    [SerializeField] Transform altitudePlayer;
    [SerializeField] Collider2D altitudeGroundCollider;

    [Header("Gameplay")]
    [SerializeField] PlayerController playerController;

    [Header("Audio")]
    [SerializeField] AudioSource musicSource;
    [SerializeField] AudioSource sfxSource;

    [Header("Minimap")]
    [SerializeField] RenderTexture closeViewTexture;
    [SerializeField] float minimapCloseViewMinHeightMeters = 6f;

    [Header("Game Over Reveal")]
    [SerializeField] float gameOverScoreCountSeconds = 1.05f;
    [SerializeField] float gameOverScorePunchSeconds = 0.22f;
    [SerializeField] float gameOverRankRevealDelay = 0.12f;
    [SerializeField] float gameOverRankRevealSeconds = 0.35f;
    [Tooltip("Delay (seconds) between FallenOffSurface and the overlay appearing.")]
    [SerializeField] float gameOverShowDelaySeconds = 1.0f;

    [Header("Finite End Mode (GameWithEnd scene)")]
    [Tooltip("When true: progress shown as percent, win condition active, scoring switches to time-to-goal.")]
    [SerializeField] bool useFiniteEndMode;
    [SerializeField] float goalHeightMeters = 1000f;
    [SerializeField] float endOverlayFadeSeconds = 0.6f;
    [SerializeField] float endUfoBoardDelaySeconds = 0.4f;
    [SerializeField] float endUfoBoardSeconds = 6f;
    [SerializeField] float endUfoBoardScaleEnd = 0.2f;
    [SerializeField] float endUfoBoardRiseMeters = 40f;
    [SerializeField] float endCreditsHoldSeconds = 22f;
    [SerializeField] float endStatsRevealFadeSeconds = 0.45f;
    [SerializeField] Transform endSequencePlayerRoot;
    [SerializeField] Transform ufoBeamTarget;
    [SerializeField] Vector3 ufoBeamWorldOffset = Vector3.zero;

    UIDocument _ui;
    UIDocument _worldFlipComboUi;
    Transform _worldFlipComboTransform;
    Vector3 _flipComboBaseScale = Vector3.one;

    // start screen
    VisualElement _startScreen;
    VisualElement _startBottomBar;
    Label _playPrompt;
    Button _btnProfile;
    Label _labelProfileName;
    Button _btnOptions;
    Button _btnLeaderboard;

    // overlays
    VisualElement _optionsOverlay;
    VisualElement _leaderboardOverlay;
    Slider _sliderSound;
    TextField _inputPlayerName;
    Button _btnOptionsClose;
    Button _btnLeaderboardClose;
    VisualElement _leaderboardList;

    VisualElement _gameOverOverlay;
    VisualElement _gameOverScoreBlock;
    Label _gameOverScore;
    Label _gameOverScoreSub;
    VisualElement _gameOverAngleRow;
    Label _gameOverAngleValue;
    Label _gameOverAngleSafe;
    VisualElement _gameOverRankBadge;
    Label _gameOverRankCaption;
    VisualElement _gameOverRankValueRow;
    Label _gameOverRankNumber;
    Label _gameOverRankFallback;
    VisualElement _gameOverLeaderboardList;
    VisualElement _gameOverExtraStats;
    VisualElement _gameOverTimeRow;
    Label _gameOverTimeValue;
    Button _btnGameOverRestart;
    VisualElement _cheeringCrowd;

    // game hud
    VisualElement _gameHud;
    VisualElement _liveWrap;
    VisualElement _liveAuraInner;
    VisualElement _liveAuraOuter;
    Label _liveLabel;
    VisualElement _meterFill;
    Label _liveMilestoneLabel;
    VisualElement _altitudeWrap;
    Label _altitudeLabel;
    Label _scoreLabel;
    VisualElement _minimapChrome;
    Image _minimapCloseView;

    Camera _cachedCam;
    Coroutine _punchRoutine;
    Coroutine _milestoneRoutine;
    Coroutine _gameOverRevealRoutine;
    Coroutine _gameOverDelayRoutine;
    Coroutine _endFlowRoutine;

    int _lastLiveFlipFloor = -1;
    int _totalFlips;
    bool _gameStarted;
    bool _overlayOpen;
    bool _gameOverOpen;
    bool _minimapVisible;
    bool _startScreenTouchHeld;

    // finite-mode end UI
    VisualElement _gameEndOverlay;
    VisualElement _gameEndCredits;
    Label _endStatFlips;
    Label _endStatPerfect;
    Label _endNewBest;

    // finite-mode per-run stats
    int _runPerfectFlips;
    int _runPerfectStreak;
    float _runStartTime;
    bool _gameWon;
    bool _isFiniteWinReveal;
    float _winRunTime;
    int _winFlips;
    int _winPerfect;
    int _winRank;
    EndFlowPhase _endPhase = EndFlowPhase.None;
    bool _perfectStreakMedalAwarded;

    enum EndFlowPhase { None, UfoBoarding, Credits, Actions }
    float _flipComboOffsetBlend;
    bool _flipComboLensClassApplied;
    float _lensOffsetReferenceZoomRatio = 1f;
    bool _lensOffsetReferenceCaptured;
    Coroutine _promptBlink;
    Rect _lastSafeArea;

    static readonly string[] TierClasses =
    {
        "tier-live-zero", "tier-low", "tier-mid", "tier-high", "tier-hype", "tier-god"
    };

    static readonly string[] MilestoneClasses =
    {
        "ms-1", "ms-2", "ms-3", "ms-4", "ms-5"
    };

    // (threshold, word, milestone-class)
    static readonly (int threshold, string word, string cls)[] LiveMilestones =
    {
        (5,   "STREAK!",   "ms-1"),
        (10,  "ON FIRE!",  "ms-2"),
        (20,  "BLAZING!",  "ms-3"),
        (50,  "INSANE!",   "ms-4"),
        (100, "LEGEND!",   "ms-5"),
    };


    const int LeaderboardMetricVersion = 2;

    /// <summary>
    /// True when a UI overlay is open. PlayerController should check this
    /// and ignore input while it's true.
    /// </summary>
    public static bool InputBlocked { get; private set; }

    public static bool GameStarted { get; private set; }

    static CrazyPanDogUIController _instance;

    /// <summary>
    /// True when the pointer is over an interactive UI button. PlayerController
    /// checks this so a tap-to-rotate anywhere on screen still leaves the
    /// start-screen / game-over buttons clickable.
    /// </summary>
    public static bool PointerOverUiButton(Vector2 screenPos)
    {
        var inst = _instance;
        if (inst == null || inst._ui == null) return false;
        VisualElement root = inst._ui.rootVisualElement;
        if (root == null || root.panel == null) return false;

        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, screenPos);
        VisualElement picked = root.panel.Pick(panelPos);
        while (picked != null)
        {
            if (picked is Button) return true;
            picked = picked.parent;
        }
        return false;
    }

    void Awake()
    {
        _ui = GetComponent<UIDocument>();
        EnsureWorldFlipComboUi();
        if (useFiniteEndMode)
            NewgroundsApi.Init();
    }

    void OnEnable()
    {
        _instance = this;
        GameplayEventBus.TrampolineLanding += OnTrampolineLanding;
        GameplayEventBus.AirborneFlipProgress += OnAirborneFlipProgress;
        GameplayEventBus.TotalLifetimeFlipsChanged += OnTotalFlipsChanged;
        GameplayEventBus.FlipHoldStarted += OnFlipHoldStarted;
        GameplayEventBus.FallenOffSurface += OnFallenOffSurface;
        GameplayEventBus.PartnersUnlocked += OnPartnersUnlocked;
        GameplayEventBus.PerfectLanding += OnPerfectLandingForRun;

        CacheAllRefs();
        SetupStartScreen();
    }

    void OnDisable()
    {
        GameplayEventBus.TrampolineLanding -= OnTrampolineLanding;
        GameplayEventBus.AirborneFlipProgress -= OnAirborneFlipProgress;
        GameplayEventBus.TotalLifetimeFlipsChanged -= OnTotalFlipsChanged;
        GameplayEventBus.FlipHoldStarted -= OnFlipHoldStarted;
        GameplayEventBus.FallenOffSurface -= OnFallenOffSurface;
        GameplayEventBus.PartnersUnlocked -= OnPartnersUnlocked;
        GameplayEventBus.PerfectLanding -= OnPerfectLandingForRun;

        UnbindButtons();
        InputBlocked = false;
        if (_instance == this) _instance = null;
    }

    void Start()
    {
        if (cameraHeightZoom == null)
            cameraHeightZoom = FindAnyObjectByType<CameraHeightZoom>();
        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();
        CacheAllRefs();
    }

    void Update()
    {
        bool spacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        var pointer = UnityEngine.InputSystem.Pointer.current; // disambiguate from System.Reflection.Pointer
        bool tapped = pointer != null && pointer.press.wasPressedThisFrame;

        // UFO boarding: space or tap skips straight to credits.
        if (_endPhase == EndFlowPhase.UfoBoarding)
        {
            if (spacePressed || tapped)
                SkipUfoBoardingToCredits();
            return;
        }

        // Credits phase: space or tap skips to the win reveal.
        if (_endPhase == EndFlowPhase.Credits)
        {
            if (spacePressed || tapped)
                SkipCreditsToReveal();
            return;
        }

        // Win reveal (Actions phase) or game-over screen: space restarts
        // (touch uses the TRY AGAIN button to avoid accidental restarts).
        if (_endPhase == EndFlowPhase.Actions || _gameOverOpen)
        {
            if (spacePressed)
                OnGameOverRestartClicked();
        }

        CheckManualButtonTaps();

        // Release external flip held that was started by start-screen touch.
        if (_startScreenTouchHeld && (pointer == null || !pointer.press.isPressed))
        {
            playerController?.SetExternalFlipHeld(false);
            _startScreenTouchHeld = false;
        }
    }

    void LateUpdate()
    {
        ApplySafeArea();
        UpdateAltitude();
        UpdateLiveCounterPlacement();
        MaintainFlipComboScreenSize();
    }

    void OnFlipHoldStarted()
    {
        if (_gameStarted || _overlayOpen) return;
        StartCoroutine(TransitionToGameNextFrame());
    }

    IEnumerator TransitionToGameNextFrame()
    {
        yield return null;
        if (!_gameStarted && !_overlayOpen)
            TransitionToGame();
    }

    // Manual button hit-testing — replaces UI Toolkit button.clicked for mobile WebGL
    // where canvas touch events may not reach the event pipeline reliably.
    void CheckManualButtonTaps()
    {
        var ptr = UnityEngine.InputSystem.Pointer.current;
        if (ptr == null || !ptr.press.wasPressedThisFrame) return;

        var pos = ptr.position.ReadValue();

        // Overlays open: handle their BACK buttons only.
        if (_overlayOpen && !_gameOverOpen)
        {
            if (_optionsOverlay != null && !_optionsOverlay.ClassListContains("hidden")
                && HitButton(_btnOptionsClose, pos))
            { OnOptionsCloseClicked(); return; }
            if (_leaderboardOverlay != null && !_leaderboardOverlay.ClassListContains("hidden")
                && HitButton(_btnLeaderboardClose, pos))
            { OnLeaderboardCloseClicked(); return; }
            return;
        }

        // Game-over card: TRY AGAIN.
        if (_gameOverOpen)
        {
            if (HitButton(_btnGameOverRestart, pos)) OnGameOverRestartClicked();
            return;
        }

        // Start screen: use screen-space zones (panel-space hit-testing is unreliable on WebGL).
        if (!_gameStarted)
        {
            float nx = pos.x / Screen.width;
            float ny = pos.y / Screen.height; // Y=0 at bottom in Unity screen space

            // Bottom bar: OPTIONS (left half) or SCORES (right half).
            if (ny < 0.12f)
            {
                if (nx < 0.5f) OnOptionsClicked();
                else OnLeaderboardClicked();
                return;
            }

            // Profile chip: top-right corner.
            if (ny > 0.88f && nx > 0.6f)
            {
                OnOptionsClicked();
                return;
            }

            // Anywhere else starts the game.
            TransitionToGame();
            if (playerController != null)
            {
                playerController.SetExternalFlipHeld(true);
                _startScreenTouchHeld = true;
            }
        }
    }

    bool HitButton(VisualElement el, Vector2 screenPos)
    {
        if (el == null || _ui == null) return false;
        var panel = _ui.rootVisualElement.panel;
        if (panel == null) return false;
        var p = RuntimePanelUtils.ScreenToPanel(panel, screenPos);
        var b = el.worldBound;
        if (b.width <= 0 || b.height <= 0) return false;
        float mx = Mathf.Max(b.width * 0.5f, 40f);
        float my = Mathf.Max(b.height * 1.0f, 44f);
        return p.x >= b.xMin - mx && p.x <= b.xMax + mx
            && p.y >= b.yMin - my && p.y <= b.yMax + my;
    }

    // ───────── caching ─────────

    void CacheAllRefs()
    {
        VisualElement root = _ui != null ? _ui.rootVisualElement : null;
        if (root == null) return;

        _startScreen = root.Q<VisualElement>("start-screen");
        _startBottomBar = root.Q<VisualElement>(className: "start-bottom-bar");
        _playPrompt = root.Q<Label>("play-prompt");
        _btnProfile = root.Q<Button>("btn-profile");
        _labelProfileName = root.Q<Label>("label-profile-name");
        _btnOptions = root.Q<Button>("btn-options");
        _btnLeaderboard = root.Q<Button>("btn-leaderboard");

        _optionsOverlay = root.Q<VisualElement>("options-overlay");
        _leaderboardOverlay = root.Q<VisualElement>("leaderboard-overlay");
        _sliderSound = root.Q<Slider>("slider-sound");
        _inputPlayerName = root.Q<TextField>("input-player-name");
        _btnOptionsClose = root.Q<Button>("btn-options-close");
        _btnLeaderboardClose = root.Q<Button>("btn-leaderboard-close");
        _leaderboardList = root.Q<VisualElement>("leaderboard-list");

        _gameOverOverlay = root.Q<VisualElement>("game-over-overlay");
        _gameOverScoreBlock = root.Q<VisualElement>("game-over-score-block");
        _gameOverScore = root.Q<Label>("game-over-score");
        _gameOverRankBadge = root.Q<VisualElement>("game-over-rank-badge");
        _gameOverRankCaption = root.Q<Label>("game-over-rank-caption");
        _gameOverRankValueRow = root.Q<VisualElement>("game-over-rank-value-row");
        _gameOverRankNumber = root.Q<Label>("game-over-rank-number");
        _gameOverRankFallback = root.Q<Label>("game-over-rank-fallback");
        _gameOverScoreSub = root.Q<Label>("game-over-score-sub");
        _gameOverAngleRow = root.Q<VisualElement>("game-over-angle-row");
        _gameOverAngleValue = root.Q<Label>("game-over-angle-value");
        _gameOverAngleSafe = root.Q<Label>("game-over-angle-safe");
        _gameOverLeaderboardList = root.Q<VisualElement>("game-over-leaderboard-list");
        _gameOverExtraStats = root.Q<VisualElement>("game-over-extra-stats");
        _gameOverTimeRow = root.Q<VisualElement>("game-over-time-row");
        _gameOverTimeValue = root.Q<Label>("game-over-time-value");
        _btnGameOverRestart = root.Q<Button>("btn-game-over-restart");

        _cheeringCrowd = root.Q<VisualElement>("cheering-crowd");

        _gameEndOverlay = root.Q<VisualElement>("game-end-overlay");
        _gameEndCredits = root.Q<VisualElement>("game-end-credits");
        _endStatFlips = root.Q<Label>("end-stat-flips");
        _endStatPerfect = root.Q<Label>("end-stat-perfect");
        _endNewBest = root.Q<Label>("end-new-best");

        _gameHud = root.Q<VisualElement>("game-hud");
        _altitudeWrap = root.Q<VisualElement>("altitude-hud-wrap");
        _altitudeLabel = root.Q<Label>("altitude-label");
        _scoreLabel = root.Q<Label>("score-label");

        _minimapChrome = root.Q<VisualElement>("minimap-chrome");
        _minimapCloseView = root.Q<Image>("minimap-close-view");
        if (_minimapCloseView != null && closeViewTexture != null)
            _minimapCloseView.image = closeViewTexture;

        CacheFlipComboWorldRefs();
    }

    void EnsureWorldFlipComboUi()
    {
        if (_worldFlipComboUi != null) return;

        if (flipComboWorldTree == null)
            flipComboWorldTree = Resources.Load<VisualTreeAsset>("UI/FlipComboWorld");
        if (flipComboPanelSettings == null || flipComboWorldTree == null) return;

        var go = new GameObject("FlipComboWorld");
        _worldFlipComboTransform = go.transform;
        _flipComboBaseScale = _worldFlipComboTransform.localScale;

        _worldFlipComboUi = go.AddComponent<UIDocument>();
        _worldFlipComboUi.panelSettings = flipComboPanelSettings;
        _worldFlipComboUi.visualTreeAsset = flipComboWorldTree;
        _worldFlipComboUi.sortingOrder = -5;
        ConfigureWorldSpacePanel(_worldFlipComboUi, 600f, 400f);

        go.SetActive(false);
    }

    static void ConfigureWorldSpacePanel(UIDocument doc, float width, float height)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(UIDocument);
        type.GetField("m_WorldSpaceSizeMode", flags)?.SetValue(doc, 1);
        type.GetField("m_WorldSpaceWidth", flags)?.SetValue(doc, width);
        type.GetField("m_WorldSpaceHeight", flags)?.SetValue(doc, height);
    }

    void CacheFlipComboWorldRefs()
    {
        EnsureWorldFlipComboUi();
        if (_worldFlipComboUi == null) return;

        VisualElement root = _worldFlipComboUi.rootVisualElement;
        if (root == null) return;

        _liveWrap = root.Q<VisualElement>("live-flip-wrap");
        _liveAuraInner = root.Q<VisualElement>("live-flip-aura");
        _liveAuraOuter = root.Q<VisualElement>("live-flip-aura-outer");
        _liveLabel = root.Q<Label>("live-flip-label");
        _meterFill = root.Q<VisualElement>("live-flip-meter-fill");
        _liveMilestoneLabel = root.Q<Label>("live-flip-milestone");
    }

    // ───────── start screen ─────────

    void SetupStartScreen()
    {
        _gameStarted = false;
        GameStarted = false;
        _overlayOpen = false;
        _gameOverOpen = false;
        _gameWon = false;
        _endPhase = EndFlowPhase.None;
        InputBlocked = false;
        Time.timeScale = 1f; // restart reloads the scene but timeScale persists across loads

        MigrateLeaderboardMetricIfNeeded();
        RefreshProfileName();

        ShowElement(_startScreen);
        HideElement(_gameHud);
        HideElement(_optionsOverlay);
        HideElement(_leaderboardOverlay);
        HideElement(_gameOverOverlay);
        HideElement(_gameEndOverlay);
        HideElement(_gameEndCredits);
        HideElement(_gameOverExtraStats);
        HideElement(_gameOverTimeRow);
        HideElement(_gameOverAngleRow);
        if (_endNewBest != null) HideElement(_endNewBest);
        _isFiniteWinReveal = false;
        if (_gameOverScoreSub != null) _gameOverScoreSub.text = "MAX HEIGHT";

        UnbindButtons();
        BindButtons();

        if (_sliderSound != null)
        {
            float savedSound = AudioManagerScript.GetSavedSoundVolume();
            _sliderSound.value = savedSound;
            ApplySoundVolume(savedSound);
            _sliderSound.RegisterValueChangedCallback(OnSoundSliderChanged);
        }

        if (_inputPlayerName != null)
        {
            _inputPlayerName.value = PlayerPrefs.GetString("PlayerName", "Player");
            _inputPlayerName.RegisterValueChangedCallback(OnPlayerNameChanged);
        }

        PopulateLeaderboard(_leaderboardList);

        if (_promptBlink != null) StopCoroutine(_promptBlink);
        _promptBlink = StartCoroutine(PromptBlinkRoutine());
    }

    void RefreshProfileName()
    {
        if (_labelProfileName == null) return;
        string name = PlayerPrefs.GetString("PlayerName", "Player");
        _labelProfileName.text = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
    }

    static void MigrateLeaderboardMetricIfNeeded()
    {
        if (PlayerPrefs.GetInt("LB_MetricVersion", 0) >= LeaderboardMetricVersion) return;
        PlayerPrefs.SetInt("LB_Count", 0);
        PlayerPrefs.SetInt("LB_MetricVersion", LeaderboardMetricVersion);
        PlayerPrefs.Save();
    }

    static int HeightScoreFromPeak(float peakMeters) =>
        Mathf.RoundToInt(Mathf.Max(0f, peakMeters) * 10f);

    static string FormatHeightScore(int storedScore) =>
        $"{storedScore / 10f:F1} m";

    void BindButtons()
    {
        if (_btnProfile != null) _btnProfile.clicked += OnOptionsClicked;
        if (_btnOptions != null) _btnOptions.clicked += OnOptionsClicked;
        if (_btnLeaderboard != null) _btnLeaderboard.clicked += OnLeaderboardClicked;
        if (_btnOptionsClose != null) _btnOptionsClose.clicked += OnOptionsCloseClicked;
        if (_btnLeaderboardClose != null) _btnLeaderboardClose.clicked += OnLeaderboardCloseClicked;
        if (_btnGameOverRestart != null) _btnGameOverRestart.clicked += OnGameOverRestartClicked;
    }

    void UnbindButtons()
    {
        if (_btnProfile != null) _btnProfile.clicked -= OnOptionsClicked;
        if (_btnOptions != null) _btnOptions.clicked -= OnOptionsClicked;
        if (_btnLeaderboard != null) _btnLeaderboard.clicked -= OnLeaderboardClicked;
        if (_btnOptionsClose != null) _btnOptionsClose.clicked -= OnOptionsCloseClicked;
        if (_btnLeaderboardClose != null) _btnLeaderboardClose.clicked -= OnLeaderboardCloseClicked;
        if (_btnGameOverRestart != null) _btnGameOverRestart.clicked -= OnGameOverRestartClicked;

        if (_sliderSound != null) _sliderSound.UnregisterValueChangedCallback(OnSoundSliderChanged);
        if (_inputPlayerName != null) _inputPlayerName.UnregisterValueChangedCallback(OnPlayerNameChanged);
    }

    IEnumerator PromptBlinkRoutine()
    {
        while (!_gameStarted)
        {
            if (_playPrompt != null) _playPrompt.AddToClassList("pulse-dim");
            yield return new WaitForSecondsRealtime(promptBlinkInterval);
            if (_playPrompt != null) _playPrompt.RemoveFromClassList("pulse-dim");
            yield return new WaitForSecondsRealtime(promptBlinkInterval);
        }
    }

    void TransitionToGame()
    {
        _gameStarted = true;
        GameStarted = true;
        if (_promptBlink != null) StopCoroutine(_promptBlink);

        GameplayEventBus.ResetPeakHeight();
        GameplayEventBus.ResetRunStats();
        if (playerController != null)
            playerController.ResetSessionScores();

        _runPerfectFlips = 0;
        _runPerfectStreak = 0;
        _perfectStreakMedalAwarded = false;
        _runStartTime = Time.unscaledTime;
        GameplayEventBus.RunStartTime = _runStartTime;
        _gameWon = false;
        _isFiniteWinReveal = false;
        _endPhase = EndFlowPhase.None;
        Time.timeScale = 1f;
        if (_gameOverScoreSub != null) _gameOverScoreSub.text = "MAX HEIGHT";
        HideElement(_gameOverExtraStats);
        HideElement(_gameOverTimeRow);
        HideElement(_gameOverAngleRow);
        if (_endNewBest != null) HideElement(_endNewBest);
        if (_cheeringCrowd != null) ShowElement(_cheeringCrowd);

        HideElement(_startScreen);
        HideElement(_optionsOverlay);
        HideElement(_leaderboardOverlay);
        ShowElement(_gameHud);

        _overlayOpen = false;
        _gameOverOpen = false;
        InputBlocked = false;
        HideElement(_gameOverOverlay);

        RefreshAltitudeVisibility();
        if (_worldFlipComboTransform != null) _worldFlipComboTransform.gameObject.SetActive(true);
        if (_liveWrap != null) _liveWrap.AddToClassList("hidden");
        HideMilestone();
        if (_scoreLabel != null) _scoreLabel.text = "0";

        _minimapVisible = false;
        if (_minimapChrome != null) _minimapChrome.RemoveFromClassList("minimap-visible");
        _flipComboOffsetBlend = 0f;
        _lensOffsetReferenceCaptured = false;
        _lensOffsetReferenceZoomRatio = 1f;
        UpdateFlipComboLensVisuals(false);
    }

    // ───────── overlay input blocking ─────────

    void OpenOverlay(VisualElement overlay)
    {
        _overlayOpen = true;
        InputBlocked = true;
        ShowElement(overlay);
        if (overlay != null)
            overlay.pickingMode = PickingMode.Position;
    }

    void CloseOverlay(VisualElement overlay)
    {
        _overlayOpen = false;
        if (!_gameOverOpen)
            InputBlocked = false;
        HideElement(overlay);
    }

    // ───────── game over ─────────

    void OnFallenOffSurface()
    {
        if (!_gameStarted || _gameOverOpen) return;
        if (_gameWon) return; // won runs never fall through to game-over

        _gameOverOpen = true;
        _overlayOpen = true;
        InputBlocked = true;

        // Freeze the falling world so it stops moving behind the end screen.
        // All end-screen UI animates on unscaled time, so it keeps running.
        Time.timeScale = 0f;

        HideElement(_optionsOverlay);
        HideElement(_leaderboardOverlay);

        if (_gameOverDelayRoutine != null) StopCoroutine(_gameOverDelayRoutine);
        _gameOverDelayRoutine = StartCoroutine(GameOverShowRoutine());
    }

    IEnumerator GameOverShowRoutine()
    {
        // Clear the in-game HUD immediately so it doesn't double up over the card.
        HideElement(_gameHud);
        _minimapVisible = false;
        if (_minimapChrome != null) _minimapChrome.RemoveFromClassList("minimap-visible");
        if (_worldFlipComboTransform != null) _worldFlipComboTransform.gameObject.SetActive(false);

        // Elapsed run time captured at the moment of the fall (unscaled time keeps
        // ticking through the frozen world).
        float runTime = Mathf.Max(0f, Time.unscaledTime - _runStartTime);

        float delay = Mathf.Max(0f, gameOverShowDelaySeconds);
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        float peak = GameplayEventBus.PeakHeightAbovePlaySurface;
        int runScore = useFiniteEndMode
            ? PercentFromHeight(peak)            // finite mode: display % of goal
            : HeightScoreFromPeak(peak);
        int rank = useFiniteEndMode ? 0 : SaveScoreToLeaderboard(runScore);
        string playerName = PlayerPrefs.GetString("PlayerName", "Player");

        if (useFiniteEndMode)
        {
            if (_gameOverLeaderboardList != null)
                _gameOverLeaderboardList.AddToClassList("hidden");
            if (_gameOverScoreSub != null) _gameOverScoreSub.text = "PROGRESS";
            HideElement(_gameOverExtraStats);
            if (_endNewBest != null) HideElement(_endNewBest);
            if (_gameOverTimeValue != null) _gameOverTimeValue.text = FormatRunTime(runTime);
            ShowElement(_gameOverTimeRow);
        }
        else
        {
            HideElement(_gameOverTimeRow);
            PopulateLeaderboard(_gameOverLeaderboardList, playerName, rank, aboveCount: 2, belowCount: 2);
        }

        PopulateAngleRow();

        ResetGameOverPresentation();

        ShowElement(_gameOverOverlay);
        if (_gameOverOverlay != null)
            _gameOverOverlay.pickingMode = PickingMode.Position;

        if (_gameOverRevealRoutine != null)
            StopCoroutine(_gameOverRevealRoutine);
        _gameOverRevealRoutine = StartCoroutine(GameOverRevealRoutine(peak, runScore, rank));
        _gameOverDelayRoutine = null;
    }

    int PercentFromHeight(float meters) =>
        goalHeightMeters > 0f
            ? Mathf.Clamp(Mathf.RoundToInt(meters / goalHeightMeters * 100f), 0, 100)
            : 0;

    void ResetGameOverPresentation()
    {
        if (_gameOverScore != null)
        {
            _gameOverScore.text = "0.0";
            _gameOverScore.style.opacity = 0f;
            _gameOverScore.style.scale = new Scale(Vector3.one * 0.35f);
            _gameOverScore.style.rotate = new Rotate(0f);
        }

        if (_gameOverScoreBlock != null)
            _gameOverScoreBlock.style.opacity = 1f;

        HideElement(_gameOverRankBadge);
        if (_gameOverRankBadge != null)
        {
            _gameOverRankBadge.RemoveFromClassList("game-over-rank-badge-top");
            _gameOverRankBadge.style.opacity = 0f;
            _gameOverRankBadge.style.scale = new Scale(Vector3.one * 0.7f);
        }

        if (_gameOverRankValueRow != null)
            ShowElement(_gameOverRankValueRow);
        if (_gameOverRankCaption != null)
            ShowElement(_gameOverRankCaption);
        HideElement(_gameOverRankFallback);

        if (_gameOverLeaderboardList != null)
            _gameOverLeaderboardList.style.opacity = 0.35f;
    }

    IEnumerator GameOverRevealRoutine(float targetValue, int runScore, int rank)
    {
        float countDur = Mathf.Max(0.01f, gameOverScoreCountSeconds);
        float punchDur = Mathf.Max(0.01f, gameOverScorePunchSeconds);
        float target = Mathf.Max(0f, targetValue);
        bool finite = useFiniteEndMode;
        bool isWin = _isFiniteWinReveal;
        int finitePct = (finite && !isWin) ? PercentFromHeight(target) : 0;

        // Count up the headline number with ease-out while scaling in.
        float t = 0f;
        while (t < countDur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / countDur);
            float eased = 1f - Mathf.Pow(1f - u, 3f);

            if (_gameOverScore != null)
            {
                string txt;
                if (isWin) txt = FormatRunTime(target * eased);
                else if (finite) txt = $"{Mathf.RoundToInt(finitePct * eased)}%";
                else txt = $"{target * eased:F1}";
                _gameOverScore.text = txt;

                float s = Mathf.Lerp(0.45f, 1.08f, eased);
                _gameOverScore.style.scale = new Scale(Vector3.one * s);
                _gameOverScore.style.opacity = Mathf.Lerp(0f, 1f, Mathf.Min(1f, u * 2.5f));
                float wobble = Mathf.Sin(u * Mathf.PI * 3f) * (1f - u) * 4f;
                _gameOverScore.style.rotate = new Rotate(wobble);
            }

            yield return null;
        }

        if (_gameOverScore != null)
        {
            string txt;
            if (isWin) txt = FormatRunTime(target);
            else if (finite) txt = $"{finitePct}%";
            else txt = $"{target:F1}";
            _gameOverScore.text = txt;
            _gameOverScore.style.opacity = 1f;
        }

        // Landing punch on the final height.
        t = 0f;
        float sign = Random.value < 0.5f ? -1f : 1f;
        float angleAmp = 7f * sign;
        while (t < punchDur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / punchDur);
            float s;
            if (u < 0.45f)       s = Mathf.Lerp(1.08f, 1.22f, u / 0.45f);
            else if (u < 0.78f)  s = Mathf.Lerp(1.22f, 0.96f, (u - 0.45f) / 0.33f);
            else                 s = Mathf.Lerp(0.96f, 1f, (u - 0.78f) / 0.22f);

            if (_gameOverScore != null)
            {
                _gameOverScore.style.scale = new Scale(Vector3.one * s);
                _gameOverScore.style.rotate = new Rotate(angleAmp * (1f - u));
            }
            yield return null;
        }

        if (_gameOverScore != null)
        {
            _gameOverScore.style.scale = new Scale(Vector3.one);
            _gameOverScore.style.rotate = new Rotate(0f);
        }

        if (gameOverRankRevealDelay > 0f)
            yield return new WaitForSecondsRealtime(gameOverRankRevealDelay);

        // Finite-mode FALL has no leaderboard (only wins enter the time leaderboard).
        // Finite-mode WIN gets the full reveal (rank + leaderboard + extra stats).
        if (useFiniteEndMode && !_isFiniteWinReveal)
        {
            _gameOverRevealRoutine = null;
            yield break;
        }

        ConfigureGameOverRankBadge(runScore, rank);
        ShowElement(_gameOverRankBadge);

        float revealDur = Mathf.Max(0.01f, gameOverRankRevealSeconds);
        t = 0f;
        while (t < revealDur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / revealDur);
            float eased = 1f - Mathf.Pow(1f - u, 3f);
            float s = Mathf.Lerp(0.72f, 1.06f, eased);
            if (_gameOverRankBadge != null)
            {
                _gameOverRankBadge.style.opacity = eased;
                _gameOverRankBadge.style.scale = new Scale(Vector3.one * s);
            }
            yield return null;
        }

        if (_gameOverRankBadge != null)
        {
            _gameOverRankBadge.style.opacity = 1f;
            _gameOverRankBadge.style.scale = new Scale(Vector3.one);
        }

        // Fade leaderboard in after the headline stats land.
        if (_gameOverLeaderboardList != null)
        {
            float lbFade = 0.28f;
            t = 0f;
            while (t < lbFade)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / lbFade);
                _gameOverLeaderboardList.style.opacity = Mathf.Lerp(0.35f, 1f, u);
                yield return null;
            }
            _gameOverLeaderboardList.style.opacity = 1f;
        }

        _gameOverRevealRoutine = null;
    }

    void ConfigureGameOverRankBadge(int runScore, int rank)
    {
        if (_gameOverRankBadge == null) return;

        // Finite-mode FALL: hide the rank badge — only wins enter the time leaderboard.
        if (useFiniteEndMode && !_isFiniteWinReveal)
        {
            HideElement(_gameOverRankBadge);
            return;
        }

        if (runScore <= 0)
        {
            if (_gameOverRankCaption != null)
                _gameOverRankCaption.text = "RESULT";
            HideElement(_gameOverRankValueRow);
            if (_gameOverRankCaption != null)
                HideElement(_gameOverRankCaption);
            if (_gameOverRankFallback != null)
            {
                _gameOverRankFallback.text = "NO HEIGHT RECORDED";
                ShowElement(_gameOverRankFallback);
            }
            return;
        }

        if (_gameOverRankCaption != null)
        {
            _gameOverRankCaption.text = "YOUR RANK";
            ShowElement(_gameOverRankCaption);
        }

        if (rank > 0)
        {
            if (rank <= 3)
                _gameOverRankBadge.AddToClassList("game-over-rank-badge-top");
            else
                _gameOverRankBadge.RemoveFromClassList("game-over-rank-badge-top");

            ShowElement(_gameOverRankValueRow);
            HideElement(_gameOverRankFallback);
            if (_gameOverRankNumber != null)
                _gameOverRankNumber.text = rank.ToString();
            return;
        }

        _gameOverRankBadge.RemoveFromClassList("game-over-rank-badge-top");

        HideElement(_gameOverRankValueRow);
        if (_gameOverRankFallback != null)
        {
            _gameOverRankFallback.text = "NOT IN TOP 10";
            ShowElement(_gameOverRankFallback);
        }
    }

    void OnGameOverRestartClicked()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ───────── options ─────────

    void OnOptionsClicked()
    {
        OpenOverlay(_optionsOverlay);
    }

    void OnOptionsCloseClicked()
    {
        CloseOverlay(_optionsOverlay);
        RefreshProfileName();
        PlayerPrefs.Save();
    }

    void OnSoundSliderChanged(ChangeEvent<float> evt)
    {
        AudioManagerScript.SetSoundVolume(evt.newValue);
    }

    void OnPlayerNameChanged(ChangeEvent<string> evt)
    {
        string name = string.IsNullOrWhiteSpace(evt.newValue) ? "Player" : evt.newValue.Trim();
        PlayerPrefs.SetString("PlayerName", name);
        RefreshProfileName();
    }

    void ApplySoundVolume(float val)
    {
        AudioManagerScript.ApplySoundVolume(val);
    }

    // ───────── leaderboard ─────────

    void OnLeaderboardClicked()
    {
        PopulateLeaderboard(_leaderboardList);
        OpenOverlay(_leaderboardOverlay);
    }

    void OnLeaderboardCloseClicked()
    {
        CloseOverlay(_leaderboardOverlay);
    }

    void PopulateLeaderboard(
        VisualElement list,
        string highlightPlayerName = null,
        int centerRank = 0,
        int aboveCount = 0,
        int belowCount = 0)
    {
        if (list == null) return;
        if (useFiniteEndMode)
        {
            PopulateTimeLeaderboard(list, highlightPlayerName, centerRank, aboveCount, belowCount);
            return;
        }
        list.Clear();

        int count = PlayerPrefs.GetInt("LB_Count", 0);
        if (count == 0)
        {
            var empty = new Label("No scores yet");
            empty.style.color = new Color(0.7f, 0.7f, 0.8f);
            empty.style.fontSize = 18;
            empty.style.unityTextAlign = TextAnchor.MiddleCenter;
            empty.style.marginTop = 16;
            list.Add(empty);
            return;
        }

        int maxShown = Mathf.Min(count, 10);
        int startIndex = 0;
        int endIndex = maxShown - 1;

        if (aboveCount > 0 || belowCount > 0)
        {
            if (centerRank > 0)
            {
                int playerIndex = centerRank - 1;
                startIndex = Mathf.Max(0, playerIndex - aboveCount);
                endIndex = Mathf.Min(maxShown - 1, playerIndex + belowCount);
            }
            else
            {
                endIndex = Mathf.Min(maxShown, 5) - 1;
            }
        }

        for (int i = startIndex; i <= endIndex; i++)
        {
            int score = PlayerPrefs.GetInt($"LB_Score_{i}", 0);
            string entryName = PlayerPrefs.GetString($"LB_Name_{i}", "Player");
            var row = new VisualElement();
            row.AddToClassList("leaderboard-row");
            if (i < 3) row.AddToClassList("leaderboard-row-top");

            bool isCurrentRun = centerRank > 0 && i + 1 == centerRank;
            bool isNameMatch = !string.IsNullOrEmpty(highlightPlayerName) &&
                string.Equals(entryName, highlightPlayerName, System.StringComparison.OrdinalIgnoreCase);
            if (isCurrentRun || (centerRank == 0 && isNameMatch))
                row.AddToClassList("leaderboard-row-you");

            var rankLabel = new Label($"#{i + 1}");
            rankLabel.AddToClassList("leaderboard-rank");

            var nameLabel = new Label(entryName);
            nameLabel.AddToClassList("leaderboard-name");

            var sc = new Label(FormatHeightScore(score));
            sc.AddToClassList("leaderboard-score");

            row.Add(rankLabel);
            row.Add(nameLabel);
            row.Add(sc);
            list.Add(row);
        }
    }

    /// <returns>1-based rank if the score made the top 10, otherwise 0.</returns>
    int SaveScoreToLeaderboard(int score)
    {
        if (score <= 0) return 0;

        string playerName = PlayerPrefs.GetString("PlayerName", "Player");

        int count = PlayerPrefs.GetInt("LB_Count", 0);
        int maxEntries = 10;

        int[] scores = new int[count];
        string[] names = new string[count];
        for (int i = 0; i < count; i++)
        {
            scores[i] = PlayerPrefs.GetInt($"LB_Score_{i}", 0);
            names[i] = PlayerPrefs.GetString($"LB_Name_{i}", "Player");
        }

        int insertIdx = count;
        for (int i = 0; i < count; i++)
        {
            if (score > scores[i]) { insertIdx = i; break; }
        }

        if (insertIdx >= maxEntries)
            return 0;

        int newCount = Mathf.Min(count + 1, maxEntries);
        PlayerPrefs.SetInt("LB_Count", newCount);

        for (int i = newCount - 1; i > insertIdx; i--)
        {
            int prev = (i - 1 >= 0 && i - 1 < count) ? scores[i - 1] : 0;
            string prevName = (i - 1 >= 0 && i - 1 < count) ? names[i - 1] : "Player";
            PlayerPrefs.SetInt($"LB_Score_{i}", prev);
            PlayerPrefs.SetString($"LB_Name_{i}", prevName);
        }

        PlayerPrefs.SetInt($"LB_Score_{insertIdx}", score);
        PlayerPrefs.SetString($"LB_Name_{insertIdx}", playerName);
        PlayerPrefs.Save();

        return insertIdx < maxEntries ? insertIdx + 1 : 0;
    }

    // ───────── safe area ─────────

    void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        if (safeArea == _lastSafeArea) return;
        _lastSafeArea = safeArea;

        float sw = Screen.width;
        float sh = Screen.height;
        if (sw <= 0 || sh <= 0) return;

        float topPct = (sh - safeArea.yMax) / sh * 100f;
        float bottomPct = safeArea.y / sh * 100f;
        float leftPct = safeArea.x / sw * 100f;
        float rightPct = (sw - safeArea.xMax) / sw * 100f;

        ApplySafeInsets(_startScreen, topPct, bottomPct, leftPct, rightPct);
        ApplySafeInsets(_gameHud, topPct, bottomPct, leftPct, rightPct);
        ApplySafeInsets(_optionsOverlay, topPct, bottomPct, leftPct, rightPct);
        ApplySafeInsets(_leaderboardOverlay, topPct, bottomPct, leftPct, rightPct);
        ApplySafeInsets(_gameOverOverlay, topPct, bottomPct, leftPct, rightPct);

        if (_startBottomBar != null)
        {
            _startBottomBar.style.paddingBottom = new Length(bottomPct + 3f, LengthUnit.Percent);
            _startBottomBar.style.paddingLeft = new Length(leftPct + 4f, LengthUnit.Percent);
            _startBottomBar.style.paddingRight = new Length(rightPct + 4f, LengthUnit.Percent);
        }
    }

    static void ApplySafeInsets(VisualElement el, float topPct, float bottomPct,
        float leftPct, float rightPct)
    {
        if (el == null) return;
        el.style.paddingTop = new Length(topPct + 3f, LengthUnit.Percent);
        el.style.paddingLeft = new Length(leftPct + 3f, LengthUnit.Percent);
        el.style.paddingRight = new Length(rightPct + 3f, LengthUnit.Percent);
        el.style.paddingBottom = new Length(bottomPct + 2f, LengthUnit.Percent);
    }

    // ───────── altitude ─────────

    void RefreshAltitudeVisibility()
    {
        if (_altitudeWrap == null) return;
        bool show = altitudePlayer != null && altitudeGroundCollider != null;
        if (show) _altitudeWrap.RemoveFromClassList("hidden");
        else _altitudeWrap.AddToClassList("hidden");
    }

    void UpdateAltitude()
    {
        float heightAboveSurface;
        if (altitudePlayer != null && altitudeGroundCollider != null)
        {
            float groundTop = altitudeGroundCollider.bounds.max.y;
            heightAboveSurface = Mathf.Max(0f, altitudePlayer.position.y - groundTop);
        }
        else
        {
            // Same value PlayerController drives each physics frame (play surface collider).
            heightAboveSurface = GameplayEventBus.HeightAbovePlaySurface;
        }

        if (_altitudeWrap != null && _altitudeLabel != null)
        {
            if (useFiniteEndMode && goalHeightMeters > 0f)
            {
                int pct = Mathf.RoundToInt(Mathf.Clamp01(heightAboveSurface / goalHeightMeters) * 100f);
                _altitudeLabel.text = $"{pct}%";
            }
            else
            {
                _altitudeLabel.text = $"{heightAboveSurface:F1} m";
            }
        }

        // Finite-mode win detection.
        if (useFiniteEndMode && _gameStarted && !_gameWon &&
            heightAboveSurface >= goalHeightMeters)
        {
            TriggerGameWon();
        }

        if (_minimapChrome != null)
        {
            float hysteresis = _minimapVisible ? 9f : 0f;
            bool showChrome = _gameStarted &&
                              heightAboveSurface >= (minimapCloseViewMinHeightMeters - hysteresis);
            if (showChrome != _minimapVisible)
            {
                _minimapVisible = showChrome;
                if (showChrome)
                    _minimapChrome.AddToClassList("minimap-visible");
                else
                    _minimapChrome.RemoveFromClassList("minimap-visible");
            }
        }
    }

    // ───────── gameplay HUD events ─────────

    void OnTotalFlipsChanged(int total)
    {
        _totalFlips = total;
        if (_scoreLabel != null)
            _scoreLabel.text = total.ToString();
    }

    void OnPartnersUnlocked(int partnerCount)
    {
        if (!_gameStarted) return;
        TriggerMilestone(PartnerMilestoneLabel(partnerCount), PartnerMilestoneClass(partnerCount));
    }

    static string PartnerMilestoneLabel(int partnerCount) => partnerCount switch
    {
        1 => "DUO!",
        2 => "TRIO!",
        3 => "QUAD!",
        4 => "QUINT!",
        5 => "SIX!",
        6 => "SEVEN!",
        _ => $"{partnerCount + 1}-PACK!",
    };

    static string PartnerMilestoneClass(int partnerCount) => partnerCount switch
    {
        1 => "ms-3",
        2 => "ms-4",
        3 => "ms-4",
        _ => "ms-5",
    };

    // 6-tier escalation used by both live counter & landing popup.
    static string TierForFlips(int flips)
    {
        if (flips <= 0) return "tier-live-zero";
        if (flips <= 1) return "tier-low";
        if (flips <= 4) return "tier-mid";
        if (flips <= 9) return "tier-high";
        if (flips <= 19) return "tier-hype";
        return "tier-god";
    }

    static void ApplyTier(VisualElement el, string tier)
    {
        if (el == null) return;
        foreach (string c in TierClasses) el.RemoveFromClassList(c);
        el.AddToClassList(tier);
    }

    // Returns the milestone tuple if newN just crossed any threshold from oldN.
    static bool TryGetCrossedMilestone(int oldN, int newN, out string word, out string cls)
    {
        // Crossing in reverse order so 100 wins over 50 etc.
        for (int i = LiveMilestones.Length - 1; i >= 0; i--)
        {
            var m = LiveMilestones[i];
            if (oldN < m.threshold && newN >= m.threshold)
            {
                word = m.word;
                cls = m.cls;
                return true;
            }
        }
        word = null; cls = null;
        return false;
    }

    VisualElement ComboRootOrRoot()
    {
        if (_ui == null) _ui = GetComponent<UIDocument>();
        VisualElement ve = _ui != null ? _ui.rootVisualElement : null;
        if (ve == null) return null;
        VisualElement named = ve.Q<VisualElement>("combo-root");
        return named != null ? named : ve;
    }

    void OnAirborneFlipProgress(AirborneFlipProgressInfo info)
    {
        if (!_gameStarted) return;

        if (_liveWrap == null || _liveLabel == null) CacheFlipComboWorldRefs();
        if (_liveWrap == null || _liveLabel == null) return;

        if (!info.IsAirborne)
        {
            _liveWrap.AddToClassList("hidden");
            HideMilestone();
            _lastLiveFlipFloor = -1;
            return;
        }

        _liveWrap.RemoveFromClassList("hidden");

        int n = info.VisibleFullFlipCount;
        _liveLabel.text = n > 0 ? $"x{n}" : string.Empty;

        string tier = TierForFlips(n);
        ApplyTier(_liveLabel, tier);
        ApplyTier(_liveAuraInner, tier);
        ApplyTier(_liveAuraOuter, tier);
        ApplyTier(_meterFill, tier);

        if (_meterFill != null)
        {
            float pct = Mathf.Clamp01(info.ProgressTowardNextFlip) * 100f;
            _meterFill.style.width = new Length(pct, LengthUnit.Percent);
        }

        bool incremented = n > _lastLiveFlipFloor && (_lastLiveFlipFloor >= 0 || n >= 1);
        if (incremented)
        {
            if (_punchRoutine != null) StopCoroutine(_punchRoutine);
            _punchRoutine = StartCoroutine(PunchLabelRoutine(n));

            if (TryGetCrossedMilestone(Mathf.Max(0, _lastLiveFlipFloor), n, out string word, out string cls))
                TriggerMilestone(word, cls);
        }

        _lastLiveFlipFloor = n;
    }

    // ───────── placement: Y from height above play surface only ─────────

    Camera ResolveCamera()
    {
        if (_cachedCam != null && _cachedCam.isActiveAndEnabled) return _cachedCam;
        _cachedCam = Camera.main;
        return _cachedCam;
    }

    void UpdateLiveCounterPlacement()
    {
        if (_worldFlipComboTransform == null) return;

        bool lensActive = IsMagnifyingLensVisible();
        float targetBlend = lensActive ? 1f : 0f;
        float blendSpeed = liveCounterLensOffsetBlendSeconds > 0f
            ? Time.deltaTime / liveCounterLensOffsetBlendSeconds
            : 1f;
        _flipComboOffsetBlend = Mathf.MoveTowards(_flipComboOffsetBlend, targetBlend, blendSpeed);

        if (lensActive && !_lensOffsetReferenceCaptured)
        {
            _lensOffsetReferenceZoomRatio = Mathf.Max(0.001f, GetLensZoomRatio());
            _lensOffsetReferenceCaptured = true;
        }
        else if (!lensActive)
        {
            _lensOffsetReferenceCaptured = false;
        }

        Vector3 scaledLensOffset = ComputeScaledLensWorldOffset();
        Vector3 offset = Vector3.Lerp(liveCounterWorldOffset, scaledLensOffset, _flipComboOffsetBlend);
        _worldFlipComboTransform.position =
            new Vector3(0f, GameplayEventBus.HeightAbovePlaySurface, 0f) + offset;
        _worldFlipComboTransform.rotation = Quaternion.identity;

        UpdateFlipComboLensVisuals(lensActive);
    }

    Vector3 ComputeScaledLensWorldOffset()
    {
        float zoomScale = GetLensZoomRatio() / _lensOffsetReferenceZoomRatio;
        Vector3 delta = liveCounterLensWorldOffset - liveCounterWorldOffset;
        return liveCounterWorldOffset + new Vector3(
            delta.x * zoomScale,
            delta.y * zoomScale,
            delta.z);
    }

    float GetLensZoomRatio()
    {
        if (magnifyingLens == null)
            magnifyingLens = FindAnyObjectByType<MagnifiyingLens>();
        if (magnifyingLens != null && magnifyingLens.OverallSizeMultiplier > 0f)
            return magnifyingLens.ScreenSizeScaleFactor / magnifyingLens.OverallSizeMultiplier;

        Camera cam = ResolveCamera();
        if (cam == null) return 1f;

        float zoomBase = referenceOrthoSize;
        float zoomCurrent = cam.orthographicSize;

        if (cameraHeightZoom == null)
            cameraHeightZoom = FindAnyObjectByType<CameraHeightZoom>();
        if (cameraHeightZoom != null)
        {
            zoomBase = cameraHeightZoom.BaseOrthoSize;
            zoomCurrent = cameraHeightZoom.CurrentOrthoSize;
        }

        return zoomBase > 0f ? zoomCurrent / zoomBase : 1f;
    }

    bool IsMagnifyingLensVisible()
    {
        if (magnifyingLens == null)
            magnifyingLens = FindAnyObjectByType<MagnifiyingLens>();
        return magnifyingLens != null && magnifyingLens.IsMinimapVisible;
    }

    void UpdateFlipComboLensVisuals(bool lensActive)
    {
        if (_liveWrap == null) return;

        if (lensActive && !_flipComboLensClassApplied)
        {
            _liveWrap.AddToClassList("live-flip-wrap--lens-active");
            _flipComboLensClassApplied = true;
        }
        else if (!lensActive && _flipComboLensClassApplied)
        {
            _liveWrap.RemoveFromClassList("live-flip-wrap--lens-active");
            _flipComboLensClassApplied = false;
        }
    }

    void MaintainFlipComboScreenSize()
    {
        if (_worldFlipComboTransform == null) return;

        Camera cam = ResolveCamera();
        if (cam == null || !cam.orthographic) return;

        float scaleFactor = ComputeFlipComboScreenSizeScale(cam);
        if (scaleFactor <= 0f) return;

        _worldFlipComboTransform.localScale = _flipComboBaseScale * scaleFactor;
    }

    float ComputeFlipComboScreenSizeScale(Camera cam)
    {
        float zoomRatio = GetLensZoomRatio();
        if (zoomRatio <= 0f) return 0f;

        // Same zoom curve as MagnifiyingLens.MaintainConstantScreenSize.
        float scaleFactor = zoomRatio * overallSizeMultiplier;

        // Combo panel is 600uu vs lens 300uu; compensate once the lens is active.
        float panelRatio = Mathf.Lerp(
            1f,
            MagnifyingLensWorldPanelWidth / FlipComboWorldPanelWidth,
            _flipComboOffsetBlend);
        return scaleFactor * panelRatio;
    }

    // ───────── punch animation: overshoot + rotation jitter ─────────

    IEnumerator PunchLabelRoutine(int flipCount)
    {
        if (_liveLabel == null) yield break;

        float d = Mathf.Max(0.10f, liveCountPulseDuration);
        // Bigger combos punch harder.
        float bonus = Mathf.Min(0.40f, Mathf.Max(0, flipCount - 1) * 0.04f);
        float peak = liveCountPulseScale + bonus;
        float undershoot = 0.92f - bonus * 0.15f;

        float sign = Random.value < 0.5f ? -1f : 1f;
        float angleAmp = Mathf.Min(14f, 4f + flipCount * 0.7f) * sign;

        float t = 0f;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / d);

            float s;
            if (u < 0.35f)            s = Mathf.Lerp(1f, peak, u / 0.35f);
            else if (u < 0.70f)       s = Mathf.Lerp(peak, undershoot, (u - 0.35f) / 0.35f);
            else                      s = Mathf.Lerp(undershoot, 1f, (u - 0.70f) / 0.30f);

            float angle = angleAmp * (1f - u) * Mathf.Sin(u * Mathf.PI * 2f);

            _liveLabel.style.scale = new Scale(Vector3.one * s);
            _liveLabel.style.rotate = new Rotate(angle);
            yield return null;
        }
        _liveLabel.style.scale = new Scale(Vector3.one);
        _liveLabel.style.rotate = new Rotate(0f);
        _punchRoutine = null;
    }

    // ───────── milestone banner (5 / 10 / 20 / 50 / 100) ─────────

    void TriggerMilestone(string word, string cls)
    {
        if (_milestoneRoutine != null) StopCoroutine(_milestoneRoutine);
        _milestoneRoutine = StartCoroutine(ShowMilestoneRoutine(word, cls));
    }

    void HideMilestone()
    {
        if (_milestoneRoutine != null) StopCoroutine(_milestoneRoutine);
        _milestoneRoutine = null;
        if (_liveMilestoneLabel != null)
        {
            _liveMilestoneLabel.AddToClassList("hidden");
            _liveMilestoneLabel.style.opacity = 0f;
            _liveMilestoneLabel.style.scale = new Scale(Vector3.one);
            _liveMilestoneLabel.style.rotate = new Rotate(0f);
        }
    }

    IEnumerator ShowMilestoneRoutine(string word, string cls)
    {
        if (_liveMilestoneLabel == null) yield break;
        _liveMilestoneLabel.text = word;
        foreach (string c in MilestoneClasses) _liveMilestoneLabel.RemoveFromClassList(c);
        _liveMilestoneLabel.AddToClassList(cls);
        _liveMilestoneLabel.RemoveFromClassList("hidden");

        // Punch in.
        float intro = 0.20f;
        float t = 0f;
        float sign = Random.value < 0.5f ? -1f : 1f;
        float spinAmp = 9f * sign;
        while (t < intro)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / intro);
            float eased = 1f - Mathf.Pow(1f - u, 3f);
            float s = Mathf.Lerp(0.35f, 1.20f, eased);
            float angle = spinAmp * (1f - eased);
            _liveMilestoneLabel.style.scale = new Scale(Vector3.one * s);
            _liveMilestoneLabel.style.rotate = new Rotate(angle);
            _liveMilestoneLabel.style.opacity = eased;
            yield return null;
        }

        // Settle bounce.
        float settle = 0.16f;
        t = 0f;
        while (t < settle)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / settle);
            float s = Mathf.Lerp(1.20f, 1.0f, u);
            _liveMilestoneLabel.style.scale = new Scale(Vector3.one * s);
            _liveMilestoneLabel.style.rotate = new Rotate(0f);
            yield return null;
        }

        // Hold while airborne.
        float hold = 0.85f;
        yield return new WaitForSecondsRealtime(hold);

        // Fade out.
        float fade = 0.35f;
        t = 0f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / fade);
            _liveMilestoneLabel.style.opacity = 1f - u;
            yield return null;
        }
        _liveMilestoneLabel.AddToClassList("hidden");
        _liveMilestoneLabel.style.opacity = 0f;
        _milestoneRoutine = null;
    }

    // ───────── landing combo popup ─────────

    void OnTrampolineLanding(TrampolineLandingInfo info)
    {
        if (!_gameStarted) return;

        // Finite-mode perfect-streak: a clean-but-not-perfect landing breaks the chain.
        if (useFiniteEndMode && info.WasCleanLanding && !info.WasPerfectLanding)
            _runPerfectStreak = 0;

        VisualElement root = ComboRootOrRoot();
        if (root == null) return;
        if (!info.WasCleanLanding) return;

        int flips = info.CompletedFullFlips;
        if (flips < 1) return;

        string tier = TierForFlips(flips);

        // Group so the milestone word sits centered above the count.
        var group = new VisualElement();
        group.pickingMode = PickingMode.Ignore;
        group.style.flexDirection = FlexDirection.Column;
        group.style.alignItems = Align.Center;
        group.style.opacity = 0f;
        group.style.marginLeft = new Length(Random.Range(-jitterPixels, jitterPixels), LengthUnit.Pixel);

        if (info.WasPerfectLanding)
        {
            var perfectLabel = new Label("PERFECT!");
            perfectLabel.AddToClassList("combo-popup-perfect");
            group.Add(perfectLabel);
        }

        if (flips >= 5 && TryGetCrossedMilestone(0, flips, out string word, out _))
        {
            var subLabel = new Label(word);
            subLabel.AddToClassList("combo-popup-sub");
            subLabel.AddToClassList(tier);
            group.Add(subLabel);
        }

        var label = new Label($"x{flips}");
        label.AddToClassList("combo-popup-label");
        label.AddToClassList(tier);
        group.Add(label);

        float introScaleCap = Mathf.Min(2.4f, 1f + flips * 0.18f);
        group.style.scale = new Scale(Vector3.one * introScaleCap);

        root.Add(group);
        StartCoroutine(AnimatePopup(group, introScaleCap));
    }

    IEnumerator AnimatePopup(VisualElement el, float introPeakScale)
    {
        float rt = Mathf.Max(0.01f, introSeconds);
        float t = 0f;

        // Bouncy entry: overshoot then settle.
        while (t < rt)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / rt);
            float eased = Mathf.Sin(u * Mathf.PI * 0.5f);
            el.style.opacity = eased;
            float s = Mathf.Lerp(introPeakScale, 0.94f, eased);
            el.style.scale = new Scale(Vector3.one * s);
            yield return null;
        }

        // Tiny over-settle back to 1.
        float settle = 0.10f;
        t = 0f;
        while (t < settle)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / settle);
            float s = Mathf.Lerp(0.94f, 1f, u);
            el.style.scale = new Scale(Vector3.one * s);
            yield return null;
        }
        el.style.opacity = 1f;
        el.style.scale = new Scale(Vector3.one);

        if (holdSeconds > 0f)
            yield return new WaitForSecondsRealtime(holdSeconds);

        float fo = Mathf.Max(0.01f, fadeOutSeconds);
        t = 0f;
        while (t < fo)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / fo);
            el.style.opacity = 1f - u;
            yield return null;
        }
        el.RemoveFromHierarchy();
    }

    // ───────── finite-mode win flow ─────────

    void OnPerfectLandingForRun()
    {
        if (!_gameStarted || _gameWon) return;

        _runPerfectFlips++;
        _runPerfectStreak++;
        GameplayEventBus.RunPerfectFlips = _runPerfectFlips;

        if (useFiniteEndMode && !_perfectStreakMedalAwarded && _runPerfectStreak >= 10)
        {
            _perfectStreakMedalAwarded = true;
            NewgroundsApi.UnlockPerfectStreakX10();
        }
    }

    void TriggerGameWon()
    {
        if (_gameWon) return;
        _gameWon = true;

        float runTime = Mathf.Max(0f, Time.unscaledTime - _runStartTime);
        int rank = SaveTimeToLeaderboard(runTime, _totalFlips, _runPerfectFlips);

        _winRunTime = runTime;
        _winFlips = _totalFlips;
        _winPerfect = _runPerfectFlips;
        _winRank = rank;

        GameplayEventBus.RunFinishTime = runTime;
        GameplayEventBus.RunFlips = _totalFlips;
        GameplayEventBus.RunPerfectFlips = _runPerfectFlips;
        GameplayEventBus.RaiseGameWon();

        SubmitWinToNewgrounds(runTime);

        // Cut HUD-driven input; only the win-flow screens accept input from here.
        InputBlocked = true;
        if (playerController != null)
        {
            playerController.SetFlipInputManaged(true);
        }

        if (_worldFlipComboTransform != null)
            _worldFlipComboTransform.gameObject.SetActive(false);

        ResolveEndSequenceRefs();

        if (_gameOverDelayRoutine != null) StopCoroutine(_gameOverDelayRoutine);
        if (_gameOverRevealRoutine != null) StopCoroutine(_gameOverRevealRoutine);
        HideElement(_gameOverOverlay);
        _gameOverOpen = false;

        if (_endFlowRoutine != null) StopCoroutine(_endFlowRoutine);
        _endFlowRoutine = StartCoroutine(EndFlowRoutine());
    }

    void SubmitWinToNewgrounds(float runTime)
    {
        NewgroundsApi.SubmitTime(runTime);
        NewgroundsApi.UnlockReachTop();
        if (runTime < 60f)
            NewgroundsApi.UnlockSpeedrunUnder60();
        // All-perfect: every clean landing this run was perfect (no streak break happened).
        if (_runPerfectFlips > 0 && _runPerfectStreak == _runPerfectFlips)
            NewgroundsApi.UnlockAllPerfect();
    }

    void ResolveEndSequenceRefs()
    {
        if (endSequencePlayerRoot == null && playerController != null)
            endSequencePlayerRoot = playerController.PlayerRoot;

        if (ufoBeamTarget == null)
        {
            var ufo = GameObject.Find("Ufo");
            if (ufo != null)
            {
                var beam = ufo.transform.Find("Square");
                ufoBeamTarget = beam != null ? beam : ufo.transform;
            }
        }
    }

    Vector3 GetUfoBoardTarget(Vector3 fromPosition)
    {
        float targetY = fromPosition.y + Mathf.Max(5f, endUfoBoardRiseMeters);
        if (ufoBeamTarget != null)
            targetY = Mathf.Max(targetY, ufoBeamTarget.position.y + ufoBeamWorldOffset.y);
        return new Vector3(fromPosition.x, targetY, fromPosition.z);
    }

    IEnumerator PlayUfoBoardingAnimation()
    {
        if (endSequencePlayerRoot == null)
            yield break;

        Vector3 startPos = endSequencePlayerRoot.position;
        Vector3 startScale = endSequencePlayerRoot.localScale;
        Quaternion startRot = endSequencePlayerRoot.rotation;
        float startY = startPos.y;
        float targetY = GetUfoBoardTarget(startPos).y;
        float dur = Mathf.Max(0.5f, endUfoBoardSeconds);

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            // Ease-in: slow start, accelerates upward into the beam like a suction pull.
            float eased = u * u * u;
            var pos = endSequencePlayerRoot.position;
            pos.y = Mathf.Lerp(startY, targetY, eased);
            endSequencePlayerRoot.position = pos;
            float scale = Mathf.Lerp(1f, endUfoBoardScaleEnd, eased);
            endSequencePlayerRoot.localScale = startScale * scale;
            endSequencePlayerRoot.rotation = Quaternion.Slerp(startRot, Quaternion.identity, eased);
            yield return null;
        }
    }

    IEnumerator EndFlowRoutine()
    {
        HideElement(_gameHud);
        HideElement(_gameOverOverlay);
        HideElement(_optionsOverlay);
        HideElement(_leaderboardOverlay);
        HideElement(_cheeringCrowd);
        HideElement(_gameEndOverlay);
        HideElement(_gameEndCredits);

        // Phase 1: pandog gets sucked into the UFO (world stays visible).
        _endPhase = EndFlowPhase.UfoBoarding;
        if (endUfoBoardDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(endUfoBoardDelaySeconds);
        yield return PlayUfoBoardingAnimation();

        // Phase 2: credits screen. Freeze the world now that boarding is done.
        _endPhase = EndFlowPhase.Credits;
        Time.timeScale = 0f;
        GameplayEventBus.RaiseEndCreditsStarted();
        ShowElement(_gameEndOverlay);
        if (_gameEndOverlay != null)
        {
            _gameEndOverlay.pickingMode = PickingMode.Position;
            _gameEndOverlay.style.opacity = 0f;
        }
        ShowElement(_gameEndCredits);
        if (_gameEndCredits != null)
            _gameEndCredits.style.opacity = 0f;

        float fadeIn = Mathf.Max(0.01f, endOverlayFadeSeconds);
        float t = 0f;
        while (t < fadeIn)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / fadeIn);
            if (_gameEndOverlay != null) _gameEndOverlay.style.opacity = u;
            if (_gameEndCredits != null) _gameEndCredits.style.opacity = u;
            yield return null;
        }
        if (_gameEndOverlay != null) _gameEndOverlay.style.opacity = 1f;
        if (_gameEndCredits != null) _gameEndCredits.style.opacity = 1f;

        float holdDur = Mathf.Max(1f, endCreditsHoldSeconds);
        t = 0f;
        while (t < holdDur)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        HideElement(_gameEndCredits);
        HideElement(_gameEndOverlay);

        StartFiniteWinReveal();
        _endFlowRoutine = null;
    }

    void SkipUfoBoardingToCredits()
    {
        if (_endFlowRoutine != null) StopCoroutine(_endFlowRoutine);
        _endFlowRoutine = null;
        _endFlowRoutine = StartCoroutine(CreditsFromUfoSkipRoutine());
    }

    IEnumerator CreditsFromUfoSkipRoutine()
    {
        _endPhase = EndFlowPhase.Credits;
        Time.timeScale = 0f;
        GameplayEventBus.RaiseEndCreditsStarted();
        ShowElement(_gameEndOverlay);
        ShowElement(_gameEndCredits);
        if (_gameEndOverlay != null) _gameEndOverlay.style.opacity = 1f;
        if (_gameEndCredits != null) _gameEndCredits.style.opacity = 1f;

        float holdDur = Mathf.Max(1f, endCreditsHoldSeconds);
        float t = 0f;
        while (t < holdDur)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        HideElement(_gameEndCredits);
        HideElement(_gameEndOverlay);
        StartFiniteWinReveal();
        _endFlowRoutine = null;
    }

    void SkipCreditsToReveal()
    {
        if (_endFlowRoutine != null) StopCoroutine(_endFlowRoutine);
        _endFlowRoutine = null;
        HideElement(_gameEndCredits);
        HideElement(_gameEndOverlay);
        StartFiniteWinReveal();
    }

    void StartFiniteWinReveal()
    {
        _endPhase = EndFlowPhase.Actions;
        _isFiniteWinReveal = true;

        HideElement(_cheeringCrowd);
        HideElement(_gameOverAngleRow);
        HideElement(_gameOverTimeRow);

        if (_gameOverScoreSub != null) _gameOverScoreSub.text = "TIME";
        if (_gameOverExtraStats != null) ShowElement(_gameOverExtraStats);
        if (_endStatFlips != null) _endStatFlips.text = _winFlips.ToString();
        if (_endStatPerfect != null) _endStatPerfect.text = _winPerfect.ToString();
        if (_endNewBest != null)
        {
            if (_winRank == 1) ShowElement(_endNewBest);
            else HideElement(_endNewBest);
        }

        string playerName = PlayerPrefs.GetString("PlayerName", "Player");
        PopulateLeaderboard(_gameOverLeaderboardList, playerName, _winRank, aboveCount: 2, belowCount: 2);

        ResetGameOverPresentation();

        ShowElement(_gameOverOverlay);
        if (_gameOverOverlay != null)
        {
            _gameOverOverlay.pickingMode = PickingMode.Position;
            _gameOverOverlay.BringToFront(); // raise above game-end-overlay's comic layer
        }
        _gameOverOpen = true;
        InputBlocked = true;

        if (_gameOverRevealRoutine != null) StopCoroutine(_gameOverRevealRoutine);
        // dummy runScore=1 so the "no height recorded" fallback branch isn't taken.
        _gameOverRevealRoutine = StartCoroutine(GameOverRevealRoutine(_winRunTime, 1, _winRank));
    }

    static string FormatRunTime(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        int m = (int)(seconds / 60f);
        float rest = seconds - m * 60f;
        return $"{m}:{rest:00.00}";
    }

    void PopulateAngleRow()
    {
        if (_gameOverAngleRow == null) return;
        if (_isFiniteWinReveal || playerController == null)
        {
            HideElement(_gameOverAngleRow);
            return;
        }

        float angle = playerController.LandingAngleDegreesFromUpright;
        float safe = playerController.maxLandingAngle;

        if (_gameOverAngleValue != null)
            _gameOverAngleValue.text = $"{Mathf.RoundToInt(Mathf.Abs(angle))}°";
        if (_gameOverAngleSafe != null)
            // Show "SAFE <=40"; the actual cap (maxLandingAngle = 41°) keeps a 1°
            // forgiveness buffer so a landing shown as 40° never fails on rounding.
            _gameOverAngleSafe.text = $"SAFE <={Mathf.RoundToInt(safe) - 1}°";

        ShowElement(_gameOverAngleRow);
    }

    // ───────── time-based leaderboard (finite mode) ─────────

    /// <returns>1-based rank if the time made the top 10, otherwise 0.</returns>
    int SaveTimeToLeaderboard(float runTimeSeconds, int flips, int perfectFlips)
    {
        int cs = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(0f, runTimeSeconds) * 100f), 1, int.MaxValue);
        string playerName = PlayerPrefs.GetString("PlayerName", "Player");

        int count = PlayerPrefs.GetInt("LBT_Count", 0);
        const int maxEntries = 10;

        int[] times = new int[count];
        string[] names = new string[count];
        int[] flipsArr = new int[count];
        int[] perfectArr = new int[count];
        for (int i = 0; i < count; i++)
        {
            times[i] = PlayerPrefs.GetInt($"LBT_Time_{i}", int.MaxValue);
            names[i] = PlayerPrefs.GetString($"LBT_Name_{i}", "Player");
            flipsArr[i] = PlayerPrefs.GetInt($"LBT_Flips_{i}", 0);
            perfectArr[i] = PlayerPrefs.GetInt($"LBT_Perfect_{i}", 0);
        }

        int insertIdx = count;
        for (int i = 0; i < count; i++)
            if (cs < times[i]) { insertIdx = i; break; }

        if (insertIdx >= maxEntries) return 0;

        int newCount = Mathf.Min(count + 1, maxEntries);
        PlayerPrefs.SetInt("LBT_Count", newCount);

        for (int i = newCount - 1; i > insertIdx; i--)
        {
            int prevT = (i - 1 < count) ? times[i - 1] : int.MaxValue;
            string prevN = (i - 1 < count) ? names[i - 1] : "Player";
            int prevF = (i - 1 < count) ? flipsArr[i - 1] : 0;
            int prevP = (i - 1 < count) ? perfectArr[i - 1] : 0;
            PlayerPrefs.SetInt($"LBT_Time_{i}", prevT);
            PlayerPrefs.SetString($"LBT_Name_{i}", prevN);
            PlayerPrefs.SetInt($"LBT_Flips_{i}", prevF);
            PlayerPrefs.SetInt($"LBT_Perfect_{i}", prevP);
        }

        PlayerPrefs.SetInt($"LBT_Time_{insertIdx}", cs);
        PlayerPrefs.SetString($"LBT_Name_{insertIdx}", playerName);
        PlayerPrefs.SetInt($"LBT_Flips_{insertIdx}", flips);
        PlayerPrefs.SetInt($"LBT_Perfect_{insertIdx}", perfectFlips);
        PlayerPrefs.Save();

        return insertIdx + 1;
    }

    static string FormatTimeScore(int centiseconds)
    {
        float s = centiseconds / 100f;
        int m = (int)(s / 60f);
        float rest = s - m * 60f;
        return $"{m}:{rest:00.00}";
    }

    void PopulateTimeLeaderboard(VisualElement list, string highlightPlayerName, int centerRank, int aboveCount, int belowCount)
    {
        list.Clear();

        int count = PlayerPrefs.GetInt("LBT_Count", 0);
        if (count == 0)
        {
            var empty = new Label("No times yet — reach 1000m!");
            empty.style.color = new Color(0.7f, 0.7f, 0.8f);
            empty.style.fontSize = 16;
            empty.style.unityTextAlign = TextAnchor.MiddleCenter;
            empty.style.marginTop = 16;
            list.Add(empty);
            return;
        }

        int maxShown = Mathf.Min(count, 10);
        int startIndex = 0;
        int endIndex = maxShown - 1;

        if (aboveCount > 0 || belowCount > 0)
        {
            if (centerRank > 0)
            {
                int playerIndex = centerRank - 1;
                startIndex = Mathf.Max(0, playerIndex - aboveCount);
                endIndex = Mathf.Min(maxShown - 1, playerIndex + belowCount);
            }
            else
            {
                endIndex = Mathf.Min(maxShown, 5) - 1;
            }
        }

        for (int i = startIndex; i <= endIndex; i++)
        {
            int cs = PlayerPrefs.GetInt($"LBT_Time_{i}", int.MaxValue);
            string entryName = PlayerPrefs.GetString($"LBT_Name_{i}", "Player");

            var row = new VisualElement();
            row.AddToClassList("leaderboard-row");
            if (i < 3) row.AddToClassList("leaderboard-row-top");

            bool isCurrentRun = centerRank > 0 && i + 1 == centerRank;
            bool isNameMatch = !string.IsNullOrEmpty(highlightPlayerName) &&
                string.Equals(entryName, highlightPlayerName, System.StringComparison.OrdinalIgnoreCase);
            if (isCurrentRun || (centerRank == 0 && isNameMatch))
                row.AddToClassList("leaderboard-row-you");

            var rankLabel = new Label($"#{i + 1}");
            rankLabel.AddToClassList("leaderboard-rank");

            var nameLabel = new Label(entryName);
            nameLabel.AddToClassList("leaderboard-name");

            var sc = new Label(FormatTimeScore(cs));
            sc.AddToClassList("leaderboard-score");

            row.Add(rankLabel);
            row.Add(nameLabel);
            row.Add(sc);
            list.Add(row);
        }
    }

    // ───────── helpers ─────────

    static void ShowElement(VisualElement el)
    {
        if (el != null) el.RemoveFromClassList("hidden");
    }

    static void HideElement(VisualElement el)
    {
        if (el != null) el.AddToClassList("hidden");
    }
}
