using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    public const int MaxHealth = 100;
    [SerializeField] int currentHealth = MaxHealth;
    [SerializeField] Slider healthSlider;
    [SerializeField] TextMeshProUGUI healthLabel;

    void Awake()
    {
        currentHealth = MaxHealth;
        UpdateUI();
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        currentHealth = Mathf.Clamp(currentHealth - amount, 0, MaxHealth);
        UpdateUI();
        if (currentHealth <= 0)
        {
            // TODO: trigger death/respawn logic here.
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
}