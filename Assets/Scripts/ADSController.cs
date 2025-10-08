using UnityEngine;

[DisallowMultipleComponent]
public class AdsController : MonoBehaviour
{
    [Header("Refs")]
    public Transform weaponRoot;      // same WeaponRoot you use in your shoot script
    public Camera viewCamera;         // main gameplay camera (drag FP camera)
    public MonoBehaviour fpsShootScript; // optional: reference to FpsGunShootAnim to pass aimWeight

    [Header("Input")]
    public KeyCode aimKey = KeyCode.Mouse1;
    public bool holdToAim = true;     // hold or toggle

    [Header("Aim pose (local to weaponRoot)")]
    public Vector3 aimLocalPosition = new Vector3(0f, -0.02f, -0.05f);
    public Vector3 aimLocalEuler = new Vector3(0f, 0f, 0f); // local rotation offset in degrees

    [Header("FOV")]
    public float hipFOV = 60f;
    public float aimFOV = 40f;

    [Header("Smoothing")]
    public float aimSpeed = 12f;      // higher = snappier

    // optional recoil reduction
    [Range(0f,1f)]
    public float aimRecoilMultiplier = 0.6f; // 0 = no recoil, 1 = full recoil

    // internals
    Vector3 wrPosHome;
    Quaternion wrRotHome;
    float currentWeight; // 0..1

    Camera cam;

    void Awake()
    {
        if (!weaponRoot) Debug.LogWarning("AdsController: weaponRoot not set.");
        if (!viewCamera) cam = Camera.main; else cam = viewCamera;

        if (cam) hipFOV = cam.fieldOfView;

        if (weaponRoot)
        {
            wrPosHome = weaponRoot.localPosition;
            wrRotHome = weaponRoot.localRotation;
        }
    }

    void Update()
    {
        // input
        bool aiming = false;
        if (holdToAim)
        {
            aiming = Input.GetKey(aimKey);
        }
        else
        {
            // simple toggle (press to flip)
            if (Input.GetKeyDown(aimKey)) currentWeight = (currentWeight > 0.5f) ? 0f : 1f; // quick toggle
            aiming = currentWeight > 0.5f || Input.GetKey(aimKey); // keep consistent if we toggled
        }

        // target weight
        float target = aiming ? 1f : 0f;
        currentWeight = Mathf.Lerp(currentWeight, target, Time.deltaTime * aimSpeed);
        var sway = GetComponentInChildren<PlayerModelIdleSway>();
    if (sway) sway.SetAimWeight(currentWeight);

        // apply weaponRoot local transform blend
        if (weaponRoot)
        {
            Vector3 targetPos = wrPosHome + aimLocalPosition;
            Quaternion targetRot = wrRotHome * Quaternion.Euler(aimLocalEuler);

            weaponRoot.localPosition = Vector3.Lerp(weaponRoot.localPosition, Vector3.Lerp(wrPosHome, targetPos, currentWeight), Time.deltaTime * aimSpeed * 4f);
            weaponRoot.localRotation = Quaternion.Slerp(weaponRoot.localRotation, Quaternion.Slerp(wrRotHome, targetRot, currentWeight), Time.deltaTime * aimSpeed * 4f);
        }

        // apply camera FOV
        if (cam)
        {
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Mathf.Lerp(hipFOV, aimFOV, currentWeight), Time.deltaTime * aimSpeed);
        }

        // pass aimWeight to your shooting script if it exposes a public float
        if (fpsShootScript != null)
        {
            var prop = fpsShootScript.GetType().GetField("aimWeight");
            if (prop != null)
                prop.SetValue(fpsShootScript, currentWeight);

            // or if fpsShootScript exposes a property/method, call it instead
        }
    }

    // helper to read current weight from other scripts if needed
    public float GetAimWeight() => currentWeight;
}
