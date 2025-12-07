using UnityEngine;

public class ScoreRewardSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("Prefab to spawn when score threshold is reached")]
    [SerializeField] GameObject rewardPrefab;

    [Tooltip("Score required to trigger the spawn")]
    [SerializeField] int scoreThreshold = 100;

    [Tooltip("Should the prefab spawn only once, or every time the threshold is reached?")]
    [SerializeField] bool spawnOnce = true;

    [Header("Spawn Location")]
    [Tooltip("Where to spawn the prefab. Leave null to spawn at this GameObject's position")]
    [SerializeField] Transform spawnPoint;

    [Tooltip("Random offset range for spawn position (0 = exact position)")]
    [SerializeField] float randomSpawnRadius = 0f;

    [Tooltip("Optional: Spawn at player's position instead")]
    [SerializeField] bool spawnAtPlayer = false;

    [Header("Optional Settings")]
    [Tooltip("Delay in seconds before spawning (0 = instant)")]
    [SerializeField] float spawnDelay = 0f;

    [Tooltip("Should the spawned object be automatically destroyed after some time?")]
    [SerializeField] bool autoDestroySpawned = false;

    [Tooltip("Time in seconds before auto-destroying the spawned object")]
    [SerializeField] float destroyAfter = 10f;

    private bool hasSpawned = false;
    private int lastCheckedScore = 0;

    void Update()
    {
        // Check if ScoreManager exists
        if (ScoreManager.Instance == null) return;

        int currentScore = ScoreManager.Instance.Score;

        // Check if we've reached the threshold
        if (currentScore >= scoreThreshold)
        {
            // If spawn once, only spawn if we haven't already
            if (spawnOnce)
            {
                if (!hasSpawned)
                {
                    SpawnReward();
                    hasSpawned = true;
                }
            }
            else
            {
                // Spawn every time we cross a multiple of the threshold
                int currentMultiple = currentScore / scoreThreshold;
                int lastMultiple = lastCheckedScore / scoreThreshold;

                if (currentMultiple > lastMultiple)
                {
                    SpawnReward();
                }
            }
        }

        lastCheckedScore = currentScore;
    }

    void SpawnReward()
    {
        if (rewardPrefab == null)
        {
            Debug.LogWarning("ScoreRewardSpawner: No reward prefab assigned!");
            return;
        }

        if (spawnDelay > 0f)
        {
            Invoke(nameof(DoSpawn), spawnDelay);
        }
        else
        {
            DoSpawn();
        }
    }

    void DoSpawn()
    {
        Vector3 spawnPosition = GetSpawnPosition();
        GameObject spawned = Instantiate(rewardPrefab, spawnPosition, Quaternion.identity);

        Debug.Log($"ScoreRewardSpawner: Spawned {rewardPrefab.name} at score {ScoreManager.Instance.Score}");

        if (autoDestroySpawned && destroyAfter > 0f)
        {
            Destroy(spawned, destroyAfter);
        }
    }

    Vector3 GetSpawnPosition()
    {
        Vector3 basePosition;

        if (spawnAtPlayer)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                basePosition = player.transform.position;
            }
            else
            {
                Debug.LogWarning("ScoreRewardSpawner: Player not found, using spawn point instead");
                basePosition = spawnPoint != null ? spawnPoint.position : transform.position;
            }
        }
        else
        {
            basePosition = spawnPoint != null ? spawnPoint.position : transform.position;
        }

        // Add random offset if specified
        if (randomSpawnRadius > 0f)
        {
            Vector2 randomCircle = Random.insideUnitCircle * randomSpawnRadius;
            basePosition += new Vector3(randomCircle.x, 0f, randomCircle.y);
        }

        return basePosition;
    }

    /// <summary>
    /// Reset the spawner so it can spawn again (useful if spawnOnce is true)
    /// </summary>
    public void ResetSpawner()
    {
        hasSpawned = false;
        lastCheckedScore = 0;
    }

    /// <summary>
    /// Manually trigger a spawn (ignores threshold check)
    /// </summary>
    public void ForceSpawn()
    {
        SpawnReward();
    }

#if UNITY_EDITOR
    // Visualize spawn point in editor
    void OnDrawGizmos()
    {
        if (spawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(spawnPoint.position, 0.5f);

            if (randomSpawnRadius > 0f)
            {
                Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
                Gizmos.DrawWireSphere(spawnPoint.position, randomSpawnRadius);
            }
        }
    }
#endif
}
