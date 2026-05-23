using UnityEngine;
using DG.Tweening;
using Unity.VisualScripting;
using System;

public class MoveCheeringPeople : MonoBehaviour
{
    [SerializeField] private SpriteRenderer people;
    [SerializeField] public AnimationCurve curve;
    [SerializeField] public float offset = 5f;
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

    void MovePeople(TrampolineLandingInfo info)
    {
        if (info.CompletedFullFlips >= trigger)
        {
            people.enabled = true;
            float pos = people.transform.position.x - offset;
            people.transform.DOLocalMoveX(pos,duration)
            .OnComplete(() => {
                people.enabled = false;
            })
            .SetEase(curve);
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
