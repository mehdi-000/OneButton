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
    public static event Action FlipHoldEnded;
    public static event Action<AirborneFlipProgressInfo> AirborneFlipProgress;
    public static event Action PerfectLanding;
    public static event Action ApexReached;
    public static event Action<PlayerController> PlayerFell;
    public static event Action<int> PartnersUnlocked;
    public static event Action GameWon;
    public static event Action EndCreditsStarted;

    // Per-run stats (reset on TransitionToGame in the UI controller).
    public static int RunFlips;
    public static int RunPerfectFlips;
    public static float RunStartTime;
    public static float RunFinishTime;

    public static float HighAltitudeThresholdWorldY { get; set; } = 8f;
    public static bool PartnersActive { get; private set; }
    public static float HeightAbovePlaySurface { get; private set; }
    public static float PeakHeightAbovePlaySurface { get; private set; }
    public static float PartnersMinHeightAboveSurface { get; private set; }
    public static float PartnersMaxHeightAboveSurface { get; private set; }

    public static float PartnersMidHeightAboveSurface =>
        (PartnersMinHeightAboveSurface + PartnersMaxHeightAboveSurface) * 0.5f;

    public static float PartnersVerticalSpread =>
        Mathf.Max(0f, PartnersMaxHeightAboveSurface - PartnersMinHeightAboveSurface);

    public static void SetPartnersActive(bool active)
    {
        PartnersActive = active;
        if (!active)
        {
            PartnersMinHeightAboveSurface = 0f;
            PartnersMaxHeightAboveSurface = 0f;
        }
    }

    public static void SetPartnersHeights(float minHeight, float maxHeight)
    {
        PartnersMinHeightAboveSurface = Mathf.Max(0f, minHeight);
        PartnersMaxHeightAboveSurface = Mathf.Max(PartnersMinHeightAboveSurface, maxHeight);
    }

    public static void SetHeightAbovePlaySurface(float worldUnits)
    {
        HeightAbovePlaySurface = Mathf.Max(0f, worldUnits);
        if (HeightAbovePlaySurface > PeakHeightAbovePlaySurface)
            PeakHeightAbovePlaySurface = HeightAbovePlaySurface;
    }

    public static void ResetPeakHeight()
    {
        PeakHeightAbovePlaySurface = 0f;
    }

    public static void RaiseTrampolineLanding(in TrampolineLandingInfo info) =>
        TrampolineLanding?.Invoke(info);

    public static void RaiseFallenOffSurface() =>
        FallenOffSurface?.Invoke();

    public static void RaiseTotalLifetimeFlips(int total) =>
        TotalLifetimeFlipsChanged?.Invoke(total);

    public static void RaiseFlipHoldStarted() =>
        FlipHoldStarted?.Invoke();

    public static void RaiseFlipHoldEnded() =>
        FlipHoldEnded?.Invoke();

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

    public static void RaisePlayerFell(PlayerController player) =>
        PlayerFell?.Invoke(player);

    public static void RaisePartnersUnlocked(int partnerCount) =>
        PartnersUnlocked?.Invoke(partnerCount);

    public static void RaiseGameWon() =>
        GameWon?.Invoke();

    public static void RaiseEndCreditsStarted() =>
        EndCreditsStarted?.Invoke();

    public static void ResetRunStats()
    {
        RunFlips = 0;
        RunPerfectFlips = 0;
        RunStartTime = 0f;
        RunFinishTime = 0f;
    }
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
