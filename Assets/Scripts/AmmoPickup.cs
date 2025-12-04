using UnityEngine;

/// <summary>
/// Ammo pickup that adds ammo to the player's AmmoSystem and plays optional effects.
/// Attach to the ammo drop prefab. Requires a trigger collider.
/// </summary>
public class AmmoPickup : MonoBehaviour
{
    [Header("Pickup Amount")]
    [SerializeField] int ammoAmount = 20;

    [Header("Particle Effect")]
    [SerializeField] GameObject pickupEffectPrefab;
    [SerializeField] float effectScale = 1f;
    [SerializeField] float effectLifetime = 2f;

    [Header("Audio (Optional)")]
    [SerializeField] AudioClip pickupSound;
    [Range(0f,1f)] [SerializeField] float soundVolume = 1f;

    bool collected;

    void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (!other.CompareTag("Player")) return;

        var ammo = other.GetComponentInChildren<AmmoSystem>();
        if (ammo == null)
        {
            Debug.LogWarning($"AmmoPickup: Player '{other.name}' has no AmmoSystem component!");
            return;
        }

        // If ammo is already at max, do not collect
        if (ammo.CurrentAmmo >= ammo.MaxAmmo)
        {
            return;
        }

        collected = true;
        ammo.AddAmmo(ammoAmount);

        if (pickupEffectPrefab)
        {
            var fx = Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);
            fx.transform.localScale = Vector3.one * effectScale;
            if (effectLifetime > 0f) Destroy(fx, effectLifetime);
        }

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

    void OnValidate()
    {
        if (ammoAmount < 0) ammoAmount = 0;
    }
}
