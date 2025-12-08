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
    [Tooltip("Layers considered obstacles for immediate movement blocking checks.")]
    public LayerMask obstacleMask = ~0;
    [Tooltip("Distance to check in front of the player for an immediate obstacle that should prevent snapping to full speed (meters).")]
    public float obstacleCheckDistance = 0.35f;

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
    [Tooltip("Grace period after landing before clamping to base speed (seconds).")]
    public float clampGracePeriod = 0.3f;

    [Header("Audio (Jump & Landing)")]
    [Tooltip("Jump audio clips to randomly play when jumping")]
    public AudioClip[] jumpClips;
    [Tooltip("Volume of the jump sound")]
    [Range(0f,1f)] public float jumpVolume = 1f;
    [Tooltip("Landing audio clips to randomly play when landing")]
    public AudioClip[] landingClips;
    [Tooltip("Volume of the landing sound")]
    [Range(0f,1f)] public float landingVolume = 1f;
    [Tooltip("Max distance at which jump/landing sounds can be heard")]
    public float audioMaxDistance = 20f;
    [Tooltip("Rolloff mode for jump/landing sound attenuation")]
    public AudioRolloffMode audioRolloffMode = AudioRolloffMode.Logarithmic;

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

    // Audio debouncing
    private float lastJumpSoundTime = -999f;
    private float lastLandingSoundTime = -999f;
    private const float SOUND_COOLDOWN = 0.1f; // minimum time between same sound type

    [Header("External Forces")]
    [Tooltip("Decay rate for externally applied impulses (per second, higher = fade faster).")]
    public float externalImpulseDecay = 6f;
    [Tooltip("Clamp for horizontal magnitude contributed by external impulses (optional, 0 = no clamp).")]
    public float externalHorizontalClamp = 0f;
    private Vector3 externalImpulse; // added to velocity each FixedUpdate, decays over time
    private float externalLockUntil; // window where we avoid instant snap-up after big impulses
    [Tooltip("Time window after external impulse where ground movement won't instantly boost speed (seconds)")]
    public float externalImpulseLockDuration = 0.3f;

    // Allow other scripts (e.g., explosions) to add impulses that the movement system will respect
    public void ApplyExternalImpulse(Vector3 impulse)
    {
        externalImpulse += impulse;
        // After significant impulse, lock ground snapping for a short duration
        float horizMag = new Vector3(impulse.x, 0f, impulse.z).magnitude;
        if (horizMag > 0.25f)
        {
            externalLockUntil = Time.time + externalImpulseLockDuration;
        }
    }

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
            
            // Play landing sound
            PlayLandingSound();
        }
        else if (!isGrounded && wasGrounded)
        {
            punishedThisLanding = false;
        }

        // --- Apply external impulse FIRST ---
        Vector3 ext = externalImpulse;
        if (externalHorizontalClamp > 0f)
        {
            Vector3 extHoriz = new Vector3(ext.x, 0f, ext.z);
            float mag = extHoriz.magnitude;
            if (mag > externalHorizontalClamp)
            {
                Vector3 clamped = extHoriz.normalized * externalHorizontalClamp;
                ext.x = clamped.x; ext.z = clamped.z;
            }
        }

        // --- Movement ---
        float effectiveSpeed = moveSpeed + bhopBonus;
        Vector3 v = rb.linearVelocity;
        v += ext; // add external impulse to current velocity before movement logic
        Vector3 horiz = new Vector3(v.x, 0f, v.z);

        // camera-relative desired move dir from LateUpdate
        Vector3 wishDir = desiredMoveDir; // already normalized (or zero)

        // Check if external forces are significant enough to override grounded snapping
        float extHorizMag = new Vector3(ext.x, 0f, ext.z).magnitude;
        bool hasSignificantExternalForce = extHorizMag > 0.5f;

        // GROUNDED: normally snap to desired dir * speed, but respect recent external impulses
        if (isGrounded && !hasSignificantExternalForce)
        {
            Vector3 targetXZ = wishDir * effectiveSpeed;

            // If there's an immediate obstacle in the wish direction, avoid snapping _up_ to full speed
            // because a collision may have reduced current horizontal velocity. In that case, cap
            // the target magnitude to the current horizontal magnitude so we don't magically push through.
            if (wishDir.sqrMagnitude > 1e-6f)
            {
                // perform a short raycast to detect blocking geometry in movement direction
                Vector3 rayOrigin = transform.position + Vector3.up * 0.1f; // small offset so ground colliders aren't hit
                RaycastHit obsHit;
                bool blocked = Physics.Raycast(rayOrigin, wishDir, out obsHit, obstacleCheckDistance, obstacleMask, QueryTriggerInteraction.Ignore);

                bool lockBoost = Time.time < externalLockUntil; // avoid instant boost after explosions
                if (blocked)
                {
                    Vector3 curHoriz = new Vector3(v.x, 0f, v.z);
                    float curMag = curHoriz.magnitude;
                    // Do not increase speed when blocked; allow reduction or maintain current speed
                    float cappedMag = Mathf.Min(curMag, effectiveSpeed);
                    Vector3 cappedTarget = wishDir * cappedMag;
                    v.x = cappedTarget.x;
                    v.z = cappedTarget.z;
                }
                else
                {
                    if (lockBoost)
                    {
                        // Steer direction without increasing magnitude above current
                        Vector3 curHoriz = new Vector3(v.x, 0f, v.z);
                        float curMag = curHoriz.magnitude;
                        float cappedMag = Mathf.Min(curMag, effectiveSpeed);
                        Vector3 cappedTarget = wishDir * cappedMag;
                        v.x = cappedTarget.x;
                        v.z = cappedTarget.z;
                    }
                    else
                    {
                        v.x = targetXZ.x;
                        v.z = targetXZ.z;
                    }
                }
            }
            else
            {
                // no input: come to rest horizontally
                v.x = targetXZ.x;
                v.z = targetXZ.z;
            }
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

        // Exponential decay toward zero so the player quickly regains full control
        if (externalImpulseDecay > 0f)
        {
            float k = 1f - Mathf.Exp(-externalImpulseDecay * Time.fixedDeltaTime);
            externalImpulse = Vector3.Lerp(externalImpulse, Vector3.zero, k);
        }


        // --- Jump (fixed, consistent height) ---
        if (isGrounded && (jumpPressed || (Time.time - lastJumpPressedTime) <= bhopWindow))
        {
            bool withinWindow = (Time.time - lastJumpPressedTime) <= bhopWindow;
            bool movingEnough = desiredMoveDir.sqrMagnitude >= (bhopMinMove * bhopMinMove);
            bool lockBoost = Time.time < externalLockUntil; // explosion/impulse recovery window

            if (withinWindow && movingEnough)
            {
                // Successful bhop: stack bonus, clamp
                // During external recovery, limit bonus so speed doesn't instantly restore
                float bonusAdd = lockBoost ? (bhopBonusPerHop * 0.33f) : bhopBonusPerHop;
                bhopBonus = Mathf.Min(bhopBonus + bonusAdd, bhopMaxBonus);
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
            
            // Play jump sound
            PlayJumpSound();

            // During recovery, gently cap horizontal speed after jump so it's reduced but not erased
            if (lockBoost)
            {
                Vector3 hv = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                float curMag = hv.magnitude;
                // Target cap is a blend toward base speed rather than full base snap
                float cap = Mathf.Lerp(curMag, moveSpeed, 0.35f);
                if (curMag > cap && cap > 0f)
                {
                    Vector3 capped = hv.normalized * cap;
                    rb.linearVelocity = new Vector3(capped.x, rb.linearVelocity.y, capped.z);
                }
            }
        }
        jumpPressed = false;

        // If we land and let the window expire without jumping, penalize once
        // Only mark as punished when we actually clamp (after grace period)
        if (isGrounded && (Time.time - groundedSince) > bhopWindow)
        {
            float timeGrounded = Time.time - groundedSince;
            if (!punishedThisLanding && timeGrounded > clampGracePeriod)
            {
                if (bhopBonus > 0f)
                {
                    HardResetToBase();
                }
                punishedThisLanding = true;
            }
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
        // Respect recent external impulses: only clamp if clearly above base and outside lock window
        bool lockBoost = Time.time < externalLockUntil;
        if (!lockBoost && horiz.magnitude > moveSpeed)
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

    void PlayJumpSound()
    {
        // Debounce: prevent multiple jump sounds in quick succession
        if (Time.time - lastJumpSoundTime < SOUND_COOLDOWN) return;
        lastJumpSoundTime = Time.time;
        
        AudioClip clip = null;
        if (jumpClips != null && jumpClips.Length > 0)
        {
            int idx = Random.Range(0, jumpClips.Length);
            clip = jumpClips[idx];
        }
        if (clip == null) return;
        
        // Create temporary AudioSource for 3D positioned audio
        var go = new GameObject("JumpAudio");
        go.transform.position = transform.position;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = jumpVolume;
        src.spatialBlend = 1f; // 3D
        src.minDistance = 1f;
        src.maxDistance = Mathf.Max(1f, audioMaxDistance);
        src.rolloffMode = audioRolloffMode;
        src.playOnAwake = false;
        src.loop = false;
        src.Stop();
        src.Play();
        Destroy(go, clip.length + 0.1f);
    }

    void PlayLandingSound()
    {
        // Debounce: prevent multiple landing sounds in quick succession
        if (Time.time - lastLandingSoundTime < SOUND_COOLDOWN) return;
        lastLandingSoundTime = Time.time;
        
        AudioClip clip = null;
        if (landingClips != null && landingClips.Length > 0)
        {
            int idx = Random.Range(0, landingClips.Length);
            clip = landingClips[idx];
        }
        if (clip == null) return;
        
        // Create temporary AudioSource for 3D positioned audio
        var go = new GameObject("LandingAudio");
        go.transform.position = transform.position;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = landingVolume;
        src.spatialBlend = 1f; // 3D
        src.minDistance = 1f;
        src.maxDistance = Mathf.Max(1f, audioMaxDistance);
        src.rolloffMode = audioRolloffMode;
        src.playOnAwake = false;
        src.loop = false;
        src.Stop();
        src.Play();
        Destroy(go, clip.length + 0.1f);
    }
}
