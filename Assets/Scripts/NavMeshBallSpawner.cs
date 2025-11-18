using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class NavMeshBallSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] GameObject enemyBallPrefab;
    [SerializeField] GameObject fleeBallPrefab;

    [Header("Spawn Settings")]
    [SerializeField] int enemyBallCount = 2;
    [SerializeField] int fleeBallCount = 2;
    [SerializeField] float minDistanceFromPlayer = 5f;
    [SerializeField] float maxDistanceFromPlayer = 30f;
    [SerializeField] int pathSampleCount = 10;
    [SerializeField] LayerMask groundMask = ~0;

    [Header("Player Reference")]
    [SerializeField] Transform player;

    [Header("Spawn Offset")]
    [SerializeField] Vector3 spawnOffset = new Vector3(0f, 1f, 0f); // default: 1 unit above ground


    [Header("Infinite Spawn Mode")]
    [SerializeField] bool infiniteSpawning = false;
    [SerializeField] float spawnInterval = 10f;
    float nextSpawnTime = 0f;

    void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }
        if (infiniteSpawning)
        {
            nextSpawnTime = Time.time + spawnInterval;
        }
        else
        {
            SpawnBalls();
        }
    }

    void Update()
    {
        if (infiniteSpawning && Time.time >= nextSpawnTime)
        {
            SpawnBalls();
            nextSpawnTime = Time.time + spawnInterval;
        }
    }

    public void SpawnBalls()
    {
        if (player == null) return;
        List<Vector3> spawnPositions = GetNavMeshPathNodesAroundPlayer(pathSampleCount);
        int totalBalls = enemyBallCount + fleeBallCount;
        if (spawnPositions.Count < totalBalls)
        {
            Debug.LogWarning("Not enough valid spawn positions found on NavMesh.");
            return;
        }

        int posIdx = 0;
        // Spawn EnemyBalls
        for (int i = 0; i < enemyBallCount; i++)
        {
            Vector3 pos = spawnPositions[posIdx++] + spawnOffset;
            Instantiate(enemyBallPrefab, pos, Quaternion.identity);
        }
        // Spawn FleeBalls
        for (int i = 0; i < fleeBallCount; i++)
        {
            Vector3 pos = spawnPositions[posIdx++] + spawnOffset;
            Instantiate(fleeBallPrefab, pos, Quaternion.identity);
        }
    }

    List<Vector3> GetNavMeshPathNodesAroundPlayer(int sampleCount)
    {
        List<Vector3> result = new List<Vector3>();
        int attempts = 0;
        while (result.Count < sampleCount && attempts < sampleCount * 5)
        {
            attempts++;
            // Pick a random direction and distance
            float angle = Random.Range(0f, 360f);
            float dist = Random.Range(minDistanceFromPlayer, maxDistanceFromPlayer);
            Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * dist;
            Vector3 target = player.position + offset;

            // Find nearest NavMesh point
            NavMeshHit hit;
            if (NavMesh.SamplePosition(target, out hit, 3f, NavMesh.AllAreas))
            {
                // Calculate path from player to this point
                NavMeshPath path = new NavMeshPath();
                if (NavMesh.CalculatePath(player.position, hit.position, NavMesh.AllAreas, path))
                {
                    if (path.status == NavMeshPathStatus.PathComplete && path.corners.Length > 1)
                    {
                        // Pick a random node along the path (not the first, which is player)
                        int nodeIdx = Random.Range(1, path.corners.Length);
                        Vector3 spawnPos = path.corners[nodeIdx];
                        // Optionally align to ground
                        if (Physics.Raycast(spawnPos + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit, 4f, groundMask, QueryTriggerInteraction.Ignore))
                        {
                            spawnPos = groundHit.point;
                        }
                        result.Add(spawnPos);
                    }
                }
            }
        }
        return result;
    }
}
