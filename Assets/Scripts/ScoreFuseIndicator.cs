using UnityEngine;
using TMPro;

/// <summary>
/// Score Fuse Indicator - Displays how much score is needed to spawn the next fuse
/// Works with ScoreRewardSpawner to show progress toward fuse spawn
/// </summary>
public class ScoreFuseIndicator : MonoBehaviour
{
    [Header("UI Display")]
    [Tooltip("TextMeshPro component to display score needed message")]
    [SerializeField] TextMeshProUGUI scoreNeededText;

    [Header("Fuse Spawn Settings")]
    [Tooltip("Score threshold to spawn fuse (must match ScoreRewardSpawner setting)")]
    [SerializeField] int scoreThresholdForFuse = 100;

    [Tooltip("Hide the text once fuse is spawned")]
    [SerializeField] bool hideAfterSpawn = true;

    private int lastSpawnedFuseThreshold = 0;
    private bool fuseSpawned = false;

    void Start()
    {
        if (scoreNeededText == null)
        {
            Debug.LogWarning("ScoreFuseIndicator: No text element assigned!");
            return;
        }

        // Initialize display
        UpdateDisplay();
    }

    void Update()
    {
        if (ScoreManager.Instance == null || scoreNeededText == null) return;

        int currentScore = ScoreManager.Instance.Score;

        // Check if we've spawned a fuse (crossed the threshold)
        int currentThreshold = Mathf.FloorToInt((float)currentScore / scoreThresholdForFuse);
        if (currentThreshold > lastSpawnedFuseThreshold)
        {
            lastSpawnedFuseThreshold = currentThreshold;
            fuseSpawned = true;

            if (hideAfterSpawn)
            {
                scoreNeededText.gameObject.SetActive(false);
                return;
            }
        }

        // If hiding after spawn and it happened, don't update
        if (fuseSpawned && hideAfterSpawn) return;

        UpdateDisplay();
    }

    void UpdateDisplay()
    {
        if (ScoreManager.Instance == null) return;

        int currentScore = ScoreManager.Instance.Score;
        
        // Calculate next threshold
        int nextThreshold = (Mathf.FloorToInt((float)currentScore / scoreThresholdForFuse) + 1) * scoreThresholdForFuse;
        int scoreNeeded = nextThreshold - currentScore;

        // Update text
        scoreNeededText.text = $"Need {scoreNeeded} score to spawn fuse!";
    }

    /// <summary>
    /// Reset the indicator (useful when respawning or between levels)
    /// </summary>
    public void Reset()
    {
        lastSpawnedFuseThreshold = 0;
        fuseSpawned = false;
        if (scoreNeededText != null)
        {
            scoreNeededText.gameObject.SetActive(true);
        }
        UpdateDisplay();
    }
}
