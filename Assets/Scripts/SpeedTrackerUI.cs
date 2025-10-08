using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpeedTrackerUI : MonoBehaviour
{
    [Header("Target")]
    public Rigidbody targetRb;          // drag your Player Rigidbody here

    [Header("UI")]
    public TextMeshProUGUI speedText;   // drag SpeedText (TMP) here
    public Image speedBar;              // (optional) drag SpeedBar image here

    [Header("Display")]
    public bool horizontalOnly = true;  // ignore fall speed
    public bool useKilometersPerHour = false; // otherwise m/s
    public float maxSpeedForBar = 12f;  // bar reaches 100% at this speed
    public float smooth = 10f;          // UI smoothing (larger = snappier)

    float smoothedDisplay;

    void Reset()
    {
        // convenience auto-fill if added on the player
        targetRb = GetComponentInParent<Rigidbody>();
    }

    void Update()
    {
        if (!targetRb) return;

        // --- get speed (uses your project's linearVelocity; change to .velocity if needed) ---
        Vector3 v = targetRb.linearVelocity;         // if you use standard PhysX, use: targetRb.velocity
        if (horizontalOnly) v = new Vector3(v.x, 0f, v.z);
        float speed = v.magnitude;

        // units
        float display = useKilometersPerHour ? speed * 3.6f : speed;
        string unit = useKilometersPerHour ? "km/h" : "m/s";

        // smooth the number so it doesn’t jitter
        smoothedDisplay = Mathf.Lerp(smoothedDisplay, display, 1f - Mathf.Exp(-smooth * Time.deltaTime));

        // text
        if (speedText)
            speedText.text = $"{smoothedDisplay:0.0} {unit}";

        // bar
        if (speedBar)
            speedBar.fillAmount = Mathf.Clamp01(speed / Mathf.Max(0.0001f, maxSpeedForBar));
    }
}
