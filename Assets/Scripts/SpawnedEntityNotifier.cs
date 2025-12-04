using UnityEngine;
using System;

// Attach to spawned Enemy/Flee objects. Call NotifyDeath(...) when they die.
public class SpawnedEntityNotifier : MonoBehaviour
{
    public enum Type { Unknown, Enemy, Flee }
    public enum DeathCause { Unknown, Player, Environment, Other }

    public static event Action<Type, DeathCause> OnEntityDied;

    public Type EntityType = Type.Unknown;

    // Example public API you can call from your health/damage scripts
    public void NotifyDeath(DeathCause cause)
    {
        OnEntityDied?.Invoke(EntityType, cause);
        Destroy(gameObject);
    }
}
