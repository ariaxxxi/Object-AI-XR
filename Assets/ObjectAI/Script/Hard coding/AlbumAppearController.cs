using UnityEngine;
using DG.Tweening;


public class AlbumAppearController : MonoBehaviour
{
    [Header("UI (RectTransforms)")]
    public RectTransform A, B, C, D;


    [Header("Timing")]
    public float duration = 1f;
    public Ease ease = Ease.InOutExpo;


    CanvasGroup aCg, bCg, cCg, dCg;
    Vector3 axz, bxz, cxz, dxz; // preserve each element's original X/Z
    Sequence active;


    void Awake()
    {
        // Cache X/Z and ensure CanvasGroups
        aCg = EnsureGroup(A); axz = KeepXZ(A);
        bCg = EnsureGroup(B); bxz = KeepXZ(B);
        cCg = EnsureGroup(C); cxz = KeepXZ(C);
        dCg = EnsureGroup(D); dxz = KeepXZ(D);
        AlbumDismiss();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.A)) AlbumAppear();
        if (Input.GetKeyDown(KeyCode.S)) AlbumDismiss();
    }



    // ---- Public controls -----------------------------------------------------


    public void AlbumAppear()
    {
        Debug.Log("Appear");

        KillActive();


        // Set exact START states
        aCg.alpha = 0f;  A.localRotation = Quaternion.Euler(axz.x, -80f, axz.z);
        bCg.alpha = 0f;  B.localRotation = Quaternion.Euler(bxz.x, -50f, bxz.z);
        cCg.alpha = 0f;  C.localRotation = Quaternion.Euler(cxz.x,  50f, cxz.z);
        dCg.alpha = 0f;  D.localRotation = Quaternion.Euler(dxz.x,  80f, dxz.z);


        active = DOTween.Sequence();


        // A: alpha 0->0.6, Y -60->-45
        active.Join(aCg.DOFade(0.6f, duration).SetEase(ease));
        active.Join(A.DOLocalRotate(new Vector3(axz.x, -45f, axz.z), duration, RotateMode.Fast).SetEase(ease));


        // B: alpha 0->0.8, Y -45->-30
        active.Join(bCg.DOFade(0.8f, duration).SetEase(ease));
        active.Join(B.DOLocalRotate(new Vector3(bxz.x, -30f, bxz.z), duration, RotateMode.Fast).SetEase(ease));


        // C: alpha 0->0.8, Y 45->30
        active.Join(cCg.DOFade(0.8f, duration).SetEase(ease));
        active.Join(C.DOLocalRotate(new Vector3(cxz.x, 30f, cxz.z), duration, RotateMode.Fast).SetEase(ease));


        // D: alpha 0->0.6, Y 60->45
        active.Join(dCg.DOFade(0.6f, duration).SetEase(ease));
        active.Join(D.DOLocalRotate(new Vector3(dxz.x, 45f, dxz.z), duration, RotateMode.Fast).SetEase(ease));


        active.Play();
    }



    public void AlbumDismiss()
    {
        Debug.Log("Dismiss");

        KillActive();


        active = DOTween.Sequence();


        // Tween from current values back to START values (alpha 0, original Y angles)
        active.Join(aCg.DOFade(0f, duration).SetEase(ease));
        active.Join(A.DOLocalRotate(new Vector3(axz.x, -80f, axz.z), duration, RotateMode.Fast).SetEase(ease));


        active.Join(bCg.DOFade(0f, duration).SetEase(ease));
        active.Join(B.DOLocalRotate(new Vector3(bxz.x, -50f, bxz.z), duration, RotateMode.Fast).SetEase(ease));


        active.Join(cCg.DOFade(0f, duration).SetEase(ease));
        active.Join(C.DOLocalRotate(new Vector3(cxz.x, 50f, cxz.z), duration, RotateMode.Fast).SetEase(ease));


        active.Join(dCg.DOFade(0f, duration).SetEase(ease));
        active.Join(D.DOLocalRotate(new Vector3(dxz.x, 80f, dxz.z), duration, RotateMode.Fast).SetEase(ease));


        active.Play();
    }


    // ---- Helpers -------------------------------------------------------------


    void KillActive()
    {
        if (active != null && active.IsActive()) active.Kill(false);
        DOTween.Kill(A); DOTween.Kill(B); DOTween.Kill(C); DOTween.Kill(D);
        DOTween.Kill(aCg); DOTween.Kill(bCg); DOTween.Kill(cCg); DOTween.Kill(dCg);
    }


    static CanvasGroup EnsureGroup(RectTransform rt)
    {
        var g = rt.GetComponent<CanvasGroup>();
        if (!g) g = rt.gameObject.AddComponent<CanvasGroup>();
        return g;
    }


    static Vector3 KeepXZ(RectTransform rt) =>
        new Vector3(rt.localEulerAngles.x, 0f, rt.localEulerAngles.z);
}
