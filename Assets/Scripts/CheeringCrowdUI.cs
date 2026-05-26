using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class CheeringCrowdUI : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] Sprite girlSprite;
    [SerializeField] Sprite manSprite;
    [SerializeField] Sprite alienSprite;

    [Header("Tier thresholds (lifetime flips, every clean landing)")]
    [SerializeField] int girlUnlockFlips = 10;
    [SerializeField] int manUnlockFlips = 40;
    [SerializeField] int alienUnlockFlips = 60;

    [Header("Title screen")]
    [SerializeField] bool showAllOnTitleScreen;

    [Header("Cameo timing")]
    [SerializeField] float slideInDuration = 0.38f;
    [SerializeField] float slideOutDuration = 0.32f;
    [SerializeField] float shakeDuration = 0.55f;
    [SerializeField] float holdAfterShake = 0.08f;
    [SerializeField] float slideOffsetPixels = 360f;
    [SerializeField] float shakeStrengthPixels = 22f;
    [SerializeField] float shakeRotationDegrees = 7f;
    [SerializeField] float shakeFrequency = 38f;
    [SerializeField] float entryOvershoot = 1.15f;

    UIDocument _ui;
    VisualElement _man;
    VisualElement _girl;
    VisualElement _alien;

    int _totalFlips;
    bool _titleCameoPlayed;

    Coroutine _manRoutine;
    Coroutine _girlRoutine;
    Coroutine _alienRoutine;

    void Awake()
    {
        _ui = GetComponent<UIDocument>();
    }

    void OnEnable()
    {
        GameplayEventBus.TrampolineLanding += OnTrampolineLanding;
        GameplayEventBus.TotalLifetimeFlipsChanged += OnTotalFlipsChanged;
        GameplayEventBus.FlipHoldStarted += OnFlipHoldStarted;
        ResetCrowd();
    }

    void Start() => CacheRefs();

    void OnDisable()
    {
        GameplayEventBus.TrampolineLanding -= OnTrampolineLanding;
        GameplayEventBus.TotalLifetimeFlipsChanged -= OnTotalFlipsChanged;
        GameplayEventBus.FlipHoldStarted -= OnFlipHoldStarted;
        StopAllCrowdRoutines();
    }

    void CacheRefs()
    {
        VisualElement root = _ui != null ? _ui.rootVisualElement : null;
        if (root == null) return;

        _man = root.Q<VisualElement>("cheering-man");
        _girl = root.Q<VisualElement>("cheering-girl");
        _alien = root.Q<VisualElement>("cheering-alien");

        ApplySprite(_man, manSprite);
        ApplySprite(_girl, girlSprite);
        ApplySprite(_alien, alienSprite);
    }

    static void ApplySprite(VisualElement el, Sprite sprite)
    {
        if (el == null || sprite == null) return;
        el.style.backgroundImage = new StyleBackground(sprite);
    }

    void OnTotalFlipsChanged(int total) => _totalFlips = total;

    void OnFlipHoldStarted()
    {
        if (!showAllOnTitleScreen || _titleCameoPlayed) return;
        _titleCameoPlayed = true;
        PlayCameo(_man, slideFromLeft: true);
        PlayCameo(_girl, slideFromLeft: false);
        PlayCameo(_alien, slideFromLeft: false);
    }

    void OnTrampolineLanding(TrampolineLandingInfo info)
    {
        if (!info.WasCleanLanding) return;

        if (_totalFlips >= girlUnlockFlips)
            PlayCameo(_girl, slideFromLeft: false);

        if (_totalFlips >= manUnlockFlips)
            PlayCameo(_man, slideFromLeft: true);

        if (_totalFlips >= alienUnlockFlips)
            PlayCameo(_alien, slideFromLeft: false);
    }

    void PlayCameo(VisualElement el, bool slideFromLeft)
    {
        if (el == null) return;

        if (el == _man)
        {
            if (_manRoutine != null) StopCoroutine(_manRoutine);
            _manRoutine = StartCoroutine(CameoRoutine(el, slideFromLeft, () => _manRoutine = null));
        }
        else if (el == _girl)
        {
            if (_girlRoutine != null) StopCoroutine(_girlRoutine);
            _girlRoutine = StartCoroutine(CameoRoutine(el, slideFromLeft, () => _girlRoutine = null));
        }
        else if (el == _alien)
        {
            if (_alienRoutine != null) StopCoroutine(_alienRoutine);
            _alienRoutine = StartCoroutine(CameoRoutine(el, slideFromLeft, () => _alienRoutine = null));
        }
    }

    IEnumerator CameoRoutine(VisualElement el, bool slideFromLeft, System.Action onDone)
    {
        el.RemoveFromClassList("hidden");
        el.style.opacity = 1f;
        ResetPersonTransform(el);

        float offX = slideFromLeft ? -slideOffsetPixels : slideOffsetPixels;
        yield return SlideHorizontal(el, offX, 0f, slideInDuration, easeOut: true, useOvershoot: true);

        yield return ShakeCelebration(el);

        if (holdAfterShake > 0f)
        {
            ResetPersonTransform(el);
            yield return new WaitForSeconds(holdAfterShake);
        }

        yield return SlideHorizontal(el, 0f, offX, slideOutDuration, easeOut: false, useOvershoot: false);

        HidePerson(el);
        onDone?.Invoke();
    }

    IEnumerator SlideHorizontal(
        VisualElement el,
        float fromX,
        float toX,
        float duration,
        bool easeOut,
        bool useOvershoot)
    {
        float dur = Mathf.Max(0.01f, duration);
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float eased = easeOut
                ? (useOvershoot ? EaseOutBack(u, entryOvershoot) : 1f - Mathf.Pow(1f - u, 3f))
                : EaseInCubic(u);
            float x = Mathf.Lerp(fromX, toX, eased);
            ApplyTransform(el, x, 0f, 0f, 1f);
            yield return null;
        }

        ApplyTransform(el, toX, 0f, 0f, 1f);
    }

    IEnumerator ShakeCelebration(VisualElement el)
    {
        float dur = Mathf.Max(0.01f, shakeDuration);
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float damp = Mathf.Exp(-4.2f * u);
            float wave = t * shakeFrequency;
            float x = Mathf.Sin(wave * 1.7f) * shakeStrengthPixels * damp;
            float y = Mathf.Cos(wave * 1.3f) * shakeStrengthPixels * 0.45f * damp;
            float rot = Mathf.Sin(wave * 2.1f) * shakeRotationDegrees * damp;
            float scale = 1f + Mathf.Sin(wave * 2.8f) * 0.06f * damp;
            ApplyTransform(el, x, y, rot, scale);
            yield return null;
        }

        ResetPersonTransform(el);
    }

    static void ApplyTransform(VisualElement el, float x, float y, float rotateDeg, float scale)
    {
        el.style.translate = new Translate(new Length(x, LengthUnit.Pixel), new Length(y, LengthUnit.Pixel));
        el.style.rotate = new Rotate(new Angle(rotateDeg, AngleUnit.Degree));
        el.style.scale = new Scale(new Vector3(scale, scale, 1f));
    }

    static void ResetPersonTransform(VisualElement el)
    {
        if (el == null) return;
        el.style.translate = new Translate(0, 0);
        el.style.rotate = new Rotate(new Angle(0, AngleUnit.Degree));
        el.style.scale = new Scale(Vector3.one);
    }

    static float EaseOutBack(float t, float overshoot)
    {
        float c1 = overshoot;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    static float EaseInCubic(float t) => t * t * t;

    static void HidePerson(VisualElement el)
    {
        if (el == null) return;
        el.AddToClassList("hidden");
        ResetPersonTransform(el);
        el.style.opacity = 0f;
    }

    void ResetCrowd()
    {
        StopAllCrowdRoutines();
        _totalFlips = 0;
        _titleCameoPlayed = false;
        HidePerson(_man);
        HidePerson(_girl);
        HidePerson(_alien);
    }

    void StopAllCrowdRoutines()
    {
        if (_manRoutine != null) StopCoroutine(_manRoutine);
        if (_girlRoutine != null) StopCoroutine(_girlRoutine);
        if (_alienRoutine != null) StopCoroutine(_alienRoutine);
        _manRoutine = _girlRoutine = _alienRoutine = null;
    }
}
