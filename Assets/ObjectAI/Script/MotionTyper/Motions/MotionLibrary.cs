using UnityEngine;
using DG.Tweening;

public class MotionLibrary : MonoBehaviour
{
    public enum MotionState { A_Tiny, B_Floating, C_Still }

    [Header("Target")]
    public Transform target;                 // defaults to this.transform

    [Header("Throw up & Start Float ")]
    public float growDuration = 0.35f;       // 0.1 -> 1
    public float throwDuration = 1.5f;       // total up + down
    private float ssTime = 0.10f;             // squash/stretch step
    private float settleToC_Duration = 0.6f;  // B -> C smooth settle
    private float toA_Duration = 0.45f;       // B/C -> A shrink

    public float throwHeight = 0.1f;         // world units
    private float arcForward = 0f;          // small forward arc

    public float spinDegrees = 360f;         // y spin during ascent
    float tiltAngle = 6f;             // cute tilt on ascent
    float tiltTime = 0.25f;           // tilt easing time

    public float growFrom = 0.1f;            // state A scale factor
    public float growTo = 1f;                // state B/C base scale
    private float squashX = 1.10f;            // anticipation widen (kept subtle for round UI)
    private float squashY = 0.90f;            // anticipation squash
    private float stretchX = 0.95f;           // takeoff narrow (subtle)
    private float stretchY = 1.05f;           // takeoff stretch (subtle)

    private float landingRebound = 0.0001f;     // small bounce height after landing

    [Header("Float Loop")]
    public float floatHeight = 0.01f;          // bob magnitude
    public float floatDuration = 0.7f;       // half-cycle (up or down)
    public float hoverYawPerLoop = 10f;      // gentle yaw

    [Header("Icon↔Pill Subtle Morph (panel-level)")]
    public float morphScaleUp = 1.02f;
    public float morphDuration = 0.35f;
    public Ease morphUpEase = Ease.OutQuad;
    public Ease morphDownEase = Ease.InQuad;

    // Internal
    private Sequence seq;                    // one-shot sequences
    private Tween floatTween;                // looping bob
    private Tween yawTween;                  // looping yaw
    private Tween morphTween;                // subtle icon↔pill scale pulse

    private Vector3 startPos;
    private Vector3 startEuler;
    private Vector3 baseScale;

    public MotionState State { get; private set; } = MotionState.A_Tiny;

    void Awake()
    {
        if (target == null) target = transform;
        startPos = target.position;
        startEuler = target.localEulerAngles;
        baseScale = target.localScale;
        // ApplyStateAInstant();
    }

    // --------------------------- Library Dispatcher -----------------------
    public void Play(MotionType t)
    {
        switch (t)
        {
            case MotionType.ThrowUpAndStartFloat:
                ThrowUpAndStartFloat();
                break;
            case MotionType.ShrinkDown:
                ShrinkDown();
                break;
            case MotionType.StopFloatLoop:
                StopFloatLoop();
                break;
            case MotionType.StartFloatLoop:
                StartFloatLoop();
                break;
            case MotionType.Appear:
                Appear();
                break;
            case MotionType.Disappear:
                Disappear();
                break;
            case MotionType.Wiggle:
                Wiggle();
                break;
            case MotionType.Bounce:
                Bounce();
                break;
            case MotionType.StartBreatheLoop:
                StartBreatheLoop();
                break;
            case MotionType.StopBreatheLoop:
                StopBreatheLoop();
                break;
            case MotionType.None:
            default:
                break;
        }
    }

    // --------------------------- A -> B (Throw) ---------------------------
    public void ThrowUpAndStartFloat()
    {
        KillAllTweens();

        Vector3 peakPos = startPos + Vector3.up * throwHeight + target.forward * arcForward;

        // Start visuals for A (tiny)
        target.position = startPos;
        target.localEulerAngles = startEuler;
        target.localScale = baseScale * growFrom;

        seq = DOTween.Sequence();

        // 1) Smooth grow 0.1 -> 1
        seq.Append(target.DOScale(baseScale * growTo, growDuration).SetEase(Ease.OutQuad));

        // 2) Anticipation dip + (subtle) squash — kept round friendly
        seq.Append(target.DOMoveY(startPos.y - landingRebound, ssTime).SetEase(Ease.InQuad));
        seq.Join(target.DOScale(new Vector3(baseScale.x * squashX, baseScale.y * squashY, baseScale.z * squashX), ssTime)
            .SetEase(Ease.OutQuad));

        // 3) Ascent to peak
        float ascendTime = throwDuration * 0.5f;
        seq.Append(target.DOMove(peakPos, ascendTime).SetEase(Ease.OutQuad));

        // Spin during ascent
        target.DOLocalRotate(new Vector3(startEuler.x, startEuler.y + spinDegrees, startEuler.z), ascendTime, RotateMode.FastBeyond360)
              .SetEase(Ease.Linear);

        // Brief stretch at lift-off then restore
        seq.Join(target.DOScale(new Vector3(baseScale.x * stretchX, baseScale.y * stretchY, baseScale.z * stretchX), ssTime)
            .SetEase(Ease.OutQuad));
        seq.AppendCallback(() =>
        {
            target.DOScale(baseScale * growTo, ssTime).SetEase(Ease.InOutQuad);
        });

        // Cute tilt wobble
        target.DOLocalRotate(new Vector3(startEuler.x + tiltAngle, startEuler.y, startEuler.z - tiltAngle),
                             tiltTime, RotateMode.Fast).SetEase(Ease.OutSine);

        // 4) Descent back to ground — using InOutQuad
        float descendTime = throwDuration * 0.5f;
        seq.Append(target.DOMoveY(startPos.y, descendTime).SetEase(Ease.InOutQuad));
        seq.Join(target.DOLocalRotate(new Vector3(startEuler.x, target.localEulerAngles.y, startEuler.z),
                                      descendTime, RotateMode.Fast).SetEase(Ease.InOutSine));

        // 5) Little landing bounce
        seq.Append(target.DOMoveY(startPos.y + landingRebound, 0.12f).SetEase(Ease.OutQuad));
        seq.Append(target.DOMoveY(startPos.y, 0.12f).SetEase(Ease.InQuad));

        // 6) Start floating loop (becomes State B)
        seq.AppendCallback(() => StartFloatLoopInternal());
        // seq.OnComplete(() => State = MotionState.B_Floating);

        seq.Play();
    }

