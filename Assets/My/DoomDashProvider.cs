using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

public class DoomfistDash : LocomotionProvider 
{
    public InputActionReference gripAction;
    public InputActionReference triggerAction;
    public CharacterController characterController;
    public Transform headsetTransform;
    public Transform handTransform;


    //Dashing
    public float maxDashDistance = 50.0f;
    public float minDashDistance = 5.0f;
    public float maxChargeTime = 3.0f;
    public float dashSpeed = 150.0f;
    public float minPunchSpeed = 5.0f;
    [Range(0f, 1f)] public float blendFactor = 0.7f;

    //Uppercut
    public float maxUpperCutDistance = 10.0f;
    public float upperCutSpeedThreshold = 5.0f;
    public float upperCutSpeed = 25.0f;    // Speed for upward movement during uppercut
    


    //Sound
    public AudioSource audioSource;
    public AudioClip chargingSound;
    public AudioClip dashSound;
    public AudioClip upperCutSound;
    

    private bool isCharging = false;
    private bool isUppercutting = false;
    private float chargeTime = 0f;
    private Vector3 dashDirection;
    private float dashDistanceRemaining;
    private bool isDashing = false;
    private Vector3 lastHandPosition;

    void Start()
    {
        if (headsetTransform == null)
        {
            Debug.LogError("No headsetTransform assigned. Assign the headset (camera) transform for look direction.");
        }

        if (audioSource == null)
        {
            Debug.LogError("No AudioSource assigned. Please assign an AudioSource.");
        }

        lastHandPosition = handTransform.localPosition;
    }

    void Update()
    {
        bool grabPressed = gripAction.action != null && gripAction.action.ReadValue<float>() > 0.5f;
        bool triggerPressed = triggerAction.action != null && triggerAction.action.ReadValue<float>() > 0.5f;

        Vector3 handVelocity = (handTransform.localPosition - lastHandPosition) / Time.deltaTime;

        // Detect forward dash
        if (grabPressed && triggerPressed && !isCharging && !isDashing)
        {
            StartCharging();
        }
        else if (isCharging && new Vector2(handVelocity.x, handVelocity.z).magnitude >= minPunchSpeed)
        {
            Dash();
        }
        
        // Detect uppercut
        if (!isUppercutting && !isDashing && !isCharging && handVelocity.y >= upperCutSpeedThreshold)
        {
            StartUppercut();
        }
      

        if (isCharging)
        {
            chargeTime += Time.deltaTime;
            chargeTime = Mathf.Clamp(chargeTime, 0, maxChargeTime);
        }

        if (isDashing || isUppercutting)
        {
            Movement();
        }

        lastHandPosition = handTransform.localPosition;
    }

    void StartCharging()
    {
        isCharging = true;
        chargeTime = 0f;

        if (audioSource != null && chargingSound != null)
        {
            audioSource.clip = chargingSound;
            audioSource.loop = true;
            audioSource.volume = 0.5f;
            audioSource.Play();
            Debug.Log("chargingSound played");
        }
    }

    void Dash()
    {
        if (!TryStartLocomotionImmediately())
            return;
        isCharging = false;
        isDashing = true;

        Vector3 handVelocity = (handTransform.localPosition - lastHandPosition).normalized;
        Vector3 headsetForward = new Vector3(headsetTransform.forward.x, 0, headsetTransform.forward.z).normalized;

        dashDirection = Vector3.Lerp(handVelocity, headsetForward, blendFactor).normalized;
        dashDistanceRemaining = Mathf.Lerp(minDashDistance, maxDashDistance, chargeTime / maxChargeTime);

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        if (audioSource != null && dashSound != null)
        {
            audioSource.PlayOneShot(dashSound, 1f);
            Debug.Log("dashSound played");
        }
    }

    void StartUppercut()
    {
        if (!TryStartLocomotionImmediately())
            return;
        isCharging = false;
        isDashing = false;
        isUppercutting = true;

        // Set the dash direction for the uppercut: mainly upwards with a slight(10%) forward push
        dashDirection = new Vector3(headsetTransform.forward.x * 0.1f, 1, headsetTransform.forward.z * 0.1f).normalized;
        dashDistanceRemaining = maxUpperCutDistance;

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        if (audioSource != null && upperCutSound != null)
        {
            audioSource.PlayOneShot(upperCutSound, 1f);
            Debug.Log("upperCutSound played");
        }
    }


    void Movement()
    {
        float step = dashSpeed * Time.deltaTime;
        Vector3 dashStep = dashDirection * Mathf.Min(step, dashDistanceRemaining);
        characterController.Move(dashStep);
        dashDistanceRemaining -= dashStep.magnitude;

        if (dashDistanceRemaining <= 0.01)
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
            TryEndLocomotion();
            isDashing = false;
            isUppercutting = false;
            isCharging = false;
        }
    }
}
