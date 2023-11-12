
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SimpleClimbingSystem : UdonSharpBehaviour
{
    [SerializeField] private Transform leftHandTransform;
    [SerializeField] private Transform rightHandTransform;
    [SerializeField] private LayerMask climableMask;

    [Space]
    [SerializeField] private bool walljumpEnabled = true;
    [Tooltip("(VR only) Use the direction from the player's head to their free hand\ninstead of their view direction for walljump")]
    [SerializeField] private bool walljumpUseFreeHand = true;
    [Tooltip("When pressing Jump while on a wall:\n- 0 is a fully vertical jump\n- 1 is a jump in the direction the player is aiming")]
    [SerializeField] private float walljumpViewDirectionAffect = 0.5f;
    [SerializeField] private float walljumpStrength = 5f;

    [Header("VR Options")]
    [SerializeField] private bool useGrabButton = true;
    [SerializeField] private float handRadius = 0.1f;

    [Header("Desktop Options")]
    [SerializeField] private float handReach = 2f;
    [SerializeField] private float handSurfaceDistance = 0.1f;
    [SerializeField] private float moveSmoothing = 1f;

    [Header("Events")]
    [SerializeField] private bool _sendEventsToClimbedObjects = true;
    [SerializeField] private UdonBehaviour _eventTarget;
    [SerializeField] private string _grabbedEvent = "ClimbingGrabbed";
    [SerializeField] private string _droppedEvent = "ClimbingDropped";

    private bool _climbing = false;
    private HandType _climbingHand;
    private Vector3 _lastClimbedVelocity;
    private Vector3 _lastClimbedPosition;
    private Collider[] grabSurfaces = new Collider[1];
    
    // Cache local player and VR status
    [HideInInspector] public VRCPlayerApi localPlayer;
    [HideInInspector] public bool inVR;
    
    private void Start()
    {
        localPlayer = Networking.LocalPlayer;
        inVR = localPlayer.IsUserInVR();
        if (leftHandTransform) leftHandTransform.localScale = Vector3.one * handRadius;
        if (rightHandTransform) rightHandTransform.localScale = Vector3.one * handRadius;
    }

    public override void PostLateUpdate()
    {
        if (_climbing) {
            DoGrab(_climbingHand, !inVR);
        }
    }

#region Inputs
    // Jump Input Handling
    // In VR and on Desktop, this event is sent when the jump button is used.
    //
    // This event is only used when climbing if walljumping is enabled. The direction of
    // the walljump depends on the head direction on PC, and either the head direction
    // or the non-climbing arm direction in VR if the climbing system is set to use the
    // free hand for walljumping.
    public override void InputJump(bool value, UdonInputEventArgs args)
    {
        if (value && walljumpEnabled && _climbing) {
            Vector3 jump_direction = Vector3.zero;
            
            if (inVR && walljumpUseFreeHand) {
                Vector3 handBonePos;
                Vector3 shoulderBonePos;
                if (_climbingHand == HandType.LEFT) {
                    handBonePos = localPlayer.GetBonePosition(HumanBodyBones.LeftHand);
                    shoulderBonePos = localPlayer.GetBonePosition(HumanBodyBones.LeftShoulder);
                }
                else {
                    handBonePos = localPlayer.GetBonePosition(HumanBodyBones.RightHand);
                    shoulderBonePos = localPlayer.GetBonePosition(HumanBodyBones.RightShoulder);
                }
                
                jump_direction = (handBonePos - shoulderBonePos).normalized;
            }
            else {
                Vector3 headPos; 
                Vector3 headDirection;
                UpdateHeadValues(out headPos, out headDirection);

                jump_direction = headDirection;
            }

            // Let go with jump force
            Vector3 force = Vector3.Slerp(Vector3.up, jump_direction, walljumpViewDirectionAffect) * walljumpStrength;
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

            HandType hand = args.handType;
            ProcessInput(value, hand);
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
            // Given that some controllers require a secondary button to drop,
            // it's necessary to only apply this if the input was just pressed
            if (value) {
                HandType hand = args.handType;
                ProcessInput(true, hand);
            }
        }
    }

    // Drop Input Handling
    // In VR, this event is sent when the grip button is released.
    // On desktop, this event is sent when the right click is used.
    // 
    // This event is always used on Desktop to simulate the right hand, but is 
    // only used in VR if the climbing system is set to use the grip buttons.
    public override void InputDrop(bool value, UdonInputEventArgs args)
    {
        if (inVR) {
            if (!useGrabButton) return; // Skip execution
            // Given that some controllers require a secondary button to drop,
            // it's necessary to only apply this if the input was just pressed
            if (value) {
                HandType hand = args.handType;
                ProcessInput(false, hand);
            }
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
        else if (!value && IsClimbingWith(hand)) {
            // ... stop climbing
            Drop();
        }
    }
#endregion

#region Climbing Actions
    public void Drop() {
        // Send events
        if (_climbingHand == HandType.LEFT) {
            SendClimbingEvents(leftHandTransform.parent.gameObject, false);
        }
        else {
            SendClimbingEvents(rightHandTransform.parent.gameObject, false);
        }
        // Apply last velocity
        localPlayer.SetVelocity(_lastClimbedVelocity);
        
        _climbing = false;
    }

    public void Grab(HandType hand) {
        if (hand == HandType.LEFT) {
            _lastClimbedPosition = leftHandTransform.position;
            // Send events
            SendClimbingEvents(leftHandTransform.parent.gameObject, true);
            if (_climbing) SendClimbingEvents(rightHandTransform.parent.gameObject, false);
        }
        else {
            _lastClimbedPosition = rightHandTransform.position;
            // Send events
            SendClimbingEvents(rightHandTransform.parent.gameObject, true);
            if (_climbing) SendClimbingEvents(leftHandTransform.parent.gameObject, false);
        }
        // Reset last velocity
        _lastClimbedVelocity = Vector3.zero;

        _climbingHand = hand;
        _climbing = true;
    }

    public void DropGrabbed(Transform tf) {
        if (IsGrabbing(tf)) Drop();
    }

    public void ForceGrab(Transform tf, HandType hand, Vector3 offset) {
        Transform hand_tf = hand == HandType.LEFT ? leftHandTransform : rightHandTransform;
        hand_tf.position = tf.position + offset;
        hand_tf.parent = tf;

        Grab(hand);
    }

    private void DoGrab(HandType hand, bool smoothing = false) {
        Vector3 handPos; 
        Transform handTransform;
        UpdateHandValues(hand, out handPos, out handTransform);

        Vector3 offset = handTransform.position - handPos;
        if (smoothing) {
            float heightDiff = offset.y;
            float distance_affect = offset.magnitude / moveSmoothing;
            offset = Vector3.Lerp(Vector3.zero, offset, distance_affect);
            offset.y = heightDiff;
        }
        localPlayer.SetVelocity(offset * (1.0f / Time.deltaTime));
        // Store the position and velocity of the target for this frame
        // This should help keeping a correct velocity (mostly on desktop) when you let go
        _lastClimbedVelocity = (handTransform.position - _lastClimbedPosition) * (1.0f / Time.deltaTime);
        _lastClimbedPosition = handTransform.position;
    }
#endregion

#region Climbing Utilities
    private bool TestGrabVR(HandType hand) {
        Vector3 handPos; 
        Transform handTransform;
        UpdateHandValues(hand, out handPos, out handTransform);

        if(Physics.OverlapSphereNonAlloc(handPos, handRadius, grabSurfaces, climableMask, QueryTriggerInteraction.Collide) >= 1) {
            handTransform.position = grabSurfaces[0].ClosestPoint(handPos);
            handTransform.parent = grabSurfaces[0].transform;
            return true;
        }
        return false;
    }

    private bool TestGrabDesktop(HandType hand) {
        Transform handTransform = hand == HandType.LEFT ? leftHandTransform : rightHandTransform;
        Vector3 headPos; 
        Vector3 headDirection;
        UpdateHeadValues(out headPos, out headDirection);

        RaycastHit hit;
        if (Physics.Raycast(headPos, headDirection, out hit, handReach, climableMask, QueryTriggerInteraction.Collide)) {
            handTransform.position = hit.point + hit.normal * handSurfaceDistance;
            handTransform.parent = hit.transform;
            return true;
        }
        return false;
    }
    
    public bool IsClimbingWith(HandType hand) {
        return _climbing && _climbingHand == hand;
    }

    public bool IsGrabbing(Transform tf) {
        if (_climbing) {
            Transform hand_tf = _climbingHand == HandType.LEFT ? leftHandTransform : rightHandTransform;
            return hand_tf.parent == tf;
        }
        return false;
    }

    private void UpdateHandValues(HandType hand, out Vector3 hand_pos, out Transform hand_tf) {
        bool isLeft = hand == HandType.LEFT;
        VRCPlayerApi.TrackingData handTrackingData = isLeft ? localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand) : localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand);
        hand_pos = handTrackingData.position;
        hand_tf = isLeft ? leftHandTransform : rightHandTransform;
    }

    private void UpdateHeadValues(out Vector3 head_pos, out Vector3 head_dir) {
        VRCPlayerApi.TrackingData headTrackingData = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        head_pos = headTrackingData.position;
        head_dir = headTrackingData.rotation * Vector3.forward;
    }

    private void SendClimbingEvents(GameObject climbed_object, bool started) {
        if (_sendEventsToClimbedObjects) {
            UdonBehaviour behavior = (UdonBehaviour)climbed_object.GetComponent(typeof(UdonBehaviour));
            if (behavior) {
                if (started) behavior.SendCustomEvent(_grabbedEvent);
                else  behavior.SendCustomEvent(_droppedEvent);
            }
        }

        if (_eventTarget) {
            if (started) _eventTarget.SendCustomEvent(_grabbedEvent);
            else  _eventTarget.SendCustomEvent(_droppedEvent);
        }
    }
#endregion
}

