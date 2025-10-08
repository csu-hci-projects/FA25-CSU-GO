using UnityEngine;

/// Attach to your player model (weapon rig or body mesh).
public class PlayerModelTiltSway : MonoBehaviour
{
    [Header("Refs")]
    public Transform cameraTransform;   // optional but recommended
    public Rigidbody playerRb;          // optional; else uses input axes

    [Header("Mouse Sway (look-based)")]
    public Vector3 mouseSwayAmount = new Vector3(1.0f, 1.2f, 1.5f); // pitch,yaw,roll
    public Vector3 mouseSwayClamp  = new Vector3(6f, 6f, 8f);       // pitch,yaw,roll
    public float mouseSensitivity  = 1.0f;
    public bool invertRollFromMouse = true;

    [Header("Movement Tilt (velocity-based)")]
    public float movePitchPerSpeed = 3.5f;
    public float moveRollPerSpeed  = 6.0f;
    public float moveYawPerSpeed   = 1.0f;
    public Vector3 moveTiltClamp   = new Vector3(8f, 6f, 12f);

    [Header("Damping")]
    public float rotationSmooth = 12f;
    public bool  dampWhenSlow   = true;
    public float slowSpeedThreshold = 0.4f;

    [Header("Input (fallback if no Rigidbody)")]
    public bool useLegacyAxesFallback = true;
    public float fallbackSpeed = 5f;

    // ---------- NEW: Landing Impact ----------
    [Header("Landing Impact")]
    [Tooltip("Enable built-in landing detection via ground probe.")]
    public bool useBuiltInGroundProbe = true;
    [Tooltip("Empty child at feet (same one you use in the movement script).")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayers = ~0;

    [Tooltip("Minimum downward speed to trigger an impact.")]
    public float impactVelocityThreshold = 6f;
    [Tooltip("How much pitch dip we add at threshold impact (degrees). Scales with fall speed.")]
    public float impactPitchAtThreshold = 8f;
    [Tooltip("How much vertical bob at threshold impact (meters). Scales with fall speed.")]
    public float impactBobAtThreshold = 0.05f;
    [Tooltip("Extra scale factor applied as fall speed grows beyond threshold.")]
    public float impactSpeedScale = 0.12f;

    [Tooltip("Spring frequency (Hz) for the landing bob/dip.")]
    public float impactSpringFrequency = 8f;
    [Tooltip("Critical damping = 1.  <1 bouncy, >1 overdamped.")]
    [Range(0.6f, 1.5f)] public float impactDampingRatio = 1.0f;

    // cache
    Quaternion _startLocalRot;
    Vector3    _startLocalPos;

    // landing spring state (for pitch degrees and local Y offset)
    float _impactPitch;     // degrees
    float _impactPitchVel;  // deg/s
    float _impactYOffset;   // meters
    float _impactYVel;      // m/s

    // landing detection state
    bool _wasGrounded;
    bool _isGrounded;
    float _lastVerticalVel;

    void Awake()
    {
        _startLocalRot = transform.localRotation;
        _startLocalPos = transform.localPosition;

        if (!cameraTransform)
        {
            var cam = GetComponentInParent<Camera>();
            if (cam) cameraTransform = cam.transform;
        }
    }

    void Update()
    {
        // --- 1) Mouse sway ---
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        float mousePitch = Mathf.Clamp(-my * mouseSwayAmount.x, -mouseSwayClamp.x, mouseSwayClamp.x);
        float mouseYaw   = Mathf.Clamp( mx * mouseSwayAmount.y, -mouseSwayClamp.y, mouseSwayClamp.y);
        float mouseRollInput = (invertRollFromMouse ? -mx : mx);
        float mouseRoll  = Mathf.Clamp(mouseRollInput * mouseSwayAmount.z, -mouseSwayClamp.z, mouseSwayClamp.z);

        // --- 2) Movement tilt (velocity-based) ---
        Vector3 localVel = Vector3.zero;

        if (playerRb)
        {
            Vector3 v = playerRb.linearVelocity; // NOTE: velocity, not linearVelocity
            if (cameraTransform)
                localVel = cameraTransform.InverseTransformDirection(v);
            else
                localVel = transform.InverseTransformDirection(v);
        }
        else if (useLegacyAxesFallback)
        {
            float ix = Input.GetAxisRaw("Horizontal");
            float iz = Input.GetAxisRaw("Vertical");
            Vector3 worldVel = Vector3.zero;
            if (cameraTransform)
            {
                Vector3 fwd   = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right,  Vector3.up).normalized;
                worldVel = (fwd * iz + right * ix) * fallbackSpeed;
                localVel = cameraTransform.InverseTransformDirection(worldVel);
            }
            else
            {
                worldVel = (transform.forward * iz + transform.right * ix) * fallbackSpeed;
                localVel = transform.InverseTransformDirection(worldVel);
            }
        }

        float movePitch = Mathf.Clamp(-localVel.z * movePitchPerSpeed, -moveTiltClamp.x, moveTiltClamp.x);
        float moveRoll  = Mathf.Clamp( localVel.x * moveRollPerSpeed,  -moveTiltClamp.z, moveTiltClamp.z);
        float moveYaw   = Mathf.Clamp( localVel.x * moveYawPerSpeed,   -moveTiltClamp.y, moveTiltClamp.y);

        if (dampWhenSlow)
        {
            float speed = new Vector2(localVel.x, localVel.z).magnitude;
            float k = Mathf.InverseLerp(slowSpeedThreshold, slowSpeedThreshold * 2f, speed);
            movePitch *= k; moveRoll *= k; moveYaw *= k;
        }

        // --- 3) LANDING DETECTION + SPRING UPDATE (NEW) ---
        HandleLandingDetectionAndKick();

        // critically-damped spring toward 0 for impact pitch & y
        SpringToZero(ref _impactPitch, ref _impactPitchVel, impactSpringFrequency, impactDampingRatio);
        SpringToZero(ref _impactYOffset, ref _impactYVel,    impactSpringFrequency, impactDampingRatio);

        // --- 4) Compose target rotation & position ---
        Quaternion targetRot =
            _startLocalRot
            * Quaternion.Euler(movePitch, moveYaw, moveRoll)
            * Quaternion.Euler(_impactPitch, 0f, 0f)                 // NEW: landing dip added
            * Quaternion.Euler(mousePitch, mouseYaw, mouseRoll);

        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            targetRot,
            1f - Mathf.Exp(-rotationSmooth * Time.deltaTime)
        );

