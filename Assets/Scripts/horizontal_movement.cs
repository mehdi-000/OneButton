using UnityEngine;
using DG.Tweening;
using System;
using Random=UnityEngine.Random;

public class horizontal_movement : MonoBehaviour
{
    [SerializeField] public Vector2 durationRange;
    //[SerializeField] public float offset = 100.0f;
    [SerializeField] public AnimationCurve curve;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        transform.DOLocalMoveX(transform.position.x * -1.0f,Random.Range(durationRange.x,durationRange.y))
        .SetEase(curve)
        .SetLoops(-1, LoopType.Restart);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
