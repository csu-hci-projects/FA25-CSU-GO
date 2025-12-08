using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to any GameObject; assign an AudioSource to control.
/// Optionally bind a UI Slider to drive the volume (0..1).
/// </summary>
public class AudioSourceVolumeController : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The AudioSource whose volume will be controlled")] 
    [SerializeField] AudioSource targetSource;

    [Header("UI Binding (Optional)")]
    [Tooltip("Optional UI Slider that sets volume 0..1")] 
    [SerializeField] Slider volumeSlider;

    [Header("Defaults")] 
    [Range(0f,1f)] [SerializeField] float defaultVolume = 0.8f;
    [Tooltip("Persist volume across sessions using PlayerPrefs")] 
    [SerializeField] bool saveToPlayerPrefs = true;
    [SerializeField] string playerPrefsKey = "MasterAudioSourceVolume";

    void Awake()
    {
        if (targetSource == null)
        {
            targetSource = GetComponent<AudioSource>();
        }

        float startVolume = defaultVolume;
        if (saveToPlayerPrefs && PlayerPrefs.HasKey(playerPrefsKey))
        {
            startVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(playerPrefsKey, defaultVolume));
        }

        SetVolume(startVolume, notifySlider:false);

        if (volumeSlider != null)
        {
            // Initialize slider without re-triggering listener
            volumeSlider.SetValueWithoutNotify(startVolume);
            volumeSlider.onValueChanged.AddListener(OnSliderChanged);
        }
    }

    void OnDestroy()
    {
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveListener(OnSliderChanged);
        }
    }

    void OnSliderChanged(float value)
    {
        SetVolume(value);
    }

    /// <summary>
    /// Sets the volume on the target AudioSource (0..1) and optionally updates PlayerPrefs/Slider.
    /// </summary>
    public void SetVolume(float normalized, bool notifySlider = true)
    {
        float v = Mathf.Clamp01(normalized);
        if (targetSource != null)
        {
            targetSource.volume = v;
        }
        if (saveToPlayerPrefs)
        {
            PlayerPrefs.SetFloat(playerPrefsKey, v);
        }
        if (notifySlider && volumeSlider != null && !Mathf.Approximately(volumeSlider.value, v))
        {
            volumeSlider.SetValueWithoutNotify(v);
        }
    }

    /// <summary>
    /// Convenience: mute/unmute while remembering previous volume.
    /// </summary>
    public void ToggleMute()
    {
        if (targetSource == null) return;
        if (Mathf.Approximately(targetSource.volume, 0f))
        {
            float v = saveToPlayerPrefs ? PlayerPrefs.GetFloat(playerPrefsKey, defaultVolume) : defaultVolume;
            SetVolume(v);
        }
        else
        {
            SetVolume(0f);
        }
    }
}
