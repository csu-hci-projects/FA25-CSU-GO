using UnityEngine;
using UnityEngine.AI;

public class EnemyBall : MonoBehaviour
{
    [Header("Chase")]
    [SerializeField] float detectionRange = 12f;
    [SerializeField] float stopRange = 1.5f;
    [SerializeField] float moveForce = 25f;
    [SerializeField] float maxSpeed = 8f;
    [Tooltip("Height offset for line-of-sight raycasts.")]
    [SerializeField] float sightHeight = 0.75f;
    [Tooltip("Layers that block line-of-sight between enemy and player.")]
    [SerializeField] LayerMask losObstructionMask = ~0;

    [Header("Explosion")]
    [SerializeField] GameObject explosionPrefab;
    [Tooltip("Seconds before spawned explosion FX is auto-destroyed (<=0 keeps it).")]
    [SerializeField] float explosionFxLifetime = 3f;
    [Tooltip("Scale multiplier for the explosion effect (1 = original size).")]
    [SerializeField] float explosionScale = 1f;
    [Header("Explosion Knockback")]
    [Tooltip("Max horizontal impulse at the center of the explosion.")]
    [SerializeField] float explosionForce = 18f;
    [Tooltip("Extra upward boost added to the impulse.")]
    [SerializeField] float explosionUpBoost = 3f;
    [Tooltip("Radius over which the impulse scales down to zero.")]
    [SerializeField] float explosionRadius = 3.5f;
    [Tooltip("Optional damage to apply to player once on explosion (0 = none).")]
    [SerializeField] int explosionDamage = 0;

    [Header("Drops")]
    [Tooltip("Optional prefab to drop on explosion (e.g., AmmoPickup or HealthPickup).")]
    [SerializeField] GameObject dropPrefab;
    [Tooltip("Chance 0..1 to spawn a drop on explosion.")]
    [Range(0f,1f)] [SerializeField] float dropChance = 0.35f;
    [Tooltip("Small upward offset to keep the drop slightly above the ground.")]
    [SerializeField] float dropSpawnUpOffset = 0.1f;
    [Tooltip("Horizontal random radius for drop spawn to avoid stacking.")]
    [SerializeField] float dropRandomRadius = 0.4f;
    [Tooltip("Layers considered ground for positioning the drop (raycast down).")]
    [SerializeField] LayerMask dropGroundMask = ~0;
    [Tooltip("How far above to start the ground ray.")]
    [SerializeField] float dropRaycastAbove = 3f;
    [Tooltip("How far below to search for ground.")]
    [SerializeField] float dropRaycastBelow = 10f;

    [Header("Knockback Limits")] 
    [Tooltip("Hard cap for player's horizontal speed right after knockback (m/s).")]
    [SerializeField] float knockbackMaxHorizontalSpeed = 8f;
    [Tooltip("Hard cap for player's upward speed right after knockback (m/s).")]
    [SerializeField] float knockbackMaxUpSpeed = 6f;

    [Header("NavMesh")]
    [SerializeField] float pathUpdateInterval = 0.9f;
    [SerializeField] float waypointReachDistance = 0.75f;
    [Tooltip("Height offset above NavMesh surface to prevent clipping into ground.")]
    [SerializeField] float navMeshHeightOffset = 0.5f;

    [Header("Chase Steering")]
    [Tooltip("How aggressively velocity is steered toward the target direction (acceleration gain).")]
    [SerializeField] float steerGain = 10f;
    [Tooltip("Damping applied to lateral (sideways) velocity to prevent orbiting.")]
    [SerializeField] float lateralDamping = 8f;
    [Tooltip("Within this distance, reduce desired speed to help arriving and reduce circling.")]
    [SerializeField] float brakingDistance = 2.5f;
    [Tooltip("Minimum approach speed when inside braking distance, to avoid stalling completely.")]
    [SerializeField] float minApproachSpeed = 1.5f;
    [Tooltip("Minimum acceleration toward desired speed (m/s^2).")]
    [SerializeField] float minAccel = 4f;
    [Tooltip("Max acceleration applied when speeding up (m/s^2).")]
    [SerializeField] float maxAccel = 18f;

