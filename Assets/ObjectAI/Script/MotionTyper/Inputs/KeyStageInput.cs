using UnityEngine;

public class KeyStageInput : MonoBehaviour
{
    public StageController controller;

    [Header("Direct stage keys (optional)")]
    public KeyCode Stage_0   = KeyCode.Alpha1;
    public KeyCode Stage_1  = KeyCode.Alpha2;
    public KeyCode Stage_2  = KeyCode.Alpha3;
    public KeyCode Stage_3 = KeyCode.Alpha4;

    [Header("Nudge")]
    public KeyCode prevKey = KeyCode.LeftBracket;
    public KeyCode nextKey = KeyCode.RightBracket;

    void Awake()
    {
        if (!controller) controller = FindObjectOfType<StageController>();
    }

    void Update()
    {
        if (!controller) return;

        if (Input.GetKeyDown(Stage_0))   controller.RequestStageIndex(0);
        if (Input.GetKeyDown(Stage_1))  controller.RequestStageIndex(1);
        if (Input.GetKeyDown(Stage_2))  controller.RequestStageIndex(2);
        if (Input.GetKeyDown(Stage_3)) controller.RequestStageIndex(3);

        if (Input.GetKeyDown(prevKey)) controller.Nudge(-1);
        if (Input.GetKeyDown(nextKey)) controller.Nudge(+1);
    }
}
