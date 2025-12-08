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

    [Header("Scoring")]
    [Tooltip("Points awarded when killing an Enemy-tagged object.")]
    public int enemyKillPoints = 100;

    [Tooltip("Points awarded when killing a Fleer-tagged object.")]
    public int fleerKillPoints = 150;

    [Tooltip("Points awarded when hitting a generic damageable (if you want per-hit score).")]
    public int damageHitPoints = 10;

    [Header("Audio (Gunshot)")]
    [Tooltip("Gunshot audio clips to randomly play when firing")]
    public AudioClip[] gunshotClips;
    [Tooltip("Volume of the gunshot sound")]
    [Range(0f,1f)] public float gunshotVolume = 1f;
    [Tooltip("Max distance at which the gunshot sound can be heard")]
    public float gunshotMaxDistance = 50f;
    [Tooltip("Rolloff mode for gunshot sound attenuation")]
    public AudioRolloffMode gunshotRolloffMode = AudioRolloffMode.Logarithmic;

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

    [Header("Ammo")]
    [Tooltip("Optional ammo system; if assigned, firing consumes ammo and blocks at zero.")]
    public AmmoSystem ammoSystem;
    [Tooltip("Ammo consumed per shot.")]
    public int ammoPerShot = 1;

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
            // Block if ammo system exists and has no ammo
            if (ammoSystem != null)
            {
                if (!ammoSystem.CanConsume(ammoPerShot))
                {
                    // No ammo: do not play slide, recoil, tracer, or muzzle flash
                    return;
                }
                ammoSystem.Consume(ammoPerShot);
            }

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
    [Tooltip("If true, parent spawned holes to the hit collider so they follow moving objects. If false, holes are left unparented and use absolute world size.")]
    public bool bulletHoleParentToTarget = false;
    [Header("Impact FX")]
    [Tooltip("Optional prefab (particle system/decal) that spawns at the same point as the bullet hole.")]
    public GameObject bulletImpactPrefab;
    [Tooltip("Seconds before the impact effect auto-destroys (<=0 means keep).")]
    public float bulletImpactLifetime = 1.5f;

    [Header("Live Aim Dot")]
    [Tooltip("Spawner that will render a UI dot at the predicted impact point on screen.")]
    public ScreenDotSpawner screenDotSpawner;
    [Tooltip("Enable a live 2D dot that shows where the current shot will hit (accounts for recoil/weapon pose).")]
    public bool showLiveAimDot = false;

    [Header("Muzzle FX")]
    [Tooltip("Prefab for the muzzle flash particle/object that should spawn at the barrel/muzzle when firing.")]
    public GameObject muzzleFlashPrefab;
    [Tooltip("Seconds before muzzle flash object auto-destroys (<=0 means keep)")]
    public float muzzleFlashLifetime = 0.6f;
    [Tooltip("Offset forward from the muzzle transform along its forward vector to place the flash (meters).")]
    public float muzzleFlashForwardOffset = 0.04f;
    [Tooltip("If true, parent the spawned muzzle flash to the muzzle transform (keeps it attached to moving gun).")]
    public bool parentMuzzleFlashToMuzzle = false;
    [Header("Tracer FX")]
    [Tooltip("Prefab that holds a BulletTracer component (LineRenderer or trail that visualizes the shot path).")]
    public BulletTracer tracerPrefab;
    [Tooltip("If > 0, overrides the tracer prefab's lifetime in seconds.")]
    public float tracerLifetimeOverride = -1f;
    [Tooltip("Small offset applied along the muzzle forward so the tracer does not start inside the barrel.")]
    public float tracerSpawnForwardOffset = 0.01f;
    [Tooltip("Uniform scale applied to the tracer's trail width for this weapon.")]
    public float tracerWidthMultiplier = 1f;

    public void Fire()
    {
        // Play gunshot sound
        PlayGunshotSound();

        if (useSlide && slide) { slidePlaying = true; slideT = 0f; }

        // recoil impulse (affects WHOLE arms now)
        float k = Mathf.Lerp(1f, aimRecoilMultiplier, aimWeight); // 1 -> aimRecoilMultiplier when aiming
        recoilPos += Vector3.back * recoilKick * k;
        recoilAngles += recoilYawPitch * k;


        if (cameraPitch) cameraPitch.localRotation *= Quaternion.Euler(-cameraKick, 0f, 0f);
        // FX hooks here

        // --- HITSCAN / BULLET HOLES ---
        TryHitscan(out Ray shotRay, out Vector3 tracerEndPoint);

        // --- TRACER ---
        SpawnTracer(shotRay, tracerEndPoint);

        // --- MUZZLE FLASH ---
        if (muzzle != null && muzzleFlashPrefab != null)
        {
            // Position slightly in front of the muzzle to avoid z-fighting with the barrel
            Vector3 fxPos = muzzle.position + muzzle.forward * muzzleFlashForwardOffset;
            Quaternion fxRot = Quaternion.LookRotation(muzzle.forward, muzzle.up);

            GameObject fx = Instantiate(muzzleFlashPrefab, fxPos, fxRot);

            if (parentMuzzleFlashToMuzzle)
                fx.transform.SetParent(muzzle, true);

            if (muzzleFlashLifetime > 0f)
                Destroy(fx, muzzleFlashLifetime);
        }

        // Add an extra local kick on the weapon for a richer feel
        if (weaponLocalRecoil)
        {
            float kw = Mathf.Lerp(1f, aimRecoilMultiplier, aimWeight);
            weaponRecoilPos    += Vector3.back * weaponKick * kw;
            weaponRecoilAngles += weaponYawPitch * kw;
        }
    }

    void SpawnTracer(Ray shotRay, Vector3 tracerEndPoint)
    {
        if (tracerPrefab == null) return;

        Vector3 start = shotRay.origin;
        if (muzzle != null)
        {
            start = muzzle.position + muzzle.forward * tracerSpawnForwardOffset;
        }

        BulletTracer tracer = Instantiate(tracerPrefab, start, Quaternion.identity);
        tracer.Initialize(start, tracerEndPoint, tracerLifetimeOverride, tracerWidthMultiplier);
    }

    bool TryHitscan(out Ray shotRay, out Vector3 tracerEndPoint)
    {
        if (muzzle != null)
        {
            shotRay = new Ray(muzzle.position, muzzle.forward);
        }
        else
        {
            Camera cam = shootCamera != null ? shootCamera : Camera.main;
            if (cam == null)
            {
                Transform originSource = weaponRoot != null ? weaponRoot : transform;
                Vector3 dir = originSource.forward.sqrMagnitude > 0f ? originSource.forward : Vector3.forward;
                shotRay = new Ray(originSource.position, dir);
                tracerEndPoint = shotRay.origin + shotRay.direction * hitscanRange;
                return false; // no camera available, so skip hitscan but still build a ray for tracer
            }
            Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            shotRay = cam.ScreenPointToRay(screenCenter);
        }

        tracerEndPoint = shotRay.origin + shotRay.direction * hitscanRange;

        if (Physics.Raycast(shotRay, out RaycastHit hit, hitscanRange, hitMask, QueryTriggerInteraction.Ignore)){
            tracerEndPoint = hit.point;

            if (hit.collider.CompareTag("Enemy") || hit.collider.CompareTag("Fleer"))
            {
                if (ScoreManager.Instance != null)
                {
                    int points =
                        hit.collider.CompareTag("Enemy")
                        ? enemyKillPoints
                        : fleerKillPoints;

                    ScoreManager.Instance.AddPoints(points);
                }

                // If this was an enemy ball, trigger its explosion (no player effect when shot)
                if (hit.collider.CompareTag("Enemy"))
                {
                    var enemyBall = hit.collider.GetComponentInParent<EnemyBall>();
                    if (enemyBall != null)
                    {
                        enemyBall.TriggerExplosion(ignorePlayerEffect: true);
                        return true;
                    }
                }

                // If this was a fleer ball, trigger its explosion
                if (hit.collider.CompareTag("Fleer"))
                {
                    var fleeBall = hit.collider.GetComponentInParent<FleeBall>();
                    if (fleeBall != null)
                    {
                        fleeBall.TriggerExplosion();
                        return true;
                    }
                }

                // Default behavior for non-exploding enemies/fleers
                Destroy(hit.collider.gameObject);
                return true;
            }

            if (hit.rigidbody)
            {
                hit.rigidbody.AddForceAtPosition(shotRay.direction * impactImpulse, hit.point, ForceMode.Impulse);
            }

            var damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.ApplyDamage(damage, hit);

                if (ScoreManager.Instance != null && damageHitPoints > 0)
                {
                    ScoreManager.Instance.AddPoints(damageHitPoints);
                }
            }

            // Spawn bullet hole
            if (bulletHolePrefab != null)
            {
                Vector3 pos = hit.point + hit.normal * Mathf.Max(0f, bulletHoleSurfaceOffset);
                // Face the surface: front side points toward the ray origin
                Quaternion rot = Quaternion.LookRotation(-hit.normal, Vector3.up);

                GameObject hole = Instantiate(bulletHolePrefab, pos, rot);

                // Randomize rotation around normal so holes don't look identical
                hole.transform.Rotate(hit.normal, Random.Range(0f, 360f), Space.World);

                // If user wants absolute-size holes that are NOT affected by the hit object's scale,
                // do not parent the hole and set its world scale directly.
                if (!bulletHoleParentToTarget)
                {
                    // When unparented, localScale == world scale, so set localScale to desired world size
                    if (bulletHoleScale > 0f)
                        hole.transform.localScale = Vector3.one * bulletHoleScale;
                }
                else
                {
                    // Parent to the hit object so it follows moving targets, but compensate for parent scale
                    hole.transform.SetParent(hit.collider.transform, true);

                    if (bulletHoleScale > 0f)
                    {
                        // Compute a uniform scale that preserves circular shape in world space by using
                        // the largest absolute component of the parent's lossyScale.
                        Vector3 parentScale = hit.collider.transform.lossyScale;
                        float maxParent = Mathf.Max(Mathf.Max(Mathf.Abs(parentScale.x), Mathf.Abs(parentScale.y)), Mathf.Abs(parentScale.z));
                        float uniformScale = (maxParent > 0f) ? (bulletHoleScale / maxParent) : bulletHoleScale;
                        hole.transform.localScale = Vector3.one * uniformScale;
                    }
                }

                if (bulletHoleLifetime > 0f)
                {
                    Destroy(hole, bulletHoleLifetime);
                }

                if (bulletImpactPrefab != null)
                {
                    Quaternion impactRot = rot * Quaternion.Euler(0f, 180f, 0f);
                    GameObject impact = Instantiate(bulletImpactPrefab, pos, impactRot);
                    impact.transform.Rotate(hit.normal, Random.Range(0f, 360f), Space.World);

                    if (bulletImpactLifetime > 0f)
                    {
                        Destroy(impact, bulletImpactLifetime);
                    }
                }
            }

            return true;
        }
        return false;
    }

    void PlayGunshotSound()
    {
        AudioClip clip = null;
        if (gunshotClips != null && gunshotClips.Length > 0)
        {
            int idx = Random.Range(0, gunshotClips.Length);
            clip = gunshotClips[idx];
        }
        if (clip == null) return;
        
        // Use muzzle position if available, otherwise use this transform
        Vector3 soundPos = muzzle != null ? muzzle.position : transform.position;
        
        // Create temporary AudioSource for 3D positioned gunshot
        var go = new GameObject("GunshotAudio");
        go.transform.position = soundPos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = gunshotVolume;
        src.spatialBlend = 1f; // 3D
        src.minDistance = 1f;
        src.maxDistance = Mathf.Max(1f, gunshotMaxDistance);
        src.rolloffMode = gunshotRolloffMode;
        src.playOnAwake = false;
        src.loop = false;
        src.Stop();
        src.Play();
        Destroy(go, clip.length + 0.1f);
    }
}
