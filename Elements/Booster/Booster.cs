using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class Booster : UdonSharpBehaviour
{
    //[UdonSynced(UdonSyncMode.Linear)]
    public float time = 0f;
    //[UdonSynced]
    public int state = -1;

    [Header("Climbing Properties")]
    [SerializeField] private SimpleClimbingSystem _climbingSystem;
    [SerializeField] private bool _forceDropOnStart = false;
    [SerializeField] private bool _forceDropOnStop = true;

    [Header("Boost Properties")]

    public BoosterLine line;
    public float startDelay = 1f; // s
    public float stopDelay = 1f; // s
    public float boostSpeed = 10f; // m/s
    public float rewindSpeed = 5f; // m/s

    [Space]
    [SerializeField] private LineRenderer _lineRenderer;
    [SerializeField] private Vector3 _lineOffset = Vector3.zero;
    [Space]

    private float _boostTimeSpeed;
    private float _rewindTimeSpeed;
    private float _cooldown = 0f;

    [Header("Feedback Properties")]
    [SerializeField] private ContinuousFeedback _boostingContinuous;
    [SerializeField] private ImpulseFeedback _startImpulse;
    [SerializeField] private ImpulseFeedback _stopImpulse;

    [Header("Events")]
    [SerializeField] private UdonBehaviour _eventTarget;
    [SerializeField] private string _startedEvent = "StartedBoost";
    [SerializeField] private string _stoppedEvent = "StoppedBoost";

    private HandType _lastInputHand;

    private void Start() {
        // Reset values
        time = 0;
        state = -1;
        PlaceOnLine();
        // Speed calculations
        if (line) {
            _boostTimeSpeed = boostSpeed / line.length;
            _rewindTimeSpeed = rewindSpeed / line.length;
        }
    }

    private void Update() {
        if (state >= 0) {
            //if (Networking.IsOwner(gameObject)) 
                ProcessState();
            PlaceOnLine();
        }
    }

    public void PlaceOnLine() {
        if (line) {
            line.Place(transform, time);
            if (_lineRenderer && _lineRenderer.positionCount >= 2) {
                _lineRenderer.SetPosition(0, transform.position + transform.rotation * _lineOffset);
                _lineRenderer.SetPosition(1, line.targetPoint);
            }
        }
    }

    #region State Handling
    // These methods handle the different states
    // that the booster will go though while it's active
    private void ProcessState() {
        switch (state)
        {
            // Cooldown states
            case 0:
            case 2:
            _cooldown -= Time.deltaTime;
            if (_cooldown <= 0f) {
                NextState();
            }
            break;

            // Boost state
            case 1:
            time += _boostTimeSpeed * Time.deltaTime;
            if(time >= 1f) {
                StopBoost();
            }
            break;

            // Rewind state
            case 3:
            time -= _rewindTimeSpeed * Time.deltaTime;
            if(time <= 0f) {
                SetState(-1);
            }
            break;
        }
    }

    private void SetState(int new_state) {
        switch (new_state)
        {
            case 0: // Start the boosting sequence
            _cooldown = startDelay;
            if(_startImpulse) _startImpulse.StartFeedback();
            break;

            case 1: // Start boosting
            if (_boostingContinuous) _boostingContinuous.StartFeedback();
            break;

            case 2: // Stop the boosting sequence
            _cooldown = stopDelay;
            if (_boostingContinuous) _boostingContinuous.StopFeedback();
            if(_stopImpulse) _stopImpulse.StartFeedback();
            break;

            case 3:
            break;
        }
        state = new_state;
    }

    private void NextState() {
        if (state >= 3) SetState(-1);
        else SetState(state + 1);
    }
    #endregion // State Handling

    #region Events
    // Thse methods are called from other scripts

    // Networking behavior --------------------------------------
    // Interacting with the booster will send a networked event 
    // so that the booster starts for everyone. This will reset
    // the booster's state and time for everyone
    // \/
    // If the booster was already started (state > -1), the event 
    // will not be sent but the booster can still be grabbed
    // \/
    // The booster is entirely processed locally, time and state
    // are not synced. This means they should travel at the same
    // speed but might have a bit of delay.
    // This also means that you might get your own booster reset
    // if someone starts the booster after not receiving the event
    
    /*
    public override void Interact()
    {
        // Only send the event if the booster is not started
        if (state == -1) _SendStartEvent();
        if (_climbingSystem) {
            if (_climbingSystem.localPlayer.IsUserInVR())
                _climbingSystem.ForceGrab(transform, _lastInputHand, transform.rotation * _climbingOffset); // Players can interact with any hand in VR
            else _climbingSystem.ForceGrab(transform, HandType.LEFT, transform.rotation * _climbingOffset); // On desktop, it's just left click -> so left hand (even if right hand is the default)
        }
    }

    public override void InputUse(bool value, UdonInputEventArgs args)
    {
        if (value) _lastInputHand = args.handType;
    }*/

    public void ClimbingGrabbed() {
        if (state == -1) _SendStartEvent();
    }

    public void _SendStartEvent() {
        // Take ownership of the object
        // Networking.SetOwner(Networking.LocalPlayer, gameObject); // introduces unneeded latency
        // Send networked event to display feedbacks for everyone
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "StartBoost");
        Debug.Log($"Booster {gameObject.name} started");
    }

    public void StartBoost() {
        SetState(0);
        time = 0;
        
        if (_forceDropOnStart && _climbingSystem) _climbingSystem.DropGrabbed(transform);
        if (_eventTarget) _eventTarget.SendCustomEvent(_startedEvent);
    }

    public void StopBoost() {
        SetState(2);

        if (_forceDropOnStop && _climbingSystem) _climbingSystem.DropGrabbed(transform);
        if (_eventTarget) _eventTarget.SendCustomEvent(_stoppedEvent);
    }
    #endregion // Events
}
