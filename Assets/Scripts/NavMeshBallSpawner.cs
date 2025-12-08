using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class NavMeshBallSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] GameObject enemyBallPrefab;
    [SerializeField] GameObject fleeBallPrefab;

    [Header("Warning Effect")]
    [Tooltip("Optional effect prefab to spawn at the intended spawn locations before balls appear.")]
    [SerializeField] GameObject warningEffectPrefab;
    [Tooltip("Seconds the warning effect shows before the ball spawns.")]
    [SerializeField] float warningLeadTime = 1.5f;
    [Tooltip("Lifetime for the warning effect (<=0 keeps it until manually destroyed).")]
    [SerializeField] float warningEffectLifetime = 2f;
    [Tooltip("Uniform scale multiplier for the warning effect.")]
    [SerializeField] float warningEffectScale = 1f;
    [Tooltip("Vertical offset applied to the warning effect position.")]
    [SerializeField] Vector3 warningEffectOffset = Vector3.zero;

    [Header("Spawn Settings")]
    [SerializeField] int enemyBallCount = 2;
    [SerializeField] int fleeBallCount = 2;
    [Tooltip("Maximum number of EnemyBalls allowed alive at once.")]
    [SerializeField] int maxEnemyBalls = 6;
    [Tooltip("Maximum number of FleeBalls allowed alive at once.")]
    [SerializeField] int maxFleeBalls = 6;
    [SerializeField] float minDistanceFromPlayer = 5f;
    [SerializeField] float maxDistanceFromPlayer = 30f;
    [SerializeField] float navMeshSampleRadius = 5f;
    [SerializeField] LayerMask groundMask = ~0;

    [Tooltip("If true, require a complete NavMesh path from the player's position to the spawn point. Disable if the player is not on the NavMesh.")]
    [SerializeField] bool requirePathFromPlayer = false;

    [Header("Player Reference")]
    [SerializeField] Transform player;

    [Header("Spawn Offset")]
    [SerializeField] Vector3 spawnOffset = new Vector3(0f, 1f, 0f); // default: 1 unit above ground
    [Tooltip("Random horizontal jitter disabled to prevent wall spawns.")]



    [Header("Infinite Spawn Mode")]
    [SerializeField] bool infiniteSpawning = false;
    [SerializeField] float spawnInterval = 10f;
    float nextSpawnTime = 0f;
    [Tooltip("Minimum seconds between spawn attempts (prevents rapid retries)")]
    [SerializeField] float spawnCooldown = 0.5f;
    float lastSpawnAttemptTime = -999f;

    [Header("Counter Health")]
    [Tooltip("Periodically recount live entities to correct counters if some deaths don't fire events.")]
    [SerializeField] float recountInterval = 5.0f; // Increased from 1.0 to reduce FindObjectsByType calls
    float nextRecountTime = 0f;

    // Tracking currently alive instances
    int currentEnemyAlive = 0;
    int currentFleeAlive = 0;
    // Pending spawns (e.g., waiting for warningLeadTime)
    int pendingEnemySpawns = 0;
    int pendingFleeSpawns = 0;

    // Cached lists to avoid GC (optional)
    readonly List<Vector3> tempPositions = new List<Vector3>();

    void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
            if (player == null)
            {
                Debug.LogWarning("NavMeshBallSpawner: No player Transform set and no GameObject with tag 'Player' found. Spawning will not occur.");
            }
        }
        if (infiniteSpawning)
        {
            nextSpawnTime = Time.time + spawnInterval;
        }
        else
        {
            SpawnBalls();
        }

        // Subscribe to spawn notifier events
        SpawnedEntityNotifier.OnEntityDied += HandleEntityDied;

        // Initialize alive counts based on existing notifiers in scene
        RecountAlive();
    }

    void Update()
    {
        if (infiniteSpawning && Time.time >= nextSpawnTime)
        {
            SpawnBalls();
            nextSpawnTime = Time.time + spawnInterval;
        }

        // Periodic recount to ensure caps aren't stuck due to missed events
        if (Time.time >= nextRecountTime)
        {
            RecountAlive();
            nextRecountTime = Time.time + recountInterval;
        }
    }

    void OnDestroy()
    {
        SpawnedEntityNotifier.OnEntityDied -= HandleEntityDied;
    }

    public void SpawnBalls()
    {
        if (player == null) return;
        // Rate-limit spawn attempts to avoid repeated retries causing job churn
        if (Time.time - lastSpawnAttemptTime < spawnCooldown) return;
        lastSpawnAttemptTime = Time.time;
        // Ensure counts reflect current scene state before deciding room
        RecountAlive();
        // Determine how many to spawn now. In infinite mode, treat counts as batch sizes under caps.
        int enemyRoom = Mathf.Max(0, maxEnemyBalls - (currentEnemyAlive + pendingEnemySpawns));
        int fleeRoom = Mathf.Max(0, maxFleeBalls - (currentFleeAlive + pendingFleeSpawns));

        int enemyNeeded = infiniteSpawning
            ? Mathf.Min(enemyBallCount, enemyRoom)
            : Mathf.Clamp(enemyBallCount - currentEnemyAlive, 0, enemyRoom);
        int fleeNeeded = infiniteSpawning
            ? Mathf.Min(fleeBallCount, fleeRoom)
            : Mathf.Clamp(fleeBallCount - currentFleeAlive, 0, fleeRoom);

        int totalNeeded = enemyNeeded + fleeNeeded;
        if (totalNeeded <= 0) return;

        tempPositions.Clear();
        tempPositions.AddRange(GetValidSpawnPositions(totalNeeded));
        if (tempPositions.Count == 0) return;

        int posIdx = 0;
        // Spawn EnemyBalls first
        int enemiesToSpawn = Mathf.Min(enemyNeeded, tempPositions.Count - posIdx);
        for (int i = 0; i < enemiesToSpawn; i++)
        {
            SpawnWithWarning(enemyBallPrefab, tempPositions[posIdx++]);
        }

        // Spawn FleeBalls
        int fleesToSpawn = Mathf.Min(fleeNeeded, tempPositions.Count - posIdx);
        for (int i = 0; i < fleesToSpawn; i++)
        {
            SpawnWithWarning(fleeBallPrefab, tempPositions[posIdx++]);
        }
    }

    void SpawnWithWarning(GameObject prefab, Vector3 pos)
    {
        bool useWarning = warningEffectPrefab != null && warningLeadTime > 0f;
        if (useWarning)
        {
            // Orient effect with ground normal as up vector
            Quaternion rot = Quaternion.identity;
            if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2f, groundMask, QueryTriggerInteraction.Ignore))
            {
                // Align the effect's up direction with the ground normal
                rot = Quaternion.FromToRotation(Vector3.up, hit.normal);
            }
            GameObject fx = Instantiate(warningEffectPrefab, pos + warningEffectOffset, rot);
            if (warningEffectScale > 0f) fx.transform.localScale = Vector3.one * warningEffectScale;
            if (warningEffectLifetime > 0f)
            {
                // Gracefully stop looping particle systems and let them fade before destroy
                StartCoroutine(StopEffectGracefully(fx, warningEffectLifetime));
            }
            // Reserve a pending slot so caps are respected during delay
            if (prefab == enemyBallPrefab) pendingEnemySpawns = Mathf.Min(maxEnemyBalls, pendingEnemySpawns + 1);
            else if (prefab == fleeBallPrefab) pendingFleeSpawns = Mathf.Min(maxFleeBalls, pendingFleeSpawns + 1);
            // Delay actual spawn
            StartCoroutine(SpawnDelayed(prefab, pos, warningLeadTime));
        }
        else
        {
            // No delay; instantiate immediately
            InstantiateAndRegister(prefab, pos);
        }
    }

    System.Collections.IEnumerator SpawnDelayed(GameObject prefab, Vector3 pos, float delay)
    {
        yield return new WaitForSeconds(delay);
        InstantiateAndRegister(prefab, pos);
    }

    void InstantiateAndRegister(GameObject prefab, Vector3 pos)
    {
        GameObject go = Instantiate(prefab, pos, Quaternion.identity);
        var notifier = go.GetComponent<SpawnedEntityNotifier>();
        if (notifier == null)
        {
            notifier = go.AddComponent<SpawnedEntityNotifier>();
        }
        // Determine type based on prefab reference
        if (prefab == enemyBallPrefab)
            notifier.EntityType = SpawnedEntityNotifier.Type.Enemy;
        else if (prefab == fleeBallPrefab)
            notifier.EntityType = SpawnedEntityNotifier.Type.Flee;
        else
            notifier.EntityType = SpawnedEntityNotifier.Type.Unknown;

        // Increment counts on actual spawn
        if (notifier.EntityType == SpawnedEntityNotifier.Type.Enemy)
        {
            // Consume pending reservation if any
            if (pendingEnemySpawns > 0) pendingEnemySpawns = Mathf.Max(0, pendingEnemySpawns - 1);
            currentEnemyAlive = Mathf.Min(maxEnemyBalls, currentEnemyAlive + 1);
        }
        else if (notifier.EntityType == SpawnedEntityNotifier.Type.Flee)
        {
            if (pendingFleeSpawns > 0) pendingFleeSpawns = Mathf.Max(0, pendingFleeSpawns - 1);
            currentFleeAlive = Mathf.Min(maxFleeBalls, currentFleeAlive + 1);
        }
    }

    System.Collections.IEnumerator StopEffectGracefully(GameObject effectRoot, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (effectRoot == null) yield break;

        // Stop all ParticleSystems so they fade naturally, then destroy them after their lifetimes
        var particles = effectRoot.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particles)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            // Detach to allow independent cleanup
            ps.transform.SetParent(null);
            var main = ps.main;
            float lifetime = main.startLifetime.constantMax + main.duration;
            if (lifetime > 0f)
                Destroy(ps.gameObject, lifetime);
        }

        // Finally remove the root object
        Destroy(effectRoot);
    }

    Vector3 AlignToGround(Vector3 pos, LayerMask mask)
    {
        if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit, 4f, mask, QueryTriggerInteraction.Ignore))
        {
            return groundHit.point;
        }
        return pos;
    }

    List<Vector3> GetValidSpawnPositions(int count)
    {
        List<Vector3> result = new List<Vector3>();
        int maxAttempts = count * 10; // Allow more attempts than needed
        int attempts = 0;
        float checkRadius = 0.5f; // Adjust as needed for ball size

        while (result.Count < count && attempts < maxAttempts)
        {
            attempts++;

            // Generate random position in ring around player
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(minDistanceFromPlayer, maxDistanceFromPlayer);
            Vector3 randomDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 targetPos = player.position + randomDir * distance;

            // Find closest point on NavMesh
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(targetPos, out navHit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                Vector3 navPos = navHit.position;

                // Verify it's reachable from player and within distance constraints
                float actualDist = Vector3.Distance(player.position, navPos);
                if (actualDist >= minDistanceFromPlayer && actualDist <= maxDistanceFromPlayer)
                {
                    bool passesPathCheck = true;
                    if (requirePathFromPlayer)
                    {
                        NavMeshPath path = new NavMeshPath();
                        passesPathCheck = NavMesh.CalculatePath(player.position, navPos, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
                        if (!passesPathCheck)
                        {
                            Debug.Log("NavMeshBallSpawner: Path check failed from player to sampled position. Consider disabling 'requirePathFromPlayer' if the player isn't on NavMesh.");
                        }
                    }
                    if (passesPathCheck)
                    {
                        Vector3 finalPos = AlignToGround(navPos, groundMask) + spawnOffset;
                        // Prevent spawn above player (allow small offset)
                        if (finalPos.y > player.position.y + 0.5f)
                            continue;
                        // Prevent spawn inside objects
                        var overlaps = Physics.OverlapSphere(finalPos, checkRadius, groundMask, QueryTriggerInteraction.Ignore);
                        if (overlaps != null && overlaps.Length > 0)
                            continue;
                        result.Add(finalPos);
                    }
                }
            }
            else
            {
                // Throttle logging to avoid spamming the console
                if (attempts == 1 || (attempts % 20) == 0)
                {
                    Debug.Log("NavMeshBallSpawner: SamplePosition failed near target; ensure NavMesh is baked and sample radius is sufficient.");
                }
            }
        }

        if (result.Count == 0)
        {
            Debug.LogWarning("NavMeshBallSpawner: No valid spawn positions found. Common causes: player not set or not on NavMesh, NavMesh not baked in area, path requirement too strict, or spawn blocked by overlap/height checks.");
        }
        return result;
    }

    void HandleEntityDied(SpawnedEntityNotifier.Type type, SpawnedEntityNotifier.DeathCause cause)
    {
        // Update alive counters
        if (type == SpawnedEntityNotifier.Type.Enemy)
            currentEnemyAlive = Mathf.Max(0, currentEnemyAlive - 1);
        else if (type == SpawnedEntityNotifier.Type.Flee)
            currentFleeAlive = Mathf.Max(0, currentFleeAlive - 1);

        // If an enemy died to player, immediately replace if under max
        if (type == SpawnedEntityNotifier.Type.Enemy && cause == SpawnedEntityNotifier.DeathCause.Player)
        {
            // Consider pending reservations to avoid overfill
            if ((currentEnemyAlive + pendingEnemySpawns) < maxEnemyBalls)
            {
                var positions = GetValidSpawnPositions(1);
                if (positions.Count > 0)
                {
                    SpawnWithWarning(enemyBallPrefab, positions[0]);
                }
            }
        }

        // FleeBalls do not immediately respawn; they are spawned in batches up to maxFleeBalls
    }

    void RecountAlive()
    {
        int enemies = 0;
        int flees = 0;

        // Count EnemyBall instances
        var enemyBalls = GameObject.FindObjectsByType<EnemyBall>(FindObjectsSortMode.None);
        foreach (var eb in enemyBalls)
        {
            if (eb.isActiveAndEnabled) enemies++;
        }

        // Count FleeBall instances
        var fleeBalls = GameObject.FindObjectsByType<FleeBall>(FindObjectsSortMode.None);
        foreach (var fb in fleeBalls)
        {
            if (fb.isActiveAndEnabled) flees++;
        }

        currentEnemyAlive = enemies;
        currentFleeAlive = flees;
    }
}
