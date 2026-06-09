using UnityEngine;

public class AudioManagerScript : MonoBehaviour
{
    [SerializeField] AudioSource bounceLowClip;
    [SerializeField] AudioSource bounceHighClip;


    [SerializeField] AudioSource kitchenBackgroundLoop;
    [SerializeField] AudioSource panFizzleSoundLoop;

    [SerializeField] AudioSource FlipSFX;
    [SerializeField] AudioSource gameOverSFX;
    [SerializeField] AudioSource[] musicLayers;
    [SerializeField] AudioSource[] cheerSound;
    [SerializeField] AudioSource[] milestoneSFX;
    [SerializeField] AudioSource winSFX;
    private float flipSfxPitchValue = 1;


    private int currentTrackNum;

    int _lastLiveFlipFloor = -1;

    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        musicLayers[0].Play();
    }

    void OnEnable()
    {
        GameplayEventBus.TrampolineLanding += OnTrampolineLanding;
        GameplayEventBus.AirborneFlipProgress += OnAirborneFlipProgress;
        GameplayEventBus.GameWon += GameWonSFX;
    }

    void OnDisable()
    {
        GameplayEventBus.TrampolineLanding -= OnTrampolineLanding;
        GameplayEventBus.AirborneFlipProgress -= OnAirborneFlipProgress;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void GameWonSFX()
    {
        winSFX.Play();
        Debug.Log("Test-Audio");
        //Invoke("StopAllMusic", 0.1f);
    }

    void StopAllMusic()
    {
        musicLayers[0].Stop();
        musicLayers[1].Stop();
        musicLayers[2].Stop();
        musicLayers[3].Stop();
    }

    void OnTrampolineLanding(TrampolineLandingInfo info)
    {
        flipSfxPitchValue = 1;
        if (info.WasCleanLanding)
        {
            if(info.CompletedFullFlips < 5)
            {
                ChangeMusic(0);
                cheerSound[0].Play();
            }
            else if(info.CompletedFullFlips < 15)
            {
                bounceLowClip.Play();
                ChangeMusic(1);
                cheerSound[0].Play();
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
            musicLayers[currentTrackNum].Stop();

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
        if (incremented){
            FlipSFX.Play();
            flipSfxPitchValue = flipSfxPitchValue + 0.03f;
            FlipSFX.pitch = flipSfxPitchValue;
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
