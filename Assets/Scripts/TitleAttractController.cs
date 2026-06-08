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
    [Tooltip("Wait after liftoff before simulating a flip press.")]
    [SerializeField] float delayBeforeFlipPress = 0.35f;
    [Tooltip("How long the spin button stays held.")]
    [SerializeField] float flipHoldDuration = 1.5f;

    PlayerController _player;
    Rigidbody2D _rb;
    Coroutine _loop;
    bool _landingSeen;

    void Awake()
    {
        _player = GetComponent<PlayerController>();
        _rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        GameplayEventBus.FlipHoldStarted += OnPlayerStart;
        GameplayEventBus.TrampolineLanding += OnTrampolineLanding;
        _player.AttractMode = true;

        if (startLaunchVelocity > 0f)
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, startLaunchVelocity);

        _loop = StartCoroutine(FlipCycleLoop());
    }

    void OnDisable()
    {
        GameplayEventBus.FlipHoldStarted -= OnPlayerStart;
        GameplayEventBus.TrampolineLanding -= OnTrampolineLanding;
        StopAttract();
    }

    void OnPlayerStart() => StopAttract();

    void OnTrampolineLanding(TrampolineLandingInfo _) => _landingSeen = true;

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
        // First cycle is anchored by the manual launch in OnEnable; subsequent
        // cycles wait for TrampolineLanding so the press always fires a fixed
        // delay after liftoff and timing can't drift across bounces.
        bool firstCycle = true;
        while (_player.AttractMode && !CrazyPanDogUIController.GameStarted)
        {
            if (!firstCycle)
            {
                _landingSeen = false;
                yield return new WaitUntil(() =>
                    _landingSeen || !_player.AttractMode || CrazyPanDogUIController.GameStarted);
                if (!_player.AttractMode || CrazyPanDogUIController.GameStarted) yield break;
            }
            firstCycle = false;

            if (delayBeforeFlipPress > 0f)
                yield return new WaitForSeconds(delayBeforeFlipPress);
            if (!_player.AttractMode || CrazyPanDogUIController.GameStarted) yield break;

            _player.SetAttractFlipHeld(true);

            if (flipHoldDuration > 0f)
                yield return new WaitForSeconds(flipHoldDuration);
            if (!_player.AttractMode || CrazyPanDogUIController.GameStarted) yield break;

            _player.SetAttractFlipHeld(false);
        }
    }
}
