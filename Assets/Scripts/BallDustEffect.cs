using UnityEngine;

public class BallDustEffect : MonoBehaviour
{
    [Header("Trail Settings")]
    [SerializeField] float minSpeedForTrail = 0.5f; // minimum horizontal speed to show trail
    [SerializeField] Material trailMaterial; // Assign a material for the trail
    [SerializeField] float trailTime = 0.5f; // how long the trail lasts before fading
    [SerializeField] float trailWidth = 0.3f; // width of the trail
    [SerializeField] Color trailStartColor = new Color(0.8f, 0.7f, 0.6f, 1f); // dust color
    [SerializeField] Color trailEndColor = new Color(0.8f, 0.7f, 0.6f, 0f); // fade to transparent

    [Header("Ground Align")] 
    [SerializeField] bool alignToGround = true;
    [SerializeField] float groundOffset = 0.05f;
    [SerializeField] float raycastHeight = 1.0f;
    [SerializeField] float raycastDistance = 2.0f;
    [SerializeField] LayerMask groundMask = ~0;

    Rigidbody rb;
    TrailRenderer trailRenderer;
    Vector3 lastGroundPos;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Create a child object for the trail renderer
        GameObject trailObj = new GameObject("DustTrail");
        trailObj.transform.SetParent(transform);
        trailObj.transform.localPosition = Vector3.zero;
        
        // Setup trail renderer
        trailRenderer = trailObj.AddComponent<TrailRenderer>();
        trailRenderer.time = trailTime;
        trailRenderer.startWidth = trailWidth;
        trailRenderer.endWidth = trailWidth * 0.5f;
        trailRenderer.numCornerVertices = 2;
        trailRenderer.numCapVertices = 2;
        trailRenderer.minVertexDistance = 0.1f;
        trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trailRenderer.receiveShadows = false;
        
        // Setup colors
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(trailStartColor, 0f), 
                new GradientColorKey(trailEndColor, 1f) 
            },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(trailStartColor.a, 0f), 
                new GradientAlphaKey(trailEndColor.a, 1f) 
            }
        );
        trailRenderer.colorGradient = gradient;
        
        // Assign material if provided
        if (trailMaterial != null)
        {
            trailRenderer.material = trailMaterial;
        }
        
        lastGroundPos = transform.position;
        trailRenderer.emitting = false;
    }

    void Update()
    {
        if (rb == null || trailRenderer == null) return;

        // Compute horizontal speed
        Vector3 vel = rb.linearVelocity;
        float speed = new Vector3(vel.x, 0f, vel.z).magnitude;
        
        // Enable/disable trail based on speed
        if (speed >= minSpeedForTrail)
        {
            trailRenderer.emitting = true;
            
            // Update trail position to ground level
            if (alignToGround)
            {
                Vector3 rayOrigin = transform.position + Vector3.up * raycastHeight;
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDistance + raycastHeight, groundMask, QueryTriggerInteraction.Ignore))
                {
                    lastGroundPos = hit.point + Vector3.up * groundOffset;
                    trailRenderer.transform.position = lastGroundPos;
                }
                else
                {
                    trailRenderer.transform.localPosition = Vector3.zero;
                }
            }
        }
        else
        {
            trailRenderer.emitting = false;
        }
    }
}