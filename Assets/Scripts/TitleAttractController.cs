using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(Rigidbody2D))]
public class TitleAttractController : MonoBehaviour
{
    [Header("Launch")]
    [Tooltip("Upward velocity applied once when the title screen loads.")]
    [SerializeField] float startLaunchVelocity = 22f;

    [Header("Auto flip cycle")]
    [Tooltip("Wait after each cycle starts (or repeats) before simulating a flip press.")]
    [SerializeField] float delayBeforeFlipPress = 0.35f;
    [Tooltip("How long the spin button stays held.")]
    [SerializeField] float flipHoldDuration = 1.5f;
    [Tooltip("Wait after release before the next press.")]
    [SerializeField] float delayBeforeNextCycle = 2f;

    PlayerController _player;
    Rigidbody2D _rb;
    Coroutine _loop;

    void Awake()
    {
        _player = GetComponent<PlayerController>();
        _rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        GameplayEventBus.FlipHoldStarted += OnPlayerStart;
        _player.AttractMode = true;

        if (startLaunchVelocity > 0f)
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, startLaunchVelocity);

        _loop = StartCoroutine(FlipCycleLoop());
    }

    void OnDisable()
    {
        GameplayEventBus.FlipHoldStarted -= OnPlayerStart;
        StopAttract();
    }

    void OnPlayerStart() => StopAttract();

    void StopAttract()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }
        _player.SetAttractFlipHeld(false);
        _player.AttractMode = false;
    }

    IEnumerator FlipCycleLoop()
    {
        while (_player.AttractMode && !CrazyPanDogUIController.GameStarted)
        {
            if (delayBeforeFlipPress > 0f)
                yield return new WaitForSeconds(delayBeforeFlipPress);
            if (!_player.AttractMode || CrazyPanDogUIController.GameStarted) yield break;

            _player.SetAttractFlipHeld(true);

            if (flipHoldDuration > 0f)
                yield return new WaitForSeconds(flipHoldDuration);
            if (!_player.AttractMode || CrazyPanDogUIController.GameStarted) yield break;

            _player.SetAttractFlipHeld(false);

            if (delayBeforeNextCycle > 0f)
                yield return new WaitForSeconds(delayBeforeNextCycle);
        }
    }
}
