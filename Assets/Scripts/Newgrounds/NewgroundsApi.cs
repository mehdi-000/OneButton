using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Thin wrapper around the NewGrounds.io v3 JS SDK exposed via Plugins/Newgrounds.jslib.
/// All IDs below are placeholders — replace with the real values from the NG project
/// dashboard before the WebGL build.
///
/// In the editor and non-WebGL builds, every method is a no-op that logs to the console
/// so gameplay code can call into it freely.
/// </summary>
public static class NewgroundsApi
{
    const string AppId = "REPLACE_WITH_NG_APP_ID";
    const string EncryptionKey = "REPLACE_WITH_NG_ENCRYPTION_KEY";

    const int ScoreboardTimeId = 0;

    const int MedalReachTop = 0;
    const int MedalPerfectStreakX10 = 0;
    const int MedalSpeedrunUnder60 = 0;
    const int MedalAllPerfect = 0;

    static bool _initialized;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void NG_Init(string appId, string encryptionKey);
    [DllImport("__Internal")] static extern void NG_LogView();
    [DllImport("__Internal")] static extern void NG_PostScore(int boardId, int value);
    [DllImport("__Internal")] static extern void NG_UnlockMedal(int medalId);
#endif

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

#if UNITY_WEBGL && !UNITY_EDITOR
        NG_Init(AppId, EncryptionKey);
        NG_LogView();
#else
        Debug.Log("[NewgroundsApi] Init (editor stub)");
#endif
    }

    /// <summary>Submit the time-to-1000m in seconds. Stored as centiseconds.</summary>
    public static void SubmitTime(float seconds)
    {
        int centiseconds = Mathf.RoundToInt(Mathf.Max(0f, seconds) * 100f);
#if UNITY_WEBGL && !UNITY_EDITOR
        NG_PostScore(ScoreboardTimeId, centiseconds);
#else
        Debug.Log($"[NewgroundsApi] SubmitTime: {seconds:F2}s ({centiseconds} cs)");
#endif
    }

    public static void UnlockReachTop()          => UnlockMedal(MedalReachTop,          nameof(UnlockReachTop));
    public static void UnlockPerfectStreakX10()  => UnlockMedal(MedalPerfectStreakX10,  nameof(UnlockPerfectStreakX10));
    public static void UnlockSpeedrunUnder60()   => UnlockMedal(MedalSpeedrunUnder60,   nameof(UnlockSpeedrunUnder60));
    public static void UnlockAllPerfect()        => UnlockMedal(MedalAllPerfect,        nameof(UnlockAllPerfect));

    static void UnlockMedal(int medalId, string name)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        NG_UnlockMedal(medalId);
#else
        Debug.Log($"[NewgroundsApi] UnlockMedal: {name} (id={medalId})");
#endif
    }
}
