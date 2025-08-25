using UnityEngine;

[DisallowMultipleComponent]
public class MotionPlayer : MonoBehaviour
{
    public SystemMovement movement; // assign or auto-find

    void Awake()
    {
        if (!movement) movement = GetComponent<SystemMovement>();
        if (!movement) movement = FindObjectOfType<SystemMovement>();
    }

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