    [Header("Ground Probe (Jump Safety)")]
    [Tooltip("Layers treated as ground when deciding if we're near the floor (affects NavMeshMove vertical snapping).")]
    [SerializeField] LayerMask groundProbeMask = ~0;
    [Tooltip("How far above the body to start the ground ray.")]
    [SerializeField] float groundProbeUp = 0.25f;
    [Tooltip("How far below to search for ground.")]
    [SerializeField] float groundProbeDown = 1.25f;
    [Tooltip("If vertical speed magnitude exceeds this, we consider the ball airborne and won't snap to ground.")]
    [SerializeField] float airborneVerticalVelThreshold = 0.05f;

    [Header("Jump Attack")]
    [SerializeField] float jumpInterval = 1.2f;
    [SerializeField] float jumpForce = 8f;
    [Tooltip("Clamp for upward component of jump direction (far from player). 0..1 fraction of unit vector.")]
    [SerializeField] float maxJumpUpComponent = 0.4f;
    [Tooltip("Minimum upward component so jumps always have some lift.")]
    [SerializeField] float minJumpUpComponent = 0.12f;
    [Tooltip("Within this distance to player, cap upward component more aggressively.")]
    [SerializeField] float closeJumpDistance = 1.25f;
    [Tooltip("Max upward component when very close to the player.")]
    [SerializeField] float closeMaxJumpUpComponent = 0.22f;
    [Tooltip("Clamp for upward velocity immediately after jump to prevent launch.")]
    [SerializeField] float maxJumpUpSpeed = 6f;
    float nextJumpTime = 0f;

    Rigidbody rb;
    Transform player;
    bool hasAggro = false;
    bool exploded = false;

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
        if (player == null || rb == null) return;

        float distanceToPlayer = Vector3.Distance(rb.position, player.position);

        // Acquire aggro when player enters detection range
        if (!hasAggro && distanceToPlayer <= detectionRange)
        {
            hasAggro = true;
        }

        if (!hasAggro) return;

        bool inSight = HasLineOfSight();

        // Update NavMesh path periodically
        if (!inSight && Time.time >= nextPathUpdate)
        {
            UpdatePath();
            nextPathUpdate = Time.time + pathUpdateInterval;
        }
        else if (inSight)
        {
            // Clear path when chasing via physics to avoid stale corners
            navPath.ClearCorners();
            currentCorner = 0;
        }

        // Stop pushing when very close to player
        if (distanceToPlayer <= stopRange) return;

        // Periodic jump toward player
        if (Time.time >= nextJumpTime)
        {
            Vector3 toPlayer = player.position - rb.position;
            if (toPlayer.sqrMagnitude > 0.5f)
            {
                // Add slight randomization to jump direction
                Vector3 randomOffset = new Vector3(Random.Range(-0.15f, 0.15f), 0f, Random.Range(-0.15f, 0.15f));

                float dist = toPlayer.magnitude;
                float upCap = dist < closeJumpDistance ? closeMaxJumpUpComponent : maxJumpUpComponent;
                upCap = Mathf.Clamp01(upCap);
                float upMin = Mathf.Clamp01(minJumpUpComponent);

                // Build primarily horizontal jump direction, then inject controlled upward component
                Vector3 toPlayerXZ = new Vector3(toPlayer.x, 0f, toPlayer.z) + new Vector3(randomOffset.x, 0f, randomOffset.z);
                Vector3 horizDir = toPlayerXZ.sqrMagnitude > 1e-4f ? toPlayerXZ.normalized : Vector3.forward;

                // Choose an upward fraction between [upMin, upCap]
                float upFrac = Mathf.Clamp((toPlayer.y > 0f) ? (toPlayer.y / Mathf.Max(0.01f, dist)) : upMin, upMin, upCap);

                // Combine horizontal and up, then normalize
                Vector3 jumpDir = (horizDir + Vector3.up * upFrac).normalized;
                rb.AddForce(jumpDir * jumpForce, ForceMode.Impulse);

                // Clamp excessive upward velocity immediately after jump
                if (maxJumpUpSpeed > 0f)
                {
                    Vector3 vAfter = rb.linearVelocity;
                    if (vAfter.y > maxJumpUpSpeed)
                    {
                        vAfter.y = maxJumpUpSpeed;
                        rb.linearVelocity = vAfter;
                    }
                }
            }
            nextJumpTime = Time.time + jumpInterval;
        }

