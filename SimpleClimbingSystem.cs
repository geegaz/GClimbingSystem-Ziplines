
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
    [SerializeField] private float walljumpViewDirectionAffect = 0.2f;
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
    [SerializeField] private string _startedEvent = "StartedClimbing";
    [SerializeField] private string _stoppedEvent = "StoppedClimbing";

    private bool _climbing = false;
    private HandType _climbingHand;
    private Collider[] grabSurfaces = new Collider[1];
    
    [HideInInspector] public VRCPlayerApi localPlayer;
    
    private void Start()
    {
        localPlayer = Networking.LocalPlayer;
        if (leftHandTransform) leftHandTransform.localScale = Vector3.one * handRadius;
        if (rightHandTransform) rightHandTransform.localScale = Vector3.one * handRadius;
    }

    public override void PostLateUpdate()
    {
        if (_climbing) {
            DoGrab(_climbingHand, !localPlayer.IsUserInVR());
        }
    }

#region Inputs
    public override void InputJump(bool value, UdonInputEventArgs args)
    {
        if (walljumpEnabled && _climbing) {
            Vector3 jump_direction = Vector3.zero;

            Vector3 headPos; 
            Vector3 headDirection;
            UpdateHeadValues(out headPos, out headDirection);
            
            if (walljumpUseFreeHand && localPlayer.IsUserInVR()) {
                Vector3 handPos;
                Transform handTf;
                // Inverse of the current climbing hand
                UpdateHandValues(1 - _climbingHand, out handPos, out handTf);

                jump_direction = (handPos - headPos).normalized;
            }
            else {
                jump_direction = headDirection;
            }

            Vector3 force = Vector3.Lerp(Vector3.up, jump_direction, walljumpViewDirectionAffect) * walljumpStrength;
            localPlayer.SetVelocity(localPlayer.GetVelocity() + force);
            LetGo();
        }
    }

    public override void InputUse(bool value, UdonInputEventArgs args)
    {
        if (localPlayer.IsUserInVR()) {
            if (useGrabButton) return; // skip execution

            HandType hand = args.handType;
            ProcessInput(value, hand);
        }
        else {
            // The Grab input is always Left Click on PC
            ProcessInput(value, HandType.LEFT);
        }
    }

    public override void InputGrab(bool value, UdonInputEventArgs args)
    {
        if (localPlayer.IsUserInVR()) {
            if (!useGrabButton) return; // Skip execution
            // Given that some controllers require a secondary button to drop,
            // it's necessary to only apply this if the input was just pressed
            if (value) {
                HandType hand = args.handType;
                ProcessInput(true, hand);
            }
        }
    }

    public override void InputDrop(bool value, UdonInputEventArgs args)
    {
        if (localPlayer.IsUserInVR()) {
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

    private void ProcessInput(bool value, HandType hand) {
        if (value && !IsClimbingWith(hand)) {
            bool can_grab = localPlayer.IsUserInVR() ? TestGrabVR(hand) : TestGrabDesktop(hand);
            if (can_grab) {
                // Start climbing
                Grab(hand);
            }
        }
        else if (!value && IsClimbingWith(hand)) {
            // Stop climbing
            LetGo();
        }
    }
#endregion

#region Climbing Actions
    public void LetGo() {
        if (_climbingHand == HandType.LEFT) {
            SendClimbingEvents(leftHandTransform.parent.gameObject, false);
        }
        else {
            SendClimbingEvents(rightHandTransform.parent.gameObject, false);
        }
        
        _climbing = false;
    }

    public void Grab(HandType hand) {
        if (hand == HandType.LEFT) {
            SendClimbingEvents(leftHandTransform.parent.gameObject, true);
            if (_climbing) SendClimbingEvents(rightHandTransform.parent.gameObject, false);
        }
        else {
            SendClimbingEvents(rightHandTransform.parent.gameObject, true);
            if (_climbing) SendClimbingEvents(leftHandTransform.parent.gameObject, false);
        }

        _climbingHand = hand;
        _climbing = true;
    }

    public void LetGoGrabbing(Transform tf) {
        if (IsGrabbing(tf)) LetGo();
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
                if (started) behavior.SendCustomEvent(_startedEvent);
                else  behavior.SendCustomEvent(_stoppedEvent);
            }
        }

        if (_eventTarget) {
            if (started) _eventTarget.SendCustomEvent(_startedEvent);
            else  _eventTarget.SendCustomEvent(_stoppedEvent);
        }
    }
#endregion
}

