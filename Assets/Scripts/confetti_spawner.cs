using UnityEngine;

public class confetti_spawner : MonoBehaviour
{
    [SerializeField] ParticleSystem particles;

    void Start()
    {
        if (particles == null)
            particles = GetComponent<ParticleSystem>();
        if (particles != null)
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void OnEnable()
    {
        GameplayEventBus.TrampolineLanding += PlayParticles;
    }

    void OnDisable()
    {
        GameplayEventBus.TrampolineLanding -= PlayParticles;
    }

    void PlayParticles(TrampolineLandingInfo info)
    {
        if (!CrazyPanDogUIController.GameStarted) return;
        if (info.WasPerfectLanding && info.CompletedFullFlips >= 1)
            particles.Play();
    }
}
