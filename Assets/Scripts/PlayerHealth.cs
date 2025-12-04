using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlayerHealth : MonoBehaviour
{
    public const int MaxHealth = 100;
    [SerializeField] int currentHealth = MaxHealth;
    [SerializeField] Slider healthSlider;
    [SerializeField] TextMeshProUGUI healthLabel;
    [SerializeField] string gameOverSceneName = ""; // if empty, reloads current scene
#if UNITY_EDITOR
    [Header("Game Over Scene (Drag)")]
    [Tooltip("Drag a Scene asset here; its name will be used for loading.")]
    [SerializeField] SceneAsset gameOverSceneAsset;
#endif
    [SerializeField] float deathDelay = 0.5f;

    bool isDead = false;

    void Awake()
    {
        currentHealth = MaxHealth;
        UpdateUI();
    }

    public int CurrentHealth => currentHealth;
    public bool IsAtMax => currentHealth >= MaxHealth;

    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        currentHealth = Mathf.Clamp(currentHealth - amount, 0, MaxHealth);
        UpdateUI();
        if (currentHealth <= 0)
        {
            HandleDeath();
        }
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, MaxHealth);
        UpdateUI();
    }

    void UpdateUI()
    {
        if (healthSlider) healthSlider.value = (float)currentHealth / MaxHealth;
        if (healthLabel) healthLabel.text = $"{currentHealth} / {MaxHealth}";
    }

    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;
        if (deathDelay > 0f)
        {
            Invoke(nameof(LoadGameOverScene), deathDelay);
        }
        else
        {
            LoadGameOverScene();
        }
    }

    void LoadGameOverScene()
    {
#if UNITY_EDITOR
        // Keep the string in sync with dragged asset name in editor
        if (gameOverSceneAsset != null)
        {
            // Use scene asset name for loading
            gameOverSceneName = gameOverSceneAsset.name;
        }
#endif
        // Ensure cursor is available on title/game-over screen
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (!string.IsNullOrEmpty(gameOverSceneName))
        {
            SceneManager.LoadScene(gameOverSceneName);
            return;
        }

        var active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.name);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Sync scene name when a scene asset is assigned in the inspector
        if (gameOverSceneAsset != null)
        {
            gameOverSceneName = gameOverSceneAsset.name;
        }
    }
#endif
}