using UnityEngine;

/// <summary>
/// Simple helper that moves a TrailRenderer from muzzle to hit point to visualize hitscan shots.
/// </summary>
[RequireComponent(typeof(TrailRenderer))]
public class BulletTracer : MonoBehaviour
{
    [Tooltip("Time it takes for the tracer to move from muzzle to impact.")]
    public float travelTime = 0.04f;
    [Tooltip("Extra time to keep the object alive so the trail can fade out.")]
    public float lingerTime = 0.04f;

    TrailRenderer trail;
    float baseWidth = 1f;
    Vector3 start;
    Vector3 end;
    float travelDuration;
    float lingerDuration;
    float travelTimer;
    float lifeTimer;
    bool initialized;

    void Awake()
    {
        trail = GetComponent<TrailRenderer>();
        if (trail != null)
        {
            baseWidth = trail.widthMultiplier;
        }
    }

    public void Initialize(Vector3 start, Vector3 end, float overrideLifetime = -1f, float widthScale = 1f)
    {
        this.start = start;
        this.end = end;
        transform.position = start;

        Vector3 dir = end - start;
        if (dir.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(dir.normalized);
        }

        float totalLifetime = (overrideLifetime > 0f) ? overrideLifetime : (travelTime + lingerTime);
        travelDuration = (overrideLifetime > 0f) ? Mathf.Min(overrideLifetime, travelTime) : travelTime;
        lingerDuration = Mathf.Max(0f, totalLifetime - travelDuration);

        travelTimer = 0f;
        lifeTimer = 0f;

        if (trail != null)
        {
            trail.Clear();
            trail.emitting = true;
            trail.widthMultiplier = baseWidth * widthScale;
        }

        initialized = true;
    }

    void Update()
    {
        if (!initialized) return;

        travelTimer += Time.deltaTime;
        lifeTimer += Time.deltaTime;

        float t = (travelDuration > 0f) ? Mathf.Clamp01(travelTimer / travelDuration) : 1f;
        transform.position = Vector3.Lerp(start, end, t);

        if (trail != null && travelTimer >= travelDuration)
        {
            trail.emitting = false;
        }

        if (lifeTimer >= travelDuration + lingerDuration)
        {
            Destroy(gameObject);
        }
    }
}
