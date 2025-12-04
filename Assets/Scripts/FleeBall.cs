using UnityEngine;
using System.Collections;
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

    [Header("Wander (post-LOS)")]
    [Tooltip("Enable wandering after losing sight and stopping fleeing.")]
    [SerializeField] bool enableWanderAfterLoseSight = true;
    [Tooltip("Seconds after losing interest before wandering begins.")]
    [SerializeField] float wanderStartDelay = 0.75f;
    [Tooltip("Radius around current position to sample random wander targets.")]
    [SerializeField] float wanderRadius = 10f;
    [Tooltip("Minimum horizontal distance from player for wander targets.")]
    [SerializeField] float wanderMinPlayerDistance = 4f;
    [Tooltip("Acceleration used while wandering.")]
    [SerializeField] float wanderMoveForce = 12f;
    [Tooltip("Max horizontal speed while wandering.")]
    [SerializeField] float wanderMaxSpeed = 6f;
    [Tooltip("Distance to consider the wander target reached, then pick a new one.")]
    [SerializeField] float wanderReachDistance = 1.0f;

    [Header("Explosion")]
    [SerializeField] GameObject explosionPrefab;
    [Tooltip("Seconds before spawned explosion FX is auto-destroyed (<=0 keeps it).")]
    [SerializeField] float explosionFxLifetime = 3f;
    [Tooltip("Scale multiplier for the explosion effect (1 = original size).")]
    [SerializeField] float explosionScale = 1f;

    [Header("Deactivation (on catch)")]
    [Tooltip("Material to apply when ball is caught/deactivated. Leave null to keep original.")]
    [SerializeField] Material deactivatedMaterial;

    [Header("Health Drop")]
    [Tooltip("Health pickup prefab to spawn when ball explodes.")]
    [SerializeField] GameObject healthDropPrefab;
    [Tooltip("Offset for spawning the health drop relative to ball position.")]
    [SerializeField] Vector3 healthDropSpawnOffset = new Vector3(0f, 0.5f, 0f);
    [Tooltip("Delay in seconds before spawning the health drop.")]
    [SerializeField] float healthDropSpawnDelay = 0f;

    Rigidbody rb;
    Transform player;
    bool isFleeing = false;
    bool isDeactivated = false;
    bool exploded = false;
    bool hasLineOfSight = false;
    float lastSeenTime = 0f;
    SpawnedEntityNotifier notifier;

    // Wander state
    bool isWandering = false;
    Vector3 wanderTarget;
    float wanderNextPickTime = 0f;

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

        // Setup notifier
        notifier = GetComponent<SpawnedEntityNotifier>();
        if (notifier == null) notifier = gameObject.AddComponent<SpawnedEntityNotifier>();
        notifier.EntityType = SpawnedEntityNotifier.Type.Flee;
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
                // Prepare to start wandering after a short delay
                if (enableWanderAfterLoseSight)
                {
                    isWandering = false; // will start after delay
                    wanderNextPickTime = Time.time + Mathf.Max(0f, wanderStartDelay);
                }
            }
        }
        else if (isFleeing && hasLineOfSight)
        {
            lastSeenTime = Time.time;
        }

        if (!isFleeing)
        {
            // Handle post-LOS wandering
            if (enableWanderAfterLoseSight)
            {
                HandleWander();
            }
            return;
        }

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

    void HandleWander()
    {
        // If not yet started and delay elapsed, pick a target
        if (!isWandering && Time.time >= wanderNextPickTime)
        {
            isWandering = PickWanderTarget(out wanderTarget);
        }
        if (!isWandering) return;

        Vector3 toTarget = wanderTarget - rb.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;
        if (dist <= wanderReachDistance)
        {
            // Reached: reset loop and pick a new target next frame
            isWandering = false;
            wanderNextPickTime = Time.time; // immediate repick allowed
            return;
        }

        Vector3 dir = dist > 0.001f ? toTarget / dist : Vector3.zero;
        rb.AddForce(dir * wanderMoveForce, ForceMode.Acceleration);

        // Clamp wander horizontal speed
        Vector3 vel = rb.linearVelocity;
        Vector3 horizVel = new Vector3(vel.x, 0f, vel.z);
        if (horizVel.magnitude > wanderMaxSpeed)
        {
            Vector3 clamped = horizVel.normalized * wanderMaxSpeed;
            rb.linearVelocity = new Vector3(clamped.x, vel.y, clamped.z);
        }
    }

    bool PickWanderTarget(out Vector3 target)
    {
        // Sample a random point around the player on NavMesh, staying near but not too close
        Vector3 center = player != null ? player.position : rb.position;
        for (int i = 0; i < 12; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float radius = Random.Range(Mathf.Max(wanderMinPlayerDistance, wanderReachDistance * 2f), wanderRadius);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            Vector3 sample = center + offset;

            if (NavMesh.SamplePosition(sample, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            {
                // Ensure horizontal min distance from player
                if (player != null)
                {
                    Vector3 hp = new Vector3(hit.position.x, 0f, hit.position.z);
                    Vector3 pp = new Vector3(player.position.x, 0f, player.position.z);
                    if (Vector3.Distance(hp, pp) < wanderMinPlayerDistance) continue;
                }
                target = hit.position;
                return true;
            }
        }
        // Fallback around current position if sampling near player fails
        float a2 = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float r2 = Random.Range(wanderReachDistance * 2f, wanderRadius);
        Vector3 off2 = new Vector3(Mathf.Cos(a2), 0f, Mathf.Sin(a2)) * r2;
        target = rb.position + off2;
        return true;
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

        // Spawn health drop (immediately or after delay)
        if (healthDropPrefab != null)
        {
            Vector3 dropPosition = transform.position + healthDropSpawnOffset;
            if (healthDropSpawnDelay <= 0f)
            {
                Instantiate(healthDropPrefab, dropPosition, Quaternion.identity);
            }
            else
            {
                // Use a short-lived helper GameObject to spawn after a delay so the ball can be destroyed immediately
                GameObject spawner = new GameObject("HealthDropSpawner");
                var hs = spawner.AddComponent<HealthDropSpawner>();
                hs.Setup(healthDropPrefab, dropPosition, Quaternion.identity, healthDropSpawnDelay);
            }
        }

        // FleeBall death likely not player kill (unless your gameplay defines it so)
        if (notifier != null)
        {
            notifier.NotifyDeath(SpawnedEntityNotifier.DeathCause.Other);
        }
        else
        {
            Destroy(gameObject);
        }
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

/// <summary>
/// Helper MonoBehaviour that spawns a prefab after a delay and then destroys itself.
/// Created at runtime by `FleeBall` so the spawn can occur after the originating object is destroyed.
/// </summary>
public class HealthDropSpawner : MonoBehaviour
{
    GameObject prefab;
    Vector3 position;
    Quaternion rotation;
    float delay;

    public void Setup(GameObject prefab, Vector3 position, Quaternion rotation, float delay)
    {
        this.prefab = prefab;
        this.position = position;
        this.rotation = rotation;
        this.delay = Mathf.Max(0f, delay);
        DontDestroyOnLoad(gameObject);
        StartCoroutine(SpawnCoroutine());
    }

    IEnumerator SpawnCoroutine()
    {
        yield return new WaitForSeconds(delay);
        if (prefab != null)
        {
            Instantiate(prefab, position, rotation);
        }
        Destroy(gameObject);
    }
}
