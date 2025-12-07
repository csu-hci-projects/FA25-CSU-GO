using UnityEngine;
using TMPro;

/// <summary>
/// Fuse Manager - Manages fuse count and HUD display
/// Works like a key system - player needs X fuses to open level exit
/// </summary>
public class FuseManager : MonoBehaviour
{
    [Header("Singleton")]
    public static FuseManager Instance { get; private set; }

    [Header("Fuse Settings")]
    [Tooltip("Number of fuses needed to unlock the level exit")]
    [SerializeField] int fusesNeededToProgress = 3;

    [Header("UI Display")]
    [Tooltip("TextMeshPro component to display fuse count (e.g., 'Fuses: 2/3')")]
    [SerializeField] TextMeshProUGUI fuseCountText;

    [Tooltip("Optional: Image to fill as fuses are collected")]
    [SerializeField] UnityEngine.UI.Image fuseProgressImage;

    [Tooltip("Optional: Image to show/hide when fuses are collected")]
    [SerializeField] UnityEngine.UI.Image fuseInventoryImage;

    [Tooltip("TextMeshPro component to display score needed to progress")]
    [SerializeField] TextMeshProUGUI scoreNeededText;

    [Header("Audio (Optional)")]
    [Tooltip("Sound to play when fuse is picked up")]
    [SerializeField] AudioClip pickupSound;

    [Tooltip("Volume for pickup sound")]
    [SerializeField, Range(0f, 1f)] float pickupVolume = 0.8f;

    private int fuseCount = 0;
    private AudioSource audioSource;

    void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // Get audio source if available
        audioSource = GetComponent<AudioSource>();
        
        // Hide inventory image at start (no fuses collected yet)
        if (fuseInventoryImage != null)
        {
            fuseInventoryImage.gameObject.SetActive(false);
        }
        
        // Initialize display
        UpdateHUD();
    }

    /// <summary>
    /// Add one fuse to the collection
    /// </summary>
    public void AddFuse()
    {
        fuseCount++;

        // Play pickup sound
        if (audioSource != null && pickupSound != null)
        {
            audioSource.PlayOneShot(pickupSound, pickupVolume);
        }

        UpdateHUD();

        // Check if player has enough fuses
        if (fuseCount >= fusesNeededToProgress)
        {
            OnAllFusesCollected();
        }
    }

    /// <summary>
    /// Get current fuse count
    /// </summary>
    public int FuseCount => fuseCount;

    /// <summary>
    /// Get fuses needed to progress
    /// </summary>
    public int FusesNeeded => fusesNeededToProgress;

    /// <summary>
    /// Check if player has all fuses needed
    /// </summary>
    public bool HasAllFuses => fuseCount >= fusesNeededToProgress;

    /// <summary>
    /// Reset fuse count (useful between levels)
    /// </summary>
    public void ResetFuses()
    {
        fuseCount = 0;
        UpdateHUD();
    }

    void UpdateHUD()
    {
        // Update text display
        if (fuseCountText != null)
        {
            fuseCountText.text = $"Fuses: {fuseCount}/{fusesNeededToProgress}";

            // Optional: Change color when complete
            if (HasAllFuses)
            {
                fuseCountText.color = Color.green;
            }
            else
            {
                fuseCountText.color = Color.white;
            }
        }

        // Update progress image fill
        if (fuseProgressImage != null)
        {
            fuseProgressImage.fillAmount = (float)fuseCount / fusesNeededToProgress;
        }

        // Show/hide inventory image based on fuse count
        if (fuseInventoryImage != null)
        {
            fuseInventoryImage.gameObject.SetActive(fuseCount > 0);
        }

        // Update score needed display
        if (scoreNeededText != null && ScoreManager.Instance != null)
        {
            int currentScore = ScoreManager.Instance.Score;
            int scoreNeeded = Mathf.Max(0, fusesNeededToProgress - fuseCount);
            scoreNeededText.text = $"Score: {currentScore}";
        }
    }

    void OnAllFusesCollected()
    {
        Debug.Log("All fuses collected! Level exit is now accessible.");
        
        // Activate level exit or unlock it
        // This will be called automatically when player collects final fuse
    }
}
