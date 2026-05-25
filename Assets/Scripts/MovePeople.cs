using UnityEngine;
using DG.Tweening;
using System;

public class ShakePeople : MonoBehaviour
{
    [SerializeField] public float duration = 1.0f;
    [SerializeField] public AnimationCurve curve;
    [SerializeField] public float offset = 5f;
    [SerializeField] public float trigger;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    void OnEnable()
    {
        GameplayEventBus.TrampolineLanding += ShakePivot;
    }

    void OnDisable()
    {
        GameplayEventBus.TrampolineLanding -= ShakePivot;
    }

    private void ShakePivot(TrampolineLandingInfo info)
    {
        if (info.CompletedFullFlips >= trigger)
        {
            float pos = transform.position.x - offset;
            transform.DOLocalMoveX(pos,duration).SetEase(curve);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
