using System;
using System.Collections;
using System.Collections.Generic;
using System.Timers;
using Lynx;
using Unity.VisualScripting;
using UnityEngine;
using XRUX.Core;
public class InputRouter : MonoExtended {
    public event Action<RingGestureSchema> OnRingInputRecieved;
    public event Action<TrackpadGestureSchema> OnTrackpadInputRecieved;
    Vector2 trackpadDelta;
    public Vector2 LastDeltaVector { get; private set; }
    [Header("Input")]
    public LynxRingMessageHandler ringInput;
    [SerializeField] private bool trackpadInput = false;
    [CloudVar("InputSettings", "User Ring Handedness (0 : Left, 1 : Right)")]
    public int userHandedness = 1;
    [CloudVar("InputSettings", "Double Tap Wait Time(s)")]
    public float _doubleTapWaitTime = 0.3f;

    private void Awake() {
        SetupEventListeners();
    }
    private void SetupEventListeners() {
        if (ringInput != null) {
            ringInput.OnRingInput += OnRingInput;
        }
        this.RegisterCloudVars();
        this.RegisterRPCs();
    }
    private void OnDestroy() {
        try {
            ringInput.OnRingInput -= OnRingInput;
            this.UnregisterCloudVars();
            this.UnregisterRPCs();
        } catch (System.Exception) { /* Ignore */ }
    }
    bool ringTouch = false;
    float ringTouchStartTime;
    float ringTouchDuration;
    private void OnRingInput(RingGestureSchema input) {
        if (input.packet.touch == 1 && !ringTouch) {
            ringTouch = true;
            ringTouchStartTime = Time.realtimeSinceStartup;
        } else if (input.packet.touch == 0 && ringTouch) {
            ringTouch = false;
            ringTouchDuration = Time.realtimeSinceStartup - ringTouchStartTime;
        }
        if (input.packet.click > 0) {
                ringTouchDuration = float.MaxValue;
                OnTap();
        } else {
            if (userHandedness == 0) { // Left Handed
                input.packet.deltaVector = new Vector2(-input.packet.deltaVector.x, input.packet.deltaVector.y);
            } else { // Right Handed
                input.packet.deltaVector = new Vector2(input.packet.deltaVector.x, -input.packet.deltaVector.y);
            }
            LastDeltaVector = input.packet.deltaVector;
            OnRingInputRecieved?.Invoke(input);
        }
    }
    void Update() {
        if (trackpadInput) {
            GetTrackpadInput();
        }
        if (trackpadDelta.sqrMagnitude == 0f) {
            LastDeltaVector = Vector2.Lerp(LastDeltaVector, Vector2.zero, Time.deltaTime * 10f);
        }
    }
    void GetTrackpadInput() {
        trackpadDelta = ReadGlobalScroll();
        if (trackpadDelta.sqrMagnitude > 0f || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) {
            LastDeltaVector = trackpadDelta;
            TrackpadGestureSchema trackpadInput = new TrackpadGestureSchema();
            trackpadInput.deltaVector = trackpadDelta;
            if (Input.GetMouseButtonDown(0)) {
                trackpadInput.haeanGesture = HaeanGesture.TAP;
            } else if (Input.GetMouseButtonDown(1)) {
                trackpadInput.haeanGesture = HaeanGesture.DOUBLE_TAP;
            } else {
                trackpadInput.haeanGesture = GetGestureLocal(trackpadDelta);
            }
            OnTrackpadInputRecieved?.Invoke(trackpadInput);
        }
    }
    private Vector2 ReadGlobalScroll() {
        float y = 0f;
        float x = 0f;
#if ENABLE_INPUT_SYSTEM && UNITY_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            y += UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y;
            x += UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().x;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !UNITY_INPUT_SYSTEM
        y += Input.mouseScrollDelta.y;
        x += Input.mouseScrollDelta.x;
#endif
        // Invert scroll on macOS to counteract "Natural Scrolling" for both axes.
        if (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor) {
            y *= -1;
        }
        return new Vector2(x, y);
    }
    HaeanGesture GetGestureLocal(Vector2 deltaVector) {
        if (deltaVector.sqrMagnitude == 0) {
            return HaeanGesture.NONE;
        }
        float angle = Mathf.Atan2(deltaVector.y, deltaVector.x) * Mathf.Rad2Deg;
        if (angle > -45 && angle <= 45) {
            return HaeanGesture.SWIPE_RIGHT;
        } else if (angle > 45 && angle <= 135) {
            return HaeanGesture.SWIPE_DOWN;
        } else if (angle > 135 || angle <= -135) {
            return HaeanGesture.SWIPE_LEFT;
        } else {
            return HaeanGesture.SWIPE_UP; // Between -45 and -135
        }
    }
    #region Single vs double click handling
    [Header("Click filter")]
    private bool _singleTap = false;
    private Coroutine _waitForDouble = null;
    public void OnTap() {
        if (!_singleTap) {
                _singleTap = true;
                if (_waitForDouble == null) {
                    _waitForDouble = StartCoroutine(WaitForDouble());
                }
            } else {
                ResetTapListenerVars();
                OnGestureDetected(HaeanGesture.DOUBLE_TAP);
            }
    }
    public void OnGestureDetected(HaeanGesture gesture) {
        RingGestureSchema input = new RingGestureSchema();
        input.packet = new RingGestureSchema.RingGesturePacket();
        input.packet.ringGestureLocal = gesture;
        OnRingInputRecieved?.Invoke(input);
        Debug.Log("Executed function");
    }
    IEnumerator WaitForDouble() {
        float timeStart = Time.realtimeSinceStartup;
        float currentTime = Time.realtimeSinceStartup;
        while (currentTime < timeStart + _doubleTapWaitTime) {
            yield return new WaitForEndOfFrame();
            currentTime = Time.realtimeSinceStartup;
        }
        OnGestureDetected(HaeanGesture.TAP);
        ResetTapListenerVars();
        yield return null;
    }
    void ResetTapListenerVars() {
        _singleTap = false;
        StopCoroutine(_waitForDouble);
        _waitForDouble = null;
    }
    // [CloudVarButton("InputSettings", "Disable Double Tap")]
    // public void DisableDoubleTap() {
    //     disableDoubleTap = true;
    // }
    // [CloudVarButton("InputSettings", "Enable Double Tap")]
    // public void EnableDoubleTap() {
    //     disableDoubleTap = false;
    // }
    #endregion Single vs double click handling
}
[Serializable]
public class TrackpadGestureSchema {
    public Vector2 deltaVector;
    public HaeanGesture haeanGesture;
}
[Serializable]
public enum HaeanGesture {
    NONE,
    SWIPE_UP,
    SWIPE_DOWN,
    SWIPE_LEFT,
    SWIPE_RIGHT,
    TAP,
    DOUBLE_TAP
}


