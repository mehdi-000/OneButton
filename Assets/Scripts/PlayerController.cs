using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Flip")]
    public float flipSpeedStart = 360f;
    public float flipSpeedMax = 450f;
    public float flipRampSeconds = 1.2f;
    [Range(0f, 1f)]
    public float releaseAngularMomentumFactor = 0.85f;

    [Header("Altitude Spin Boost")]
    public float spinBoostStartHeight = 8f;
    public float spinBoostPerUnit = 25f;
    public float maxSpinBoost = 200f;

    [Header("Bounce")]
    public float bounceBonusPerFlip = 0.18f;
    public int maxFlipsForBonus = 20;
    public float maxBounceSpeed = 38f;

    [Header("Landing")]
    [Range(0f, 90f)]
    public float maxLandingAngle = 25f;
    [Range(0f, 15f)]
    public float perfectLandingAngle = 5f;

    [Header("Apex Hang")]
    public float apexSpeedThreshold = 2.5f;
    [Range(0f, 1f)]
    public float apexGravityScale = 0.18f;
    public float apexHeightScaleMax = 3f;
    public float apexHeightForFullScale = 40f;

    [Header("Fall Speed")]
    public float maxFallSpeed = 30f;

    [Header("Movement")]
    public bool freezeHorizontalPosition = true;

    [Header("Touch")]
    public Rect centerTouchRect = new Rect(0.25f, 0.25f, 0.5f, 0.5f);

    [Header("Altitude Events")]
    [SerializeField] Collider2D playSurfaceCollider;
    [SerializeField] Collider2D playerBodyCollider;

    Rigidbody2D _rb;
    float _defaultGravity;
    InputAction _flipAction;

    bool _flipHeld, _touchAccepted, _fallen, _onTrampoline, _pastApex;
    float _flipHoldTime, _lastFlipSpeed;
    float _prevRotation, _airSpinDegrees;
    float _baselineY, _peakY;
    bool _highAltActive;
    int _lifetimeFlips;

    public int LastLandingFlips { get; private set; }
    public bool IsOnTrampoline => _onTrampoline;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _defaultGravity = _rb.gravityScale;

        if (freezeHorizontalPosition)
            _rb.constraints |= RigidbodyConstraints2D.FreezePositionX;

        _prevRotation = _rb.rotation;
        if (playerBodyCollider == null)
            playerBodyCollider = GetComponent<Collider2D>();
        _baselineY = _peakY = CurrentReferenceY();

        _flipAction = new InputAction("Flip", InputActionType.Button, "<Keyboard>/space");
        _flipAction.AddBinding("<Keyboard>/enter");
        _flipAction.AddBinding("<Pointer>/press");
    }

    void OnEnable()
    {
        _flipAction.started += OnFlipDown;
        _flipAction.canceled += OnFlipUp;
        _flipAction.Enable();
    }

    void OnDisable()
    {
        _flipAction.started -= OnFlipDown;
        _flipAction.canceled -= OnFlipUp;
        _flipAction.Disable();
    }

    void FixedUpdate()
    {
        PublishAltitude();
        if (_fallen) return;

        if (_flipHeld)
        {
            _flipHoldTime += Time.fixedDeltaTime;
            float t = flipRampSeconds > 0f ? Mathf.Clamp01(_flipHoldTime / flipRampSeconds) : 1f;
            t = t * t * (3f - 2f * t);

            float height = Mathf.Max(0f, CurrentReferenceY() - _baselineY);
            float boost = height > spinBoostStartHeight
                ? Mathf.Min((height - spinBoostStartHeight) * spinBoostPerUnit, maxSpinBoost)
                : 0f;

            _lastFlipSpeed = Mathf.Lerp(flipSpeedStart, flipSpeedMax + boost, t);
            _rb.MoveRotation(_rb.rotation + _lastFlipSpeed * Time.fixedDeltaTime);
            _rb.angularVelocity = 0f;
        }

        if (!_onTrampoline)
        {
            _airSpinDegrees += Mathf.Abs(Mathf.DeltaAngle(_prevRotation, _rb.rotation));
            float currentY = CurrentReferenceY();
            if (currentY > _peakY) _peakY = currentY;

            if (!_pastApex && _rb.linearVelocity.y < 0f)
            {
                _pastApex = true;
                GameplayEventBus.RaiseApexReached();
            }

            UpdateHighAltitude();
        }

        ApplyApexHang();

        if (_rb.linearVelocity.y < -maxFallSpeed)
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, -maxFallSpeed);

        _prevRotation = _rb.rotation;
    }

    void LateUpdate()
    {
        bool airborne = !_onTrampoline && !_fallen;
        int flips = airborne ? Mathf.FloorToInt(_airSpinDegrees / 330f) : 0;
        float progress = airborne ? Mathf.Repeat(_airSpinDegrees, 330f) / 330f : 0f;

        GameplayEventBus.RaiseAirborneFlipProgress(new AirborneFlipProgressInfo
        {
            IsAirborne = airborne,
            VisibleFullFlipCount = flips,
            ProgressTowardNextFlip = Mathf.Clamp01(progress),
        });
    }

    void ApplyApexHang()
    {
        if (_onTrampoline || _fallen)
        {
            _rb.gravityScale = _defaultGravity;
            return;
        }

        float absVy = Mathf.Abs(_rb.linearVelocity.y);
        if (absVy < apexSpeedThreshold)
        {
            float heightFactor = apexHeightForFullScale > 0f
                ? Mathf.Clamp01((_peakY - _baselineY) / apexHeightForFullScale)
                : 0f;
            float scaledThreshold = apexSpeedThreshold * Mathf.Lerp(1f, apexHeightScaleMax, heightFactor);

            float t = 1f - absVy / scaledThreshold;
            _rb.gravityScale = Mathf.Lerp(_defaultGravity, _defaultGravity * apexGravityScale, Mathf.Clamp01(t));
        }
        else
        {
            _rb.gravityScale = _defaultGravity;
        }
    }

    void PublishAltitude()
    {
        if (_fallen || playSurfaceCollider == null)
        {
            GameplayEventBus.SetHeightAbovePlaySurface(0f);
            return;
        }
        GameplayEventBus.SetHeightAbovePlaySurface(
            CurrentReferenceY() - playSurfaceCollider.bounds.max.y);
    }

    void UpdateHighAltitude()
    {
        if (_fallen) { ClearHighAlt(); return; }
        bool high = CurrentReferenceY() >= GameplayEventBus.HighAltitudeThresholdWorldY;
        if (high == _highAltActive) return;
        _highAltActive = high;
        if (high) GameplayEventBus.RaiseEnteredHighAir();
        else GameplayEventBus.RaiseExitedHighAir();
    }

    float CurrentReferenceY()
    {
        // Use body center so player rotation does not affect perceived altitude.
        return playerBodyCollider != null ? playerBodyCollider.bounds.center.y : _rb.position.y;
    }

    void ClearHighAlt()
    {
        if (!_highAltActive) return;
        _highAltActive = false;
        GameplayEventBus.RaiseExitedHighAir();
    }

    void OnFlipDown(InputAction.CallbackContext ctx)
    {
        if (_fallen || CrazyPanDogUIController.InputBlocked) return;

        if (ctx.control?.device is Pointer)
        {
            var pos = Pointer.current?.position.ReadValue() ?? Vector2.zero;
            if (!IsInCenterRect(pos)) { _touchAccepted = false; return; }
            _touchAccepted = true;
        }

        _flipHeld = true;
        _flipHoldTime = 0f;
        GameplayEventBus.RaiseFlipHoldStarted();
    }

    void OnFlipUp(InputAction.CallbackContext ctx)
    {
        if (ctx.control?.device is Pointer && !_touchAccepted) return;
        bool wasFlipping = _flipHeld && !_fallen;
        _flipHeld = false;
        _touchAccepted = false;
        if (wasFlipping && releaseAngularMomentumFactor > 0f)
            _rb.angularVelocity = _lastFlipSpeed * Mathf.Deg2Rad * releaseAngularMomentumFactor;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (_fallen || col.collider.GetComponent<bounce>() == null) return;
        _onTrampoline = true;
        ClearHighAlt();
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (col.collider.GetComponent<bounce>() == null) return;
        _onTrampoline = false;
        _pastApex = false;
        _baselineY = _peakY = CurrentReferenceY();
    }

    bool IsInCenterRect(Vector2 screenPos)
    {
        var cam = Camera.main;
        return !cam || centerTouchRect.Contains(cam.ScreenToViewportPoint(screenPos));
    }

    float AngleFromUpright()
    {
        float z = transform.eulerAngles.z;
        return z > 180f ? z - 360f : z;
    }

    public void HandleTrampolineBounce(float bounceForce)
    {
        if (_fallen) return;

        int flips = Mathf.FloorToInt(_airSpinDegrees / 330f);
        float angle = AngleFromUpright();
        float absAngle = Mathf.Abs(angle);
        bool clean = absAngle <= maxLandingAngle;
        bool perfect = absAngle <= perfectLandingAngle;

        var landing = new TrampolineLandingInfo
        {
            JumpHeight = Mathf.Max(0f, _peakY - _baselineY),
            CompletedFullFlips = flips,
            LandingAngleDegreesFromUpright = angle,
            WasCleanLanding = clean,
            WasPerfectLanding = perfect,
            WorldPosition = transform.position,
            PeakWorldY = _peakY,
        };

        if (!clean)
        {
            GameplayEventBus.RaiseTrampolineLanding(landing);
            GameplayEventBus.RaiseFallenOffSurface();
            FallOff();
            return;
        }

        int capped = Mathf.Min(flips, maxFlipsForBonus);
        float multiplier = 1f + capped * bounceBonusPerFlip;

        LastLandingFlips = flips;
        _airSpinDegrees = 0f;
        _lifetimeFlips += flips;

        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);
        _rb.AddForce(Vector2.up * bounceForce * multiplier, ForceMode2D.Impulse);

        if (_rb.linearVelocity.y > maxBounceSpeed)
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, maxBounceSpeed);

        GameplayEventBus.RaiseTotalLifetimeFlips(_lifetimeFlips);
        GameplayEventBus.RaiseTrampolineLanding(landing);
        if (perfect) GameplayEventBus.RaisePerfectLanding();
    }

    void FallOff()
    {
        _fallen = true;
        _flipHeld = false;
        _touchAccepted = false;
        _airSpinDegrees = 0f;
        ClearHighAlt();

        if (freezeHorizontalPosition)
            _rb.constraints &= ~RigidbodyConstraints2D.FreezePositionX;

        float dir = Random.value < 0.5f ? -1f : 1f;
        _rb.AddForce(new Vector2(4f * dir, 2f), ForceMode2D.Impulse);
        _rb.AddTorque(8f * dir, ForceMode2D.Impulse);
    }
}
