using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Centralized ammo system with configurable starting and max ammo.
/// Expose this on the player or weapon root and hook UI via the provided fields.
/// </summary>
public class AmmoSystem : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] int maxAmmo = 120;
    [SerializeField] int startingAmmo = 60;

    [Header("UI (Optional)")]
    [SerializeField] TextMeshProUGUI ammoLabel;
    [SerializeField] Slider ammoSlider;

    [Header("Audio (Optional)")]
    [SerializeField] AudioClip pickupSound;
    [Range(0f,1f)] [SerializeField] float pickupVolume = 1f;
    [SerializeField] AudioClip dryFireSound;
    [Range(0f,1f)] [SerializeField] float dryFireVolume = 1f;
    [Tooltip("If true, play dry-fire sound when a consume check fails (e.g., player clicks while empty)")]
    [SerializeField] bool playDryFireOnFailedCheck = true;

    public int CurrentAmmo { get; private set; }
    public int MaxAmmo => maxAmmo;

    void Awake()
    {
        startingAmmo = Mathf.Clamp(startingAmmo, 0, maxAmmo);
        CurrentAmmo = startingAmmo;
        UpdateUI();
    }

    public bool CanConsume(int amount)
    {
        if (amount <= 0) return false;
        bool ok = CurrentAmmo >= amount;
        if (!ok && playDryFireOnFailedCheck && dryFireSound)
        {
            AudioSource.PlayClipAtPoint(dryFireSound, transform.position, dryFireVolume);
        }
        return ok;
    }

    public void Consume(int amount)
    {
        if (amount <= 0) return;
        CurrentAmmo = Mathf.Clamp(CurrentAmmo - amount, 0, maxAmmo);
        UpdateUI();
        // Do not auto-play dry fire here; it's handled on failed checks
    }

    public void PlayDryFire()
    {
        if (dryFireSound)
        {
            AudioSource.PlayClipAtPoint(dryFireSound, transform.position, dryFireVolume);
        }
    }

    public void AddAmmo(int amount)
    {
        if (amount <= 0) return;
        CurrentAmmo = Mathf.Clamp(CurrentAmmo + amount, 0, maxAmmo);
        UpdateUI();
        if (pickupSound)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, pickupVolume);
        }
    }

    public void SetMaxAmmo(int newMax, bool clampCurrent = true)
    {
        maxAmmo = Mathf.Max(0, newMax);
        if (clampCurrent)
        {
            CurrentAmmo = Mathf.Clamp(CurrentAmmo, 0, maxAmmo);
        }
        UpdateUI();
    }

    public void SetStartingAmmo(int newStart)
    {
        startingAmmo = Mathf.Clamp(newStart, 0, maxAmmo);
        CurrentAmmo = startingAmmo;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (ammoSlider) ammoSlider.value = (maxAmmo > 0) ? (float)CurrentAmmo / maxAmmo : 0f;
        if (ammoLabel) ammoLabel.text = $"{CurrentAmmo} / {maxAmmo}";
    }
}
