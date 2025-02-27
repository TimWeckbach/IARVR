using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

public class DoomfistDash : LocomotionProvider
{
    [Header("References")]
    public InputActionReference leftGripAction;
    public InputActionReference leftTriggerAction;
    public Transform leftHandTransform;

    public InputActionReference rightGripAction;
    public InputActionReference rightTriggerAction;
    public Transform rightHandTransform;

    public CharacterController characterController;
    public Transform headsetTransform;

    [Header("Dash Settings")]
    public float maxDashDistance = 50.0f;
    public float minDashDistance = 5.0f;
    public float maxChargeTime = 3.0f;
    public float dashSpeed = 25.0f;
    public float minPunchSpeed = 5.0f;
    [Range(0f, 1f)] public float blendFactor_howMuchHeadsetDirection = 0.7f;

    [Header("Uppercut Settings")]
    public float maxUpperCutDistance = 20.0f;
    public float upperCutSpeedThreshold = 5.0f;

    [Header("Ground Slam Settings")]
    public float slamSpeedThreshold = 5.0f;
    public float maxSlamDistance = 20f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip chargingSound;
    public AudioClip dashSound;
    public AudioClip upperCutSound;
    public AudioClip groundSlamSound;

    [Header("Decal")]
    public GameObject groundSlamDecal;

    private struct HandState
    {
        public bool isCharging;
        public bool isDashing;
        public bool isUppercutting;
        public bool isGroundSlamming;
        public float chargeTime;
        public Vector3 lastHandPosition;
        public Vector3 moveDirection;
        public float distanceRemaining;
    }

    private HandState leftHandState, rightHandState;

    void Start()
    {
        if (!headsetTransform) Debug.LogError("No headsetTransform assigned.");
        if (!audioSource) Debug.LogError("No AudioSource assigned.");

        leftHandState.lastHandPosition = leftHandTransform.localPosition;
        rightHandState.lastHandPosition = rightHandTransform.localPosition;
    }

    void Update()
    {
        HandleHand(leftGripAction, leftTriggerAction, leftHandTransform, ref leftHandState);
        HandleHand(rightGripAction, rightTriggerAction, rightHandTransform, ref rightHandState);

        // Combine both hands' movement in one frame
        Vector3 totalMove = Movement(ref leftHandState) + Movement(ref rightHandState);
        if (totalMove.sqrMagnitude > 0.0001f) characterController.Move(totalMove);

        ApplyGravity();
    }

    void HandleHand(InputActionReference grip, InputActionReference trigger, Transform handTransform, ref HandState h)
    {
        bool grabPressed = (grip.action != null && grip.action.ReadValue<float>() > 0.5f);
        bool triggerPressed = (trigger.action != null && trigger.action.ReadValue<float>() > 0.5f);
        Vector3 handVelocity = (handTransform.localPosition - h.lastHandPosition) / Time.deltaTime;

        // Charging and dash
        if (grabPressed && triggerPressed && !h.isCharging && !AnyMovementActive(h))
            StartCharging(ref h);
        else if (h.isCharging && new Vector2(handVelocity.x, handVelocity.z).magnitude >= minPunchSpeed)
            StartDash(handVelocity, ref h);

        // Uppercut
        if (!h.isUppercutting && !h.isDashing && !h.isCharging && !h.isGroundSlamming && handVelocity.y >= upperCutSpeedThreshold)
            StartUppercut(ref h);

        // Ground slam
        if (!h.isGroundSlamming && !h.isCharging && !h.isDashing && !h.isUppercutting && handVelocity.y <= -slamSpeedThreshold)
            StartGroundSlam(ref h);

        // Charging time
        if (h.isCharging)
        {
            h.chargeTime += Time.deltaTime;
            h.chargeTime = Mathf.Clamp(h.chargeTime, 0, maxChargeTime);
        }

        h.lastHandPosition = handTransform.localPosition;
    }

    bool AnyMovementActive(HandState h)
    {
        return h.isDashing || h.isUppercutting || h.isGroundSlamming;
    }

    void StartCharging(ref HandState h)
    {
        h.isCharging = true;
        h.chargeTime = 0f;
        if (audioSource && chargingSound) audioSource.PlayOneShot(chargingSound);
    }

    void StartDash(Vector3 handVelocity, ref HandState h)
    {
        if (!TryStartLocomotionImmediately()) return;

        
        h.isCharging = false;
        h.isDashing = true;
        Vector3 headsetFwd = new Vector3(headsetTransform.forward.x, 0f, headsetTransform.forward.z).normalized;
        h.moveDirection = Vector3.Lerp(handVelocity.normalized, headsetFwd, blendFactor_howMuchHeadsetDirection).normalized;
        h.distanceRemaining = Mathf.Lerp(minDashDistance, maxDashDistance, h.chargeTime / maxChargeTime);

        if (audioSource && dashSound){
            audioSource.Stop();
            audioSource.PlayOneShot(dashSound);
        } 
    }

    void StartUppercut(ref HandState h)
    {
        if (!TryStartLocomotionImmediately()) return;

        h.isCharging = false;
        h.isDashing = false;
        h.isUppercutting = true;
        h.moveDirection = (new Vector3(headsetTransform.forward.x * 0.1f, 1f, headsetTransform.forward.z * 0.1f)).normalized;
        h.distanceRemaining = maxUpperCutDistance;

        if (audioSource && upperCutSound) audioSource.PlayOneShot(upperCutSound);
    }

    void StartGroundSlam(ref HandState h)
    {
        if (!TryStartLocomotionImmediately()) return;

        h.isCharging = false;
        h.isDashing = false;
        h.isUppercutting = false;
        h.isGroundSlamming = true;
        h.moveDirection = Vector3.down;
        h.distanceRemaining = maxSlamDistance;

        if (audioSource && groundSlamSound) audioSource.PlayOneShot(groundSlamSound);
    }

    // Return the movement for this frame (instead of moving the character immediately)
    Vector3 Movement(ref HandState h)
    {
        if (!AnyMovementActive(h)) return Vector3.zero;

        float step = dashSpeed * Time.deltaTime;
        float moveStep = Mathf.Min(step, h.distanceRemaining);
        Vector3 movement = h.moveDirection * moveStep;
        h.distanceRemaining -= moveStep;

        if (h.distanceRemaining <= 0.01f)
        {
            TryEndLocomotion();
            if (h.isGroundSlamming && groundSlamDecal && characterController.isGrounded)
            {
                Vector3 spawnPos = characterController.transform.position;
                spawnPos.y += 0.1f;
                Quaternion rotation = Quaternion.Euler(90f, 0f, 0f);
                Instantiate(groundSlamDecal, spawnPos, rotation);
            }
            ResetHandState(ref h);
        }

        return movement;
    }

    void ResetHandState(ref HandState h)
    {
        h.isDashing = false;
        h.isUppercutting = false;
        h.isGroundSlamming = false;
        h.isCharging = false;
        h.distanceRemaining = 0f;
    }

    void ApplyGravity()
    {
        if (characterController.isGrounded) return;
        characterController.Move(Physics.gravity * Time.deltaTime);
    }
}
