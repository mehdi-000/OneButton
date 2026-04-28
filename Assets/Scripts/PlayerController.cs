using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Flip")]
    [Tooltip("Spin when you first press (deg/s). Positive = front flip (Unity 2D CCW).")]
    public float flipDegreesPerSecondStart = 320f;

    [Tooltip("Spin after holding flipRampSecondsToMax (deg/s). Positive = front flip.")]
    public float flipDegreesPerSecondMax = 600f;

    [Tooltip("Seconds of hold to ramp from start spin to max spin.")]
    public float flipRampSecondsToMax = 1.4f;

    [Tooltip("Angular damping when not holding flip (settles rotation after collisions).")]
    public float idleAngularDamping = 6f;

    [Tooltip("On release, fraction of current ramped spin becomes angular velocity (coast).")]
    [Range(0f, 1f)]
    public float releaseAngularMomentumFactor = 0.85f;

    [Header("Bounce vs spin")]
    [Tooltip("Airborne rotation (degrees) needed to count as one full spin for bounce bonus.")]
    public float degreesPerSuccessfulSpin = 360f;

    [Tooltip("Bounce multiplier per spin after a clean landing: base × (1 + spins × this). E.g. 0.04 = +4% bounce per spin.")]
    public float bounceBonusPerSuccessfulSpin = 0.1f;

    [Tooltip("Maximum spins that add bounce bonus (avoids huge impulses).")]
    public int maxSpinsForBounceBonus = 20;

    [Header("Movement")]
    [Tooltip("Locks horizontal drift while bouncing (recommended for vertical trampoline gameplay). Disabled when you fall off.")]
    public bool freezeHorizontalPosition = true;

    [Header("Landing")]
    [Tooltip("Maximum degrees from upright allowed to count as a clean landing.")]
    [Range(0f, 90f)]
    public float maxUprightAngleDeg = 25f;

    [Header("Touch")]
    [Tooltip("Only accept touch/press if it's within this center-screen box (normalized viewport coords).")]
    public Rect centerTouchViewportRect = new Rect(0.25f, 0.25f, 0.5f, 0.5f);

    [Header("Realtime flip combo ( HUD + idle timeout )")]
    [Tooltip("If you stop rotating noticeably for this long while airborne, the flip session resets to 0 (HUD + bounce math).")]
    public float flipSessionIdleSeconds = 1.05f;

    [Tooltip("Per physics frame: rotation below this (degrees) does not refresh the idle timer.")]
    public float flipSessionMinDegreesForActivityPerFixed = 1.75f;

    private Rigidbody2D _rb;
    private bool _flipHeld;
    private bool _touchAccepted;
    private bool _fallen;

    private float _flipHoldElapsed;
    private float _lastHoldDegreesPerSecond;

    private bool _onTrampoline;
    private float _prevRotationForAirSpin;
    private float _airborneRotationDegrees;

    public int LastLandingSuccessfulSpins { get; private set; }
    public bool IsOnTrampoline => _onTrampoline;

    private InputAction _flipAction;

    private float _jumpBaselineWorldY;
    private float _jumpPeakWorldY;
    private bool _highAltitudeMusicBandActive;
    private int _lifetimeFlipsCounted;

    private float _lastFlipActivityFixedTime;
    private bool _idleFlipResetThisFixedStep;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();

        if (freezeHorizontalPosition)
            _rb.constraints |= RigidbodyConstraints2D.FreezePositionX;

        _prevRotationForAirSpin = _rb.rotation;
        _flipAction = new InputAction(
            name: "FlipHold",
            type: InputActionType.Button,
            binding: "<Keyboard>/space");

        _flipAction.AddBinding("<Keyboard>/enter");
        _flipAction.AddBinding("<Pointer>/press");

        float startY = transform.position.y;
        _jumpBaselineWorldY = startY;
        _jumpPeakWorldY = startY;
        _lastFlipActivityFixedTime = Time.fixedTime;
    }
    private void OnEnable()
    {
        _flipAction.started += OnFlipStarted;
        _flipAction.canceled += OnFlipCanceled;
        _flipAction.Enable();
    }

    private void OnDisable()
    {
        _flipAction.started -= OnFlipStarted;
        _flipAction.canceled -= OnFlipCanceled;
        _flipAction.Disable();
    }

    private void FixedUpdate()
    {
        if (_fallen) return;
        if (_flipHeld)
        {
            _flipHoldElapsed += Time.fixedDeltaTime;
            float rampT = flipRampSecondsToMax > 0f
                ? Mathf.Clamp01(_flipHoldElapsed / flipRampSecondsToMax)
                : 1f;
            rampT = rampT * rampT * (3f - 2f * rampT); // smoothstep
            _lastHoldDegreesPerSecond = Mathf.Lerp(flipDegreesPerSecondStart, flipDegreesPerSecondMax, rampT);

            float deltaDeg = _lastHoldDegreesPerSecond * Time.fixedDeltaTime;
            _rb.MoveRotation(_rb.rotation + deltaDeg);
            _rb.angularVelocity = 0f;
        }
        else
        {
            _rb.angularDamping = idleAngularDamping;
        }
        if (!_onTrampoline)
        {
            float frameSpin = Mathf.Abs(Mathf.DeltaAngle(_prevRotationForAirSpin, _rb.rotation));
            _airborneRotationDegrees += frameSpin;

            if (frameSpin >= flipSessionMinDegreesForActivityPerFixed)
                _lastFlipActivityFixedTime = Time.fixedTime;

            EvaluateFlipSessionIdle(frameSpin);

            float y = transform.position.y;
            if (y > _jumpPeakWorldY)
                _jumpPeakWorldY = y;

            UpdateHighAltitudeMusicBand();
        }

        _prevRotationForAirSpin = _rb.rotation;
    }

    private void EvaluateFlipSessionIdle(float frameSpinDegrees)
    {
        if (!_onTrampoline && !_fallen && _airborneRotationDegrees > 0.01f
            && Time.fixedTime - _lastFlipActivityFixedTime > flipSessionIdleSeconds)
        {
            ResetAirFlipSessionBecauseIdle(frameSpinDegrees);
        }
    }

    private void ResetAirFlipSessionBecauseIdle(float frameSpinIgnored)
    {
        _idleFlipResetThisFixedStep = true;

        _airborneRotationDegrees = 0f;
        _prevRotationForAirSpin = _rb.rotation;
        _lastFlipActivityFixedTime = Time.fixedTime;
    }

    private void LateUpdate()
    {
        EmitAirborneFlipProgress();
    }

    private void EmitAirborneFlipProgress()
    {
        bool airborne = !_onTrampoline && !_fallen;

        float div = Mathf.Max(1f, degreesPerSuccessfulSpin);
        float deg = _airborneRotationDegrees;

        int visibleFlips = Mathf.FloorToInt(deg / div);
        float remainder = Mathf.Repeat(deg, div);
        float progress = remainder / div;

        bool idleResetNow = _idleFlipResetThisFixedStep;
        _idleFlipResetThisFixedStep = false;

        GameplayEventBus.RaiseAirborneFlipProgress(new AirborneFlipProgressInfo
        {
            IsAirborne = airborne,
            VisibleFullFlipCount = airborne ? visibleFlips : 0,
            ProgressTowardNextFlip = airborne ? Mathf.Clamp01(progress) : 0f,
            SessionRotationDegrees = airborne ? deg : 0f,
            IdleResetThisFrame = idleResetNow,
            DegreesPerFullFlipForUi = div,
        });

    }

    private void UpdateHighAltitudeMusicBand()
    {
        if (_fallen)
        {
            ClearHighAltitudeMusicBandIfNeeded();
            return;
        }

        float th = GameplayEventBus.HighAltitudeThresholdWorldY;
        bool nowHigh = transform.position.y >= th;
        if (nowHigh == _highAltitudeMusicBandActive)
            return;

        _highAltitudeMusicBandActive = nowHigh;
        if (nowHigh)
            GameplayEventBus.RaiseEnteredHighAir();
        else
            GameplayEventBus.RaiseExitedHighAir();
    }

    private void ClearHighAltitudeMusicBandIfNeeded()
    {
        if (!_highAltitudeMusicBandActive)
            return;

        _highAltitudeMusicBandActive = false;
        GameplayEventBus.RaiseExitedHighAir();
    }

    private void OnFlipStarted(InputAction.CallbackContext ctx)
    {
        if (_fallen) return;
        if (ctx.control?.device is Pointer)
        {
            var pos = Pointer.current?.position.ReadValue() ?? Vector2.zero;
            if (!IsInCenterRect(pos))
            {
                _touchAccepted = false;
                return;
            }

            _touchAccepted = true;
        }

        _flipHeld = true;
        _flipHoldElapsed = 0f;
        _lastHoldDegreesPerSecond = flipDegreesPerSecondStart;
    }

    private void OnFlipCanceled(InputAction.CallbackContext ctx)
    {
        if (ctx.control?.device is Pointer && !_touchAccepted)
            return;

        bool wasFlipping = _flipHeld && !_fallen;
        _flipHeld = false;
        _touchAccepted = false;

        if (wasFlipping && releaseAngularMomentumFactor > 0f)
        {
            float omegaRad = _lastHoldDegreesPerSecond * Mathf.Deg2Rad * releaseAngularMomentumFactor;
            _rb.angularVelocity = omegaRad;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_fallen) return;
        if (collision.collider.GetComponent<bounce>() == null)
            return;

        _onTrampoline = true;
        ClearHighAltitudeMusicBandIfNeeded();
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.collider.GetComponent<bounce>() == null)
            return;

        _onTrampoline = false;
        _jumpBaselineWorldY = transform.position.y;
        _jumpPeakWorldY = transform.position.y;
        _lastFlipActivityFixedTime = Time.fixedTime;
    }

    private bool IsInCenterRect(Vector2 screenPos)
    {
        var cam = Camera.main;
        if (!cam) return true; 

        Vector2 vp = cam.ScreenToViewportPoint(screenPos);
        return centerTouchViewportRect.Contains(vp);
    }

    public void HandleTrampolineBounce(float bounceForce)
    {
        if (_fallen) return;

        TrampolineLandingInfo landing = BuildLandingSnapshot();

        if (!IsUprightEnough())
        {
            GameplayEventBus.RaiseTrampolineLanding(landing);
            GameplayEventBus.RaiseFallenOffSurface();
            FallOff();
            return;
        }

        int spins = landing.CompletedFullFlips;
        LastLandingSuccessfulSpins = spins;
        float bounceMul = 1f + spins * bounceBonusPerSuccessfulSpin;
        _airborneRotationDegrees = 0f;

        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);
        _rb.AddForce(Vector2.up * (bounceForce * bounceMul), ForceMode2D.Impulse);

        _lifetimeFlipsCounted += spins;
        GameplayEventBus.RaiseTotalLifetimeFlips(_lifetimeFlipsCounted);

        GameplayEventBus.RaiseTrampolineLanding(landing);
    }

    private TrampolineLandingInfo BuildLandingSnapshot()
    {
        float spinDeg = _airborneRotationDegrees;
        int spins = Mathf.FloorToInt(spinDeg / Mathf.Max(1f, degreesPerSuccessfulSpin));
        spins = Mathf.Clamp(spins, 0, Mathf.Max(0, maxSpinsForBounceBonus));

        float height = Mathf.Max(0f, _jumpPeakWorldY - _jumpBaselineWorldY);

        return new TrampolineLandingInfo
        {
            JumpHeight = height,
            CompletedFullFlips = spins,
            LandingAngleDegreesFromUpright = GetLandingAngleDegreesFromUpright(),
            WasCleanLanding = IsUprightEnough(),
            WorldPosition = transform.position,
            PeakWorldY = _jumpPeakWorldY,
            BaselineWorldY = _jumpBaselineWorldY,
            AccumulatedSpinDegreesForJump = spinDeg
        };
    }

    private float GetLandingAngleDegreesFromUpright()
    {
        float z = transform.eulerAngles.z;
        if (z > 180f) z -= 360f;
        return z;
    }

    private bool IsUprightEnough()
    {
        return Mathf.Abs(GetLandingAngleDegreesFromUpright()) <= maxUprightAngleDeg;
    }

    private void FallOff()
    {
        _fallen = true;
        _flipHeld = false;
        _touchAccepted = false;
        _airborneRotationDegrees = 0f;
        ClearHighAltitudeMusicBandIfNeeded();

        if (freezeHorizontalPosition)
            _rb.constraints &= ~RigidbodyConstraints2D.FreezePositionX;

        float dir = Random.value < 0.5f ? -1f : 1f;
        _rb.AddForce(new Vector2(4f * dir, 2f), ForceMode2D.Impulse);
        _rb.AddTorque(8f * dir, ForceMode2D.Impulse);
    }
}