        // NEW: add vertical bob
        Vector3 targetPos = _startLocalPos + new Vector3(0f, _impactYOffset, 0f);
        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetPos,
            1f - Mathf.Exp(-rotationSmooth * Time.deltaTime)
        );
    }

    // ---------- PUBLIC: manual trigger from your movement script ----------
    public void TriggerLandingImpact(float fallSpeed)
    {
        if (fallSpeed < impactVelocityThreshold) return;

        float over = fallSpeed - impactVelocityThreshold;
        float scale = 1f + over * impactSpeedScale; // grows with speed

        // push DOWN (negative pitch), then spring back; bob goes slightly UP first
        _impactPitch   -= impactPitchAtThreshold * scale;
        _impactYOffset += impactBobAtThreshold   * scale;

        // optional: clamp extremes
        _impactPitch   = Mathf.Clamp(_impactPitch, -30f, 30f);
        _impactYOffset = Mathf.Clamp(_impactYOffset, -0.15f, 0.15f);
    }

    // ---------- NEW: built-in landing detection ----------
    void HandleLandingDetectionAndKick()
    {
        if (!playerRb) return;

        float dt = Time.deltaTime;
        float vy = playerRb.linearVelocity.y;

        if (useBuiltInGroundProbe && groundCheck)
        {
            bool nowGrounded = Physics.CheckSphere(
                groundCheck.position, groundCheckRadius, groundLayers, QueryTriggerInteraction.Ignore);

            _isGrounded = nowGrounded;
        }
        else
        {
            // heuristic: consider grounded if vertical velocity ~0 and we weren't moving up
            _isGrounded = (Mathf.Abs(vy) < 0.05f && _lastVerticalVel <= 0f);
        }

        // landing: was airborne with downward velocity, now grounded
        if (_isGrounded && !_wasGrounded && _lastVerticalVel < -0.01f)
        {
            float impactSpeed = Mathf.Abs(_lastVerticalVel);
            TriggerLandingImpact(impactSpeed);
        }

        _wasGrounded = _isGrounded;
        _lastVerticalVel = vy;
    }

    // critically-damped spring toward zero
    void SpringToZero(ref float x, ref float v, float freq, float zeta)
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Stable, framerate-independent critically/over/under damped spring
        float w = 2f * Mathf.PI * Mathf.Max(0.01f, freq);
        float f = 1f + 2f * dt * zeta * w;
        float ww = w * w;
        float dtww = dt * ww;

        float detInv = 1f / (f + dtww * dt);
        float xNew = (f * x + dt * v) * detInv;
        float vNew = (v + dtww * (-x)) * detInv;

        x = xNew; v = vNew;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (groundCheck)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
#endif
}
