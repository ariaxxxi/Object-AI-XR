using UnityEngine;

// Here are all the native android events you can get from the Vuzix Blade.  
public enum VINPUT_EVENT {
    TAP_1FINGER,
    TAP_2FINGER,
    SWIPE_FORWARD_1FINGER,
    SWIPE_BACKWARD_1FINGER,
    SWIPE_UP_1FINGER,
    SWIPE_DOWN_1FINGER,
    SWIPE_FORWARD_2FINGER,
    SWIPE_BACKWARD_2FINGER,
    HOLD_1FINGER,
}
// Here's some sample code on how to add and handle the event: 
// Be sure to remove the event listener when disposing. 
/*
    ...
        void Start () {
            ...
            VInput.onVInputEvent += _onVInputEvent;
        }
    ...
        void Update() {
            VInput.Update(Time.unscaledDeltaTime);
        }
    ...
    
    protected virtual void _onVInputEvent( VINPUT_EVENT pEvent ){
        switch( pEvent){
            case VINPUT_EVENT.TAP_1FINGER:
                // basic input "click"
                break;
            case VINPUT_EVENT.TAP_2FINGER:
                // back or cancel. 
                break;
            case VINPUT_EVENT.SWIPE_FORWARD_1FINGER:
                // scroll right through menu. 
                break;
            case VINPUT_EVENT.SWIPE_BACKWARD_1FINGER:
                // scroll left through menu. 
                break;
            case VINPUT_EVENT.SWIPE_UP_1FINGER:
                break;
            case VINPUT_EVENT.SWIPE_DOWN_1FINGER:
                break;
            case VINPUT_EVENT.SWIPE_FORWARD_2FINGER:
                break;
            case VINPUT_EVENT.SWIPE_BACKWARD_2FINGER:
                break;
            case VINPUT_EVENT.HOLD_1FINGER:
                // open a menu! 
                break;
        }
    }
*/
public static class VInput {
    /// <summary>
    /// Delegate for handling Vuzix input events
    /// </summary>
    /// <param name="pEvent">The input event that occurred</param>
    public delegate void onVInput(VINPUT_EVENT pEvent);
    
    /// <summary>
    /// Event triggered when any Vuzix input occurs
    /// </summary>
    public static event onVInput onVInputEvent;
    
    /// <summary>
    /// Dispatches a Vuzix input event to all registered listeners
    /// </summary>
    /// <param name="pEvent">The input event to dispatch</param>
    public static void dispatchVInputEvent(VINPUT_EVENT pEvent) { if (onVInputEvent != null) onVInputEvent(pEvent); }
    private static bool _tap1F = false;
    private static bool _tap2F = false;
    private static bool _swipeForward1F = false;
    private static bool _swipeBackward1F = false;
    private static bool _swipeUp1F = false;
    private static bool _swipeDown1F = false;
    private static bool _swipeForward2F = false;
    private static bool _swipeBackward2F = false;
    private static bool _hold1F = false;
    // optional static accessors if you don't want to use an event. 
    public static bool tap1F { get { return _tap1F; } }
    public static bool tap2F { get { return _tap2F; } }
    public static bool swipeForward1F { get { return _swipeForward1F; } }
    public static bool swipeBackward1F { get { return _swipeBackward1F; } }
    public static bool swipeUp1F { get { return _swipeUp1F; } }
    public static bool swipeDown1F { get { return _swipeDown1F; } }
    public static bool swipeForward2F { get { return _swipeForward2F; } }
    public static bool swipeBackward2F { get { return _swipeBackward2F; } }
    public static bool hold1F { get { return _hold1F; } }
    /// <summary>
    /// Updates the input system by checking for Vuzix touchpad interactions and dispatching appropriate events.
    /// Handles both Unity Editor simulation (keyboard/mouse) and actual Vuzix device input mapping
    /// </summary>
    /// <param name="dt">Delta time (currently unused but follows Unity convention)</param>
    public static void Update(float dt) {
#if UNITY_EDITOR
        // This is a simple input setup to run the game in the unity editor. 
        // WSAD for swipes, QE for two-finger horizontal swipes, 
        // Left/Right click for tap, and 2-finger tap, and ESC for the 1-second hold. 
        _tap1F = Input.GetMouseButtonDown(0);
        _tap2F = Input.GetMouseButtonDown(1);
        _swipeBackward1F = Input.GetKeyDown(KeyCode.A);
        _swipeForward1F = Input.GetKeyDown(KeyCode.D);
        _swipeUp1F = Input.GetKeyDown(KeyCode.W);
        _swipeDown1F = Input.GetKeyDown(KeyCode.S);
        _swipeBackward2F = Input.GetKeyDown(KeyCode.Q);
        _swipeForward2F = Input.GetKeyDown(KeyCode.E);
        _hold1F = Input.GetKeyDown(KeyCode.Escape);
#else
            // The vuzix touchpad is mapped to more or less a virtual "trackball". 
            // Many of the input events from the touchpad are mapped to native android events. 
            // Here's a quick'n' dirty event system to handle all the different inputs. 
            _tap1F = Input.inputString == ("\n");
            _tap2F = Input.GetKeyDown(KeyCode.Escape);
            _swipeBackward1F = Input.GetAxisRaw("Horizontal") < 0;
            _swipeForward1F = Input.GetAxisRaw("Horizontal") > 0;
            _swipeUp1F = Input.GetAxisRaw("Vertical") > 0;
            _swipeDown1F = Input.GetAxisRaw("Vertical") < 0;
            _swipeBackward2F = Input.GetKeyDown(KeyCode.Backspace);
            _swipeForward2F = Input.GetKeyDown(KeyCode.Delete);
            _hold1F = Input.GetKeyDown(KeyCode.Menu);
#endif
        // dispatch events for anything that happened. 
        if (_tap1F) { dispatchVInputEvent(VINPUT_EVENT.TAP_1FINGER); }
        if (_tap2F) { dispatchVInputEvent(VINPUT_EVENT.TAP_2FINGER); }
        if (_swipeBackward1F) { dispatchVInputEvent(VINPUT_EVENT.SWIPE_BACKWARD_1FINGER); }
        if (_swipeForward1F) { dispatchVInputEvent(VINPUT_EVENT.SWIPE_FORWARD_1FINGER); }
        if (_swipeUp1F) { dispatchVInputEvent(VINPUT_EVENT.SWIPE_UP_1FINGER); }
        if (_swipeDown1F) { dispatchVInputEvent(VINPUT_EVENT.SWIPE_DOWN_1FINGER); }
        if (_swipeBackward2F) { dispatchVInputEvent(VINPUT_EVENT.SWIPE_BACKWARD_2FINGER); }
        if (_swipeForward2F) { dispatchVInputEvent(VINPUT_EVENT.SWIPE_FORWARD_2FINGER); }
        if (_hold1F) { dispatchVInputEvent(VINPUT_EVENT.HOLD_1FINGER); }
    }
}
