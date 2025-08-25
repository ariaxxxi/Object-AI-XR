using UnityEngine; 

using UnityEngine.UI; 

using DG.Tweening; 

using UnityEngine.Video;

public class PanelStateController : MonoBehaviour 

{ 

    [Header("Outline (Image)")] 

    [SerializeField] private RectTransform outlineRT; 

    [SerializeField] private CanvasGroup outlineCG; 

  

    [Header("Album (Image)")] 

    [SerializeField] private RectTransform albumRT; 

    [SerializeField] private CanvasGroup albumCG; 

    
    [Header("Album video (Webm)")] 

    [SerializeField] private RectTransform albumVideoRT; 

    [SerializeField] private CanvasGroup albumVideoCG; 

    [SerializeField] VideoPlayer albumVideoPlayer;


    [Header("Title (Text/Graphic)")] 

    [SerializeField] private RectTransform titleRT; 

    [SerializeField] private CanvasGroup titleCG; 

  

    [Header("Subtitle (Text/Graphic)")] 

    [SerializeField] private RectTransform subtitleRT; 

    [SerializeField] private CanvasGroup subtitleCG; 

  

    [Header("UI Group (Container under panel)")] 

    [SerializeField] private RectTransform groupRT; 

    [SerializeField] private CanvasGroup groupCG; 

  

    [Header("Tween Defaults")] 

    [SerializeField] private float uiDuration = 1f; 

    [SerializeField] private Ease uiEase = Ease.InOutExpo; 

  

    [Header("Positioning")] 

    [Tooltip("World Space Canvas friendly: OFF = use RectTransform.localPosition; ON = use anchoredPosition.")] 

    [SerializeField] private bool useAnchoredPosition = false; 

  

    // ------------------- PUBLIC TRANSITIONS ------------------- 

  

    public void DotToIcon() 

    { 

        TweenUI(outlineRT, outlineCG, alpha: 1f, size: new Vector2(100, 100)); 

        TweenUI(albumRT, albumCG, alpha: 1f, uniformScale: 0.8f, localPos: new Vector3(0, 0, 0)); 

        TweenUI(titleRT, titleCG, alpha: 0f, localPos: new Vector3(80, 16, 0)); 

        TweenUI(subtitleRT, subtitleCG, alpha: 0f, localPos: new Vector3(80, -17, 0)); 

        // TweenUI(groupRT, groupCG, alpha: 0f, localPos: new Vector3(0, -180, 0)); 

    } 

  

    public void IconToPill() 

    { 

        TweenUI(outlineRT, outlineCG, alpha: 1f, size: new Vector2(380, 100)); 

        TweenUI(albumRT, albumCG, alpha: 1f, uniformScale: 0.8f, localPos: new Vector3(-133, 0, 0)); 
        //Album video
        TweenUI(albumVideoRT, albumVideoCG, alpha: 0f, customDuration: 0.3f); 

        TweenUI(titleRT, titleCG, alpha: 1f, localPos: new Vector3(-16, 16, 0)); 

        TweenUI(subtitleRT, subtitleCG, alpha: 1f, localPos: new Vector3(32, -17, 0)); 

        // TweenUI(groupRT, groupCG, alpha: 0f, localPos: new Vector3(0, -180, 0)); 

    } 

  

    public void PillToPanel() 

    { 
        //Outline
        TweenUI(outlineRT, outlineCG, alpha: 0f); 
        //Album cover
        TweenUI(albumRT, albumCG, alpha: 1f, uniformScale: 3.6f, localPos: new Vector3(0, 380, 0)); 
        //Album video
        TweenUI(albumVideoRT, albumVideoCG, alpha: 1f, customDuration: 0.3f ); 

        albumVideoPlayer.time = 0;
        albumVideoPlayer.Play();
        albumVideoPlayer.isLooping = true;

        //Title
        TweenUI(titleRT, titleCG, alpha: 1f, localPos: new Vector3(0, 167, 0)); 
        //Subtitle
        TweenUI(subtitleRT, subtitleCG, alpha: 1f, localPos: new Vector3(0, 134, 0)); 
        //Controller
        TweenUI(groupRT, groupCG, alpha: 1f, localPos: new Vector3(0, 16 , 0)); 

    } 

  

    public void PanelToPill() 

    { 

        TweenUI(outlineRT, outlineCG, alpha: 1f, size: new Vector2(380, 100)); 

        TweenUI(albumRT, albumCG, alpha: 1f, uniformScale: 0.8f, localPos: new Vector3(-133, 0, 0)); 

        //Album video
        TweenUI(albumVideoRT, albumVideoCG, alpha: 0f); 
        albumVideoPlayer.Stop();

        TweenUI(titleRT, titleCG, alpha: 1f, localPos: new Vector3(-16, 16, 0)); 

        TweenUI(subtitleRT, subtitleCG, alpha: 1f, localPos: new Vector3(32, -17, 0)); 

        TweenUI(groupRT, groupCG, alpha: 0f, localPos: new Vector3(0, -180, 0)); 

    } 

  

    public void PillToIcon() 

    { 

        TweenUI(outlineRT, outlineCG, alpha: 1f, size: new Vector2(100, 100)); 

        TweenUI(albumRT, albumCG, alpha: 1f, uniformScale: 0.8f, localPos: new Vector3(0, 0, 0)); 

        TweenUI(titleRT, titleCG, alpha: 0f, localPos: new Vector3(80, 16, 0)); 

        TweenUI(subtitleRT, subtitleCG, alpha: 0f, localPos: new Vector3(80, -17, 0)); 


    } 

  

    public void IconToDot() 

    { 

        TweenUI(outlineRT, outlineCG, alpha: 1f, size: new Vector2(100, 100)); 

        TweenUI(albumRT, albumCG, alpha: 0f, uniformScale: 0.8f, localPos: new Vector3(0, 0, 0)); 

        TweenUI(titleRT, titleCG, alpha: 0f, localPos: new Vector3(80, 16, 0)); 

        TweenUI(subtitleRT, subtitleCG, alpha: 0f, localPos: new Vector3(80, -17, 0)); 


    } 

  

    // ------------------- GENERIC TWEEN HELPER ------------------- 

    private void TweenUI( 

        RectTransform rt, 

        CanvasGroup cg, 

        float? alpha = null, 

        Vector2? anchoredPos = null, 

        Vector3? localPos = null, 

        Vector2? size = null, 

        float? uniformScale = null, 

        float? customDuration = null, 

        Ease? customEase = null) 

    { 

        float dur = customDuration ?? uiDuration; 

        Ease ease = customEase ?? uiEase; 

  

        if (rt) 

        { 

            DOTween.Kill(rt); 

  

            if (useAnchoredPosition) 

            { 

                if (anchoredPos.HasValue) 

                    rt.DOAnchorPos(anchoredPos.Value, dur).SetEase(ease); 

            } 

            else 

            { 

                if (localPos.HasValue) 

                    rt.DOLocalMove(localPos.Value, dur).SetEase(ease); 

            } 

  

            if (size.HasValue) 

                rt.DOSizeDelta(size.Value, dur).SetEase(ease); 

  

            if (uniformScale.HasValue) 

                rt.DOScale(uniformScale.Value, dur).SetEase(ease); 

        } 

  

        if (cg && alpha.HasValue) 

        { 

            DOTween.Kill(cg); 

            cg.DOFade(alpha.Value, dur).SetEase(ease); 

        } 

    } 

} 