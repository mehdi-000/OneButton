using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class SoundSettings
{
    [Tooltip("Master toggle for this sound.")]
    public bool enabled = true;

    [Range(0f, 2f)]
    public float volume = 1f;

    [Range(0.1f, 3f)]
    public float pitch = 1f;
}

public class SoundManager : MonoBehaviour
{
    [Serializable]
    class BounceSoundSettings : SoundSettings
    {
        [Tooltip("Extra pitch added per completed flip on landing.")]
        public float pitchPerFlip = 0.03f;

        [Tooltip("Maximum landing pitch after flip bonus is applied.")]
        public float maxPitch = 1.4f;
    }

    [Serializable]
    class FlipCounterSoundSettings : SoundSettings
    {
        [Tooltip("Seconds after the flip lead-in before the combo blip plays.")]
        public float delayAfterFlip = 0.05f;
    }

    [Serializable]
    class FizzleSoundSettings : SoundSettings
    {
        [Tooltip("Fizzle is audible while the player is at or below this height above the play surface.")]
        public float maxHeight = 10f;

        [Tooltip("Volume when touching the ground.")]
        [Range(0f, 2f)]
        public float volumeAtGround = 1f;

        [Tooltip("Volume at max height (still audible, but quieter).")]
        [Range(0f, 2f)]
        public float volumeAtMaxHeight = 0.15f;
    }

    [Header("Clips")]
    [SerializeField] AudioClip bounceClip;
    [SerializeField] AudioClip flipClip;
    [SerializeField] AudioClip flipCounterClip;
    [SerializeField] AudioClip fizzleLoopClip;
    [SerializeField] AudioClip musicClip;

    [Header("Sound Settings")]
    [SerializeField] BounceSoundSettings bounce = new();
    [SerializeField] SoundSettings flip = new();
    [SerializeField] FlipCounterSoundSettings flipCounter = new();
    [SerializeField] FizzleSoundSettings fizzle = new();
    [SerializeField] SoundSettings music = new();

    [Header("Sources")]
    [SerializeField] AudioSource sfxSource;
    [SerializeField] AudioSource flipSfxSource;
    [SerializeField] AudioSource loopSource;
    [SerializeField] AudioSource musicSource;

    int _lastLiveFlipFloor = -1;
    float _lastMasterSfxVolume = -1f;
    float _lastMasterMusicVolume = -1f;
    float _lastFizzleProximityVolume = -1f;
    bool _gameOver;

    void Awake()
    {
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        if (flipSfxSource == null)
        {
            flipSfxSource = gameObject.AddComponent<AudioSource>();
            flipSfxSource.playOnAwake = false;
        }

        if (loopSource == null)
        {
            loopSource = gameObject.AddComponent<AudioSource>();
            loopSource.playOnAwake = false;
            loopSource.loop = true;
        }
    }

    void Start()
    {
        SyncMasterVolumes(force: true);
        StartMusicIfNeeded();
    }

    void OnEnable()
    {
        GameplayEventBus.TrampolineLanding += OnTrampolineLanding;
        GameplayEventBus.AirborneFlipProgress += OnAirborneFlipProgress;
        GameplayEventBus.FallenOffSurface += OnFallenOffSurface;
    }

    void OnDisable()
    {
        GameplayEventBus.TrampolineLanding -= OnTrampolineLanding;
        GameplayEventBus.AirborneFlipProgress -= OnAirborneFlipProgress;
        GameplayEventBus.FallenOffSurface -= OnFallenOffSurface;
        StopAllCoroutines();
        StopFizzle();
    }

    void Update()
    {
        SyncMasterVolumes();
        UpdateFizzleByHeight();
    }

    void SyncMasterVolumes(bool force = false)
    {
        float sfx = MasterSfxVolume;
        float musicVol = MasterMusicVolume;
        if (!force
            && Mathf.Approximately(sfx, _lastMasterSfxVolume)
            && Mathf.Approximately(musicVol, _lastMasterMusicVolume))
            return;

        _lastMasterSfxVolume = sfx;
        _lastMasterMusicVolume = musicVol;

        if (musicSource != null)
            musicSource.volume = musicVol * music.volume;
    }

