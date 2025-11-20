using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class TwoStageScrollController : MonoBehaviour, IPointerClickHandler
{
    private enum Stage
    {
        Home,
        Widget,
        App
    }
    public enum InteractionType
    {
        AppLauncher,
        QuickAction,
        Expandable
    }

    [Header("Widget/Home Transition Visuals")]
    [SerializeField] private RectTransform topBlockRoot;
    [SerializeField] private RectTransform bottomBlockRoot;
    [SerializeField] private CanvasGroup bottomBlockCanvasGroup;
    [SerializeField] private CanvasGroup widgetCanvasGroup;
    [SerializeField] [Min(0f)] private float transitionDuration = 0.35f;
    [SerializeField] [Min(0f)] private float topBlockMoveDownDistance = 120f;
    [SerializeField] [Range(0f, 1f)] private float bottomBlockDimmedAlpha = 0.4f;
    [SerializeField] [Range(0f, 1f)] private float bottomBlockDimmedScale = 0.8f;
    [SerializeField] private bool autoAddBottomBlockCanvasGroup = true;

    [Header("Widget Selection")]
    [SerializeField] private List<TopBlockItemConfig> topBlockItems = new();
    [SerializeField] private bool sendPointerEvents = false;
    [SerializeField] private bool sendClickEvents = false;
    [SerializeField] private UnityEvent<int> onHoverIndexChanged;
    [SerializeField] private UnityEvent<int> onItemClicked;

    [Header("Hover Effect")]
    [SerializeField] [Min(0.05f)] private float hoverFadeDuration = 0.15f;
    [Header("Input Behavior")]
    [SerializeField] [Min(0f)] private float backHoverGraceSeconds = 2f;
    [Header("App Stage Visuals")]
    [SerializeField] [Min(0f)] private float appLogoDisplaySeconds = 1f;
    [SerializeField] [Min(0f)] private float appScreenExitDuration = 0.3f;
    [SerializeField] [Min(0f)] private float detailFadeDuration = 0.2f;
    [Header("Widget Item Visuals")]
    [SerializeField] private float widgetVisualStartY = 240f;
    [SerializeField] private float widgetVisualEndY = 200f;
    [SerializeField] [Range(0f, 1f)] private float widgetMinScale = 0.5f;

    private Stage _currentStage = Stage.Home;
    private int _hoveredIndex = -1;
    private Coroutine _blockTransitionRoutine;
    private bool _hasCachedBlockDefaults;
    private Vector2 _topBlockBaseAnchoredPosition;
    private Vector3 _bottomBlockBaseScale = Vector3.one;
    private float _bottomBlockBaseAlpha = 1f;
    private float _blockVisualProgress;
    private float _lastBackTime = float.NegativeInfinity;
    private int _lastWidgetHoverIndex = -1;
    private bool _detailOpen;
    private int _detailIndex = -1;
    private Coroutine _detailRoutine;
    private Coroutine _appRoutine;
    private Coroutine _quickActionRoutine;
    private TopBlockItemConfig _activeAppItem;
    private readonly Vector3[] _corners = new Vector3[4];

    private void Awake()
    {
        CacheBlockDefaults();
        ApplyBlockVisualState(_currentStage, true);
        ApplyAllNormalStates();
        PrepareWidgetVisuals();
    }

    private void OnValidate()
    {
        transitionDuration = Mathf.Max(0.05f, transitionDuration);
        topBlockMoveDownDistance = Mathf.Max(0f, topBlockMoveDownDistance);
        bottomBlockDimmedAlpha = Mathf.Clamp01(bottomBlockDimmedAlpha);
        bottomBlockDimmedScale = Mathf.Clamp01(bottomBlockDimmedScale);
        hoverFadeDuration = Mathf.Max(0.05f, hoverFadeDuration);
        backHoverGraceSeconds = Mathf.Max(0f, backHoverGraceSeconds);
        appLogoDisplaySeconds = Mathf.Max(0f, appLogoDisplaySeconds);
        appScreenExitDuration = Mathf.Max(0.01f, appScreenExitDuration);
        detailFadeDuration = Mathf.Max(0.01f, detailFadeDuration);
        if (Mathf.Approximately(widgetVisualStartY, widgetVisualEndY))
        {
            widgetVisualEndY = widgetVisualStartY - 0.01f;
        }
        widgetMinScale = Mathf.Clamp01(widgetMinScale);

        if (!Application.isPlaying)
        {
            ResetBlockDefaultsCache();
            CacheBlockDefaults();
            ApplyBlockVisualState(_currentStage, true);
            ApplyAllNormalStates();
            PrepareWidgetVisuals();
        }
    }

    private void OnEnable()
    {
        CacheBlockDefaults();
        ApplyBlockVisualState(_currentStage, true);
        PrepareWidgetVisuals();
    }

    private void OnDisable()
    {
        if (_blockTransitionRoutine != null)
        {
            StopCoroutine(_blockTransitionRoutine);
            _blockTransitionRoutine = null;
        }
        if (_detailRoutine != null)
        {
            StopCoroutine(_detailRoutine);
            _detailRoutine = null;
        }
        if (_appRoutine != null)
        {
            StopCoroutine(_appRoutine);
            _appRoutine = null;
        }
        if (_quickActionRoutine != null)
        {
            StopCoroutine(_quickActionRoutine);
            _quickActionRoutine = null;
        }
    }

    private void Update()
    {
        HandleKeyboardInput();
        UpdateWidgetItemVisuals();
    }

    private void HandleKeyboardInput()
    {
        bool aPressed = Input.GetKeyDown(KeyCode.A);
        bool dPressed = Input.GetKeyDown(KeyCode.D);
        bool backPressed = Input.GetKeyDown(KeyCode.Space);

        if (backPressed)
        {
            if (_currentStage == Stage.Widget)
            {
                _lastWidgetHoverIndex = _hoveredIndex;
                _lastBackTime = Time.unscaledTime;
            }
            else if (_currentStage == Stage.App)
            {
                if (_appRoutine != null)
                {
                    StopCoroutine(_appRoutine);
                    _appRoutine = null;
                }
                _lastWidgetHoverIndex = _hoveredIndex;
                _lastBackTime = Time.unscaledTime;
                StartCoroutine(ExitAppStage());
                return;
            }
            SetStage(Stage.Home);
            return;
        }

        if (aPressed)
        {
            if (_currentStage == Stage.Home)
            {
                bool reuseLastHover = Time.unscaledTime - _lastBackTime <= backHoverGraceSeconds &&
                                      _lastWidgetHoverIndex >= 0 &&
                                      _lastWidgetHoverIndex < topBlockItems.Count;
                int? desiredHover = reuseLastHover ? _lastWidgetHoverIndex : (int?)null;
                SetStage(Stage.Widget, desiredHover);
            }
            else if (_currentStage == Stage.Widget)
            {
                HoverPreviousItem();
            }
        }
        else if (dPressed)
        {
            if (_currentStage == Stage.Widget)
            {
                if (_hoveredIndex >= topBlockItems.Count - 1)
                {
                    SetStage(Stage.Home);
                }
                else
                {
                    HoverNextItem();
                }
            }
        }
    }

    private void SetStage(Stage stage, int? desiredHoverIndex = null)
    {
        if (_currentStage == stage)
        {
            return;
        }

        _currentStage = stage;
        if (stage == Stage.Widget)
        {
            if (desiredHoverIndex.HasValue && desiredHoverIndex.Value >= 0 && desiredHoverIndex.Value < topBlockItems.Count)
            {
                SetHoveredIndex(desiredHoverIndex.Value, true);
            }
            else
            {
                EnsureLastItemHovered();
            }
            var widgetCg = GetWidgetCanvasGroup();
            if (widgetCg != null)
            {
                widgetCg.alpha = 1f;
            }
        }
        else
        {
            ClearHover();
            if (_detailOpen)
            {
                CloseDetailInstant();
            }
        }

        ApplyBlockVisualState(stage, false);
    }

    private void ApplyBlockVisualState(Stage stage, bool instant)
    {
        if (topBlockRoot == null && bottomBlockRoot == null)
        {
            _blockVisualProgress = StageToProgress(stage);
            return;
        }

        CacheBlockDefaults();
        if (_blockTransitionRoutine != null)
        {
            StopCoroutine(_blockTransitionRoutine);
            _blockTransitionRoutine = null;
        }

        float targetProgress = StageToProgress(stage);
        if (instant || !Application.isPlaying || Mathf.Approximately(_blockVisualProgress, targetProgress))
        {
            SetBlockVisualState(targetProgress);
            return;
        }

        float duration = Mathf.Max(0.05f, transitionDuration);
        _blockTransitionRoutine = StartCoroutine(AnimateBlockVisuals(targetProgress, duration));
    }

    private IEnumerator AnimateBlockVisuals(float targetProgress, float duration)
    {
        float startProgress = _blockVisualProgress;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutCubic(t);
            float progress = Mathf.Lerp(startProgress, targetProgress, eased);
            SetBlockVisualState(progress);
            yield return null;
        }

        SetBlockVisualState(targetProgress);
        _blockTransitionRoutine = null;
    }

    private void SetBlockVisualState(float progress)
    {
        progress = Mathf.Clamp01(progress);
        _blockVisualProgress = progress;

        if (bottomBlockRoot != null)
        {
            Vector3 targetScale = Vector3.Lerp(_bottomBlockBaseScale, _bottomBlockBaseScale * bottomBlockDimmedScale, progress);
            if (bottomBlockRoot.localScale != targetScale)
            {
                bottomBlockRoot.localScale = targetScale;
            }

            CanvasGroup group = GetBottomBlockCanvasGroup();
            if (group != null)
            {
                float targetAlpha = Mathf.Lerp(_bottomBlockBaseAlpha, bottomBlockDimmedAlpha, progress);
                if (_currentStage == Stage.App)
                {
                    targetAlpha = 0f;
                }
                group.alpha = targetAlpha;
            }
        }

        if (topBlockRoot != null)
        {
            Vector2 targetPosition = Vector2.Lerp(_topBlockBaseAnchoredPosition, _topBlockBaseAnchoredPosition + Vector2.down * topBlockMoveDownDistance, progress);
            if (topBlockRoot.anchoredPosition != targetPosition)
            {
                topBlockRoot.anchoredPosition = targetPosition;
            }
        }
    }

    private float StageToProgress(Stage stage)
    {
        return stage == Stage.Widget ? 1f : 0f;
    }

    private void CacheBlockDefaults()
    {
        if (_hasCachedBlockDefaults)
        {
            return;
        }

        if (topBlockRoot != null)
        {
            _topBlockBaseAnchoredPosition = topBlockRoot.anchoredPosition;
        }

        if (bottomBlockRoot != null)
        {
            _bottomBlockBaseScale = bottomBlockRoot.localScale;
            CanvasGroup group = GetBottomBlockCanvasGroup();
            if (group != null)
            {
                _bottomBlockBaseAlpha = group.alpha;
            }
        }

        _blockVisualProgress = StageToProgress(_currentStage);
        _hasCachedBlockDefaults = true;
    }

    private void ResetBlockDefaultsCache()
    {
        _hasCachedBlockDefaults = false;
    }

    private CanvasGroup GetBottomBlockCanvasGroup()
    {
        if (bottomBlockCanvasGroup == null && bottomBlockRoot != null)
        {
            bottomBlockCanvasGroup = bottomBlockRoot.GetComponent<CanvasGroup>();
            if (bottomBlockCanvasGroup == null && autoAddBottomBlockCanvasGroup)
            {
                bottomBlockCanvasGroup = bottomBlockRoot.gameObject.AddComponent<CanvasGroup>();
            }
        }

        return bottomBlockCanvasGroup;
    }

    private CanvasGroup GetWidgetCanvasGroup()
    {
        if (widgetCanvasGroup == null && topBlockRoot != null)
        {
            widgetCanvasGroup = topBlockRoot.GetComponent<CanvasGroup>();
            if (widgetCanvasGroup == null)
            {
                widgetCanvasGroup = topBlockRoot.gameObject.AddComponent<CanvasGroup>();
            }
        }
        return widgetCanvasGroup;
    }

    private void PrepareWidgetVisuals()
    {
        for (int i = 0; i < topBlockItems.Count; i++)
        {
            var entry = topBlockItems[i];
            if (entry == null)
            {
                continue;
            }

            entry.AppLogo = EnsureCanvasGroup(entry.AppLogo != null ? entry.AppLogo.gameObject : null);
            entry.AppScreen = EnsureCanvasGroup(entry.AppScreen != null ? entry.AppScreen.gameObject : null);
            entry.Detail = EnsureCanvasGroup(entry.Detail != null ? entry.Detail.gameObject : null);
            entry.QuickAction = EnsureCanvasGroup(entry.QuickAction != null ? entry.QuickAction.gameObject : null);

            if (entry.AppLogo != null)
            {
                entry.AppLogo.alpha = 0f;
            }
            if (entry.AppScreen != null)
            {
                entry.AppScreen.alpha = 0f;
            }
            if (entry.Detail != null)
            {
                entry.Detail.alpha = 0f;
            }
            if (entry.AppPill != null)
            {
                var pillGroup = EnsureCanvasGroup(entry.AppPill);
                if (pillGroup != null)
                {
                    pillGroup.alpha = 1f;
                }
            }
            if (entry.QuickAction != null)
            {
                entry.QuickAction.alpha = 0f;
            }
            entry.QuickActionActive = false;
            entry.AppLaunchedOnce = false;
        }
    }

    private CanvasGroup EnsureCanvasGroup(GameObject go)
    {
        if (go == null)
        {
            return null;
        }
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = go.AddComponent<CanvasGroup>();
        }
        return cg;
    }

    private void ApplyAllNormalStates()
    {
        for (int i = 0; i < topBlockItems.Count; i++)
        {
            SetItemHoverState(i, false);
        }
    }

    private void EnsureLastItemHovered()
    {
        if (topBlockItems.Count == 0)
        {
            ClearHover();
            return;
        }
        int lastIndex = topBlockItems.Count - 1;
        if (GetItem(lastIndex) == null)
        {
            ClearHover();
            return;
        }
        if (_hoveredIndex != lastIndex)
        {
            SetHoveredIndex(lastIndex, true);
        }
    }

    private void HoverPreviousItem()
    {
        if (topBlockItems.Count == 0)
        {
            return;
        }
        int targetIndex = Mathf.Max(0, _hoveredIndex - 1);
        SetHoveredIndex(targetIndex, true);
    }

    private void HoverNextItem()
    {
        if (topBlockItems.Count == 0)
        {
            return;
        }
        int targetIndex = Mathf.Min(topBlockItems.Count - 1, _hoveredIndex + 1);
        SetHoveredIndex(targetIndex, true);
    }

    private void ClearHover()
    {
        RectTransform previousRect = GetItem(_hoveredIndex);
        if (previousRect != null)
        {
            GameObject previous = previousRect.gameObject;
            if (sendPointerEvents)
            {
                ExecutePointerExit(previous);
            }
            SetItemHoverState(_hoveredIndex, false);
        }

        _hoveredIndex = -1;
        onHoverIndexChanged?.Invoke(-1);
    }

    private void SetHoveredIndex(int index, bool force = false)
    {
        if (topBlockItems.Count == 0)
        {
            ClearHover();
            return;
        }

        index = Mathf.Clamp(index, 0, topBlockItems.Count - 1);
        if (!force && index == _hoveredIndex)
        {
            return;
        }

        GameObject previous = null;
        RectTransform previousRect = GetItem(_hoveredIndex);
        if (previousRect != null)
        {
            previous = previousRect.gameObject;
        }

        RectTransform currentRect = GetItem(index);
        GameObject current = currentRect != null ? currentRect.gameObject : null;

        if (sendPointerEvents && previous != null)
        {
            ExecutePointerExit(previous);
        }

        SetItemHoverState(_hoveredIndex, false);
        _hoveredIndex = index;
        SetItemHoverState(_hoveredIndex, true);

        if (current != null && sendPointerEvents)
        {
            ExecutePointerEnter(current);
        }

        onHoverIndexChanged?.Invoke(_hoveredIndex);
        if (_hoveredIndex >= 0)
        {
            _lastWidgetHoverIndex = _hoveredIndex;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_currentStage != Stage.Widget || _hoveredIndex < 0 || _hoveredIndex >= topBlockItems.Count)
        {
            return;
        }

        onItemClicked?.Invoke(_hoveredIndex);
        if (sendClickEvents && EventSystem.current != null)
        {
            RectTransform rect = GetItem(_hoveredIndex);
            GameObject current = rect != null ? rect.gameObject : null;
            if (current != null)
            {
                ExecutePointerClick(current, eventData);
            }
        }

        HandleItemSelection(_hoveredIndex);
    }

    private void SetItemHoverState(int index, bool isHovered)
    {
        RectTransform rect = GetItem(index);
        if (rect == null)
        {
            return;
        }

        CanvasGroup hoverChild = GetHoverChild(rect);
        if (hoverChild != null)
        {
            float targetAlpha = isHovered ? 1f : 0f;
            if (Application.isPlaying)
            {
                StartCoroutine(FadeHoverChild(hoverChild, targetAlpha));
            }
            else
            {
                hoverChild.alpha = targetAlpha;
            }
        }
    }

    private CanvasGroup GetHoverChild(RectTransform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            CanvasGroup canvasGroup = child.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                return canvasGroup;
            }
        }

        return null;
    }

    private IEnumerator FadeHoverChild(CanvasGroup canvasGroup, float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsedTime = 0f;
        while (elapsedTime < hoverFadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / hoverFadeDuration);
            float smoothProgress = progress * progress * (3f - 2f * progress);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothProgress);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    private RectTransform GetItem(int logicalIndex)
    {
        if (logicalIndex < 0 || logicalIndex >= topBlockItems.Count)
        {
            return null;
        }

        return topBlockItems[logicalIndex]?.Item;
    }

    private void ExecutePointerEnter(GameObject target)
    {
        if (EventSystem.current == null)
        {
            return;
        }

        var data = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerEnterHandler);
    }

    private void ExecutePointerExit(GameObject target)
    {
        if (EventSystem.current == null)
        {
            return;
        }

        var data = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerExitHandler);
    }

    private void ExecutePointerClick(GameObject target, PointerEventData original)
    {
        if (EventSystem.current == null)
        {
            return;
        }

        var data = new PointerEventData(EventSystem.current)
        {
            button = original != null ? original.button : PointerEventData.InputButton.Left,
            clickCount = original != null ? original.clickCount : 1
        };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inv = t - 1f;
        return 1f + inv * inv * inv;
    }

    private void HandleItemSelection(int index)
    {
        TopBlockItemConfig entry = GetEntry(index);
        if (entry == null)
        {
            return;
        }

        switch (entry.Interaction)
        {
            case InteractionType.AppLauncher:
                BeginAppLaunch(entry);
                break;
            case InteractionType.Expandable:
                ToggleDetail(entry, index);
                break;
            case InteractionType.QuickAction:
                ToggleQuickAction(entry);
                break;
        }
    }

    private void BeginAppLaunch(TopBlockItemConfig entry)
    {
        if (_appRoutine != null)
        {
            StopCoroutine(_appRoutine);
        }
        _appRoutine = StartCoroutine(RunAppLaunch(entry));
    }

    private IEnumerator RunAppLaunch(TopBlockItemConfig entry)
    {
        _activeAppItem = entry;
        _detailOpen = false;
        _detailIndex = -1;
        if (_detailRoutine != null)
        {
            StopCoroutine(_detailRoutine);
            _detailRoutine = null;
        }

        yield return FadeWidgetView(false);
        SetStage(Stage.App);

        if (entry.DefaultText != null)
        {
            entry.DefaultText.SetActive(false);
        }

        if (entry.AppPill != null)
        {
            entry.AppPill.SetActive(true);
        }

        if (entry.AppLogo != null)
        {
            if (!entry.AppLaunchedOnce)
            {
                entry.AppLogo.gameObject.SetActive(true);
                yield return FadeCanvas(entry.AppLogo, 0f, 1f, transitionDuration * 0.5f);
                if (appLogoDisplaySeconds > 0f)
                {
                    yield return new WaitForSeconds(appLogoDisplaySeconds);
                }
                yield return FadeCanvas(entry.AppLogo, entry.AppLogo.alpha, 0f, transitionDuration * 0.5f);
                entry.AppLogo.gameObject.SetActive(false);
            }
        }

        if (entry.AppScreen != null)
        {
            entry.AppScreen.gameObject.SetActive(true);
            float fromScale = 0.8f;
            float fromAlpha = 0f;
            entry.AppScreen.alpha = fromAlpha;
            entry.AppScreen.transform.localScale = Vector3.one * fromScale;
            yield return FadeAndScaleCanvas(entry.AppScreen, fromScale, 1f, transitionDuration, false, fromAlpha);
        }

        entry.AppLaunchedOnce = true;
        _appRoutine = null;
    }

    private IEnumerator ExitAppStage()
    {
        if (_activeAppItem != null && _activeAppItem.AppLogo != null)
        {
            _activeAppItem.AppLogo.alpha = 0f;
            _activeAppItem.AppLogo.gameObject.SetActive(false);
        }

        if (_activeAppItem != null && _activeAppItem.AppScreen != null)
        {
            yield return FadeAndScaleCanvas(_activeAppItem.AppScreen, 1f, 0.8f, appScreenExitDuration, fadeOut: true);
            _activeAppItem.AppScreen.gameObject.SetActive(false);
        }

        CanvasGroup homeGroup = GetBottomBlockCanvasGroup();
        if (homeGroup != null && bottomBlockRoot != null)
        {
            bottomBlockRoot.localScale = _bottomBlockBaseScale * bottomBlockDimmedScale;
            homeGroup.alpha = 0f;
            homeGroup.gameObject.SetActive(true);
            yield return FadeHomeViewIn(homeGroup, bottomBlockRoot, transitionDuration);
        }

        SetStage(Stage.Home);
        _activeAppItem = null;
    }

    private void ToggleDetail(TopBlockItemConfig entry, int index)
    {
        if (entry.Detail == null)
        {
            return;
        }

        bool open = !(_detailOpen && _detailIndex == index);
        if (_detailRoutine != null)
        {
            StopCoroutine(_detailRoutine);
        }
        _detailRoutine = StartCoroutine(RunDetailToggle(entry.Detail, open, index));
    }

    private IEnumerator RunDetailToggle(CanvasGroup detail, bool open, int index)
    {
        _detailOpen = open;
        _detailIndex = open ? index : -1;

        float duration = Mathf.Max(0.01f, detailFadeDuration);
        float elapsed = 0f;

        // Prepare starting states
        detail.gameObject.SetActive(true);
        float startAlpha = detail.alpha;
        float targetAlpha = open ? 1f : 0f;
        Vector3 startScale = detail.transform.localScale;
        Vector3 targetScale = open ? Vector3.one : Vector3.one * 0.8f;

        // Fade out/in all widgets
        var itemConfigs = new List<(RectTransform rect, CanvasGroup group, Vector3 startScale, Vector3 targetScale, float startA, float targetA)>();
        for (int i = 0; i < topBlockItems.Count; i++)
        {
            var rect = GetItem(i);
            if (rect == null) continue;
            rect.gameObject.SetActive(true);

            CanvasGroup cg = EnsureCanvasGroup(rect.gameObject);
            float startItemAlpha = cg != null ? cg.alpha : 1f;
            float targetItemAlpha = open ? 0f : 1f;
            Vector3 startItemScale = rect.localScale;
            Vector3 targetItemScale = open ? Vector3.one * 0.8f : Vector3.one;
            itemConfigs.Add((rect, cg, startItemScale, targetItemScale, startItemAlpha, targetItemAlpha));
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            detail.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
            detail.transform.localScale = Vector3.Lerp(startScale, targetScale, eased);

            for (int i = 0; i < itemConfigs.Count; i++)
            {
                var cfg = itemConfigs[i];
                if (cfg.group != null)
                {
                    cfg.group.alpha = Mathf.Lerp(cfg.startA, cfg.targetA, eased);
                }
                cfg.rect.localScale = Vector3.Lerp(cfg.startScale, cfg.targetScale, eased);
            }
            yield return null;
        }

        detail.alpha = targetAlpha;
        detail.transform.localScale = targetScale;
        if (!open)
        {
            detail.gameObject.SetActive(false);
        }
        // Ensure final state for widgets after animation completes
        for (int i = 0; i < itemConfigs.Count; i++)
        {
            var cfg = itemConfigs[i];
            if (cfg.group != null)
            {
                // Keep hidden while detail is open, restore to 1 when closing
                cfg.group.alpha = open ? 0f : 1f;
            }
            cfg.rect.localScale = cfg.targetScale;
            // Keep widgets active; rely on alpha/scale for visibility during detail view
            cfg.rect.gameObject.SetActive(true);
        }
        if (!open && _hoveredIndex >= 0 && _hoveredIndex < topBlockItems.Count)
        {
            SetItemHoverState(_hoveredIndex, true);
        }
        _detailRoutine = null;
    }

    private void CloseDetailInstant()
    {
        if (_detailIndex < 0 || _detailIndex >= topBlockItems.Count)
        {
            _detailOpen = false;
            _detailIndex = -1;
            return;
        }

        var entry = GetEntry(_detailIndex);
        if (entry != null && entry.Detail != null)
        {
            entry.Detail.alpha = 0f;
            entry.Detail.transform.localScale = Vector3.one * 0.8f;
            entry.Detail.gameObject.SetActive(false);
        }

        for (int i = 0; i < topBlockItems.Count; i++)
        {
            if (i == _detailIndex) continue;
            var rect = GetItem(i);
            if (rect == null) continue;
            rect.gameObject.SetActive(true);
            rect.localScale = Vector3.one;
            CanvasGroup cg = GetHoverChild(rect);
            if (cg != null)
            {
                cg.alpha = i == _hoveredIndex ? 1f : 0f;
            }
            SetItemHoverState(i, i == _hoveredIndex);
        }

        _detailOpen = false;
        _detailIndex = -1;
    }

    private IEnumerator FadeCanvas(CanvasGroup group, float from, float to, float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        group.alpha = from;
        group.gameObject.SetActive(true);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            group.alpha = Mathf.Lerp(from, to, eased);
            yield return null;
        }
        group.alpha = to;
        if (Mathf.Approximately(to, 0f))
        {
            group.gameObject.SetActive(false);
        }
    }

    private IEnumerator FadeAndScaleCanvas(CanvasGroup group, float fromScale, float toScale, float duration, bool fadeOut = false, float? startAlphaOverride = null)
    {
        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        group.gameObject.SetActive(true);
        float startAlpha = startAlphaOverride.HasValue ? startAlphaOverride.Value : (fadeOut ? 1f : 0f);
        float targetAlpha = fadeOut ? 0f : 1f;
        group.alpha = startAlpha;
        group.transform.localScale = Vector3.one * fromScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutCubic(t);
            group.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
            group.transform.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, eased);
            yield return null;
        }

        group.alpha = targetAlpha;
        group.transform.localScale = Vector3.one * toScale;
        if (fadeOut)
        {
            group.gameObject.SetActive(false);
        }
    }

    private IEnumerator FadeWidgetView(bool show)
    {
        CanvasGroup widgetCg = GetWidgetCanvasGroup();
        if (widgetCg == null)
        {
            yield break;
        }
        float duration = Mathf.Max(0.01f, transitionDuration * 0.5f);
        float startAlpha = widgetCg.alpha;
        float targetAlpha = show ? 1f : 0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutCubic(t);
            widgetCg.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
            yield return null;
        }
        widgetCg.alpha = targetAlpha;
    }

    private void UpdateWidgetItemVisuals()
    {
        if (_detailOpen)
        {
            return;
        }
        if (topBlockItems.Count == 0 || topBlockRoot == null)
        {
            return;
        }
        Canvas canvas = topBlockRoot.GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
        if (canvasRect == null)
        {
            return;
        }
        float startY = widgetVisualStartY;
        float endY = widgetVisualEndY;
        for (int i = 0; i < topBlockItems.Count; i++)
        {
            RectTransform rect = GetItem(i);
            if (rect == null)
            {
                continue;
            }
            rect.GetWorldCorners(_corners);
            Vector3 centerWorld = (_corners[0] + _corners[2]) * 0.5f;
            Vector3 centerCanvas = canvasRect.InverseTransformPoint(centerWorld);
            float t = Mathf.Clamp01(Mathf.InverseLerp(startY, endY, centerCanvas.y));
            float scale = Mathf.Lerp(widgetMinScale, 1f, t);
            float alpha = Mathf.Lerp(0f, 1f, t);
            CanvasGroup cg = EnsureCanvasGroup(rect.gameObject);
            cg.alpha = alpha;
            rect.localScale = Vector3.one * scale;
        }
    }

    private void ToggleQuickAction(TopBlockItemConfig entry)
    {
        if (entry.QuickAction == null)
        {
            return;
        }
        if (_quickActionRoutine != null)
        {
            StopCoroutine(_quickActionRoutine);
        }
        bool show = !entry.QuickActionActive;
        _quickActionRoutine = StartCoroutine(RunQuickActionToggle(entry, show));
    }

    private IEnumerator RunQuickActionToggle(TopBlockItemConfig entry, bool show)
    {
        float duration = Mathf.Max(0.05f, detailFadeDuration);
        if (show)
        {
            entry.QuickAction.gameObject.SetActive(true);
        }
        float startAlpha = entry.QuickAction.alpha;
        float targetAlpha = show ? 1f : 0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            entry.QuickAction.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
            yield return null;
        }
        entry.QuickAction.alpha = targetAlpha;
        entry.QuickActionActive = show;
        if (!show)
        {
            entry.QuickAction.gameObject.SetActive(false);
        }
        _quickActionRoutine = null;
    }

    private IEnumerator FadeHomeViewIn(CanvasGroup group, RectTransform root, float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        Vector3 startScale = _bottomBlockBaseScale * bottomBlockDimmedScale;
        Vector3 targetScale = _bottomBlockBaseScale;
        float startAlpha = 0f;
        float targetAlpha = _bottomBlockBaseAlpha;
        group.alpha = startAlpha;
        root.localScale = startScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutCubic(t);
            group.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
            root.localScale = Vector3.Lerp(startScale, targetScale, eased);
            yield return null;
        }

        group.alpha = targetAlpha;
        root.localScale = targetScale;
    }

    private TopBlockItemConfig GetEntry(int index)
    {
        if (index < 0 || index >= topBlockItems.Count)
        {
            return null;
        }
        return topBlockItems[index];
    }

    [System.Serializable]
    public class TopBlockItemConfig
    {
        public RectTransform Item;
        public InteractionType Interaction = InteractionType.QuickAction;
        public CanvasGroup AppLogo;
        public CanvasGroup AppScreen;
        public GameObject AppPill;
        public GameObject DefaultText;
        public CanvasGroup Detail;
        public CanvasGroup QuickAction;
        [System.NonSerialized] public bool QuickActionActive;
        [System.NonSerialized] public bool AppLaunchedOnce;
    }
}
