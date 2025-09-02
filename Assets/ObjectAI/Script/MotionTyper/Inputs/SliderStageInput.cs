using UnityEngine;
using UnityEngine.UI;

// Optional: keep using a slider, but now via the generic request API.
public class SliderStageInput : StageInputSource
{
    public Slider slider;

    void OnEnable()
    {
        if (slider) slider.onValueChanged.AddListener(OnSlider);
    }

    void OnDisable()
    {
        if (slider) slider.onValueChanged.RemoveListener(OnSlider);
    }

    void OnSlider(float v) => RequestByValue(v);
}
