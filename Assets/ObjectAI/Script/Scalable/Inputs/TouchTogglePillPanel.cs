using UnityEngine;
using UnityEngine.EventSystems;

public class TouchTogglePillPanel : MonoBehaviour
{
    [Header("Controller")]
    public StageController controller;

    [Header("Indices (match StageController order)")]
    public int pillIndex = 2;
    public int panelIndex = 3;

    [Header("Tap Detection")]
    [Tooltip("Ignore taps that start over UI (e.g., buttons, sliders).")]
    public bool ignoreUI = true;
    [Tooltip("Max time (seconds) between touch/mouse down & up to count as a tap.")]
    public float maxTapTime = 0.35f;
    [Tooltip("Max movement (pixels) between down & up to count as a tap.")]
    public float maxTapMove = 15f;

    private bool downActive;
    private float downTime;
    private Vector2 downPos;

    void Awake()
    {
        if (!controller) controller = FindObjectOfType<StageController>();
        if (!controller) Debug.LogWarning("[TouchTogglePillPanel] No StageController found.");
    }

    void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouse();
#endif
        HandleTouch();
    }

    void HandleMouse()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (ignoreUI && EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;
            downActive = true;
            downTime = Time.unscaledTime;
            downPos = Input.mousePosition;
        }
        else if (downActive && Input.GetMouseButtonUp(0))
        {
            var isTap = IsTap(downTime, downPos, (Vector2)Input.mousePosition);
            downActive = false;
            if (isTap) TogglePillPanel();
        }
    }

    void HandleTouch()
    {
        if (Input.touchCount == 0) return;

        var t = Input.GetTouch(0);

        if (t.phase == TouchPhase.Began)
        {
            if (ignoreUI && EventSystem.current && EventSystem.current.IsPointerOverGameObject(t.fingerId)) return;
            downActive = true;
            downTime = Time.unscaledTime;
            downPos = t.position;
        }
        else if (downActive && (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled))
        {
            var isTap = IsTap(downTime, downPos, t.position);
            downActive = false;
            if (isTap) TogglePillPanel();
        }
    }

    bool IsTap(float startedAt, Vector2 startPos, Vector2 endPos)
    {
        if (Time.unscaledTime - startedAt > maxTapTime) return false;
        if ((endPos - startPos).sqrMagnitude > maxTapMove * maxTapMove) return false;
        return true;
    }

    void TogglePillPanel()
    {
        if (controller == null || controller.CurrentIndex < 0) return;

        // Only toggle when already at Pill or Panel.
        if (controller.CurrentIndex == panelIndex)
            controller.RequestStageIndex(pillIndex);
        else if (controller.CurrentIndex == pillIndex)
            controller.RequestStageIndex(panelIndex);
        // else: ignore taps when in Dot/Icon (by design)
    }
}
