using UnityEngine;

public class EnemyBall : MonoBehaviour
{
    [SerializeField] float moveSpeed = 3f;
    [SerializeField] float detectionRange = 10f;
    [SerializeField] int damageAmount = 10;
    [SerializeField] float damageInterval = 1f;

    Transform player;
    float lastDamageTime;

    void Start()
    {
        // Find the player by tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }

    void Update()
    {
        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // Only track if player is within detection range
        if (distanceToPlayer <= detectionRange)
        {
            // Move towards the player
            Vector3 direction = (player.position - transform.position).normalized;
            transform.position += direction * moveSpeed * Time.deltaTime;

            // Optional: Make the ball face the player
            transform.LookAt(player);
        }
    }

    void OnCollisionStay(Collision collision)
    {
        // Deal damage when touching the player
        if (collision.gameObject.CompareTag("Player"))
        {
            if (Time.time >= lastDamageTime + damageInterval)
            {
                PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damageAmount);
                    lastDamageTime = Time.time;
                }
            }
        }
    }
}
