using UnityEngine;
using UnityEngine.Serialization;

public class AudioManagerScript : MonoBehaviour
{
    const string SoundVolumePref = "SoundVolume";

    [SerializeField] AudioSource bounceLowClip;
    [SerializeField] AudioSource bounceHighClip;


    [SerializeField] AudioSource kitchenBackgroundLoop;
    [SerializeField] AudioSource panFizzleSoundLoop;

    [SerializeField] AudioSource FlipSFX;
    [SerializeField] AudioSource gameOverSFX;
    [SerializeField] AudioSource[] musicLayers;
    [SerializeField] AudioSource[] cheerSound;
    [SerializeField] AudioSource[] milestoneSFX;
    [SerializeField] AudioSource gameWonSFX;

    [Header("Sound Volume")]
    [SerializeField]
    [FormerlySerializedAs("musicMixer")]
    [FormerlySerializedAs("soundMixer")]
    UnityEngine.Audio.AudioMixer soundMixer;

    [Header ("FlipSFX")]
    public float pitchIncrease = 0.015f;
    [Range(0f, 1f)] public float flipVolumeScale = 0.7f;



    private int currentTrackNum;

    int _lastLiveFlipFloor = -1;

    void Start()
    {
        musicLayers[0].Play();
        ApplySoundVolume(GetSavedSoundVolume());
        if (FlipSFX != null)
            FlipSFX.volume *= flipVolumeScale;
    }

    public static float GetSavedSoundVolume()
    {
        if (PlayerPrefs.HasKey(SoundVolumePref))
            return PlayerPrefs.GetFloat(SoundVolumePref, 80f);

        return PlayerPrefs.GetFloat("MusicVolume", 80f);
    }

    public static void SetSoundVolume(float percent)
    {
        percent = Mathf.Clamp(percent, 0f, 100f);
        PlayerPrefs.SetFloat(SoundVolumePref, percent);
        ApplySoundVolume(percent);
    }

    public static void ApplySoundVolume(float percent)
    {
        AudioListener.volume = percent / 100f;
    }

    void OnEnable()
    {
        GameplayEventBus.TrampolineLanding += OnTrampolineLanding;
        GameplayEventBus.AirborneFlipProgress += OnAirborneFlipProgress;
        GameplayEventBus.GameWon += PlayGameWonSFX;
        GameplayEventBus.EndCreditsStarted += StopMusic;
    }

    void OnDisable()
    {
        GameplayEventBus.TrampolineLanding -= OnTrampolineLanding;
        GameplayEventBus.AirborneFlipProgress -= OnAirborneFlipProgress;
        GameplayEventBus.GameWon -= PlayGameWonSFX;
        GameplayEventBus.EndCreditsStarted -= StopMusic;
    }

    void PlayGameWonSFX()
    {
        gameWonSFX.Play();
    }

    void StopMusic()
    {
        foreach (var layer in musicLayers)
            if (layer != null) layer.Stop();
    }

    void OnTrampolineLanding(TrampolineLandingInfo info)
    {
        if (info.WasCleanLanding)
        {
            FlipSFX.pitch = 1;
            if(info.CompletedFullFlips < 5)
            {
                ChangeMusic(0);
                cheerSound[0].Play();
            }
            else if(info.CompletedFullFlips < 15)
            {
                bounceLowClip.Play();
                ChangeMusic(1);
                cheerSound[1].Play();
            }
            else if(info.CompletedFullFlips < 30)
            {
                ChangeMusic(2);
                cheerSound[2].Play();
                cheerSound[3].Play();
            }
            else
            {
                bounceHighClip.Play();
                ChangeMusic(3);
                cheerSound[4].Play();
                cheerSound[5].Play();
            }


            if(info.CompletedFullFlips < 15)
            {
                bounceLowClip.Play();
            }
            else
            {
                bounceHighClip.Play();

            }

            
            /*
            if(info.CompletedFullFlips < 20 && info.CompletedFullFlips >= 10)
            {
                cheerSound[0].Play();
            }
            else if(info.CompletedFullFlips > 20)
            {
                cheerSound[1].Play();
            }
            */
            
        }
        else
        {
            gameOverSFX.Play();
            StopMusic();
        }
        
    }

    void ChangeMusic(int trackNum)
    {
        if(trackNum != currentTrackNum)
        {
            for(int i = 0; i < 4; i++)
            {
                if(trackNum == i)
                {
                    musicLayers[i].Play();
                }
                else
                {
                    musicLayers[i].Stop();
                }
            }
        }
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
        {
            FlipSFX.Play();
            FlipSFX.pitch += pitchIncrease;
        }

        if(info.VisibleFullFlipCount == 5)
        {
            milestoneSFX[0].Play();
        }
        if(info.VisibleFullFlipCount == 10)
        {
            milestoneSFX[1].Play();
        }
        if(info.VisibleFullFlipCount == 20)
        {
            milestoneSFX[2].Play();
        }
        
        _lastLiveFlipFloor = n;
    }



}
