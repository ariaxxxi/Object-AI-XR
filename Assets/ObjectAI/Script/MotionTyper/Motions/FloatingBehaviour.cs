using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public class FloatingBehaviour : MonoBehaviour
{
    [Header("Params")]
    public float floatHeight = 0.006f;
    public float floatDuration = 0.7f;
    public float hoverYawDegrees = 10f;

    Tween floatTween, yawTween;
    Vector3 basePos, baseEuler;

    void Awake()
    {
        basePos = transform.position;
        baseEuler = transform.localEulerAngles;
    }

    [ContextMenu("StartFloating")]
    public void StartFloating()
    {
        StopFloating();
        floatTween = transform.DOMoveY(basePos.y + floatHeight, floatDuration)
                              .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        yawTween = transform.DOLocalRotate(baseEuler + new Vector3(0, hoverYawDegrees, 0), floatDuration * 2f, RotateMode.Fast)
                            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }

    [ContextMenu("StopFloating")]
    public void StopFloating()
    {
        if (floatTween != null && floatTween.IsActive()) floatTween.Kill(false);
        if (yawTween   != null && yawTween.IsActive())   yawTween.Kill(false);
        floatTween = yawTween = null;

        // optional: settle back
        transform.DOLocalRotate(baseEuler, 0.25f).SetEase(Ease.InOutSine);
        transform.DOMoveY(basePos.y, 0.25f).SetEase(Ease.InOutSine);
    }
}