using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovementFPSBhop : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Assign your FPS camera (the Transform whose forward/right you want to use).")]
    public Transform cameraTransform;
    [Tooltip("Empty child at the player's feet for ground check.")]
    public Transform groundCheck;

    [Header("Movement")]
    [Tooltip("Base walk speed (before bhop bonus).")]
    public float moveSpeed = 6f;

    [Header("Jump")]
    [Tooltip("Upward impulse for a single, consistent jump.")]
    public float jumpForce = 5f;
    public LayerMask groundLayers = ~0;
    public float groundCheckRadius = 0.2f;

    [Header("Debug / Testing")]
    [Tooltip("If true, holding Space will auto-jump whenever grounded (useful for testing).")]
    public bool holdToJump = false;

    // ---------- BHOP ----------
    [Header("BHop")]
    [Tooltip("Max time after landing that a jump counts as a bhop (seconds).")]
    public float bhopWindow = 0.12f;
    [Tooltip("How much input is required to count as moving for bhop.")]
    public float bhopMinMove = 0.25f;
    [Tooltip("Speed bonus added per successful hop.")]
    public float bhopBonusPerHop = 0.75f;
    [Tooltip("Clamp for the total stacked bhop bonus.")]
    public float bhopMaxBonus = 3f;

    [Header("BHop Decay")]
    [Tooltip("Grounded bleed per second, regardless of speed.")]
    public float bhopBaseDecay = 1.5f;
    [Tooltip("Extra grounded bleed per unit of horizontal speed.")]
    public float bhopSpeedDecayFactor = 0.3f;
    [Tooltip("Always-on decay (air or ground).")]
    public float constantDecayPerSecond = 0.5f;

    [Header("Air Control (CS-like)")]
    [Tooltip("How quickly your horizontal velocity can rotate toward the wish direction while airborne (radians/sec). ~8–16 is strong, CS-like.")]
    public float airTurnRate = 12f;

    [Tooltip("If true, preserve current horizontal speed in air (classic bhop feel). If false, aim for moveSpeed+bhopBonus magnitude.")]
    public bool preserveAirSpeed = true;

    private Rigidbody rb;

    // Input & camera caching (to avoid jitter)
    private Vector2 rawMoveInput;             // from Update()
    private Vector3 camFwdXZ = Vector3.forward;
    private Vector3 camRightXZ = Vector3.right;
    private Vector3 desiredMoveDir;           // built in LateUpdate(), used in FixedUpdate()
    public Vector3 DesiredMoveDir() => desiredMoveDir;
    private bool isGrounded;
    public bool IsGrounded() => isGrounded;
    private bool wasGrounded;
    private bool jumpPressed;
    private float lastJumpPressedTime = -999f;
    public float LastJumpPressedTime() => lastJumpPressedTime;

    // bhop state
    private float groundedSince;
    public float GroundedSince() => groundedSince;
    private bool punishedThisLanding;
    private float bhopBonus; // current stacked bonus speed

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // FPS cursor lock
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (!cameraTransform && Camera.main)
            cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        // Read WASD (no physics here)
        rawMoveInput.x = Input.GetAxisRaw("Horizontal"); // A/D
        rawMoveInput.y = Input.GetAxisRaw("Vertical");   // W/S

        // Jump input (edge or hold-to-jump test mode)
        if (holdToJump)
        {
            if (Input.GetButton("Jump"))
            {
                jumpPressed = true;
                lastJumpPressedTime = Time.time;
            }
        }
        else
        {
            if (Input.GetButtonDown("Jump"))
            {
                jumpPressed = true;
                lastJumpPressedTime = Time.time;
            }
        }

        // Optional quick unlock in editor
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void LateUpdate()
    {
        // After Cinemachine has positioned/rotated the camera this frame
        if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;

        if (cameraTransform)
        {
            // Use yaw-only basis so movement stays horizontal and fully camera-relative
            float yaw = cameraTransform.eulerAngles.y;
            Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
            camFwdXZ = yawRot * Vector3.forward;
            camRightXZ = yawRot * Vector3.right;
        }

        // Build camera-relative move dir using latest camera pose
        desiredMoveDir = (camFwdXZ * rawMoveInput.y + camRightXZ * rawMoveInput.x).normalized;
    }

    void FixedUpdate()
    {
        // --- Ground check ---
        Vector3 probePos = groundCheck ? groundCheck.position : (transform.position + Vector3.down * 0.5f);
        isGrounded = Physics.CheckSphere(probePos, groundCheckRadius, groundLayers, QueryTriggerInteraction.Ignore);

        // Landing state transitions
        if (isGrounded && !wasGrounded)
        {
            groundedSince = Time.time;
            punishedThisLanding = false;
        }
        else if (!isGrounded && wasGrounded)
        {
            punishedThisLanding = false;
        }

        // --- Movement ---
        float effectiveSpeed = moveSpeed + bhopBonus;
        Vector3 v = rb.linearVelocity;
        Vector3 horiz = new Vector3(v.x, 0f, v.z);

        // camera-relative desired move dir from LateUpdate
        Vector3 wishDir = desiredMoveDir; // already normalized (or zero)

        // GROUNDED: snap to desired dir * speed (instant accel is OK on ground)
        if (isGrounded)
        {
            Vector3 targetXZ = wishDir * effectiveSpeed;
            v.x = targetXZ.x;
            v.z = targetXZ.z;
        }
        else
        {
            // AIR: CS-style air control — rotate current horizontal velocity toward wishDir,
            // preserving speed magnitude (optionally), no instant direction sets from input.
            float curSpeed = horiz.magnitude;

            if (wishDir.sqrMagnitude > 1e-6f && curSpeed > 1e-6f)
            {
                // rotate current direction toward wish direction by airTurnRate
                Vector3 curDir = horiz / curSpeed;
                Vector3 newDir = Vector3.RotateTowards(curDir, wishDir, airTurnRate * Time.fixedDeltaTime, 0f);

                float targetMag = preserveAirSpeed ? curSpeed : Mathf.Max(curSpeed, effectiveSpeed);
                Vector3 newHoriz = newDir * targetMag;

                v.x = newHoriz.x;
                v.z = newHoriz.z;
            }
            else if (wishDir.sqrMagnitude > 1e-6f && curSpeed <= 1e-6f)
            {
                // starting from rest midair: take a *small* step in the wish direction (no huge snap)
                Vector3 smallKick = wishDir * Mathf.Min(effectiveSpeed, 0.2f * effectiveSpeed);
                v.x = smallKick.x;
                v.z = smallKick.z;
            }
            else
            {
                // no input midair: keep drifting — do NOT zero horizontal velocity
                // (do nothing)
            }
        }

        rb.linearVelocity = v;


        // --- Jump (fixed, consistent height) ---
        if (isGrounded && (jumpPressed || (Time.time - lastJumpPressedTime) <= bhopWindow))
        {
            bool withinWindow = (Time.time - lastJumpPressedTime) <= bhopWindow;
            bool movingEnough = desiredMoveDir.sqrMagnitude >= (bhopMinMove * bhopMinMove);

            if (withinWindow && movingEnough)
            {
                // Successful bhop: stack bonus, clamp
                bhopBonus = Mathf.Min(bhopBonus + bhopBonusPerHop, bhopMaxBonus);
                punishedThisLanding = true;
            }
            else
            {
                // Missed timing or standing still: reset to base speed
                HardResetToBase();
                punishedThisLanding = true;
            }

            // Clear vertical velocity and apply a fixed jump impulse
            Vector3 vel = rb.linearVelocity;
            vel.y = 0f;
            rb.linearVelocity = vel;
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
        jumpPressed = false;

        // If we land and let the window expire without jumping, penalize once
        if (isGrounded && !punishedThisLanding && (Time.time - groundedSince) > bhopWindow)
        {
            if (bhopBonus > 0f)
                HardResetToBase();
            punishedThisLanding = true;
        }

        // --- Decay mechanics ---
        if (bhopBonus > 0f)
        {
            // Grounded decay scales with horizontal speed
            if (isGrounded)
            {
                float horizSpeed = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;
                float totalGroundDecay = (bhopBaseDecay + horizSpeed * bhopSpeedDecayFactor) * Time.fixedDeltaTime;
                bhopBonus = Mathf.Max(0f, bhopBonus - totalGroundDecay);
            }

            // Constant decay always on
            bhopBonus = Mathf.Max(0f, bhopBonus - constantDecayPerSecond * Time.fixedDeltaTime);
        }

        wasGrounded = isGrounded;
    }


    private void HardResetToBase()
    {
        bhopBonus = 0f;
        // If horizontal speed exceeds base speed, clamp it down
        Vector3 vel = rb.linearVelocity;
        Vector3 horiz = new Vector3(vel.x, 0f, vel.z);
        if (horiz.magnitude > moveSpeed)
        {
            horiz = horiz.normalized * moveSpeed;
            rb.linearVelocity = new Vector3(horiz.x, vel.y, horiz.z);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (groundCheck)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
#endif
}
