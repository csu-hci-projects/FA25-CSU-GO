using UnityEngine;
using TMPro;

public class GameTimer : MonoBehaviour
{
    [Header("UI Reference")]
    [Tooltip("TextMeshPro component to display the timer")]
    [SerializeField] TextMeshProUGUI timerText;

    [Header("Timer Settings")]
    [Tooltip("Should the timer start automatically when the scene loads?")]
    [SerializeField] bool startOnAwake = true;
    
    [Tooltip("Format: 'mm:ss' or 'mm:ss.ff' (ff = hundredths)")]
    [SerializeField] bool showHundredths = false;

    private float elapsedTime = 0f;
    private bool isRunning = false;

    void Start()
    {
        if (startOnAwake)
        {
            StartTimer();
        }
        UpdateDisplay();
    }

    void Update()
    {
        if (isRunning)
        {
            elapsedTime += Time.deltaTime;
            UpdateDisplay();
        }
    }

    /// <summary>
    /// Start or resume the timer
    /// </summary>
    public void StartTimer()
    {
        isRunning = true;
    }

    /// <summary>
    /// Pause the timer
    /// </summary>
    public void PauseTimer()
    {
        isRunning = false;
    }

    /// <summary>
    /// Reset the timer to 0 and stop it
    /// </summary>
    public void ResetTimer()
    {
        elapsedTime = 0f;
        isRunning = false;
        UpdateDisplay();
    }

    /// <summary>
    /// Reset to 0 and immediately start counting
    /// </summary>
    public void RestartTimer()
    {
        elapsedTime = 0f;
        isRunning = true;
        UpdateDisplay();
    }

    /// <summary>
    /// Get the current elapsed time in seconds
    /// </summary>
    public float GetElapsedTime()
    {
        return elapsedTime;
    }

    /// <summary>
    /// Check if timer is currently running
    /// </summary>
    public bool IsRunning()
    {
        return isRunning;
    }

    void UpdateDisplay()
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(elapsedTime / 60f);
        int seconds = Mathf.FloorToInt(elapsedTime % 60f);

        if (showHundredths)
        {
            int hundredths = Mathf.FloorToInt((elapsedTime * 100f) % 100f);
            timerText.text = string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, hundredths);
        }
        else
        {
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }
}
