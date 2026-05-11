using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using UnityEngine.SceneManagement;


[RequireComponent(typeof(UIDocument))]
public class ComboCounterController : MonoBehaviour
{

    UIDocument _ui;

    [SerializeField] float initialSize = 10;
    [SerializeField] float sizeModifier = 4.2f;
    [SerializeField] float maxSize = 100;
    void Awake(){
        _ui = GetComponent<UIDocument>();
    }

        void OnEnable()
    {
        GameplayEventBus.TrampolineLanding += OnTrampolineLanding;
        GameplayEventBus.AirborneFlipProgress += OnAirborneFlipProgress; // Flips counted while still in air
        GameplayEventBus.TotalLifetimeFlipsChanged += OnTotalFlipsChanged; //Flips added to score on landing
        // GameplayEventBus.FallenOffSurface += OnFallenOffSurface;
    }

    void OnDisable()
    {
        GameplayEventBus.TrampolineLanding -= OnTrampolineLanding;
        GameplayEventBus.AirborneFlipProgress -= OnAirborneFlipProgress;
        GameplayEventBus.TotalLifetimeFlipsChanged -= OnTotalFlipsChanged;
        //GameplayEventBus.FallenOffSurface -= OnFallenOffSurface;
    }

    void OnTrampolineLanding(TrampolineLandingInfo info) {
        // Do some cool stuff
    }

    void OnAirborneFlipProgress(AirborneFlipProgressInfo info) {

        Label ComboCounter = _ui.rootVisualElement.Q<Label>();

        if (info.VisibleFullFlipCount <= 0) {
            ComboCounter.style.color = new Color(0,0,0,0);
            ComboCounter.style.fontSize = new StyleLength(new Length(initialSize, LengthUnit.Percent));
        }
        else {
            ComboCounter.style.color = new Color(1,1,1,1);
        }
        
        ComboCounter.text = info.VisibleFullFlipCount.ToString();

        // Change total font size
        if (ComboCounter.resolvedStyle.fontSize < maxSize){
            ComboCounter.style.fontSize = new StyleLength(new Length(initialSize + (info.VisibleFullFlipCount * sizeModifier)));
        }
        else{
            ComboCounter.style.fontSize = new StyleLength(new Length(maxSize));
        }

        // Tween Font Size, actual size > big > actual size, slow fade out

        // Set Combo Color based on combo (read from gradient map?)
        // Tween Color, Grey > color > Grey, slow fade out (same as font)
    }

    void OnTotalFlipsChanged(int total) {
        // When the player lands, "celebrate" the new total flips
        Debug.Log("total flips: "+ total.ToString());
    }


}