        if (inSight)
        {
            PhysicsChase();
        }
        else
        {
            NavMeshMove();
        }
    }

    bool HasLineOfSight()
    {
        if (player == null) return false;
        Vector3 origin = rb.position + Vector3.up * sightHeight;
        Vector3 target = player.position + Vector3.up * sightHeight;
        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist <= 0.001f) return true;
        dir /= dist;

        // Raycast: if we hit something before the player, line of sight is blocked
        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, losObstructionMask, QueryTriggerInteraction.Ignore))
        {
            // If we hit the player, we have line of sight
            return hit.collider.CompareTag("Player");
        }
        // Nothing was hit: unobstructed
        return true;
    }

    void PhysicsChase()
    {
        // Direct force toward player using physics
        Vector3 toPlayer = player.position - rb.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f) return;

        Vector3 dir = toPlayer.normalized;

        // Desired speed: slow down as we get close to reduce overshoot/orbiting
        float dist = toPlayer.magnitude;
        float desiredSpeed = maxSpeed;
        if (brakingDistance > 0.01f && dist < brakingDistance)
        {
            float t = Mathf.Clamp01(dist / brakingDistance);
            desiredSpeed = Mathf.Lerp(minApproachSpeed, maxSpeed, t);
        }

        // Ensure we at least match player's horizontal speed (so we can catch up)
        float playerHorizSpeed = 0f;
        if (player != null)
        {
            var prb = player.GetComponent<Rigidbody>();
            if (prb != null)
            {
                Vector3 pv = prb.linearVelocity;
                playerHorizSpeed = new Vector3(pv.x, 0f, pv.z).magnitude;
            }
        }
        desiredSpeed = Mathf.Max(desiredSpeed, playerHorizSpeed);

        // Current horizontal velocity
        Vector3 vel = rb.linearVelocity;
        Vector3 horizVel = new Vector3(vel.x, 0f, vel.z);

        // Velocity matching steering (seek)
        Vector3 desiredVel = dir * desiredSpeed;
        Vector3 velError = desiredVel - horizVel;

        // Limit acceleration magnitude to keep a gradual speed-up
        float accelGain = Mathf.Max(0f, steerGain);
        Vector3 desiredAccel = velError * accelGain;
        float aMag = desiredAccel.magnitude;
        float minA = Mathf.Max(0f, minAccel);
        float maxA = Mathf.Max(minA, maxAccel);
        if (aMag > maxA)
            desiredAccel = desiredAccel.normalized * maxA;
        else if (aMag < minA)
            desiredAccel = desiredAccel.normalized * minA;

        rb.AddForce(desiredAccel, ForceMode.Acceleration);

        // Lateral damping: kill sideways component to avoid orbiting
        Vector3 lateral = horizVel - Vector3.Project(horizVel, dir);
        rb.AddForce(-lateral * Mathf.Max(0f, lateralDamping), ForceMode.Acceleration);

        // Clamp horizontal speed
        vel = rb.linearVelocity;
        horizVel = new Vector3(vel.x, 0f, vel.z);
        if (horizVel.magnitude > maxSpeed)
        {
            Vector3 clamped = horizVel.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(clamped.x, vel.y, clamped.z);
        }
    }

    void NavMeshMove()
    {
        // Move strictly along NavMesh path - no direct movement allowed
        Vector3 targetDirection = GetNavMeshDirection();
        if (targetDirection == Vector3.zero)
        {
            // No valid path, stop moving
            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(0f, v.y, 0f);
            return;
        }

        // Determine if we're near ground; if airborne, avoid snapping Y to NavMesh height
        bool nearGround = IsNearGround(out _);
        bool airborne = Mathf.Abs(rb.linearVelocity.y) > airborneVerticalVelThreshold || !nearGround;

        float speed = Mathf.Max(0.01f, maxSpeed);
        Vector3 desiredStep = targetDirection * speed * Time.fixedDeltaTime;
        Vector3 candidate = rb.position + desiredStep;

        // Sample NavMesh to stay on surface and prevent clipping through floor
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            // Keep the NavMesh Y position plus offset only if grounded; preserve Y while airborne
            float newY = airborne ? rb.position.y : hit.position.y + navMeshHeightOffset;
            candidate = new Vector3(candidate.x, newY, candidate.z);
            rb.MovePosition(candidate);
        }
        else
        {
            // If candidate is off NavMesh, try to get back onto it from current position
            if (NavMesh.SamplePosition(rb.position, out NavMeshHit currentHit, 5f, NavMesh.AllAreas))
            {
                // Preserve Y while airborne; otherwise snap to mesh height + offset
                float newY = airborne ? rb.position.y : currentHit.position.y + navMeshHeightOffset;
                Vector3 backOnMesh = new Vector3(rb.position.x, newY, rb.position.z);
                rb.MovePosition(backOnMesh);
            }
        }

        // Zero horizontal velocity so physics doesn't drift us off-path
        // (Do not touch vertical velocity so jumping works)
    }

    bool IsNearGround(out RaycastHit hit)
    {
        Vector3 origin = rb.position + Vector3.up * Mathf.Max(0f, groundProbeUp);
        float rayDist = Mathf.Max(0.01f, groundProbeUp + groundProbeDown);
        return Physics.Raycast(origin, Vector3.down, out hit, rayDist, groundProbeMask, QueryTriggerInteraction.Ignore);
    }

    void UpdatePath()
    {
        if (player == null) return;

        // Calculate path from current position to player
        if (navPath == null) navPath = new NavMeshPath();
        
        // Ensure we're starting from a valid NavMesh position
        Vector3 startPos = rb.position;
        if (!NavMesh.SamplePosition(rb.position, out NavMeshHit startHit, 5f, NavMesh.AllAreas))
        {
            // Enemy is too far from NavMesh, can't calculate path
            navPath.ClearCorners();
            currentCorner = 0;
            return;
        }
        startPos = startHit.position;
        
        // Ensure destination is valid
        Vector3 endPos = player.position;
        if (!NavMesh.SamplePosition(player.position, out NavMeshHit endHit, 5f, NavMesh.AllAreas))
        {
            // Player is off NavMesh, can't calculate path
            navPath.ClearCorners();
            currentCorner = 0;
            return;
        }
        endPos = endHit.position;
        
        if (NavMesh.CalculatePath(startPos, endPos, NavMesh.AllAreas, navPath))
        {
            if (navPath.status == NavMeshPathStatus.PathComplete || navPath.status == NavMeshPathStatus.PathPartial)
            {
                if (navPath.corners.Length > 1)
                {
                    currentCorner = 1; // Start at index 1 (skip current position)
                    return;
                }
            }
        }

        // No valid path - clear it
        navPath.ClearCorners();
        currentCorner = 0;
    }

    Vector3 GetNavMeshDirection()
    {
        // ONLY follow NavMesh path corners, never direct to player
        if (navPath == null || navPath.corners == null || navPath.corners.Length <= 1)
        {
            // No valid path - request update and don't move
            nextPathUpdate = 0f; // Force immediate path update
            return Vector3.zero;
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

        // All corners reached - force immediate path update
        nextPathUpdate = 0f;
        return Vector3.zero;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (exploded) return;
        if (collision.collider.CompareTag("Player"))
        {
            Rigidbody playerRb = collision.collider.attachedRigidbody;
            TriggerExplosionAffectPlayer(playerRb);
        }
    }

    public void TriggerExplosion(bool ignorePlayerEffect)
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

        // If ignoring player effect (e.g., shot by player), skip knockback/damage
        if (!ignorePlayerEffect)
        {
            // If we have a cached player, apply an area check just in case
            if (player != null)
            {
                Rigidbody prb = player.GetComponent<Rigidbody>();
                if (prb != null)
                {
                    ApplyKnockback(prb);
                    ApplyExplosionDamage(player.gameObject);
                }
            }
        }

        TrySpawnDrop();
        Destroy(gameObject);
    }

    public void TriggerExplosionAffectPlayer(Rigidbody playerRb)
    {
        if (exploded) return;
        exploded = true;

        if (explosionPrefab != null)
        {
            var fx = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            if (explosionScale > 0f)
                fx.transform.localScale = Vector3.one * explosionScale;
            if (explosionFxLifetime > 0f)
                Destroy(fx, explosionFxLifetime);
        }

        if (playerRb != null)
        {
            ApplyKnockback(playerRb);
            ApplyExplosionDamage(playerRb.gameObject);
        }

        TrySpawnDrop();
        Destroy(gameObject);
    }

    void TrySpawnDrop()
    {
        if (dropPrefab == null) return;
        if (Random.value > Mathf.Clamp01(dropChance)) return;

        // Base point near where the enemy exploded
        Vector2 rnd = Random.insideUnitCircle * Mathf.Max(0f, dropRandomRadius);
        Vector3 basePos = transform.position + new Vector3(rnd.x, 0f, rnd.y);

        // Raycast down to find ground
        Vector3 rayStart = basePos + Vector3.up * Mathf.Max(0f, dropRaycastAbove);
        float rayDistance = Mathf.Max(0.01f, dropRaycastAbove + dropRaycastBelow);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, rayDistance, dropGroundMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 pos = hit.point + Vector3.up * Mathf.Max(0f, dropSpawnUpOffset);
            Instantiate(dropPrefab, pos, Quaternion.identity);
            return;
        }

        // Fallback: Snap to NavMesh height if available
        if (NavMesh.SamplePosition(basePos, out NavMeshHit nmHit, 5f, NavMesh.AllAreas))
        {
            Vector3 pos = nmHit.position + Vector3.up * Mathf.Max(0f, dropSpawnUpOffset);
            Instantiate(dropPrefab, pos, Quaternion.identity);
            return;
        }

        // Last resort: place at current height with minimal lift
        Instantiate(dropPrefab, basePos + Vector3.up * Mathf.Max(0f, dropSpawnUpOffset), Quaternion.identity);
    }

    void ApplyKnockback(Rigidbody playerRb)
    {
        // Radial falloff 0..1 based on distance
        Vector3 toPlayer = playerRb.position - transform.position;
        float dist = toPlayer.magnitude;
        float t = explosionRadius > 0.0001f ? Mathf.Clamp01(1f - (dist / explosionRadius)) : 1f;

        // Horizontal knockback direction
        Vector3 horiz = new Vector3(toPlayer.x, 0f, toPlayer.z);
        if (horiz.sqrMagnitude < 1e-6f) horiz = Vector3.forward; // fallback to any direction
        Vector3 horizDir = horiz.normalized;

        // Build a single impulse (momentum) vector
        float horizImpulseMag = explosionForce * t;
        float upImpulseMag = Mathf.Max(0f, explosionUpBoost * t);
        Vector3 impulse = horizDir * horizImpulseMag + Vector3.up * upImpulseMag;

        // Prefer handing off to the movement script so ground logic still adds this shove
        var mover = playerRb.GetComponent<PlayerMovementFPSBhop>();
        if (mover != null)
        {
            // Convert physics impulse (N·s) to delta-velocity by dividing by mass
            float mass = Mathf.Max(0.0001f, playerRb.mass);
            Vector3 deltaV = impulse / mass;

            // Clamp the added deltaV so we don't exceed caps after this frame
            Vector3 vCur = playerRb.linearVelocity;
            Vector3 vCurH = new Vector3(vCur.x, 0f, vCur.z);

            float maxH = Mathf.Max(0f, knockbackMaxHorizontalSpeed);
            if (maxH > 0f)
            {
                Vector3 addH = new Vector3(deltaV.x, 0f, deltaV.z);
                float allowedAdd = Mathf.Max(0f, maxH - vCurH.magnitude);
                if (addH.magnitude > allowedAdd)
                {
                    Vector3 clamped = addH.normalized * allowedAdd;
                    deltaV.x = clamped.x; deltaV.z = clamped.z;
                }
            }

            float maxUp = Mathf.Max(0f, knockbackMaxUpSpeed);
            if (maxUp > 0f && deltaV.y > 0f)
            {
                float allowedUp = Mathf.Max(0f, maxUp - Mathf.Max(0f, vCur.y));
                if (deltaV.y > allowedUp)
                    deltaV.y = allowedUp;
            }

            mover.ApplyExternalImpulse(deltaV);
            return;
        }

        // Fallback: apply directly to the rigidbody and clamp velocity
        playerRb.AddForce(impulse, ForceMode.Impulse);

        Vector3 v = playerRb.linearVelocity;
        Vector3 vHoriz = new Vector3(v.x, 0f, v.z);
        float maxH2 = Mathf.Max(0f, knockbackMaxHorizontalSpeed);
        if (vHoriz.magnitude > maxH2 && maxH2 > 0f)
        {
            Vector3 clampedH = vHoriz.normalized * maxH2;
            v.x = clampedH.x; v.z = clampedH.z;
        }

        float maxUp2 = Mathf.Max(0f, knockbackMaxUpSpeed);
        if (v.y > maxUp2 && maxUp2 > 0f) v.y = maxUp2;
        playerRb.linearVelocity = v;
    }

    void ApplyExplosionDamage(GameObject playerObj)
    {
        if (explosionDamage <= 0) return;
        var ph = playerObj.GetComponent<PlayerHealth>();
        if (ph != null)
        {
            ph.TakeDamage(explosionDamage);
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
