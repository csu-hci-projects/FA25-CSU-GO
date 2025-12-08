using UnityEngine;

/// <summary>
/// A simple world-space aim dot that positions a 3D object (like a small sphere or sprite) 
/// at the raycast hit point. This avoids all screen-space conversion issues.
/// Attach this to the player/gun and assign a dot prefab (a small unlit sphere or billboard sprite).
/// </summary>
public class WorldSpaceAimDot : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The dot object that will be positioned at the aim point. Can be a small sphere, quad, or sprite.")]
    public Transform dotTransform;
    
    [Tooltip("Optional muzzle transform to cast from. If null, uses camera center ray.")]
    public Transform muzzle;
    
    [Tooltip("Camera used for raycasting. If null, uses Camera.main.")]
    public Camera shootCamera;

    [Header("Raycast Settings")]
    [Tooltip("Max range for the aim raycast.")]
    public float maxRange = 200f;
    
    [Tooltip("Layers the raycast should hit.")]
    public LayerMask hitMask = ~0;
    
    [Tooltip("Small offset toward camera to prevent z-fighting with surfaces.")]
    public float surfaceOffset = 0.02f;

    [Header("Fixed Screen Size")]
    [Tooltip("The desired size of the dot in screen pixels (approximately).")]
    public float dotScreenSize = 4f;

    [Header("Target Colors")]
    [Tooltip("Default color of the aim dot.")]
    public Color defaultColor = Color.white;
    
    [Tooltip("Color when aiming at a FleeBall.")]
    public Color fleeBallColor = Color.red;
    
    [Tooltip("Color when aiming at an EnemyBall.")]
    public Color enemyBallColor = Color.red;

    // Cached components
    private Renderer dotRenderer;
    private MaterialPropertyBlock propBlock;
    
    // Cache for target detection
    private Collider lastHitCollider;
    private bool lastWasFleeBall;
    private bool lastWasEnemyBall;

    void Awake()
    {
        if (dotTransform != null)
        {
            dotRenderer = dotTransform.GetComponent<Renderer>();
            propBlock = new MaterialPropertyBlock();
        }
    }

    void LateUpdate()
    {
        if (dotTransform == null) return;

        Camera cam = shootCamera != null ? shootCamera : Camera.main;
        if (cam == null)
        {
            dotTransform.gameObject.SetActive(false);
            return;
        }

        // Build ray from muzzle or screen center
        Ray ray;
        if (muzzle != null)
        {
            ray = new Ray(muzzle.position, muzzle.forward);
        }
        else
        {
            Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            ray = cam.ScreenPointToRay(screenCenter);
        }

        Vector3 hitPoint;
        float hitDistance;
        Vector3 offsetDir;
        Color dotColor = defaultColor;

        if (Physics.Raycast(ray, out RaycastHit hit, maxRange, hitMask, QueryTriggerInteraction.Ignore))
        {
            // Position the dot at the hit point, slightly offset along surface normal
            hitPoint = hit.point;
            hitDistance = hit.distance;
            offsetDir = hit.normal;

            // Determine color based on what we hit (with caching)
            if (hit.collider != lastHitCollider)
            {
                lastHitCollider = hit.collider;
                var fleeBall = hit.collider.GetComponentInParent<FleeBall>();
                lastWasFleeBall = fleeBall != null;
                if (!lastWasFleeBall)
                {
                    var enemyBall = hit.collider.GetComponentInParent<EnemyBall>();
                    lastWasEnemyBall = enemyBall != null;
                }
                else
                {
                    lastWasEnemyBall = false;
                }
            }

            if (lastWasFleeBall)
                dotColor = fleeBallColor;
            else if (lastWasEnemyBall)
                dotColor = enemyBallColor;
        }
        else
        {
            // No hit - place dot at max range
            hitPoint = ray.origin + ray.direction * maxRange;
            hitDistance = maxRange;
            offsetDir = -ray.direction;
            
            // Clear cache
            lastHitCollider = null;
            lastWasFleeBall = false;
            lastWasEnemyBall = false;
        }

        // Position the dot with surface offset to prevent z-fighting
        dotTransform.position = hitPoint + offsetDir * surfaceOffset;
        
        // Make the dot face the camera (billboard effect)
        dotTransform.rotation = cam.transform.rotation;

        // Calculate world scale to maintain fixed screen pixel size
        // frustumHeight = how tall the view frustum is (in world units) at this distance
        // worldSizePerPixel = world units per screen pixel at this distance
        float frustumHeight = 2.0f * hitDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float worldSizePerPixel = frustumHeight / Screen.height;
        float worldSize = dotScreenSize * worldSizePerPixel;
        dotTransform.localScale = Vector3.one * worldSize;

        // Set color via MaterialPropertyBlock (efficient, no material instance created)
        if (dotRenderer != null)
        {
            dotRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_Color", dotColor);
            propBlock.SetColor("_BaseColor", dotColor); // URP
            propBlock.SetColor("_EmissionColor", dotColor); // For emissive materials
            dotRenderer.SetPropertyBlock(propBlock);
        }

        dotTransform.gameObject.SetActive(true);
    }

    void OnDisable()
    {
        if (dotTransform != null)
            dotTransform.gameObject.SetActive(false);
    }
}
