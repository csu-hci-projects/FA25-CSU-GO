using UnityEngine;

public class FpsGunShootAnim : MonoBehaviour
{
    [Header("Refs")]
    public Transform recoilTarget;   // <- NEW: the thing that should move the WHOLE rig (set to Arms)
    public Transform weaponRoot;     // gun parent under the wrist (kept for slide offsets)
    public Transform slide;          // Pistol3_01_2

    [Header("Fire control")]
    public bool automatic = false;     // already there
    public float fireRate = 8f;        // shots per second (SPS)
    public bool requireReleaseInSemi = true; // blocks autoclick in semi

    private bool triggerLocked; // semi-auto: must release before next shot

    [Header("Slide")]
    public bool useSlide = true;
    public float slideTravel = 0.035f;
    public Vector3 slideAxis = Vector3.back;
    public float slideBackTime = 0.04f;
    public float slideReturnTime = 0.07f;
    public AnimationCurve slideCurve = AnimationCurve.EaseInOut(0,0,1,1);

    [Header("Recoil (applied to recoilTarget)")]
    public float recoilKick = 0.045f;            // meters back
    public Vector2 recoilYawPitch = new(0.0f, -2.0f); // X=yaw, Y=pitch (set X=0 to avoid sideways drift)
    public float recoilSnap = 18f;
    public float recoilReturn = 9f;
    public float maxPitch = 12f;

    [Header("Recoil sharpness")]
    public float returnSharpness = 24f;  // higher = snappier return to zero
    public float snapSharpness   = 32f;  // higher = chase target faster

    [Header("Optional camera kick")]
    public Transform cameraPitch;
    public float cameraKick = 0.6f;
    public float cameraReturn = 12f;

    [HideInInspector] public float aimWeight = 0f; // 0..1, set by AdsController
    [Header("Aim tuning")]
    [Range(0f,1f)]
    public float aimRecoilMultiplier = 0.6f; // how much recoil remains when fully aimed

    // homes
    Vector3 rtPosHome; Quaternion rtRotHome;   // recoilTarget home
    Vector3 wrPosHome; Quaternion wrRotHome;   // weaponRoot home (for slide only)
    Vector3 slideHome;

    // state
    float nextFire;
    bool slidePlaying; float slideT;
    Vector3 recoilPos;        // local positional offset (for recoilTarget)
    Vector2 recoilAngles;     // yaw(x)/pitch(y) degrees (for recoilTarget)

    [Header("Weapon local recoil (additive)")]
    [Tooltip("Optional child under WeaponRoot used for additive local recoil (so ADS can stay absolute on WeaponRoot).")]
    public Transform weaponLocalRecoil;
    public float weaponKick = 0.012f;
    public Vector2 weaponYawPitch = new(0.35f, -0.9f);
    public float weaponReturnSharpness = 28f;
    public float weaponSnapSharpness = 36f;
    Vector3 wlrPosHome; Quaternion wlrRotHome;
    Vector3 weaponRecoilPos; Vector2 weaponRecoilAngles;
    Vector3 wlrTargetPos; Quaternion wlrTargetRot;

    void Awake()
    {
        if (!recoilTarget) recoilTarget = weaponRoot; // fallback if not set
        // If recoilTarget points to the same transform AdsController drives (weaponRoot),
        // use the parent so ADS absolute pose doesn't overwrite recoil.
        if (recoilTarget == weaponRoot && weaponRoot && weaponRoot.parent)
        {
            Debug.LogWarning("FpsGunShootAnim: recoilTarget == weaponRoot; using parent for recoil so ADS can be absolute without killing recoil.");
            recoilTarget = weaponRoot.parent;
        }

        rtPosHome = recoilTarget.localPosition;
        rtRotHome = recoilTarget.localRotation;

        if (weaponRoot)
        {
            wrPosHome = weaponRoot.localPosition;
            wrRotHome = weaponRoot.localRotation;
        }
        if (slide) slideHome = slide.localPosition;

        if (weaponLocalRecoil)
        {
            wlrPosHome = weaponLocalRecoil.localPosition;
            wlrRotHome = weaponLocalRecoil.localRotation;
        }
    }

