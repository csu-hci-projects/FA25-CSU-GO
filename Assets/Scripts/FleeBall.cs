using UnityEngine;
using UnityEngine.AI;

public class FleeBall : MonoBehaviour
{
    [Header("Flee Behavior")]
    [SerializeField] float detectionRange = 15f;
    [SerializeField] float fleeDistance = 20f; // how far ahead to look for flee points
    [SerializeField] float fleeSearchRadius = 50f; // search radius for valid NavMesh points
    [SerializeField] float moveForce = 30f;
    [SerializeField] float maxSpeed = 10f;
    [SerializeField] int searchSamples = 50; // number of random points to sample for farthest node
    [SerializeField] float loseInterestTime = 2f; // seconds without line of sight before stopping

    [Header("NavMesh")]
    [SerializeField] float pathUpdateInterval = 0.3f;
    [SerializeField] float waypointReachDistance = 1.5f;

    [Header("Explosion")]
    [SerializeField] GameObject explosionPrefab;
    [Tooltip("Seconds before spawned explosion FX is auto-destroyed (<=0 keeps it).")]
    [SerializeField] float explosionFxLifetime = 3f;
    [Tooltip("Scale multiplier for the explosion effect (1 = original size).")]
    [SerializeField] float explosionScale = 1f;

    [Header("Deactivation (on catch)")]
    [Tooltip("Material to apply when ball is caught/deactivated. Leave null to keep original.")]
    [SerializeField] Material deactivatedMaterial;

    Rigidbody rb;
    Transform player;
    bool isFleeing = false;
    bool isDeactivated = false;
    bool exploded = false;
    bool hasLineOfSight = false;
    float lastSeenTime = 0f;

