using UnityEngine; 

using UnityEngine.UI; 

using UnityEngine.Events; 

using DG.Tweening; 

  

public class StateController : MonoBehaviour 

{ 

    public enum FourState { Dot, Icon, Pill, Panel } 

  

    [Header("UI")] 

    public Slider slider; // expects 0..10 

  

    [Header("Slider tween")] 

    public float sliderTweenDuration = 0.35f; 

    public Ease sliderEase = Ease.InOutQuad; 

  

    [Header("Transitions (hook your functions here)")] 

    public UnityEvent onDotToIcon;     // forward 

    public UnityEvent onIconToPill; 

    public UnityEvent onPillToPanel; 

    public UnityEvent onPanelToPill;   // backward 

    public UnityEvent onPillToIcon; 

    public UnityEvent onIconToDot; 

  

    public FourState CurrentState { get; private set; } 

  

    Tween sliderTween; 

    bool _initialized; 

  

    // Range mapping: [0,2) Dot, [2,4) Icon, [4,7) Pill, [7,10] Panel 

    static FourState StateFromValue(float v) 

    { 

        if (v < 2f) return FourState.Dot; 

        if (v < 4f) return FourState.Icon; 

        if (v < 7f) return FourState.Pill; 

        return FourState.Panel; 

    } 

  

    void Awake() 

    { 

        if (!slider) 

        { 

            Debug.LogError("[AppStateController] Slider not assigned."); 

            enabled = false; 

            return; 

        } 

        slider.minValue = 0f; 

        slider.maxValue = 10f; 

  

        CurrentState = StateFromValue(slider.value); 

        slider.onValueChanged.AddListener(OnSliderChanged); 

        _initialized = true; 

    } 

  

    void OnDestroy() 

    { 

        if (slider) slider.onValueChanged.RemoveListener(OnSliderChanged); 

    } 

  

    void OnSliderChanged(float v) 

    { 

        if (!_initialized) return; 

  

        var next = StateFromValue(v); 

        if (next == CurrentState) return; 

  

        // Only allow linear transitions; step through one boundary at a time. 

        Step(CurrentState, next); 

        CurrentState = next; 

    } 

  

    void Step(FourState from, FourState to) 

    { 

        // Forward direction 

        if (from == FourState.Dot && (to == FourState.Icon || to == FourState.Pill || to == FourState.Panel)) 

        { 

            onDotToIcon?.Invoke(); 

            if (to == FourState.Pill || to == FourState.Panel) onIconToPill?.Invoke(); 

            if (to == FourState.Panel) onPillToPanel?.Invoke(); 

            return; 

        } 

        if (from == FourState.Icon && (to == FourState.Pill || to == FourState.Panel)) 

        { 

            onIconToPill?.Invoke(); 

            if (to == FourState.Panel) onPillToPanel?.Invoke(); 

            return; 

        } 

        if (from == FourState.Pill && to == FourState.Panel) 

        { 

            onPillToPanel?.Invoke(); 

            return; 

        } 

  

        // Backward direction 

        if (from == FourState.Panel && (to == FourState.Pill || to == FourState.Icon || to == FourState.Dot)) 

        { 

            onPanelToPill?.Invoke(); 

            if (to == FourState.Icon || to == FourState.Dot) onPillToIcon?.Invoke(); 

            if (to == FourState.Dot) onIconToDot?.Invoke(); 

            return; 

        } 

        if (from == FourState.Pill && (to == FourState.Icon || to == FourState.Dot)) 

        { 

            onPillToIcon?.Invoke(); 

            if (to == FourState.Dot) onIconToDot?.Invoke(); 

            return; 

        } 

        if (from == FourState.Icon && to == FourState.Dot) 

        { 

            onIconToDot?.Invoke(); 

        } 

    } 

  

    // --- Public helpers to drive the slider (keys, buttons, code) --- 

  

    public float CenterOf(FourState s) => s switch 

    { 

        FourState.Dot   => 1.0f, 

        FourState.Icon  => 3.0f, 

        FourState.Pill  => 5.5f, 

        FourState.Panel => 8.5f, 

        _ => 1.0f 

    }; 

  

    public void PlayTo(float to) => TweenSlider(to); 

  

    public void PlayFromTo(float from, float to) 

    { 

        slider.value = Mathf.Clamp(from, 0f, 10f); // sets start and may trigger a step 

        TweenSlider(to); 

    } 

  

    public void GoDot()   => TweenSlider(CenterOf(FourState.Dot)); 

    public void GoIcon()  => TweenSlider(CenterOf(FourState.Icon)); 

    public void GoPill()  => TweenSlider(CenterOf(FourState.Pill)); 

    public void GoPanel() => TweenSlider(CenterOf(FourState.Panel)); 

  

    void TweenSlider(float to) 

    { 

        KillSliderTween(); 

        sliderTween = slider.DOValue(Mathf.Clamp(to, 0f, 10f), sliderTweenDuration).SetEase(sliderEase); 

    } 

  

    void KillSliderTween() 

    { 

        if (sliderTween != null && sliderTween.IsActive()) sliderTween.Kill(false); 

        sliderTween = null; 

    } 

} 

 