    void Update()
    {
        // --- FIRE INPUT & RATE LIMIT ---
        bool pressed = automatic ? Input.GetMouseButton(0)
                                : Input.GetMouseButtonDown(0);

        // In semi-auto, require a release between shots (prevents autoclick spam)
        if (!automatic && requireReleaseInSemi)
        {
            // lock trigger immediately after a shot; unlock only when button is released
            if (Input.GetMouseButtonUp(0)) triggerLocked = false;
            if (triggerLocked) pressed = false;
        }

        // time gate: max shots per second = fireRate
        float minDelay = 1f / Mathf.Max(0.0001f, fireRate);
        bool canShootNow = Time.time >= nextFire;

        if (pressed && canShootNow)
        {
            Fire();
            nextFire = Time.time + minDelay;

            // lock the trigger until released (semi only)
            if (!automatic && requireReleaseInSemi)
                triggerLocked = true;
        }


        // --- SLIDE (runs relative to weaponRoot home, unaffected by arm recoil) ---
        if (useSlide && slide && slidePlaying)
        {
            float total = slideBackTime + slideReturnTime;
            slideT += Time.deltaTime / total;
            if (slideT >= 1f) { slideT = 1f; slidePlaying = false; }

            float backPhase = slideBackTime / total;
            float t = slideT <= backPhase
                ? slideCurve.Evaluate(slideT / backPhase)
                : 1f - slideCurve.Evaluate((slideT - backPhase) / (1f - backPhase));

            slide.localPosition = slideHome + slideAxis.normalized * (slideTravel * t);
        }
        else if (slide)
        {
            slide.localPosition = Vector3.Lerp(slide.localPosition, slideHome, Time.deltaTime * 20f);
        }

        // --- RECOIL (apply to WHOLE ARMS via recoilTarget) ---
        // --- RECOIL (apply to WHOLE ARMS via recoilTarget) ---
        float dt = Time.deltaTime;

        // framerate-independent exponential smoothing factors
        float ret  = 1f - Mathf.Exp(-returnSharpness * dt); // toward zero
        float snap = 1f - Mathf.Exp(-snapSharpness   * dt); // toward target

        // drive state back to zero (snappy)
        recoilPos    = Vector3.Lerp(recoilPos,    Vector3.zero, ret);
        recoilAngles = Vector2.Lerp(recoilAngles, Vector2.zero, ret);
        recoilAngles.y = Mathf.Clamp(recoilAngles.y, -maxPitch, maxPitch);

        // build target from state
        Vector3 rtTargetPos = rtPosHome + recoilPos;
        Quaternion rtTargetRot =
            Quaternion.Euler(recoilAngles.y, 0f, 0f) *
            Quaternion.Euler(0f, -recoilAngles.x, 0f) *
            rtRotHome;

        // snap transforms toward target (snappy)
        recoilTarget.localPosition = Vector3.Lerp(recoilTarget.localPosition, rtTargetPos, snap);
        recoilTarget.localRotation = Quaternion.Slerp(recoilTarget.localRotation, rtTargetRot, snap);

        // optional camera relax
        if (cameraPitch)
        {
            Vector3 e = cameraPitch.localEulerAngles;
            e.x = (e.x > 180f) ? e.x - 360f : e.x;
            e.x = Mathf.Lerp(e.x, 0f, Time.deltaTime * cameraReturn);
            cameraPitch.localEulerAngles = new Vector3(e.x, 0f, 0f);
        }

        // --- WEAPON LOCAL RECOIL (compute target; apply in LateUpdate so it layers after ADS) ---
        if (weaponLocalRecoil)
        {
            float retW  = 1f - Mathf.Exp(-weaponReturnSharpness * dt);
            float snapW = 1f - Mathf.Exp(-weaponSnapSharpness   * dt);

            weaponRecoilPos    = Vector3.Lerp(weaponRecoilPos,    Vector3.zero, retW);
            weaponRecoilAngles = Vector2.Lerp(weaponRecoilAngles, Vector2.zero, retW);

            Vector3 addPos = weaponRecoilPos;
            Quaternion addRot =
                Quaternion.Euler(weaponRecoilAngles.y, 0f, 0f) *
                Quaternion.Euler(0f, -weaponRecoilAngles.x, 0f);

            // Build targets relative to the local recoil node home
            wlrTargetPos = wlrPosHome + addPos;
            wlrTargetRot = addRot * wlrRotHome;

            // Optionally pre-snap a bit in Update to reduce visible lag if LateUpdate order changes
            weaponLocalRecoil.localPosition = Vector3.Lerp(weaponLocalRecoil.localPosition, wlrTargetPos, snapW * 0.5f);
            weaponLocalRecoil.localRotation = Quaternion.Slerp(weaponLocalRecoil.localRotation, wlrTargetRot, snapW * 0.5f);
        }
    }