    // --------------------------- Simple Appear/Disappear ------------------
    [Header("Appear / Disappear")]
    public float appearDuration = 0.3f;
    public float disappearDuration = 0.3f;

    public void Appear()
    {
        KillAllTweens();
        target.position = startPos;
        target.localEulerAngles = startEuler;
        target.localScale = baseScale * growFrom;
        target.DOScale(baseScale * growTo, appearDuration).SetEase(Ease.OutQuad);
    }

    public void Disappear()
    {
        // shrink to tiny; keep position
        if (seq != null && seq.IsActive()) seq.Kill(false);
        target.DOScale(baseScale * growFrom, disappearDuration).SetEase(Ease.InOutQuad);
    }

    // --------------------------- One-shot Wiggle/Bounce --------------------
    [Header("Wiggle / Bounce")]
    public float wiggleHeight = 0.01f;
    public float wiggleDuration = 0.5f; // total up+down time is 2x
    public float bounceScale = 1.1f;
    public float bounceDuration = 0.5f;

    public void Wiggle()
    {
        // quick up and down around current Y
        if (seq != null && seq.IsActive()) seq.Kill(false);
        var s = DOTween.Sequence();
        s.Append(target.DOMoveY(target.position.y + wiggleHeight, wiggleDuration * 0.5f).SetEase(Ease.OutSine));
        s.Append(target.DOMoveY(target.position.y, wiggleDuration * 0.5f).SetEase(Ease.InSine));
        seq = s.Play();
    }

    public void Bounce()
    {
        // Three-step pulse: 1.1 → 0.95 → 1.0 (relative to current)
        if (morphTween != null && morphTween.IsActive()) morphTween.Kill(false);
        Vector3 cur = target.localScale;
        Vector3 up = cur * 1.10f;
        Vector3 down = cur * 0.95f;
        float upTime = bounceDuration * 0.4f;
        float downTime = bounceDuration * 0.3f;
        float backTime = bounceDuration - (upTime + downTime); // quick settle

        var s = DOTween.Sequence();
        s.Append(target.DOScale(up, upTime).SetEase(Ease.OutQuad));
        s.Append(target.DOScale(down, downTime).SetEase(Ease.OutQuad));
        s.Append(target.DOScale(cur, backTime).SetEase(Ease.InQuad));
        morphTween = s.Play();
    }

    // --------------------------- Breathe loop -----------------------------
    [Header("Breathe Loop")]
    public float breatheScale = 1.03f;
    public float breatheDuration = 1.6f; // up or down
    private Tween breatheTween;

    public void StartBreatheLoop()
    {
        StopBreatheLoop();
        Vector3 cur = target.localScale;
        Vector3 up = cur * breatheScale;
        breatheTween = target.DOScale(up, breatheDuration)
                              .SetEase(Ease.InOutSine)
                              .SetLoops(-1, LoopType.Yoyo);
    }

    public void StopBreatheLoop()
    {
        if (breatheTween != null && breatheTween.IsActive()) breatheTween.Kill(false);
        breatheTween = null;
    }



