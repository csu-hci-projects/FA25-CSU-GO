using UnityEngine;

/// <summary>
/// Attach this to the player/gun and assign a ScreenDotSpawner. It will cast the same ray the shooter uses
/// (from muzzle or camera center) and update the dot position in screen space every frame so the player
/// sees where the next shot will land.
/// </summary>
public class LiveAimDot : MonoBehaviour
{
    [Tooltip("Reference to the ScreenDotSpawner that will display the dot.")]
    public ScreenDotSpawner spawner;

    [Tooltip("Optional muzzle transform to cast from. If null, uses camera center ray.")]
    public Transform muzzle;

    [Tooltip("Optional camera to use for screen/world conversions. If null, Camera.main is used.")]
    public Camera shootCamera;

    [Tooltip("Max range to project the predicted hit point.")]
    public float maxRange = 200f;

    [Tooltip("Layer mask used for hits (should match your hitscan mask).")]
    public LayerMask hitMask = ~0;

    [Tooltip("If true, show the dot even when nothing is hit (placed at maxRange along ray). If false, dot hides when nothing hit.")]
    public bool showAtMaxRange = true;

    [Header("Target Colors")]
    [Tooltip("Default color of the aim dot.")]
    public Color defaultColor = Color.white;

    [Tooltip("Color when aiming at a FleeBall.")]
    public Color fleeBallColor = Color.red;

    [Tooltip("Color when aiming at an EnemyBall.")]
    public Color enemyBallColor = Color.red;

    void Update()
    {
        if (spawner == null) return;

        Camera cam = shootCamera != null ? shootCamera : Camera.main;
        if (cam == null) return;

        Ray ray;
        if (muzzle != null)
            ray = new Ray(muzzle.position, muzzle.forward);
        else
        {
            Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            ray = cam.ScreenPointToRay(screenCenter);
        }

        if (Physics.Raycast(ray, out RaycastHit hit, maxRange, hitMask, QueryTriggerInteraction.Ignore))
        {
            // Determine the color based on what we hit
            Color dotColor = defaultColor;
            
            var fleeBall = hit.collider.GetComponentInParent<FleeBall>();
            if (fleeBall != null)
            {
                dotColor = fleeBallColor;
            }
            else
            {
                var enemyBall = hit.collider.GetComponentInParent<EnemyBall>();
                if (enemyBall != null)
                {
                    dotColor = enemyBallColor;
                }
            }
            
            spawner.SetDotColor(dotColor);
            spawner.ShowDotAtWorldPosition(hit.point, cam);
        }
        else
        {
            if (showAtMaxRange)
            {
                spawner.SetDotColor(defaultColor);
                Vector3 p = ray.origin + ray.direction * maxRange;
                spawner.ShowDotAtWorldPosition(p, cam);
            }
            else
            {
                // No hit and not showing at max range: ensure the dot is hidden.
                spawner.HideDot();
            }
        }
    }
}
