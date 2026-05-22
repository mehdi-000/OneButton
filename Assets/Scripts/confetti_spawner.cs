using UnityEngine; 

public class confetti_spawner : MonoBehaviour

{

    [SerializeField] private ParticleSystem particles;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        particles = GetComponent<ParticleSystem>();
    }

    void OnEnable()
    {
        GameplayEventBus.TrampolineLanding += PlayParticles;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void PlayParticles(TrampolineLandingInfo info)
    {

        if (info.CompletedFullFlips >= 1 && info.WasCleanLanding)
        {
            particles.Play();
        }
    }
}
