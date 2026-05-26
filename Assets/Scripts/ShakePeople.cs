using UnityEngine;
using DG.Tweening;
using Unity.VisualScripting;
using System;

public class MoveCheeringPeople : MonoBehaviour
{
    [SerializeField] private SpriteRenderer people;
    [SerializeField] public float strength = 1f;
    [SerializeField] public int vibratio = 10;

    [SerializeField] public float duration = 1.0f;
    [SerializeField] public float trigger;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {   
        people = GetComponent<SpriteRenderer>();
        people.enabled = false;
    }

    void OnEnable()
    {
        GameplayEventBus.TrampolineLanding += MovePeople;
    }

    void OnDisable()
    {
        GameplayEventBus.TrampolineLanding -= MovePeople;
    }

    void MovePeople(TrampolineLandingInfo info)
    {
        if (info.CompletedFullFlips >= trigger)
        {
            people.enabled = true;
            transform.DOShakePosition(duration, strength, vibratio, 90, false, true)
            .OnComplete(() => {
                people.enabled = false;
            });
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
