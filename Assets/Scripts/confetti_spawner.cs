using UnityEngine;

public class confetti_spawner : MonoBehaviour
{
    [SerializeField] ParticleSystem particles;

    void Start()
    {
        if (particles == null)
            particles = GetComponent<ParticleSystem>();
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
        if (info.WasPerfectLanding && info.CompletedFullFlips >= 1)
            particles.Play();
    }
}