    // --------------------------- B/C -> A (Round-friendly shrink) ---------
    public void ShrinkDown()
    {
        // Stop float/yaw and any running sequence
        KillFloatLoops();
        if (seq != null && seq.IsActive()) seq.Kill(false);
        if (morphTween != null && morphTween.IsActive()) morphTween.Kill(false);

        seq = DOTween.Sequence();

        // Gently settle to base Y & yaw=0 (no line squeeze; keep it round)
        seq.Append(target.DOMoveY(startPos.y, settleToC_Duration).SetEase(Ease.InOutQuad));
        seq.Join(target.DOLocalRotate(new Vector3(startEuler.x, 0f, startEuler.z), settleToC_Duration, RotateMode.Fast)
            .SetEase(Ease.InOutSine));

        // Round-friendly TV-off: quick pulse up then uniform shrink to tiny
        Vector3 overshoot = baseScale * (growTo * 1.08f);
        seq.Append(target.DOScale(overshoot, toA_Duration * 0.4f).SetEase(Ease.OutQuad));
        seq.Append(target.DOScale(baseScale * growFrom, 1f).SetEase(Ease.InOutExpo));

        // seq.OnComplete(() => State = MotionState.A_Tiny);
        seq.Play();
    }

    // --------------------------- B -> C (settle & stop) --------------------
    public void StopFloatLoop()
    {
        KillFloatLoops();
        if (seq != null && seq.IsActive()) seq.Kill(false);
        if (morphTween != null && morphTween.IsActive()) morphTween.Kill(false);

        seq = DOTween.Sequence();

        // Smoothly rotate yaw back to 0 and return to ground Y
        seq.Append(target.DOMoveY(startPos.y, settleToC_Duration).SetEase(Ease.InOutQuad));
        seq.Join(target.DOLocalRotate(new Vector3(startEuler.x, 0f, startEuler.z), settleToC_Duration, RotateMode.Fast)
            .SetEase(Ease.InOutSine));

        // Ensure base scale remains at grown size (perfect circle)
        seq.Join(target.DOScale(baseScale * growTo, settleToC_Duration * 0.6f).SetEase(Ease.InOutQuad));

        // seq.OnComplete(() => State = MotionState.C_Still);
        seq.Play();
    }

    public void StartFloatLoop()
    {
        KillAllTweens();

        seq = DOTween.Sequence();
        seq.Append(target.DOMoveY(startPos.y, toA_Duration * 0.5f).SetEase(Ease.InOutQuad));
        seq.Join(target.DOLocalRotate(startEuler, toA_Duration * 0.5f, RotateMode.Fast).SetEase(Ease.InOutSine));
        seq.AppendCallback(() => StartFloatLoopInternal());
        // seq.OnComplete(() => State = MotionState.B_Floating);
        seq.Play();
    }

    // --------------------------- Icon ↔ Pill (subtle morph only) ----------
    // These are for StageController's Icon<->Pill edge. They DO NOT disrupt float/yaw loops.
    public void SubtleMorphPulseUp()
    {
        SubtleMorphPulse(upwards: true);
    }
    public void SubtleMorphPulseDown()
    {
        SubtleMorphPulse(upwards: false);
    }

    private void SubtleMorphPulse(bool upwards)
    {
        // Keep it independent of float/yaw: do NOT KillFloatLoops; only pulse scale.
        if (morphTween != null && morphTween.IsActive()) morphTween.Kill(false);

        // Pulse relative to current scale (robust if called mid-animation)
        Vector3 cur = target.localScale;
        Vector3 up = cur * morphScaleUp;
        float upTime = morphDuration * 0.45f;
        float downTime = morphDuration - upTime;

        // Optionally bias easing a bit depending on direction (purely aesthetic)
        Ease upEase = morphUpEase;
        Ease downEase = morphDownEase;

        var s = DOTween.Sequence();
        s.Append(target.DOScale(up, upTime).SetEase(upEase));
        s.Append(target.DOScale(cur, downTime).SetEase(downEase));
        morphTween = s.Play();
    }

    // --------------------------- Loops & Helpers ---------------------------
    private void StartFloatLoopInternal()
    {
        KillFloatLoops();
        // Bob up/down forever around the landing level
        floatTween = target.DOMoveY(startPos.y + floatHeight, floatDuration)
                            .SetEase(Ease.InOutSine)
                            .SetLoops(-1, LoopType.Yoyo);
        // Slow hover yaw loop (ping-pong)
        yawTween = target.DOLocalRotate(startEuler + new Vector3(0f, hoverYawPerLoop, 0f), floatDuration * 2f, RotateMode.Fast)
                           .SetEase(Ease.InOutSine)
                           .SetLoops(-1, LoopType.Yoyo);
    }

    private void KillFloatLoops()
    {
        if (floatTween != null && floatTween.IsActive()) floatTween.Kill(false);
        if (yawTween != null && yawTween.IsActive()) yawTween.Kill(false);
        floatTween = null; yawTween = null;
    }

    private void KillAllTweens()
    {
        KillFloatLoops();
        StopBreatheLoop();
        if (seq != null && seq.IsActive()) seq.Kill(false);
        if (morphTween != null && morphTween.IsActive()) morphTween.Kill(false);
        DOTween.Kill(target);
    }

    private void ApplyStateAInstant()
    {
        KillAllTweens();
        target.position = startPos;
        target.localEulerAngles = startEuler;
        target.localScale = baseScale * growFrom;
        // State = MotionState.A_Tiny;
    }
}
