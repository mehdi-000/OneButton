using System;
using UnityEngine;

public class ParticleEmitter : MonoBehaviour
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

    void OnDisable()
    {
        GameplayEventBus.TrampolineLanding -= PlayParticles;
    }

    private void PlayParticles(TrampolineLandingInfo info)
    {

        if (info.WasCleanLanding)
        {
            particles.Play();
        }    
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
