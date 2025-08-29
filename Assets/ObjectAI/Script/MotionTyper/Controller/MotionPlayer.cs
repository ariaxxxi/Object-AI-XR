using UnityEngine;

[DisallowMultipleComponent]
public class MotionPlayer : MonoBehaviour
{
    [Tooltip("Movement/motion component for THIS system/panel.")]
    public SystemMovement movement; // assign in inspector (no global Find)

    public void Play(MotionType t)
    {
        if (!movement) return;

        switch (t)
        {
            case MotionType.BouncyJumpAppearAndFloating: movement.BouncyJumpAppearAndFloating(); break;
            case MotionType.ShrinkDown:                  movement.ShrinkDown();                  break;
            case MotionType.StopFloating:                movement.StopFloating();                break;
            case MotionType.StartFloating:               movement.StartFloating();               break;
            case MotionType.None:
            default: break;
        }
    }
}