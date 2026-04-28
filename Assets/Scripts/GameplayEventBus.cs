using System;
using UnityEngine;

/// <summary>
/// Single hub for gameplay signals. Audio, VFX, and UI subscribe here without coupling to PlayerController.
/// </summary>
public static class GameplayEventBus
{
    /// <summary>Fired once per trampoline contact after a jump. Landing angle uses body Z rotation vs upright (degrees).</summary>
    public static event Action<TrampolineLandingInfo> TrampolineLanding;

    /// <summary>Player landed badly / fell off the run.</summary>
    public static event Action FallenOffSurface;

    /// <summary>Fired once when airborne Y crosses upward past <see cref="HighAltitudeThresholdWorldY"/>.</summary>
    public static event Action EnteredAirAboveThreshold;

    /// <summary>Fired once when airborne Y crosses downward past the altitude threshold.</summary>
    public static event Action ExitedAirAboveThreshold;

    /// <summary>Total full flips accumulated across landed jumps (lifetime this session).</summary>
    public static event Action<int> TotalLifetimeFlipsChanged;

    /// <summary>Raised once per frame (LateUpdate order) while the player is airborne; hides when grounded or fallen.</summary>
    public static event Action<AirborneFlipProgressInfo> AirborneFlipProgress;

    /// <summary>Set at runtime before play or from Inspector via <see cref="GameplayAltitudeSettings"/> helper.</summary>
    public static float HighAltitudeThresholdWorldY { get; set; } = 8f;

    public static void RaiseTrampolineLanding(in TrampolineLandingInfo info)
    {
        TrampolineLanding?.Invoke(info);
    }

    public static void RaiseFallenOffSurface()
    {
        FallenOffSurface?.Invoke();
    }

    public static void RaiseTotalLifetimeFlips(int total)
    {
        TotalLifetimeFlipsChanged?.Invoke(total);
    }

    public static void RaiseEnteredHighAir()
    {
        EnteredAirAboveThreshold?.Invoke();
    }

    public static void RaiseExitedHighAir()
    {
        ExitedAirAboveThreshold?.Invoke();
    }

    public static void RaiseAirborneFlipProgress(in AirborneFlipProgressInfo info)
    {
        AirborneFlipProgress?.Invoke(info);
    }
}

[Serializable]
public struct TrampolineLandingInfo
{
    /// <summary>Vertical gain this jump (world units): peak altitude minus altitude when trampoline contact ended.</summary>
    public float JumpHeight;

    /// <summary>Full rotations completed in air since last tramp contact (360° increments).</summary>
    public int CompletedFullFlips;

    /// <summary>Signed deviation of body Z rotation from upright, in approximate range [-180, 180]. Use absolute value vs your max.</summary>
    public float LandingAngleDegreesFromUpright;

    /// <summary>True if within <see cref="PlayerController"/> upright tolerance.</summary>
    public bool WasCleanLanding;

    public Vector3 WorldPosition;

    /// <summary>Peak Y reached during this airtime (world).</summary>
    public float PeakWorldY;

    /// <summary>Y when tramp contact broke (baseline for height).</summary>
    public float BaselineWorldY;

    /// <summary>Sum of rotations accumulated in air since last tramp (degrees).</summary>
    public float AccumulatedSpinDegreesForJump;
}

[Serializable]
public struct AirborneFlipProgressInfo
{
    /// <summary>Flying (not touching trampoline bounce surface) and not failed off.</summary>
    public bool IsAirborne;

    /// <summary>Full flips accumulated in the current air session (floored), same divisor as gameplay spin.</summary>
    public int VisibleFullFlipCount;

    /// <summary>Fraction toward the next flip [0,1).</summary>
    public float ProgressTowardNextFlip;

    /// <summary>Degrees used for VisibleFullFlipCount (since last tramp contact or idle reset).</summary>
    public float SessionRotationDegrees;

    /// <summary>True on the frame the session cleared because rotation went idle mid-air.</summary>
    public bool IdleResetThisFrame;

    /// <summary>Copy of degrees-per-spin used for HUD (matches gameplay spin rule).</summary>
    public float DegreesPerFullFlipForUi;
}

/// <summary>Optional: drop on any object and configure altitude threshold used by GameplayEventBus.</summary>
public class GameplayAltitudeSettings : MonoBehaviour
{
    [Tooltip("When airborne and world Y reaches this value or higher, EnteredAirAboveThreshold fires until touch-down.")]
    public float highAltitudeThresholdWorldY = 8f;

    private void OnEnable()
    {
        GameplayEventBus.HighAltitudeThresholdWorldY = highAltitudeThresholdWorldY;
    }

    private void OnValidate()
    {
        GameplayEventBus.HighAltitudeThresholdWorldY = highAltitudeThresholdWorldY;
    }
}
