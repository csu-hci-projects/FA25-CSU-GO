using UnityEngine;

// Attach this to the visual child (e.g., BombBall) to make it
// visually roll based on the parent Rigidbody's horizontal velocity.
// Works even if the Rigidbody's rotation is frozen.
public class VisualRollSync : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Rigidbody driving the motion (usually on the parent sphere).")]
    public Rigidbody sourceRb;

    [Tooltip("Approximate radius of the rolling sphere in meters.")]
    public float radius = 0.5f;

    [Tooltip("Optional: ground normal to roll around. If null, uses Vector3.up.")]
    public Transform groundNormalSource;

    [Header("Tuning")]
    [Tooltip("Multiplier for rolling speed (1 = no slip). Increase for faster spin.")]
    public float spinMultiplier = 1.6f;

    [Tooltip("Smooths the rotation updates.")]
    public float rotationSmoothing = 20f;

    [Header("Radius Detection")]
    [Tooltip("If true, estimate radius from a Renderer bounds at startup.")]
    public bool autoDetectRadius = true;
    [Tooltip("Optional Renderer used for auto radius; if null, uses first found in children.")]
    public Renderer radiusSource;

    [Header("Constraints")]
    [Tooltip("Keep the visual upright (world up) and only spin around constrained axes.")]
    public bool keepUpright = true;
    [Tooltip("If true, spin around local X (red) and local Z (blue) based on velocity components.")]
    public bool constrainedAxisMode = true;

    private Transform self;
    private float cumulativeRollXDeg; // keeps continuous tire spin around local X

    void Awake()
    {
        self = transform;
        if (!sourceRb)
        {
            // Try to find on parent
            sourceRb = GetComponentInParent<Rigidbody>();
        }

        if (autoDetectRadius)
        {
            var rend = radiusSource ? radiusSource : GetComponentInChildren<Renderer>();
            if (rend)
            {
                // Approximate sphere radius from the smallest horizontal extent
                var bounds = rend.bounds;
                float x = bounds.extents.x;
                float z = bounds.extents.z;
                float estimated = Mathf.Max(0.01f, Mathf.Min(x, z));
                radius = estimated;
            }
        }
    }

    void LateUpdate()
    {
        if (!sourceRb) return;

        Vector3 vel = sourceRb.linearVelocity;
        Vector3 horizVel = new Vector3(vel.x, 0f, vel.z);
        float speed = horizVel.magnitude;
        if (speed < 0.01f) return; // avoid jitter

        if (keepUpright && constrainedAxisMode)
        {
            // Desired yaw aligns the model's forward with the horizontal velocity (GREEN axis)
            Vector3 desiredForward = horizVel.normalized; // world-space
            if (desiredForward.sqrMagnitude < 1e-6f)
                desiredForward = (sourceRb.transform.rotation * Vector3.forward).normalized;

            // Build yaw-only rotation looking along desired forward
            Quaternion yawOnly = Quaternion.LookRotation(desiredForward, Vector3.up);

            // Local axes in world space given this yaw
            Vector3 right = yawOnly * Vector3.right;   // local X (red) — axle
            Vector3 forward = yawOnly * Vector3.forward; // local Z (blue) — wheel tread

            // Roll around local X based on forward motion along the tread
            float vAlongTread = Vector3.Dot(horizVel, forward);
            float omegaX = (radius > 1e-4f ? vAlongTread / radius : 0f) * spinMultiplier; // rad/s

            float dt = Time.deltaTime;
            // Accumulate roll angle around local X so the tire continuously spins
            cumulativeRollXDeg += omegaX * Mathf.Rad2Deg * dt;
            // Build final orientation: first yaw (aim forward), then local X roll
            Quaternion target = yawOnly * Quaternion.AngleAxis(cumulativeRollXDeg, Vector3.right);
            self.rotation = Quaternion.Slerp(self.rotation, target, Mathf.Clamp01(rotationSmoothing * Time.deltaTime));
        }
        else
        {
            Vector3 up = groundNormalSource ? groundNormalSource.up : Vector3.up;
            // Rolling axis is perpendicular to motion and up: axis = cross(up, v)
            Vector3 rollAxis = Vector3.Cross(up, horizVel.normalized);

            // For pure rolling: omega = v / r (rad/s)
            // Slightly amplify to compensate visual slip vs physics
            float baseOmega = radius > 1e-4f ? speed / radius : 0f;
            float omega = baseOmega * spinMultiplier;

            // Apply an incremental rotation this frame
            float angleDeg = omega * Mathf.Rad2Deg * Time.deltaTime;
            Quaternion delta = Quaternion.AngleAxis(angleDeg, rollAxis);

            // Smoothly apply relative rotation
            self.rotation = Quaternion.Slerp(self.rotation, delta * self.rotation, Mathf.Clamp01(rotationSmoothing * Time.deltaTime));
        }
    }
}
