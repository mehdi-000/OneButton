using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class CrazyPanDogUIController : MonoBehaviour
{
    [SerializeField] float introSeconds = 0.28f;
    [SerializeField] float holdSeconds = 0.42f;
    [SerializeField] float fadeOutSeconds = 0.22f;
    [SerializeField] float jitterPixels = 22f;
    [SerializeField] float liveCountPulseScale = 1.22f;
    [SerializeField] float liveCountPulseDuration = 0.14f;
    [SerializeField] float promptBlinkInterval = 0.8f;

    [Header("Altitude (optional)")]
    [SerializeField] Transform altitudePlayer;
    [SerializeField] Collider2D altitudeGroundCollider;

    [Header("Audio")]
    [SerializeField] AudioSource musicSource;
    [SerializeField] AudioSource sfxSource;

    UIDocument _ui;

    // start screen
    VisualElement _startScreen;
    Label _playPrompt;
    Button _btnOptions;
    Button _btnLeaderboard;

    // overlays
    VisualElement _optionsOverlay;
    VisualElement _leaderboardOverlay;
    Slider _sliderMusic;
    Slider _sliderSfx;
    TextField _inputPlayerName;
    Button _btnOptionsClose;
    Button _btnLeaderboardClose;
    VisualElement _leaderboardList;

    VisualElement _gameOverOverlay;
    Label _gameOverScore;
    Label _gameOverHeight;
    Button _btnGameOverRestart;

    // game hud
    VisualElement _gameHud;
    VisualElement _liveWrap;
    Label _liveLabel;
    VisualElement _meterFill;
    VisualElement _altitudeWrap;
    Label _altitudeLabel;
    Label _scoreLabel;

    int _lastLiveFlipFloor = -1;
    int _totalFlips;
    bool _gameStarted;
    bool _overlayOpen;
    bool _gameOverOpen;
    Coroutine _promptBlink;


    /// <summary>
    /// True when a UI overlay is open. PlayerController should check this
    /// and ignore input while it's true.
    /// </summary>
    public static bool InputBlocked { get; private set; }

    void Awake()
    {
        _ui = GetComponent<UIDocument>();
    }

    void OnEnable()
    {
        GameplayEventBus.TrampolineLanding += OnTrampolineLanding;
        GameplayEventBus.AirborneFlipProgress += OnAirborneFlipProgress;
        GameplayEventBus.TotalLifetimeFlipsChanged += OnTotalFlipsChanged;
        GameplayEventBus.FlipHoldStarted += OnFlipHoldStarted;
        GameplayEventBus.FallenOffSurface += OnFallenOffSurface;

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

        UnbindButtons();
        InputBlocked = false;
    }

    void Start()
    {
        CacheAllRefs();
    }

    void LateUpdate()
    {
        UpdateAltitude();
    }

    void OnFlipHoldStarted()
    {
        if (_gameStarted || _overlayOpen)
            return;
        TransitionToGame();
    }

    // ───────── caching ─────────

    void CacheAllRefs()
    {
        VisualElement root = _ui != null ? _ui.rootVisualElement : null;
        if (root == null) return;

        _startScreen = root.Q<VisualElement>("start-screen");
        _playPrompt = root.Q<Label>("play-prompt");
        _btnOptions = root.Q<Button>("btn-options");
        _btnLeaderboard = root.Q<Button>("btn-leaderboard");

        _optionsOverlay = root.Q<VisualElement>("options-overlay");
        _leaderboardOverlay = root.Q<VisualElement>("leaderboard-overlay");
        _sliderMusic = root.Q<Slider>("slider-music");
        _sliderSfx = root.Q<Slider>("slider-sfx");
        _inputPlayerName = root.Q<TextField>("input-player-name");
        _btnOptionsClose = root.Q<Button>("btn-options-close");
        _btnLeaderboardClose = root.Q<Button>("btn-leaderboard-close");
        _leaderboardList = root.Q<VisualElement>("leaderboard-list");

        _gameOverOverlay = root.Q<VisualElement>("game-over-overlay");
        _gameOverScore = root.Q<Label>("game-over-score");
        _gameOverHeight = root.Q<Label>("game-over-height");
        _btnGameOverRestart = root.Q<Button>("btn-game-over-restart");

        _gameHud = root.Q<VisualElement>("game-hud");
        _liveWrap = root.Q<VisualElement>("live-flip-wrap");
        _liveLabel = root.Q<Label>("live-flip-label");
        _meterFill = root.Q<VisualElement>("live-flip-meter-fill");
        _altitudeWrap = root.Q<VisualElement>("altitude-hud-wrap");
        _altitudeLabel = root.Q<Label>("altitude-label");
        _scoreLabel = root.Q<Label>("score-label");
    }

    // ───────── start screen ─────────

    void SetupStartScreen()
    {
        _gameStarted = false;
        _overlayOpen = false;
        _gameOverOpen = false;
        InputBlocked = false;

        ShowElement(_startScreen);
        HideElement(_gameHud);
        HideElement(_optionsOverlay);
        HideElement(_leaderboardOverlay);
        HideElement(_gameOverOverlay);

        UnbindButtons();
        BindButtons();

        if (_sliderMusic != null)
        {
            float savedMusic = PlayerPrefs.GetFloat("MusicVolume", 80f);
            _sliderMusic.value = savedMusic;
            ApplyMusicVolume(savedMusic);
            _sliderMusic.RegisterValueChangedCallback(OnMusicSliderChanged);
        }

        if (_sliderSfx != null)
        {
            float savedSfx = PlayerPrefs.GetFloat("SfxVolume", 80f);
            _sliderSfx.value = savedSfx;
            ApplySfxVolume(savedSfx);
            _sliderSfx.RegisterValueChangedCallback(OnSfxSliderChanged);
        }

        if (_inputPlayerName != null)
        {
            _inputPlayerName.value = PlayerPrefs.GetString("PlayerName", "Player");
            _inputPlayerName.RegisterValueChangedCallback(OnPlayerNameChanged);
        }

        PopulateLeaderboard();

        if (_promptBlink != null) StopCoroutine(_promptBlink);
        _promptBlink = StartCoroutine(PromptBlinkRoutine());
    }

    void BindButtons()
    {
        if (_btnOptions != null) _btnOptions.clicked += OnOptionsClicked;
        if (_btnLeaderboard != null) _btnLeaderboard.clicked += OnLeaderboardClicked;
        if (_btnOptionsClose != null) _btnOptionsClose.clicked += OnOptionsCloseClicked;
        if (_btnLeaderboardClose != null) _btnLeaderboardClose.clicked += OnLeaderboardCloseClicked;
        if (_btnGameOverRestart != null) _btnGameOverRestart.clicked += OnGameOverRestartClicked;
    }

    void UnbindButtons()
    {
        if (_btnOptions != null) _btnOptions.clicked -= OnOptionsClicked;
        if (_btnLeaderboard != null) _btnLeaderboard.clicked -= OnLeaderboardClicked;
        if (_btnOptionsClose != null) _btnOptionsClose.clicked -= OnOptionsCloseClicked;
        if (_btnLeaderboardClose != null) _btnLeaderboardClose.clicked -= OnLeaderboardCloseClicked;
        if (_btnGameOverRestart != null) _btnGameOverRestart.clicked -= OnGameOverRestartClicked;

        if (_sliderMusic != null) _sliderMusic.UnregisterValueChangedCallback(OnMusicSliderChanged);
        if (_sliderSfx != null) _sliderSfx.UnregisterValueChangedCallback(OnSfxSliderChanged);
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
        if (_promptBlink != null) StopCoroutine(_promptBlink);

        HideElement(_startScreen);
        HideElement(_optionsOverlay);
        HideElement(_leaderboardOverlay);
        ShowElement(_gameHud);

        _overlayOpen = false;
        _gameOverOpen = false;
        InputBlocked = false;
        HideElement(_gameOverOverlay);

        RefreshAltitudeVisibility();
        if (_liveWrap != null) _liveWrap.AddToClassList("hidden");
        if (_scoreLabel != null) _scoreLabel.text = "0";
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

        _gameOverOpen = true;
        _overlayOpen = true;
        InputBlocked = true;

        HideElement(_optionsOverlay);
        HideElement(_leaderboardOverlay);

        if (_gameOverScore != null)
            _gameOverScore.text = _totalFlips.ToString();

        if (_gameOverHeight != null)
        {
            float h = GameplayEventBus.PeakHeightAbovePlaySurface;
            _gameOverHeight.text = $"{h:F1} m";
        }

        ShowElement(_gameOverOverlay);
        if (_gameOverOverlay != null)
            _gameOverOverlay.pickingMode = PickingMode.Position;
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
        PlayerPrefs.Save();
    }

    void OnMusicSliderChanged(ChangeEvent<float> evt)
    {
        ApplyMusicVolume(evt.newValue);
        PlayerPrefs.SetFloat("MusicVolume", evt.newValue);
    }

    void OnSfxSliderChanged(ChangeEvent<float> evt)
    {
        ApplySfxVolume(evt.newValue);
        PlayerPrefs.SetFloat("SfxVolume", evt.newValue);
    }

    void OnPlayerNameChanged(ChangeEvent<string> evt)
    {
        string name = string.IsNullOrWhiteSpace(evt.newValue) ? "Player" : evt.newValue.Trim();
        PlayerPrefs.SetString("PlayerName", name);
    }

    void ApplyMusicVolume(float val)
    {
        if (musicSource != null) musicSource.volume = val / 100f;
    }

    void ApplySfxVolume(float val)
    {
        if (sfxSource != null) sfxSource.volume = val / 100f;
    }

    // ───────── leaderboard ─────────

    void OnLeaderboardClicked()
    {
        PopulateLeaderboard();
        OpenOverlay(_leaderboardOverlay);
    }

    void OnLeaderboardCloseClicked()
    {
        CloseOverlay(_leaderboardOverlay);
    }

    void PopulateLeaderboard()
    {
        if (_leaderboardList == null) return;
        _leaderboardList.Clear();

        int count = PlayerPrefs.GetInt("LB_Count", 0);
        if (count == 0)
        {
            var empty = new Label("No scores yet");
            empty.style.color = new Color(0.7f, 0.7f, 0.8f);
            empty.style.fontSize = 18;
            empty.style.unityTextAlign = TextAnchor.MiddleCenter;
            empty.style.marginTop = 16;
            _leaderboardList.Add(empty);
            return;
        }

        for (int i = 0; i < Mathf.Min(count, 10); i++)
        {
            int score = PlayerPrefs.GetInt($"LB_Score_{i}", 0);
            string playerName = PlayerPrefs.GetString($"LB_Name_{i}", "Player");
            var row = new VisualElement();
            row.AddToClassList("leaderboard-row");
            if (i < 3) row.AddToClassList("leaderboard-row-top");

            var rank = new Label($"#{i + 1}");
            rank.AddToClassList("leaderboard-rank");

            var nameLabel = new Label(playerName);
            nameLabel.AddToClassList("leaderboard-name");

            var sc = new Label($"{score}");
            sc.AddToClassList("leaderboard-score");

            row.Add(rank);
            row.Add(nameLabel);
            row.Add(sc);
            _leaderboardList.Add(row);
        }
    }

    void SaveScoreToLeaderboard(int score)
    {
        if (score <= 0) return;

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
        if (_altitudeWrap == null || _altitudeLabel == null) return;
        if (altitudePlayer == null || altitudeGroundCollider == null) return;

        float groundTop = altitudeGroundCollider.bounds.max.y;
        float meters = Mathf.Max(0f, altitudePlayer.position.y - groundTop);
        _altitudeLabel.text = $"{meters:F1} m";
    }

    // ───────── gameplay HUD events ─────────

    void OnTotalFlipsChanged(int total)
    {
        _totalFlips = total;
        if (_scoreLabel != null)
            _scoreLabel.text = total.ToString();
    }

    static string TierPopupClass(int flips)
    {
        if (flips <= 1) return "tier-low";
        return flips <= 3 ? "tier-mid" : "tier-high";
    }

    static string TierLiveClass(int visibleFlips)
    {
        if (visibleFlips <= 0) return "tier-live-zero";
        return TierPopupClass(visibleFlips);
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

        if (_liveWrap == null || _liveLabel == null) CacheAllRefs();
        if (_liveWrap == null || _liveLabel == null) return;

        if (!info.IsAirborne)
        {
            _liveWrap.AddToClassList("hidden");
            _lastLiveFlipFloor = -1;
            return;
        }

        _liveWrap.RemoveFromClassList("hidden");

        int n = info.VisibleFullFlipCount;
        _liveLabel.text = $"x{n}";

        foreach (string c in new[] { "tier-live-zero", "tier-low", "tier-mid", "tier-high" })
            _liveLabel.RemoveFromClassList(c);
        _liveLabel.AddToClassList(TierLiveClass(n));

        if (_meterFill != null)
        {
            float pct = Mathf.Clamp01(info.ProgressTowardNextFlip) * 100f;
            _meterFill.style.width = new Length(pct, LengthUnit.Percent);
        }

        if (n > _lastLiveFlipFloor && (_lastLiveFlipFloor >= 0 || n >= 1))
            StartCoroutine(PunchLabelRoutine());

        _lastLiveFlipFloor = n;
    }

    IEnumerator PunchLabelRoutine()
    {
        if (_liveLabel == null) yield break;
        float t = 0f;
        float d = Mathf.Max(0.04f, liveCountPulseDuration);
        float peak = liveCountPulseScale;

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / d);
            float s = Mathf.SmoothStep(peak, 1f, u);
            _liveLabel.style.scale = new Scale(Vector3.one * s);
            yield return null;
        }
        _liveLabel.style.scale = new Scale(Vector3.one);
    }

    void OnTrampolineLanding(TrampolineLandingInfo info)
    {
        if (!_gameStarted) return;

        VisualElement root = ComboRootOrRoot();
        if (root == null) return;
        if (!info.WasCleanLanding) return;

        int flips = info.CompletedFullFlips;
        if (flips < 1) return;

        var label = new Label($"x{flips}");
        label.AddToClassList("combo-popup-label");
        label.AddToClassList(TierPopupClass(flips));
        label.style.opacity = 0f;
        label.style.marginLeft = new Length(Random.Range(-jitterPixels, jitterPixels), LengthUnit.Pixel);

        float introScaleCap = Mathf.Min(2.2f, 1f + flips * 0.22f);
        label.style.scale = new Scale(Vector3.one * introScaleCap);

        root.Add(label);
        StartCoroutine(AnimatePopup(label, introScaleCap));

        SaveScoreToLeaderboard(_totalFlips);
    }

    IEnumerator AnimatePopup(VisualElement label, float introPeakScale)
    {
        float rt = Mathf.Max(0.01f, introSeconds);
        float t = 0f;

        while (t < rt)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / rt);
            float eased = Mathf.Sin(u * Mathf.PI * 0.5f);
            label.style.opacity = eased;
            float s = Mathf.Lerp(introPeakScale, 1f, eased);
            label.style.scale = new Scale(Vector3.one * s);
            yield return null;
        }

        label.style.opacity = 1f;
        label.style.scale = new Scale(Vector3.one);

        if (holdSeconds > 0f)
            yield return new WaitForSecondsRealtime(holdSeconds);

        float fo = Mathf.Max(0.01f, fadeOutSeconds);
        t = 0f;
        while (t < fo)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / fo);
            label.style.opacity = 1f - u;
            yield return null;
        }
        label.RemoveFromHierarchy();
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
