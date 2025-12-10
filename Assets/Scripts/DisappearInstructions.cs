using UnityEngine;

public class DestroyAfterTime : MonoBehaviour
{
    // You can change this number in the Inspector if you make it public
    public float delay = 6f;

    void Start()
    {
        // The Destroy function takes an optional second argument for time delay
        Destroy(gameObject, delay);
    }
}