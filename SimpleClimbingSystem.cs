
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SimpleClimbingSystem : UdonSharpBehaviour
{
    [SerializeField] private Transform HandTransform;
    [SerializeField] private LayerMask climableMask;

    [Space]
    [SerializeField] private bool walljumpEnabled = true;
    [SerializeField] private float walljumpStrength = 5f;

    [Header("VR Options")]
    [SerializeField] private bool useGrabButton = true;
    [SerializeField] private float handRadius = 0.1f;

    [Header("Desktop Options")]
    [SerializeField] private float handReach = 2f;

    [Header("Events")]
    [SerializeField] private bool _sendEventsToClimbedObjects = true;
    [SerializeField] private UdonBehaviour[] _eventTargets;
    [SerializeField] private string _grabbedEvent = "ClimbingGrabbed";
    [SerializeField] private string _droppedEvent = "ClimbingDropped";

    private bool _climbing = false;
    private HandType _climbingHand;
    private Vector3 _lastClimbedPosition;
    private Vector3 _lastClimbedVelocity;
    private Transform _lastClimbedTransform;
    private Collider[] grabSurfaces = new Collider[1];
    
    // Cache local player and VR status
    [HideInInspector] public VRCPlayerApi localPlayer;
    [HideInInspector] public bool inVR;
    
    private void Start()
    {
        localPlayer = Networking.LocalPlayer;
        if (localPlayer != null) inVR = localPlayer.IsUserInVR();
        if (HandTransform) HandTransform.localScale = Vector3.one * handRadius;
    }

    public override void PostLateUpdate()
    {
        if (_climbing) {
            UpdateGrab(_climbingHand);
        }
    }

#region Inputs
    // Jump Input Handling
    // In VR and on Desktop, this event is sent when the jump button is used.
    //
    // This event is only used when climbing if walljumping is enabled.
    public override void InputJump(bool value, UdonInputEventArgs args)
    {
        if (value && walljumpEnabled && _climbing) {
            // Let go with jump force
            Vector3 force = Vector3.up * walljumpStrength;
            _lastClimbedVelocity += force; // Add jump force to last velocity
            
            Drop();
        }
    }

    
    // Use Input Handling
    // In VR, this event is sent when the trigger buttons are used.
    // On Desktop, this event is sent when the left click is used.
    //
    // This event is always used on Desktop to simulate the left hand, and is 
    // used in VR if the climbing system is set not to use the grip buttons.
    public override void InputUse(bool value, UdonInputEventArgs args)
    {
        if (inVR) {
            if (useGrabButton) return; // skip execution
            ProcessInput(value, args.handType);
        }
        else {
            // The Grab input is always Left Click on Desktop
            ProcessInput(value, HandType.LEFT);
        }
    }

    // Grab Input Handling
    // In VR, this event is sent when the grip button is used.
    // On desktop, this event is also sent when the left click is used.
    //
    // Since the left click on Desktop is already handled by the InputUse event, this event is 
    // only used in VR when the climbing system is set to use the grip buttons.
    public override void InputGrab(bool value, UdonInputEventArgs args)
    {
        if (inVR) {
            if (!useGrabButton) return; // Skip execution
            ProcessInput(value, args.handType);
        }
    }

    // Drop Input Handling
    // This event is only sent on desktop, when the left click is used.
    // 
    // This event is always used on Desktop to simulate the right hand.
    public override void InputDrop(bool value, UdonInputEventArgs args)
    {
        if (inVR) {
            Debug.LogError("Climbing system detected InputDrop event in VR. This is not handled by the climbing system");
            return; // not handled - skip execution
        }
        else {
            // The Drop input is always Left Click on PC
            ProcessInput(value, HandType.RIGHT);
        }
    }

    // Input Processing
    // If the climbing hand lets go, then it makes the player drop.
    // If a non-climbing hand grabs something, it makes the player start climbing.
    //
    // When one of the Use, Grab and Drop event is handled, this method checks whenever
    // the climbing hand let go or if a non-climbing hand grabbed something (this can 
    // either mean that the player was grounded and started grabbing with any hand,
    // or that they started grabbing with their non-climbing hand).
    private void ProcessInput(bool value, HandType hand) {
        // If the player grabbed with a non-climbing hand...
        if (value && !IsClimbingWith(hand)) {
            // ... check if that hand has something to grab, then...
            bool can_grab = inVR ? TestGrabVR(hand) : TestGrabDesktop(hand);
            if (can_grab) {
                // ... climb with that hand
                Grab(hand);
            }
        }
        // if the player let go of the climbing hand...
        if (!value && IsClimbingWith(hand)) {
            // ... stop climbing
            Drop();
        }
    }
#endregion

#region Climbing Actions
    private void UpdateGrab(HandType hand) {
        Vector3 handPos;
        GetHandPos(hand, out handPos);

        Vector3 offset = HandTransform.position - handPos;
        Vector3 velocity = offset * (1.0f / Time.deltaTime);
        if (inVR) {
            // TODO: fix massive upwards velocity when moving against a ceiling
            _lastClimbedVelocity = velocity;
        }
        else {
            // Store the position and velocity of the target for this frame
            // This should help keeping a correct velocity when you let go on desktop
            _lastClimbedVelocity = (HandTransform.position - _lastClimbedPosition) * (1.0f / Time.deltaTime);
        }
        _lastClimbedPosition = HandTransform.position;

        // Apply velocity
        localPlayer.SetVelocity(velocity);
    }

    public void Grab(HandType hand) {
        // Reset last velocity
        _lastClimbedVelocity = Vector3.zero;
        _lastClimbedPosition = HandTransform.position;

        // Send events
        if (_climbing) SendDroppedEvent(_lastClimbedTransform.gameObject); // previous climbed object
        SendGrabbedEvent(HandTransform.parent.gameObject); // current climbed object

        _climbingHand = hand;
        _climbing = true;
    }

    public void Drop() {
        // Apply last velocity if on desktop
        localPlayer.SetVelocity(_lastClimbedVelocity);

        // Send events
        SendDroppedEvent(HandTransform.parent.gameObject);

        _climbing = false;
    }

    public void DropGrabbed(Transform tf) {
        if (IsGrabbing(tf)) Drop();
    }

    public void ForceGrab(Transform tf, HandType hand, Vector3 offset) {
        HandTransform.position = tf.position + offset;
        HandTransform.parent = tf;

        Grab(hand);
    }
#endregion

#region Climbing Utilities
    private bool TestGrabVR(HandType hand) {
        Vector3 handPos;
        GetHandPos(hand, out handPos);

        if(Physics.OverlapSphereNonAlloc(handPos, handRadius, grabSurfaces, climableMask, QueryTriggerInteraction.Collide) >= 1) {
            // Store previous transform to send let go events
            _lastClimbedTransform = HandTransform.parent;
            // Reparent hand transform to new parent
            HandTransform.position = grabSurfaces[0].ClosestPoint(handPos);
            HandTransform.parent = grabSurfaces[0].transform;
            return true;
        }
        return false;
    }

    private bool TestGrabDesktop(HandType hand) {
        Vector3 headPos, headDir;
        GetHeadPos(out headPos, out headDir);

        RaycastHit hit;
        if (Physics.Raycast(headPos, headDir, out hit, handReach, climableMask, QueryTriggerInteraction.Collide)) {
            // Store previous transform to send let go events
            _lastClimbedTransform = HandTransform.parent;
            // Reparent hand transform to new parent
            HandTransform.position = hit.point;
            HandTransform.parent = hit.transform;
            return true;
        }
        return false;
    }
    
    public bool IsClimbingWith(HandType hand) {
        return _climbing && _climbingHand == hand;
    }

    public bool IsGrabbing(Transform tf) {
        if (_climbing) {
            return HandTransform.parent == tf;
        }
        return false;
    }

    private void GetHandPos(HandType hand, out Vector3 hand_pos) {
        VRCPlayerApi.TrackingData handTrackingData = hand == HandType.LEFT ? localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand) : localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand);
        hand_pos = handTrackingData.position;
    }

    private void GetHeadPos(out Vector3 head_pos, out Vector3 head_dir) {
         VRCPlayerApi.TrackingData headTrackingData = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
         head_pos = headTrackingData.position;
         head_dir = headTrackingData.rotation * Vector3.forward;
    }
#endregion

#region Climbing Events

    private void SendGrabbedEvent(GameObject climbed_object) {
        if (_sendEventsToClimbedObjects) {
            UdonBehaviour behavior = (UdonBehaviour)climbed_object.GetComponent(typeof(UdonBehaviour));
            if (behavior) behavior.SendCustomEvent(_grabbedEvent);
        }
        foreach (UdonBehaviour target in _eventTargets)
        {
            target.SendCustomEvent(_grabbedEvent);
        }

    }

    private void SendDroppedEvent(GameObject climbed_object) {
        if (_sendEventsToClimbedObjects) {
            UdonBehaviour behavior = (UdonBehaviour)climbed_object.GetComponent(typeof(UdonBehaviour));
            if (behavior) behavior.SendCustomEvent(_droppedEvent);
        }
        foreach (UdonBehaviour target in _eventTargets)
        {
            target.SendCustomEvent(_droppedEvent);
        }
    }
#endregion
}

