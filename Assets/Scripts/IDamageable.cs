using UnityEngine;

public interface IDamageable
{
    // Called when a hitscan (or other) applies damage. Implement on enemies/targets.
    void ApplyDamage(float amount, RaycastHit hitInfo);
}

