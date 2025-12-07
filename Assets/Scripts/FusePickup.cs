using UnityEngine;

/// <summary>
/// Fuse Pickup - Collect this prefab to gain a fuse for level progression
/// Automatically destroyed when picked up by the player
/// </summary>
public class FusePickup : MonoBehaviour
{
    [Header("Particle Effect")]
    [SerializeField] GameObject pickupEffectPrefab;
    [SerializeField] float effectScale = 1f;
    [SerializeField] float effectLifetime = 2f;

    [Header("Audio (Optional)")]
    [SerializeField] AudioClip pickupSound;
    [Range(0f, 1f)] [SerializeField] float soundVolume = 1f;

    private bool isPickedUp = false;

    private void OnTriggerEnter(Collider other)
    {
        // Check if player picked it up
        if (other.CompareTag("Player"))
        {
            PickupFuse();
        }
    }

    void PickupFuse()
    {
        if (isPickedUp) return; // Prevent multiple pickups
        isPickedUp = true;

        // Add fuse to manager
        if (FuseManager.Instance != null)
        {
            FuseManager.Instance.AddFuse();
            Debug.Log("Fuse picked up! Total fuses: " + FuseManager.Instance.FuseCount);
        }
        else
        {
            Debug.LogWarning("FuseManager not found in scene!");
        }

        // Spawn pickup effect
        if (pickupEffectPrefab)
        {
            var fx = Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);
            fx.transform.localScale = Vector3.one * effectScale;
            if (effectLifetime > 0f) Destroy(fx, effectLifetime);
        }

        // Play pickup sound
        if (pickupSound)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, soundVolume);
        }

        // Gracefully stop and detach any particle systems before destroying
        GameObject targetToDestroy = transform.parent != null ? transform.parent.gameObject : gameObject;
        var particles = targetToDestroy.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particles)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            ps.transform.SetParent(null);
            float maxLifetime = ps.main.startLifetime.constantMax + ps.main.duration;
            Destroy(ps.gameObject, maxLifetime);
        }

        Destroy(targetToDestroy);
    }
}


