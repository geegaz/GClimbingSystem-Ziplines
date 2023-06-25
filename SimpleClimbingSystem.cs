
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SimpleClimbingSystem : UdonSharpBehaviour
{
    public Transform leftHandTransform;
    public Transform rightHandTransform;
    public LayerMask climableMask;

    [Space]
    public bool walljumpEnabled = true;
    [Tooltip("When pressing Jump while on a wall:\n- 0 is fully vertical jump\n- 1 is a jump in the direction the player is looking")]
    public float walljumpViewDirectionAffect = 0.5f;
    public float walljumpStrength = 6f;

    [Header("VR Options")]
    public bool useGrabButton = true;
    public float handRadius = 0.1f;

    [Header("Desktop Options")]
    public float handReach = 2f;
    public float handSurfaceDistance = 0.2f;
    public float moveSmoothing = 1f;

    private bool _climbing = false;
    private HandType _climbingHand;
    private Transform _climbedObject;
    
    [HideInInspector] public VRCPlayerApi localPlayer;

    private Collider[] grabSurfaces = new Collider[1];
    
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
            Vector3 headPos; 
            Vector3 headDirection;
            UpdateHeadValues(out headPos, out headDirection);

            Vector3 force = Vector3.Lerp(Vector3.up, headDirection, walljumpViewDirectionAffect) * walljumpStrength;
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

    public void LetGo() {
        _climbing = false;
    }

    public void LetGoGrabbing(Transform tf) {
        if (IsGrabbing(tf)) LetGo();
    }

    public void Grab(HandType hand) {
        _climbingHand = hand;
        _climbing = true;
    }

    public void ForceGrab(Transform tf, HandType hand, Vector3 offset) {
        Transform hand_tf = hand == HandType.LEFT ? leftHandTransform : rightHandTransform;
        hand_tf.position = tf.position + offset;
        hand_tf.parent = tf;

        Grab(hand);
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
}