    NavMeshPath navPath;
    int currentCorner = 0;
    float nextPathUpdate = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        navPath = new NavMeshPath();
    }

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }

    void FixedUpdate()
    {
        if (player == null || rb == null || isDeactivated) return;

        float distanceToPlayer = Vector3.Distance(rb.position, player.position);

        // Check line of sight to player
        hasLineOfSight = CheckLineOfSight();

        // Start fleeing when player gets close
        if (!isFleeing && distanceToPlayer <= detectionRange)
        {
            isFleeing = true;
            lastSeenTime = Time.time;
        }

        // Stop fleeing if player hasn't been seen for too long
        if (isFleeing && !hasLineOfSight)
        {
            if (Time.time - lastSeenTime > loseInterestTime)
            {
                isFleeing = false;
            }
        }
        else if (isFleeing && hasLineOfSight)
        {
            lastSeenTime = Time.time;
        }

        if (!isFleeing) return;

        // Update flee path periodically
        if (Time.time >= nextPathUpdate)
        {
            UpdateFleePath();
            nextPathUpdate = Time.time + pathUpdateInterval;
        }

        // Get direction to next waypoint away from player
        Vector3 targetDirection = GetFleeDirection();
        if (targetDirection == Vector3.zero) return;

        // Apply force away from player
        rb.AddForce(targetDirection * moveForce, ForceMode.Acceleration);

        // Clamp horizontal speed
        Vector3 vel = rb.linearVelocity;
        Vector3 horizVel = new Vector3(vel.x, 0f, vel.z);
        if (horizVel.magnitude > maxSpeed)
        {
            Vector3 clamped = horizVel.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(clamped.x, vel.y, clamped.z);
        }
    }

    void UpdateFleePath()
    {
        if (player == null) return;

        Vector3 fleeTarget = FindFleePoint();

        // Calculate path from current position to flee point
        if (navPath == null) navPath = new NavMeshPath();
        
        if (NavMesh.CalculatePath(rb.position, fleeTarget, NavMesh.AllAreas, navPath))
        {
            if (navPath.status == NavMeshPathStatus.PathComplete && navPath.corners.Length > 1)
            {
                currentCorner = 1; // Start at index 1 (skip current position)
                return;
            }
        }

        // No valid path - clear it
        navPath.ClearCorners();
    }

    Vector3 FindFleePoint()
    {
        if (player == null) return rb.position;

        float farthestDistance = 0f;
        Vector3 farthestPoint = rb.position;
        bool foundValidPoint = false;

        // Sample random points in a large radius around the ball
        for (int i = 0; i < searchSamples; i++)
        {
            // Generate random point in circle around ball
            float angle = Random.Range(0f, 360f);
            float distance = Random.Range(fleeDistance * 0.5f, fleeSearchRadius);
            
            Vector3 randomOffset = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
                0f,
                Mathf.Sin(angle * Mathf.Deg2Rad) * distance
            );
            
            Vector3 samplePoint = rb.position + randomOffset;

            // Try to find nearest NavMesh point to this sample
            NavMeshHit hit;
            if (NavMesh.SamplePosition(samplePoint, out hit, fleeSearchRadius, NavMesh.AllAreas))
            {
                // Check if we can reach this point via NavMesh path
                NavMeshPath testPath = new NavMeshPath();
                if (NavMesh.CalculatePath(rb.position, hit.position, NavMesh.AllAreas, testPath))
                {
                    if (testPath.status == NavMeshPathStatus.PathComplete)
                    {
                        // Calculate distance from player to this point
                        float distFromPlayer = Vector3.Distance(hit.position, player.position);
                        
                        if (distFromPlayer > farthestDistance)
                        {
                            farthestDistance = distFromPlayer;
                            farthestPoint = hit.position;
                            foundValidPoint = true;
                        }
                    }
                }
            }
        }

        // If found a valid far point, use it
        if (foundValidPoint)
        {
            return farthestPoint;
        }

        // Fallback: run directly away from player
        Vector3 awayFromPlayer = (rb.position - player.position).normalized;
        return rb.position + awayFromPlayer * fleeDistance;
    }

    Vector3 GetFleeDirection()
    {
        if (navPath == null || navPath.corners == null || navPath.corners.Length <= 1)
        {
            // No path - run directly away from player
            Vector3 away = rb.position - player.position;
            away.y = 0f;
            return away.sqrMagnitude > 0.01f ? away.normalized : Vector3.zero;
        }

        // Advance to next corner when close
        while (currentCorner < navPath.corners.Length)
        {
            Vector3 corner = navPath.corners[currentCorner];
            Vector3 toCorner = corner - rb.position;
            toCorner.y = 0f;

            if (toCorner.magnitude <= waypointReachDistance)
            {
                currentCorner++;
            }
            else
            {
                return toCorner.normalized;
            }
        }

        // All corners reached - keep running away
        Vector3 awayFinal = rb.position - player.position;
        awayFinal.y = 0f;
        return awayFinal.sqrMagnitude > 0.01f ? awayFinal.normalized : Vector3.zero;
    }

    bool CheckLineOfSight()
    {
        if (player == null) return false;

        Vector3 directionToPlayer = player.position - rb.position;
        float distanceToPlayer = directionToPlayer.magnitude;

        // Raycast to check for obstacles between ball and player
        RaycastHit hit;
        if (Physics.Raycast(rb.position, directionToPlayer.normalized, out hit, distanceToPlayer))
        {
            // If the raycast hit the player, we have line of sight
            if (hit.collider.CompareTag("Player"))
            {
                return true;
            }
            // Otherwise something is blocking the view
            return false;
        }

        // No obstacles detected, have line of sight
        return true;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isDeactivated || exploded) return;
        
        // Player catches the ball
        if (collision.collider.CompareTag("Player"))
        {
            DeactivateAI();
        }
    }

    public void DeactivateAI()
    {
        if (isDeactivated) return;
        isDeactivated = true;

        // Stop current movement but keep physics active
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            // Keep rb.isKinematic = false so physics still affects it
        }

        // Change material if provided
        if (deactivatedMaterial != null)
        {
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = deactivatedMaterial;
            }
        }
    }

    public void TriggerExplosion()
    {
        if (exploded) return;
        exploded = true;

        // Spawn FX
        if (explosionPrefab != null)
        {
            var fx = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            if (explosionScale > 0f)
                fx.transform.localScale = Vector3.one * explosionScale;
            if (explosionFxLifetime > 0f)
                Destroy(fx, explosionFxLifetime);
        }

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Draw current flee path
        if (navPath != null && navPath.corners != null && navPath.corners.Length > 1)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < navPath.corners.Length - 1; i++)
            {
                Gizmos.DrawLine(navPath.corners[i], navPath.corners[i + 1]);
            }
        }
    }
}
