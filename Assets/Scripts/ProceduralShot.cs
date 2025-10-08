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

    void Awake()
    {
        if (!recoilTarget) recoilTarget = weaponRoot; // fallback if not set

        rtPosHome = recoilTarget.localPosition;
        rtRotHome = recoilTarget.localRotation;

        if (weaponRoot)
        {
            wrPosHome = weaponRoot.localPosition;
            wrRotHome = weaponRoot.localRotation;
        }
        if (slide) slideHome = slide.localPosition;
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
    }
}
