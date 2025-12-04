using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    public const int MaxHealth = 100;
    [SerializeField] int currentHealth = MaxHealth;
    [SerializeField] Slider healthSlider;
    [SerializeField] TextMeshProUGUI healthLabel;
    [SerializeField] string gameOverSceneName = ""; // if empty, reloads current scene
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
        if (!string.IsNullOrEmpty(gameOverSceneName))
        {
            SceneManager.LoadScene(gameOverSceneName);
            return;
        }

        var active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.name);
    }
}