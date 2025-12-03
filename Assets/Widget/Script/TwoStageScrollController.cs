using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using DG.Tweening;

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
        Expandable,
        NonSelectable
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
    [SerializeField] private UnityEvent<int> onHoverIndexChanged;
    [SerializeField] private UnityEvent<int> onItemClicked;
    [Header("Scroll Input")]
    [SerializeField] [Min(0.01f)] private float scrollPixelsPerStep = 100f;
    [SerializeField] private InputRouter inputRouter;

    [Header("Hover Effect")]
    [SerializeField] [Min(0.05f)] private float hoverFadeDuration = 0.15f;
    [Header("Input Behavior")]
    [SerializeField] [Min(0f)] private float backHoverGraceSeconds = 2f;
    [Header("App Stage Visuals")]
    [SerializeField] [Min(0f)] private float appLogoDisplaySeconds = 1f;
    [SerializeField] [Min(0f)] private float appScreenExitDuration = 0.3f;
    [Header("Widget Item Visuals")]
    [SerializeField] private float widgetVisualStartY = 240f;
    [SerializeField] private float widgetVisualEndY = 200f;
    [SerializeField] [Range(0f, 1f)] private float widgetMinScale = 0.5f;
    [SerializeField] private bool controlWidgetAlpha = true;
    [SerializeField] private bool controlWidgetScale = true;

    private Stage _currentStage = Stage.Home;
    private int _hoveredIndex = -1;
    private bool _hasCachedBlockDefaults;
    private Vector2 _topBlockBaseAnchoredPosition;
    private Vector3 _bottomBlockBaseScale = Vector3.one;
    private float _bottomBlockBaseAlpha = 1f;
    private float _blockVisualProgress;
    private float _lastBackTime = float.NegativeInfinity;
    private int _lastWidgetHoverIndex = -1;
    private TopBlockItemConfig _activeAppItem;
    private readonly Vector3[] _corners = new Vector3[4];
    private float _scrollAccumulator;
    private Tweener _blockTween;
    private Sequence _appSequence;

    // Cache defaults and prime visuals on startup.
    private void Awake()
    {
        CacheBlockDefaults();
        ApplyBlockVisualState(_currentStage, true);
        ApplyAllNormalStates();
        PrepareWidgetVisuals();
    }

    // Clamp inspector values and refresh visuals when edited in the editor.
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
        scrollPixelsPerStep = Mathf.Max(0.01f, scrollPixelsPerStep);
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

    // Reset cached data and register listeners when enabled.
    private void OnEnable()
    {
        CacheBlockDefaults();
        ApplyBlockVisualState(_currentStage, true);
        PrepareWidgetVisuals();
        RegisterRingInput();
    }

    // Clean up coroutines and listeners when disabled.
    private void OnDisable()
    {
        UnregisterRingInput();
        _blockTween?.Kill();
        _blockTween = null;
        _appSequence?.Kill();
        _appSequence = null;
    }

    // Poll frame-based inputs and update per-frame visuals.
    private void Update()
    {
        HandleKeyboardInput();
        HandleScrollInput();
        UpdateWidgetItemVisuals();
    }

    // Handle keyboard A/D navigation and space back actions.
    private void HandleKeyboardInput()
    {
        bool aPressed = Input.GetKeyDown(KeyCode.A);
        bool dPressed = Input.GetKeyDown(KeyCode.D);
        bool backPressed = Input.GetKeyDown(KeyCode.Space);

        if (backPressed)
        {
            HandleBackAction();
            return;
        }

        if (aPressed)
        {
            HandleNavigationStep(-1);
        }
        else if (dPressed)
        {
            HandleNavigationStep(1);
        }
    }

    // Shared navigation logic for directional input (left/up = -1, right/down = 1).
    private void HandleNavigationStep(int direction)
    {
        if (direction == 0)
        {
            return;
        }

        if (direction < 0)
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
        else
        {
            if (_currentStage == Stage.Widget)
            {
                int nextIndex = FindNextSelectable(_hoveredIndex, 1);
                if (nextIndex < 0)
                {
                    SetStage(Stage.Home);
                }
                else
                {
                    SetHoveredIndex(nextIndex, true);
                }
            }
        }
    }

    // Convert mouse scroll wheel movement into navigation steps.
    private void HandleScrollInput()
    {
        float scrollDelta = Input.mouseScrollDelta.y;
        ApplyScrollDelta(scrollDelta);
    }

    // Consume ring input: taps map to click/back and delta maps to scrolling.
    private void HandleRingInput(RingGestureSchema input)
    {
        if (input == null || input.packet == null)
        {
            return;
        }

        var packet = input.packet;

        if (packet.ringGestureLocal == HaeanGesture.DOUBLE_TAP)
        {
            HandleBackAction();
            return;
        }

        if (packet.ringGestureLocal == HaeanGesture.TAP)
        {
            PerformHoveredClick();
        }

        float scrollDelta = packet.deltaVector.y;
        ApplyScrollDelta(scrollDelta);
    }

    // Accumulate scroll distance and trigger navigation for each threshold passed.
    private void ApplyScrollDelta(float scrollDelta)
    {
        if (Mathf.Approximately(scrollDelta, 0f))
        {
            return;
        }

        float stepSize = Mathf.Max(0.01f, scrollPixelsPerStep);
        _scrollAccumulator += scrollDelta;
        int steps = Mathf.FloorToInt(Mathf.Abs(_scrollAccumulator) / stepSize);
        if (steps <= 0)
        {
            return;
        }

        int navigationDirection = _scrollAccumulator > 0f ? -1 : 1; // up scroll maps to "A", down scroll maps to "D"
        for (int i = 0; i < steps; i++)
        {
            HandleNavigationStep(navigationDirection);
        }

        float consumed = steps * stepSize * Mathf.Sign(_scrollAccumulator);
        _scrollAccumulator -= consumed;
    }

    // Centralized back behavior used by keyboard and ring double-tap.
    private void HandleBackAction()
    {
        if (_currentStage == Stage.Widget)
        {
            _lastWidgetHoverIndex = _hoveredIndex;
            _lastBackTime = Time.unscaledTime;
        }
        else if (_currentStage == Stage.App)
        {
            _lastWidgetHoverIndex = _hoveredIndex;
            _lastBackTime = Time.unscaledTime;
            StartExitAppStage();
            return;
        }

        SetStage(Stage.Home);
    }

    // Perform a click action on the currently hovered widget item.
    private void PerformHoveredClick(PointerEventData eventData = null)
    {
        if (_currentStage != Stage.Widget || _hoveredIndex < 0 || _hoveredIndex >= topBlockItems.Count)
        {
            return;
        }
        if (!IsSelectable(_hoveredIndex))
        {
            return;
        }

        onItemClicked?.Invoke(_hoveredIndex);

        HandleItemSelection(_hoveredIndex);
    }

    // Register to ring input events when available.
    private void RegisterRingInput()
    {
        if (inputRouter != null)
        {
            inputRouter.OnRingInputRecieved += HandleRingInput;
        }
    }

    // Unregister ring input events to avoid leaks.
    private void UnregisterRingInput()
    {
        if (inputRouter != null)
        {
            inputRouter.OnRingInputRecieved -= HandleRingInput;
        }
    }

    // Stop any running app sequence tweens.
    private void KillAppSequence()
    {
        if (_appSequence != null)
        {
            _appSequence.Kill();
            _appSequence = null;
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
            CanvasGroup widgetCg = GetWidgetCanvasGroup();
            if (widgetCg != null)
            {
                widgetCg.alpha = 1f;
            }
        }
        else
        {
            ClearHover();
        }

        ApplyBlockVisualState(stage, false);
    }

    // Update visuals for the home/widget transition, optionally animated.
    private void ApplyBlockVisualState(Stage stage, bool instant)
    {
        if (topBlockRoot == null && bottomBlockRoot == null)
        {
            _blockVisualProgress = StageToProgress(stage);
            return;
        }

        CacheBlockDefaults();
        _blockTween?.Kill();
        float targetProgress = StageToProgress(stage);
        if (instant || !Application.isPlaying || Mathf.Approximately(_blockVisualProgress, targetProgress))
        {
            SetBlockVisualState(targetProgress);
            return;
        }

        float duration = Mathf.Max(0.05f, transitionDuration);
        _blockTween = DOTween.To(() => _blockVisualProgress, SetBlockVisualState, targetProgress, duration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    // Apply visual transform/alpha values based on progress.
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

    // Convert stage enum to normalized progress value.
    private float StageToProgress(Stage stage)
    {
        return stage == Stage.Widget ? 1f : 0f;
    }

    // Store baseline transform/alpha data for later interpolation.
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

    // Reset cache flag so defaults are re-read.
    private void ResetBlockDefaultsCache()
    {
        _hasCachedBlockDefaults = false;
    }

    // Get or add canvas group for bottom block.
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

    // Get or add canvas group for the widget root.
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

    // Initialize per-item visuals and alpha defaults.
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

            if (entry.AppLogo != null)
            {
                entry.AppLogo.alpha = 0f;
            }
            if (entry.AppScreen != null)
            {
                entry.AppScreen.alpha = 0f;
            }
            if (entry.AppPill != null)
            {
                var pillGroup = EnsureCanvasGroup(entry.AppPill);
                if (pillGroup != null)
                {
                    pillGroup.alpha = 1f;
                }
            }
            entry.AppLaunchedOnce = false;
        }
    }

    // Ensure a canvas group exists on the provided object.
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

    // Reset all items to non-hover state.
    private void ApplyAllNormalStates()
    {
        for (int i = 0; i < topBlockItems.Count; i++)
        {
            SetItemHoverState(i, false);
        }
    }

    // Select the last selectable item when entering widget stage.
    private void EnsureLastItemHovered()
    {
        if (topBlockItems.Count == 0)
        {
            ClearHover();
            return;
        }
        for (int i = topBlockItems.Count - 1; i >= 0; i--)
        {
            if (!IsSelectable(i))
            {
                continue;
            }
            if (_hoveredIndex != i)
            {
                SetHoveredIndex(i, true);
            }
            return;
        }

        ClearHover();
    }

    // Move hover to the previous selectable item.
    private void HoverPreviousItem()
    {
        int startIndex = _hoveredIndex >= 0 ? _hoveredIndex : topBlockItems.Count;
        int targetIndex = FindNextSelectable(startIndex, -1);
        if (targetIndex < 0)
        {
            return;
        }
        SetHoveredIndex(targetIndex, true);
    }

    // Move hover to the next selectable item.
    private void HoverNextItem()
    {
        int startIndex = _hoveredIndex;
        int targetIndex = FindNextSelectable(startIndex, 1);
        if (targetIndex < 0)
        {
            return;
        }
        SetHoveredIndex(targetIndex, true);
    }

    // Remove hover selection and notify listeners.
    private void ClearHover()
    {
        RectTransform previousRect = GetItem(_hoveredIndex);
        if (previousRect != null)
        {
            SetItemHoverState(_hoveredIndex, false);
        }

        _hoveredIndex = -1;
        onHoverIndexChanged?.Invoke(-1);
    }

    // Update hovered index with bounds/selectable checks.
    private void SetHoveredIndex(int index, bool force = false)
    {
        if (topBlockItems.Count == 0)
        {
            ClearHover();
            return;
        }

        index = GetSelectableOrFallbackIndex(index);
        if (index < 0)
        {
            ClearHover();
            return;
        }

        if (!force && index == _hoveredIndex)
        {
            return;
        }

        SetItemHoverState(_hoveredIndex, false);
        _hoveredIndex = index;
        SetItemHoverState(_hoveredIndex, true);

        onHoverIndexChanged?.Invoke(_hoveredIndex);
        if (_hoveredIndex >= 0)
        {
            _lastWidgetHoverIndex = _hoveredIndex;
        }
    }

    // IPointerClickHandler entry: route UI clicks to hovered item handling.
    public void OnPointerClick(PointerEventData eventData)
    {
        PerformHoveredClick(eventData);
    }

    // Set hover visuals for a given item.
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
            hoverChild.DOKill();
            hoverChild.DOFade(targetAlpha, Application.isPlaying ? hoverFadeDuration : 0f).SetEase(Ease.InOutSine);
        }
    }

    // Find first canvas group child to drive hover alpha.
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

    private RectTransform GetItem(int logicalIndex)
    {
        if (logicalIndex < 0 || logicalIndex >= topBlockItems.Count)
        {
            return null;
        }

        return topBlockItems[logicalIndex]?.Item;
    }

    // Cubic easing helper for smoother animations.
    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inv = t - 1f;
        return 1f + inv * inv * inv;
    }

    // Dispatch the interaction for the selected item.
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
                ToggleStageController(entry);
                break;
            case InteractionType.QuickAction:
                ToggleStageController(entry);
                break;
            case InteractionType.NonSelectable:
                return;
        }
    }

    // Start an app launch tween sequence.
    private void BeginAppLaunch(TopBlockItemConfig entry)
    {
        KillAppSequence();
        _activeAppItem = entry;
        SetStage(Stage.App);

        if (entry.DefaultText != null)
        {
            entry.DefaultText.SetActive(false);
        }

        if (entry.AppPill != null)
        {
            entry.AppPill.SetActive(true);
        }

        var seq = DOTween.Sequence().SetUpdate(false);

        if (entry.AppLogo != null && !entry.AppLaunchedOnce)
        {
            entry.AppLogo.gameObject.SetActive(true);
            entry.AppLogo.alpha = 0f;
            seq.Append(entry.AppLogo.DOFade(1f, appScreenExitDuration).SetEase(Ease.OutCubic));
            if (appLogoDisplaySeconds > 0f)
            {
                seq.AppendInterval(appLogoDisplaySeconds);
            }
            seq.Append(entry.AppLogo.DOFade(0f, appScreenExitDuration)
                .SetEase(Ease.OutCubic)
                .OnComplete(() => entry.AppLogo.gameObject.SetActive(false)));
        }

        if (entry.AppScreen != null)
        {
            entry.AppScreen.gameObject.SetActive(true);
            float fromScale = 0.8f;
            float fromAlpha = 0f;
            entry.AppScreen.alpha = fromAlpha;
            entry.AppScreen.transform.localScale = Vector3.one * fromScale;
            seq.Append(entry.AppScreen.DOFade(1f, appScreenExitDuration).SetEase(Ease.OutCubic));
            seq.Join(entry.AppScreen.transform.DOScale(1f, appScreenExitDuration).SetEase(Ease.OutCubic));
        }

        seq.OnComplete(() =>
        {
            entry.AppLaunchedOnce = true;
            _appSequence = null;
        });

        _appSequence = seq;
    }

    // Animate returning from app stage back to home/widget.
    private void StartExitAppStage()
    {
        KillAppSequence();
        var seq = DOTween.Sequence().SetUpdate(false);

        if (_activeAppItem != null && _activeAppItem.AppLogo != null)
        {
            _activeAppItem.AppLogo.alpha = 0f;
            _activeAppItem.AppLogo.gameObject.SetActive(false);
        }

        if (_activeAppItem != null && _activeAppItem.AppScreen != null)
        {
            var screen = _activeAppItem.AppScreen;
            screen.gameObject.SetActive(true);
            seq.Append(screen.DOFade(0f, appScreenExitDuration).SetEase(Ease.OutCubic));
            seq.Join(screen.transform.DOScale(0.8f, appScreenExitDuration).SetEase(Ease.OutCubic));
            seq.AppendCallback(() => screen.gameObject.SetActive(false));
        }

        CanvasGroup homeGroup = GetBottomBlockCanvasGroup();
        if (homeGroup != null && bottomBlockRoot != null)
        {
            bottomBlockRoot.localScale = _bottomBlockBaseScale * bottomBlockDimmedScale;
            homeGroup.alpha = 0f;
            homeGroup.gameObject.SetActive(true);
            seq.Append(homeGroup.DOFade(_bottomBlockBaseAlpha, transitionDuration).SetEase(Ease.OutCubic));
            seq.Join(bottomBlockRoot.DOScale(_bottomBlockBaseScale, transitionDuration).SetEase(Ease.OutCubic));
        }

        seq.OnComplete(() =>
        {
            SetStage(Stage.Home);
            _activeAppItem = null;
            _appSequence = null;
        });

        _appSequence = seq;
    }

    private void UpdateWidgetItemVisuals()
    {
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
        bool allowAlpha = controlWidgetAlpha && !IsExpandableHovered();
        bool allowScale = controlWidgetScale;
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
            CanvasGroup cg = EnsureCanvasGroup(rect.gameObject);
            if (allowAlpha && cg != null)
            {
                float alpha = Mathf.Lerp(0f, 1f, t);
                cg.alpha = alpha;
            }
            if (allowScale)
            {
                float scale = Mathf.Lerp(widgetMinScale, 1f, t);
                rect.localScale = Vector3.one * scale;
            }
        }
    }

    // Toggle a StageController between its first two stages.
    private void ToggleStageController(TopBlockItemConfig entry)
    {
        var controller = entry.StageController;
        if (controller == null)
        {
            return;
        }
        if (controller.stages == null || controller.stages.Count < 2)
        {
            return;
        }

        int currentIndex = controller.CurrentIndex;
        int target = currentIndex == 0 ? 1 : 0;
        if (target >= controller.stages.Count)
        {
            target = 0;
        }
        controller.RequestStageIndex(target);
    }

    // Safely fetch an item config by index.
    private TopBlockItemConfig GetEntry(int index)
    {
        if (index < 0 || index >= topBlockItems.Count)
        {
            return null;
        }
        return topBlockItems[index];
    }

    // Determine if the hovered item is expandable.
    private bool IsExpandableHovered()
    {
        var entry = GetEntry(_hoveredIndex);
        return entry != null && entry.Interaction == InteractionType.Expandable;
    }

    // Check if an index corresponds to a selectable item.
    private bool IsSelectable(int index)
    {
        if (index < 0 || index >= topBlockItems.Count)
        {
            return false;
        }

        TopBlockItemConfig entry = topBlockItems[index];
        return entry != null &&
               entry.Item != null &&
               entry.Interaction != InteractionType.NonSelectable;
    }

    // Find the next selectable index in a direction from a start.
    private int FindNextSelectable(int startIndex, int direction)
    {
        if (direction == 0 || topBlockItems.Count == 0)
        {
            return -1;
        }

        int i = startIndex + direction;
        while (i >= 0 && i < topBlockItems.Count)
        {
            if (IsSelectable(i))
            {
                return i;
            }
            i += direction;
        }

        return -1;
    }

    // Resolve to the nearest selectable index or -1 if none.
    private int GetSelectableOrFallbackIndex(int index)
    {
        if (IsSelectable(index))
        {
            return index;
        }

        int forward = FindNextSelectable(index, 1);
        int backward = FindNextSelectable(index, -1);

        if (forward < 0 && backward < 0)
        {
            return -1;
        }

        if (forward < 0)
        {
            return backward;
        }

        if (backward < 0)
        {
            return forward;
        }

        int forwardDistance = Mathf.Abs(forward - index);
        int backwardDistance = Mathf.Abs(index - backward);
        return forwardDistance <= backwardDistance ? forward : backward;
    }

    [System.Serializable]
    public class TopBlockItemConfig
    {
        // Data backing a single widget item and its visuals/interactions.
        public RectTransform Item;
        public InteractionType Interaction = InteractionType.QuickAction;
        public StageController StageController;
        public CanvasGroup AppLogo;
        public CanvasGroup AppScreen;
        public GameObject AppPill;
        public GameObject DefaultText;
        [System.NonSerialized] public bool AppLaunchedOnce;
    }
}
