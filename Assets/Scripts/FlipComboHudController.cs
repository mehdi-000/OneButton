using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class FlipComboHudController : MonoBehaviour
{
    [SerializeField]
    float introSeconds = 0.28f;

    [SerializeField]
    float holdSeconds = 0.42f;

    [SerializeField]
    float fadeOutSeconds = 0.22f;

    [SerializeField]
    float jitterPixels = 22f;

    [SerializeField]
    float liveCountPulseScale = 1.22f;

    [SerializeField]
    float liveCountPulseDuration = 0.14f;

    UIDocument _ui;
    VisualElement _liveWrap;
    Label _liveLabel;
    VisualElement _meterFill;

    int _lastLiveFlipFloor = -1;

    void Awake()
    {
        _ui = GetComponent<UIDocument>();
    }

    void OnEnable()
    {
        GameplayEventBus.TrampolineLanding += OnTrampolineLanding;
        GameplayEventBus.AirborneFlipProgress += OnAirborneFlipProgress;
        CacheHudRefs();
        if (_liveWrap != null)
            _liveWrap.AddToClassList("hidden");
    }

    void OnDisable()
    {
        GameplayEventBus.TrampolineLanding -= OnTrampolineLanding;
        GameplayEventBus.AirborneFlipProgress -= OnAirborneFlipProgress;
    }

    void Start()
    {
        CacheHudRefs();
        if (_liveWrap != null)
            _liveWrap.AddToClassList("hidden");
    }

    static string TierPopupClass(int flips)
    {
        if (flips <= 1)
            return "tier-low";

        return flips <= 3 ? "tier-mid" : "tier-high";
    }

    static string TierLiveClass(int visibleFlips)
    {
        if (visibleFlips <= 0)
            return "tier-live-zero";

        return TierPopupClass(visibleFlips);
    }

    void CacheHudRefs()
    {
        VisualElement ve = _ui != null ? _ui.rootVisualElement : null;
        if (ve == null)
            return;

        _liveWrap = ve.Q<VisualElement>("live-flip-wrap");
        _liveLabel = ve.Q<Label>("live-flip-label");
        _meterFill = ve.Q<VisualElement>("live-flip-meter-fill");
    }

    VisualElement ComboRootOrRoot()
    {
        if (_ui == null)
            _ui = GetComponent<UIDocument>();

        VisualElement ve = _ui != null ? _ui.rootVisualElement : null;
        if (ve == null)
            return null;

        VisualElement named = ve.Q<VisualElement>("combo-root");
        return named != null ? named : ve;
    }

    void OnAirborneFlipProgress(AirborneFlipProgressInfo info)
    {
        if (_liveWrap == null || _liveLabel == null)
            CacheHudRefs();

        if (_liveWrap == null || _liveLabel == null)
            return;

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

        if (info.IdleResetThisFrame)
            StartCoroutine(IdleFlashRoutine());
        else
        {
            bool bumped = n > _lastLiveFlipFloor;
            bool allowPunch = bumped && (_lastLiveFlipFloor >= 0 || n >= 1);
            if (allowPunch)
                StartCoroutine(PunchLabelRoutine());
        }

        _lastLiveFlipFloor = n;
    }

    IEnumerator IdleFlashRoutine()
    {
        if (_liveLabel == null)
            yield break;

        _liveLabel.AddToClassList("idle-flash");
        yield return new WaitForSecondsRealtime(0.18f);
        _liveLabel.RemoveFromClassList("idle-flash");
    }

    IEnumerator PunchLabelRoutine()
    {
        if (_liveLabel == null)
            yield break;

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
        VisualElement root = ComboRootOrRoot();
        if (root == null)
            return;

        if (!info.WasCleanLanding)
            return;

        int flips = info.CompletedFullFlips;
        if (flips < 1)
            return;

        var label = new Label($"x{flips}");
        label.AddToClassList("combo-popup-label");
        label.AddToClassList(TierPopupClass(flips));

        label.style.opacity = 0f;
        label.style.marginLeft = new Length(Random.Range(-jitterPixels, jitterPixels), LengthUnit.Pixel);

        float introScaleCap = Mathf.Min(2.2f, 1f + flips * 0.22f);

        Vector3 sv = Vector3.one * introScaleCap;
        label.style.scale = new Scale(sv);

        root.Add(label);
        StartCoroutine(AnimatePopup(label, introScaleCap));
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
}
