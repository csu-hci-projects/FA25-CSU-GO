using UnityEngine;
using System.Collections.Generic;

public class BallDustEffect : MonoBehaviour
{
    [Header("Particle Prefab (WARFX)")]
    [SerializeField] GameObject dustPrefab; // Assign your WARFX dust prefab here
    [SerializeField] float prefabScale = 1f; // scale multiplier for the spawned prefab

    [Header("Spawn Logic")] 
    [SerializeField] float minSpeedForDust = 0.5f; // minimum horizontal speed to spawn dust
    [SerializeField] float spawnInterval = 0.2f;   // how often to spawn
    [SerializeField] float spawnDelay = 0.2f;      // spawn at the position from this many seconds ago
    [SerializeField] Vector3 spawnOffset = new Vector3(0f, 0f, 0f); // additional world offset

    [Header("Ground Align")] 
    [SerializeField] bool alignToGround = true;
    [SerializeField] float raycastHeight = 1.0f;
    [SerializeField] float raycastDistance = 2.0f;
    [SerializeField] LayerMask groundMask = ~0; // default: everything

    [Header("Lifetime Override (Optional)")]
    [SerializeField] bool overrideLifetime = false;
    [SerializeField] float destroyAfterSeconds = 3f;

    struct PosSample { public float t; public Vector3 pos; }

    Rigidbody rb;
    List<PosSample> samples = new List<PosSample>(128);
    float nextSpawnTime = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (rb == null || dustPrefab == null) return;

        // Record current position with timestamp
        samples.Add(new PosSample { t = Time.time, pos = transform.position });
        // Prune old samples (keep ~2 seconds beyond needed delay)
        float pruneBefore = Time.time - (spawnDelay + 2f);
        int removeCount = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            if (samples[i].t < pruneBefore) removeCount++;
            else break;
        }
        if (removeCount > 0) samples.RemoveRange(0, removeCount);

        // Compute horizontal speed
        Vector3 vel = rb.linearVelocity;
        float speed = new Vector3(vel.x, 0f, vel.z).magnitude;
        if (speed < minSpeedForDust) return;
        if (Time.time < nextSpawnTime) return;

        // Determine target time in the past
        float targetTime = Time.time - spawnDelay;
        Vector3 spawnPos = transform.position;

        // Find the most recent sample at or before targetTime
        for (int i = samples.Count - 1; i >= 0; i--)
        {
            if (samples[i].t <= targetTime)
            {
                spawnPos = samples[i].pos;
                break;
            }
            else if (i == 0)
            {
                // No sample older than targetTime; use oldest available
                spawnPos = samples[0].pos;
            }
        }

        // Optionally align to ground
        Quaternion rot = Quaternion.identity;
        if (alignToGround)
        {
            Vector3 rayOrigin = spawnPos + Vector3.up * raycastHeight;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDistance + raycastHeight, groundMask, QueryTriggerInteraction.Ignore))
            {
                spawnPos = hit.point;
                rot = Quaternion.FromToRotation(Vector3.up, hit.normal);
            }
        }

        spawnPos += spawnOffset;

        // Spawn the WARFX prefab at the historical position (not parented)
        GameObject fx = Instantiate(dustPrefab, spawnPos, rot);
        fx.transform.localScale = Vector3.one * prefabScale;

        // Ensure it plays if it has a particle system
        var ps = fx.GetComponent<ParticleSystem>();
        if (ps == null) ps = fx.GetComponentInChildren<ParticleSystem>();
        if (ps != null && !ps.isPlaying) ps.Play();

        // Optional lifetime override (in case prefab doesn't self-destroy)
        if (overrideLifetime)
        {
            Destroy(fx, destroyAfterSeconds);
        }

        // Schedule next spawn
        nextSpawnTime = Time.time + spawnInterval;
    }
}