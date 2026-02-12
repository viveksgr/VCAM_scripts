using UnityEngine;

public class MouseMovement_backup : MonoBehaviour
{
    [Header("Sensitivity")]
    [Tooltip("Base sensitivity multiplier (higher = faster)")]
    public float mouseSensitivity = 80f;

    [Header("Pitch Clamp (Comfort)")]
    [Tooltip("Max up/down look in degrees (lower reduces motion sickness)")]
    public float maxPitchDegrees = 14f;

    [Header("Comfort Smoothing + Speed Limits")]
    [Tooltip("Smoothing time constant in seconds. 0.06–0.12 is a good range.")]
    public float smoothingTime = 0.09f;

    [Tooltip("Hard cap on yaw speed (deg/sec). Lower = less motion sickness.")]
    public float maxYawSpeedDegPerSec = 110f;

    [Tooltip("Hard cap on pitch speed (deg/sec). Lower than yaw is recommended.")]
    public float maxPitchSpeedDegPerSec = 55f;

    [Tooltip("Scale vertical sensitivity relative to horizontal (0.4–0.8 recommended).")]
    public float verticalSensitivityScale = 0.6f;

    [Header("Optional Deadzone")]
    [Tooltip("Ignore tiny mouse input (reduces jitter). 0.00–0.05 typical.")]
    public float deadzone = 0.02f;

    // internal state (smoothed)
    private float pitch; // xRotation
    private float yaw;   // yRotation

    // target state (raw)
    private float targetPitch;
    private float targetYaw;

    // SmoothDamp velocities
    private float pitchVel;
    private float yawVel;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Initialize from current transform to avoid jumps on scene load
        var e = transform.localEulerAngles;

        // Unity stores angles 0..360; convert pitch to -180..180 for clamping
        float currentPitch = e.x;
        if (currentPitch > 180f) currentPitch -= 360f;

        pitch = targetPitch = Mathf.Clamp(currentPitch, -maxPitchDegrees, maxPitchDegrees);
        yaw = targetYaw = e.y;

        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void Update()
    {
        // Use unscaled time so pausing UI (Time.timeScale=0) doesn't cause jumps on resume
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity * verticalSensitivityScale;

        // Deadzone to reduce micro-jitter
        if (Mathf.Abs(mx) < deadzone) mx = 0f;
        if (Mathf.Abs(my) < deadzone) my = 0f;

        // Update targets (degrees)
        targetYaw += mx * dt;
        targetPitch -= my * dt;

        // Clamp pitch
        targetPitch = Mathf.Clamp(targetPitch, -maxPitchDegrees, maxPitchDegrees);

        // Smooth toward targets with speed caps (deg/sec)
        yaw = Mathf.SmoothDampAngle(
            yaw,
            targetYaw,
            ref yawVel,
            smoothingTime,
            maxYawSpeedDegPerSec,
            dt
        );

        pitch = Mathf.SmoothDampAngle(
            pitch,
            targetPitch,
            ref pitchVel,
            smoothingTime,
            maxPitchSpeedDegPerSec,
            dt
        );

        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void OnDisable()
    {
        // Prevent a big velocity carry-over if this component gets disabled/enabled
        pitchVel = 0f;
        yawVel = 0f;
    }
}