    public void Fire()
    {
        if (useSlide && slide) { slidePlaying = true; slideT = 0f; }

        // recoil impulse (affects WHOLE arms now)
        float k = Mathf.Lerp(1f, aimRecoilMultiplier, aimWeight); // 1 -> aimRecoilMultiplier when aiming
        recoilPos += Vector3.back * recoilKick * k;
        recoilAngles += recoilYawPitch * k;


        if (cameraPitch) cameraPitch.localRotation *= Quaternion.Euler(-cameraKick, 0f, 0f);
        // FX hooks here

        // --- HITSCAN / BULLET HOLES ---
        TryHitscan();

        // Add an extra local kick on the weapon for a richer feel
        if (weaponLocalRecoil)
        {
            float kw = Mathf.Lerp(1f, aimRecoilMultiplier, aimWeight);
            weaponRecoilPos    += Vector3.back * weaponKick * kw;
            weaponRecoilAngles += weaponYawPitch * kw;
        }
    }

    [Header("Hitscan")]
    [Tooltip("Camera used for screen-center raycasting. If empty, falls back to Camera.main")] public Camera shootCamera;
    [Tooltip("Barrel/muzzle tip; if set, rays are cast from here using its forward direction")] public Transform muzzle;
    [Tooltip("Max ray distance")] public float hitscanRange = 200f;
    [Tooltip("Damage applied to IDamageable targets")] public float damage = 10f;
    [Tooltip("Physics impulse applied to rigidbodies along shot direction")] public float impactImpulse = 4f;
    [Tooltip("Layers the hitscan should collide with")] public LayerMask hitMask = ~0;
    [Header("Bullet Hole")]
    [Tooltip("Prefab of a small quad/decal aligned to the surface normal")] public GameObject bulletHolePrefab;
    [Tooltip("Seconds before hole auto-destroys (<=0 means keep)")] public float bulletHoleLifetime = 20f;
    [Tooltip("Local uniform scale for spawned bullet hole")] public float bulletHoleScale = 1f;
    [Tooltip("Offset along normal to avoid z-fighting")] public float bulletHoleSurfaceOffset = 0.002f;

    void TryHitscan()
    {
        Ray ray;
        if (muzzle != null)
        {
            ray = new Ray(muzzle.position, muzzle.forward);
        }
        else
        {
            Camera cam = shootCamera != null ? shootCamera : Camera.main;
            if (cam == null) return; // no camera available
            Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            ray = cam.ScreenPointToRay(screenCenter);
        }

        if (Physics.Raycast(ray, out RaycastHit hit, hitscanRange, hitMask, QueryTriggerInteraction.Ignore))
        {
            // Apply physics impulse
            if (hit.rigidbody)
            {
                hit.rigidbody.AddForceAtPosition(ray.direction * impactImpulse, hit.point, ForceMode.Impulse);
            }

            // Apply damage if the target supports it
            var damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.ApplyDamage(damage, hit);
            }

            // Spawn bullet hole
            if (bulletHolePrefab != null)
            {
                Vector3 pos = hit.point + hit.normal * Mathf.Max(0f, bulletHoleSurfaceOffset);
                // Face the shooter: flip so the quad's front side looks toward the ray origin
                Quaternion rot = Quaternion.LookRotation(-hit.normal, Vector3.up);

                GameObject hole = Instantiate(bulletHolePrefab, pos, rot);

                // Randomize rotation around normal so holes don't all look identical
                hole.transform.Rotate(Vector3.forward, Random.Range(0f, 360f), Space.Self);

                // Parent to the hit object so it follows moving targets
                hole.transform.SetParent(hit.collider.transform, true);

                if (bulletHoleScale > 0f)
                {
                    hole.transform.localScale = Vector3.one * bulletHoleScale;
                }

                if (bulletHoleLifetime > 0f)
                {
                    Destroy(hole, bulletHoleLifetime);
                }
            }
        }
    }

    void LateUpdate()
    {
        // Apply local weapon recoil after ADS likely set WeaponRoot in its LateUpdate
        if (weaponLocalRecoil)
        {
            weaponLocalRecoil.localPosition = wlrTargetPos;
            weaponLocalRecoil.localRotation = wlrTargetRot;
        }
    }
}
