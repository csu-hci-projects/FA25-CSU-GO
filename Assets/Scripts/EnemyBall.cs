using UnityEngine;
using UnityEngine.AI;

public class EnemyBall : MonoBehaviour
{
    [Header("Chase")]
    [SerializeField] float detectionRange = 12f;
    [SerializeField] float stopRange = 1.5f;
    [SerializeField] float moveForce = 25f;
    [SerializeField] float maxSpeed = 8f;

    [Header("Damage")]
    [SerializeField] int damageAmount = 10;
    [SerializeField] float damageInterval = 1f;

    [Header("NavMesh")]
    [SerializeField] float pathUpdateInterval = 0.4f;
    [SerializeField] float waypointReachDistance = 1.5f;
    [SerializeField] float navMeshStickDistance = 2f; // max distance from NavMesh before forcing back

    [Header("Jump")]
    [SerializeField] float jumpForce = 10f;
    [SerializeField] float jumpCooldown = 2f;
    [SerializeField] float stuckTimeThreshold = 3f; // time before considering stuck
    [SerializeField] float stuckDistanceThreshold = 2f; // distance moved to not be stuck
    [SerializeField] float minJumpInterval = 2f; // minimum time before jumping at player
    [SerializeField] float maxJumpInterval = 5f; // maximum time before jumping at player
    [SerializeField] float lungeJumpForce = 15f; // force for jumping directly at player

    Rigidbody rb;
    Transform player;
    float lastDamageTime;
    bool hasAggro = false;

    NavMeshPath navPath;
    int currentCorner = 0;
    float nextPathUpdate = 0f;

    // Jump/stuck detection
    float lastJumpTime = -999f;
    Vector3 lastPositionCheck;
    float stuckTimer = 0f;
    float nextLungeTime = 0f;

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
        
        lastPositionCheck = transform.position;
        ScheduleNextLunge();
    }

    void ScheduleNextLunge()
    {
        float randomDelay = Random.Range(minJumpInterval, maxJumpInterval);
        nextLungeTime = Time.time + randomDelay;
    }

    void FixedUpdate()
    {
        if (player == null || rb == null) return;

        float distanceToPlayer = Vector3.Distance(rb.position, player.position);

        // Acquire aggro when player enters detection range
        if (!hasAggro && distanceToPlayer <= detectionRange)
        {
            hasAggro = true;
        }

        if (!hasAggro) return;

        // Check if it's time for a lunge jump at the player
        if (Time.time >= nextLungeTime)
        {
            LungeAtPlayer();
            ScheduleNextLunge();
        }

        // Check if stuck (not making progress toward player)
        CheckIfStuck();

        // Try to jump if stuck and cooldown passed
        if (stuckTimer >= stuckTimeThreshold && Time.time >= lastJumpTime + jumpCooldown)
        {
            JumpTowardPlayer();
        }

        // Update NavMesh path periodically
        if (Time.time >= nextPathUpdate)
        {
            UpdatePath();
            nextPathUpdate = Time.time + pathUpdateInterval;
        }

        // Stop pushing when very close to player
        if (distanceToPlayer <= stopRange) return;

        // Get direction to next waypoint or player
        Vector3 targetDirection = GetTargetDirection();
        if (targetDirection == Vector3.zero) return;

        // Apply force toward target
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

    void CheckIfStuck()
    {
        float distanceMoved = Vector3.Distance(rb.position, lastPositionCheck);
        
        if (distanceMoved < stuckDistanceThreshold)
        {
            stuckTimer += Time.fixedDeltaTime;
        }
        else
        {
            stuckTimer = 0f;
            lastPositionCheck = rb.position;
        }
    }

    void JumpTowardPlayer()
    {
        if (player == null) return;

        // Direction toward player
        Vector3 toPlayer = (player.position - rb.position).normalized;
        
        // Jump force: forward toward player + upward
        Vector3 jumpDirection = toPlayer;
        jumpDirection.y = 1f; // Add upward component
        jumpDirection.Normalize();

        rb.AddForce(jumpDirection * jumpForce, ForceMode.Impulse);
        
        lastJumpTime = Time.time;
        stuckTimer = 0f;
        lastPositionCheck = rb.position;
    }

    void LungeAtPlayer()
    {
        if (player == null) return;

        // Calculate direct path to player's current position
        Vector3 toPlayer = player.position - rb.position;
        float horizontalDistance = new Vector3(toPlayer.x, 0f, toPlayer.z).magnitude;
        
        // Calculate trajectory to reach player
        // Add strong upward and forward force to create an arc
        Vector3 direction = toPlayer.normalized;
        direction.y = Mathf.Clamp(0.5f + (horizontalDistance * 0.05f), 0.5f, 1.5f); // Scale height with distance
        direction.Normalize();

        // Apply lunge force
        rb.linearVelocity = Vector3.zero; // Reset velocity for clean jump
        rb.AddForce(direction * lungeJumpForce, ForceMode.Impulse);
        
        lastJumpTime = Time.time;
        stuckTimer = 0f;
        lastPositionCheck = rb.position;
    }

    void UpdatePath()
    {
        if (player == null) return;

        // Calculate path from current position to player
        if (navPath == null) navPath = new NavMeshPath();
        
        if (NavMesh.CalculatePath(rb.position, player.position, NavMesh.AllAreas, navPath))
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

    Vector3 GetTargetDirection()
    {
        if (navPath == null || navPath.corners == null || navPath.corners.Length <= 1)
        {
            // No path - go direct to player
            Vector3 dir = player.position - rb.position;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.zero;
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

        // All corners reached, go direct to player
        Vector3 toPlayer = player.position - rb.position;
        toPlayer.y = 0f;
        return toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : Vector3.zero;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            TryDamage(collision.collider.gameObject);
        }
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            TryDamage(collision.collider.gameObject);
        }
    }

    void TryDamage(GameObject playerObj)
    {
        if (Time.time < lastDamageTime + damageInterval) return;
        PlayerHealth playerHealth = playerObj.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damageAmount);
            lastDamageTime = Time.time;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, stopRange);

        // Draw current path
        if (navPath != null && navPath.corners != null && navPath.corners.Length > 1)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < navPath.corners.Length - 1; i++)
            {
                Gizmos.DrawLine(navPath.corners[i], navPath.corners[i + 1]);
            }
        }
    }
}
