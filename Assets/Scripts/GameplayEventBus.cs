using System;
using UnityEngine;

public static class GameplayEventBus
{
    public static event Action<TrampolineLandingInfo> TrampolineLanding;
    public static event Action FallenOffSurface;
    public static event Action EnteredAirAboveThreshold;
    public static event Action ExitedAirAboveThreshold;
    public static event Action<int> TotalLifetimeFlipsChanged;
    public static event Action FlipHoldStarted;
    public static event Action<AirborneFlipProgressInfo> AirborneFlipProgress;
    public static event Action PerfectLanding;
    public static event Action ApexReached;

    public static float HighAltitudeThresholdWorldY { get; set; } = 8f;
    public static float HeightAbovePlaySurface { get; private set; }
    public static float PeakHeightAbovePlaySurface { get; private set; }

    public static void SetHeightAbovePlaySurface(float worldUnits)
    {
        HeightAbovePlaySurface = Mathf.Max(0f, worldUnits);
        if (HeightAbovePlaySurface > PeakHeightAbovePlaySurface)
            PeakHeightAbovePlaySurface = HeightAbovePlaySurface;
    }

    public static void RaiseTrampolineLanding(in TrampolineLandingInfo info) =>
        TrampolineLanding?.Invoke(info);

    public static void RaiseFallenOffSurface() =>
        FallenOffSurface?.Invoke();

    public static void RaiseTotalLifetimeFlips(int total) =>
        TotalLifetimeFlipsChanged?.Invoke(total);

    public static void RaiseFlipHoldStarted() =>
        FlipHoldStarted?.Invoke();

    public static void RaiseEnteredHighAir() =>
        EnteredAirAboveThreshold?.Invoke();

    public static void RaiseExitedHighAir() =>
        ExitedAirAboveThreshold?.Invoke();

    public static void RaiseAirborneFlipProgress(in AirborneFlipProgressInfo info) =>
        AirborneFlipProgress?.Invoke(info);

    public static void RaisePerfectLanding() =>
        PerfectLanding?.Invoke();

    public static void RaiseApexReached() =>
        ApexReached?.Invoke();
}

[Serializable]
public struct TrampolineLandingInfo
{
    public float JumpHeight;
    public int CompletedFullFlips;
    public float LandingAngleDegreesFromUpright;
    public bool WasCleanLanding;
    public bool WasPerfectLanding;
    public Vector3 WorldPosition;
    public float PeakWorldY;
}

[Serializable]
public struct AirborneFlipProgressInfo
{
    public bool IsAirborne;
    public int VisibleFullFlipCount;
    public float ProgressTowardNextFlip;
}

public class GameplayAltitudeSettings : MonoBehaviour
{
    [Tooltip("World Y where EnteredAirAboveThreshold fires.")]
    public float highAltitudeThresholdWorldY = 8f;

    void OnEnable() => GameplayEventBus.HighAltitudeThresholdWorldY = highAltitudeThresholdWorldY;
    void OnValidate() => GameplayEventBus.HighAltitudeThresholdWorldY = highAltitudeThresholdWorldY;
}
