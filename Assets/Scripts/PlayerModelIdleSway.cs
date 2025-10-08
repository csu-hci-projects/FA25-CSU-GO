using UnityEngine;

public class PlayerModelIdleSway : MonoBehaviour
{
    [Header("Refs")]
    public Rigidbody playerRb;          // Assign the player Rigidbody
    public Transform cameraTransform;   // Optional, if you want camera-relative sway

    [Header("Idle Sway Settings")]
    public float swayAmplitude = 0.02f;    // How far it sways
    public float swayFrequency = 1.5f;     // Speed of sway
    public float swayLerpSpeed = 3f;       // How fast sway fades in/out
    public float movementThreshold = 0.1f; // Min speed to count as "moving"

    [Header("Aim gating")]
    [Range(0f,1f)] public float aimWeight = 0f; // 0=hip, 1=fully ADS (set by your ADS script)

    private Vector3 baseLocalPos;
    private float swayWeight; // 0 = off, 1 = full sway

    public void SetAimWeight(float w) => aimWeight = Mathf.Clamp01(w);

    void Start()
    {
        baseLocalPos = transform.localPosition;
        if (!playerRb) playerRb = GetComponentInParent<Rigidbody>();
    }

    void Update()
    {
        // Detect if the player is moving
        Vector3 horizVel = playerRb ? new Vector3(playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z) : Vector3.zero;
        bool isMoving = horizVel.magnitude > movementThreshold;

        // Stop sway if moving OR aiming
        bool shouldSway = !isMoving && aimWeight < 0.01f;

        // Lerp sway weight up/down
        float targetWeight = shouldSway ? 1f : 0f;
        swayWeight = Mathf.Lerp(swayWeight, targetWeight, Time.deltaTime * swayLerpSpeed);

        // Apply idle sway
        if (swayWeight > 0.001f)
        {
            float t = Time.time * swayFrequency;
            Vector3 offset = new Vector3(
                Mathf.Sin(t) * swayAmplitude,
                Mathf.Cos(t * 0.5f) * swayAmplitude * 0.5f,
                0f
            );
            transform.localPosition = baseLocalPos + offset * swayWeight;
        }
        else
        {
            // Reset to base when moving/aiming
            transform.localPosition = Vector3.Lerp(transform.localPosition, baseLocalPos, Time.deltaTime * swayLerpSpeed);
        }
    }
}