    static float MasterSfxVolume => PlayerPrefs.GetFloat("SfxVolume", 80f) / 100f;
    static float MasterMusicVolume => PlayerPrefs.GetFloat("MusicVolume", 80f) / 100f;

    void StartMusicIfNeeded()
    {
        if (musicSource == null || musicClip == null || !music.enabled)
            return;

        musicSource.clip = musicClip;
        musicSource.loop = true;
        musicSource.pitch = music.pitch;
        musicSource.volume = MasterMusicVolume * music.volume;
        if (!musicSource.isPlaying)
            musicSource.Play();
    }

    void OnTrampolineLanding(TrampolineLandingInfo info)
    {
        if (!info.WasCleanLanding || !bounce.enabled || bounceClip == null)
            return;

        float landingPitch = bounce.pitch + info.CompletedFullFlips * bounce.pitchPerFlip;
        landingPitch = Mathf.Min(landingPitch, bounce.maxPitch);
        PlayOneShot(sfxSource, bounceClip, bounce.volume, landingPitch);
    }

    void OnAirborneFlipProgress(AirborneFlipProgressInfo info)
    {
        if (!info.IsAirborne)
        {
            _lastLiveFlipFloor = -1;
            StopAllCoroutines();
            return;
        }

        int n = info.VisibleFullFlipCount;
        bool incremented = n > _lastLiveFlipFloor && (_lastLiveFlipFloor >= 0 || n >= 1);
        if (incremented)
            PlayFlipComboSequence();

        _lastLiveFlipFloor = n;
    }

    void PlayFlipComboSequence()
    {
        if (!flipCounter.enabled || flipCounterClip == null)
            return;

        StartCoroutine(FlipComboSequenceRoutine());
    }

    IEnumerator FlipComboSequenceRoutine()
    {
        if (flip.enabled && flipClip != null)
            PlayOneShot(flipSfxSource, flipClip, flip.volume, flip.pitch);

        if (flipCounter.delayAfterFlip > 0f)
            yield return new WaitForSeconds(flipCounter.delayAfterFlip);

        PlayOneShot(sfxSource, flipCounterClip, flipCounter.volume, flipCounter.pitch);
    }

    void OnFallenOffSurface()
    {
        _gameOver = true;
        StopAllCoroutines();
        StopFizzle();
    }

    void UpdateFizzleByHeight()
    {
        if (!fizzle.enabled || fizzleLoopClip == null || loopSource == null || _gameOver)
        {
            StopFizzle();
            return;
        }

        float height = GameplayEventBus.HeightAbovePlaySurface;
        if (height > fizzle.maxHeight)
        {
            StopFizzle();
            _lastFizzleProximityVolume = -1f;
            return;
        }

        if (!loopSource.isPlaying)
        {
            loopSource.clip = fizzleLoopClip;
            loopSource.pitch = fizzle.pitch;
            loopSource.Play();
            _lastFizzleProximityVolume = -1f;
        }

        float t = fizzle.maxHeight > 0f ? 1f - Mathf.Clamp01(height / fizzle.maxHeight) : 1f;
        float proximityVolume = Mathf.Lerp(fizzle.volumeAtMaxHeight, fizzle.volumeAtGround, t) * fizzle.volume;

        if (Mathf.Approximately(proximityVolume, _lastFizzleProximityVolume))
            return;

        _lastFizzleProximityVolume = proximityVolume;
        loopSource.volume = MasterSfxVolume * proximityVolume;
    }

    void StopFizzle()
    {
        if (loopSource != null && loopSource.isPlaying)
            loopSource.Stop();
    }

    static void PlayOneShot(AudioSource source, AudioClip clip, float volumeScale, float pitch)
    {
        if (clip == null || source == null)
            return;

        source.pitch = pitch;
        source.PlayOneShot(clip, MasterSfxVolume * volumeScale);
        source.pitch = 1f;
    }
}
