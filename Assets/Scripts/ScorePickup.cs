using UnityEngine;

public class ScorePickup : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Amount of score to give when picked up")]
    [SerializeField] int scoreAmount = 8000;

    [Header("Audio (Optional)")]
    [SerializeField] AudioClip pickupSound;
    [SerializeField, Range(0f, 1f)] float volume = 0.8f;

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object colliding is the Player
        // Make sure your player object has the tag "Player"
        if (other.CompareTag("Player"))
        {
            CollectScore();
        }
    }

    void CollectScore()
    {
        // 1. Add score using your existing ScoreManager logic
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddPoints(scoreAmount);
        }
        else
        {
            Debug.LogWarning("ScoreManager missing from scene!");
        }

        // 2. Play sound at this location before destroying the object
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, volume);
        }

        // 3. Remove the object from the game
        Destroy(gameObject);
    }
}