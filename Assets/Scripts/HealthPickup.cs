using UnityEngine;

/// <summary>
/// Health pickup that heals the player on collision and spawns a particle effect on pickup.
/// Attach to health pack prefabs and ensure they have a Collider with "Is Trigger" checked.
/// </summary>
public class HealthPickup : MonoBehaviour
{
    [Header("Healing")]
    [Tooltip("Amount of health restored to the player.")]
    [SerializeField] int healAmount = 25;

    [Header("Particle Effect")]
    [Tooltip("Optional particle effect prefab to spawn when picked up.")]
    [SerializeField] GameObject pickupEffectPrefab;
    [Tooltip("Scale multiplier for the particle effect.")]
    [SerializeField] float effectScale = 1f;
    [Tooltip("Seconds before the effect is destroyed (0 = use particle lifetime).")]
    [SerializeField] float effectLifetime = 3f;

    [Header("Audio (Optional)")]
    [Tooltip("Optional audio clip to play on pickup.")]
    [SerializeField] AudioClip pickupSound;
    [Tooltip("Volume for the pickup sound.")]
    [SerializeField] [Range(0f, 1f)] float soundVolume = 1f;

    bool collected = false;

    void OnTriggerEnter(Collider other)
    {
        // Prevent multiple pickups
        if (collected) return;

        // Check if the colliding object is the player
        if (!other.CompareTag("Player")) return;

        // Try to heal the player
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            Debug.LogWarning($"HealthPickup: Player tagged object '{other.name}' has no PlayerHealth component!");
            return;
        }

        // Mark as collected to prevent double-pickup
        collected = true;

        // Heal the player
        playerHealth.Heal(healAmount);

        // Spawn particle effect at pickup position
        if (pickupEffectPrefab != null)
        {
            GameObject effect = Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);
            effect.transform.localScale = Vector3.one * effectScale;

            // Destroy the effect after the specified lifetime
            if (effectLifetime > 0f)
            {
                Destroy(effect, effectLifetime);
            }
        }

        // Play pickup sound
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, soundVolume);
        }

        // Stop particle systems so they fade naturally instead of being cut off
        GameObject targetToDestroy = transform.parent != null ? transform.parent.gameObject : gameObject;
        ParticleSystem[] particles = targetToDestroy.GetComponentsInChildren<ParticleSystem>();
        foreach (ParticleSystem ps in particles)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            
            // Unparent the particle system so it won't be destroyed with the parent
            ps.transform.SetParent(null);
            
            // Destroy the particle system after its lifetime
            float maxLifetime = ps.main.startLifetime.constantMax + ps.main.duration;
            Destroy(ps.gameObject, maxLifetime);
        }

        // Destroy the parent GameObject (and all its children)
        Destroy(targetToDestroy);
    }

    void OnValidate()
    {
        // Ensure heal amount is positive
        if (healAmount < 0)
        {
            healAmount = 0;
        }
    }
}
