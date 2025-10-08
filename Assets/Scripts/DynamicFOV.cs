using UnityEngine;


[RequireComponent(typeof(Camera))]
public class DynamicFOV : MonoBehaviour
{
    [Header("References")]
    public Rigidbody playerRb;   // assign your player’s Rigidbody

    [Header("FOV Settings")]
    public float baseFOV = 60f;       // default FOV when standing still
    public float maxFOV = 90f;        // max FOV at top speed
    public float minSpeed = 2f;       // speed at which FOV starts increasing
    public float maxSpeed = 10f;      // speed at which FOV = maxFOV
    public float smoothSpeed = 5f;    // how quickly FOV changes

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.fieldOfView = baseFOV;
    }

    void LateUpdate()
    {
        if (playerRb == null) return;

        // get horizontal speed (ignore Y so falling doesn’t spike FOV)
        Vector3 horizontalVel = new Vector3(playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z);
        float speed = horizontalVel.magnitude;

        float targetFOV = baseFOV;

        if (speed > minSpeed)
        {
            // map (minSpeed → maxSpeed) to (baseFOV → maxFOV)
            float t = Mathf.InverseLerp(minSpeed, maxSpeed, speed);
            targetFOV = Mathf.Lerp(baseFOV, maxFOV, t);
        }

        // smoothly adjust
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * smoothSpeed);
    }
}