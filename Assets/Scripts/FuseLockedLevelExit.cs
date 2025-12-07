using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Level Exit with Fuse Lock - Player must collect all fuses to progress
/// </summary>
public class FuseLockedLevelExit : MonoBehaviour
{
    [Header("Level Progression")]
    [Tooltip("The name of the next scene to load")]
    [SerializeField] string nextLevelName = "Scenes/Level2";

    [Header("Visual Feedback")]
    [Tooltip("Material or color to show when locked")]
    [SerializeField] Color lockedColor = Color.red;

    [Tooltip("Material or color to show when unlocked")]
    [SerializeField] Color unlockedColor = Color.green;

    [Tooltip("Optional: Particle effect to spawn when unlocked")]
    [SerializeField] GameObject unlockedEffectPrefab;

    [Header("Audio (Optional)")]
    [Tooltip("Sound to play when attempting to enter while locked")]
    [SerializeField] AudioClip lockedSound;

    [Tooltip("Sound to play when successfully entering")]
    [SerializeField] AudioClip unlockedSound;

    private Renderer rend;
    private AudioSource audioSource;
    private bool isUnlocked = false;

    void Start()
    {
        rend = GetComponent<Renderer>();
        audioSource = GetComponent<AudioSource>();

        // Set initial locked state
        UpdateVisuals();
    }

    void Update()
    {
        // Check if fuses are collected
        if (!isUnlocked && FuseManager.Instance != null && FuseManager.Instance.HasAllFuses)
        {
            UnlockExit();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (isUnlocked)
            {
                // All fuses collected - progress to next level
                EnterNextLevel();
            }
            else
            {
                // Not enough fuses
                ShowLockedMessage();
            }
        }
    }

    void UnlockExit()
    {
        isUnlocked = true;
        Debug.Log("Level exit unlocked!");

        // Play unlock sound
        if (audioSource != null && unlockedSound != null)
        {
            audioSource.PlayOneShot(unlockedSound);
        }

        // Spawn unlock effect
        if (unlockedEffectPrefab != null)
        {
            Instantiate(unlockedEffectPrefab, transform.position, Quaternion.identity);
        }

        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        if (rend != null)
        {
            rend.material.color = isUnlocked ? unlockedColor : lockedColor;
        }
    }

    void ShowLockedMessage()
    {
        // Play locked sound
        if (audioSource != null && lockedSound != null)
        {
            audioSource.PlayOneShot(lockedSound);
        }

        // Show debug message (optional: implement UI popup here)
        if (FuseManager.Instance != null)
        {
            int fuseCount = FuseManager.Instance.FuseCount;
            int fusesNeeded = FuseManager.Instance.FusesNeeded;
            Debug.Log($"Exit locked! You need {fusesNeeded - fuseCount} more fuse(s). ({fuseCount}/{fusesNeeded})");
        }
    }

    void EnterNextLevel()
    {
        Debug.Log($"Loading level: {nextLevelName}");
        
        // Verify scene exists in build settings
        if (SceneUtility.GetBuildIndexByScenePath(nextLevelName) >= 0)
        {
            SceneManager.LoadScene(nextLevelName);
        }
        else
        {
            Debug.LogError($"Scene '{nextLevelName}' not found in Build Settings! Make sure it's added to File > Build Settings.");
        }
    }
}
