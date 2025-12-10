using UnityEngine;
using TMPro;

public class GameTimer : MonoBehaviour
{
    public static GameTimer Instance;

    [Header("UI Reference")]
    [Tooltip("This will be auto-filled by the Connector script in each level")]
    public TextMeshProUGUI timerText; // Made public so Connector can access it

    [Header("Timer Settings")]
    [SerializeField] bool startOnAwake = true;
    [SerializeField] bool showHundredths = false;

    private float elapsedTime = 0f;
    private bool isRunning = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (startOnAwake) StartTimer();
    }

    void Update()
    {
        if (isRunning)
        {
            elapsedTime += Time.deltaTime;
            UpdateDisplay();
        }
    }

    // --- NEW FUNCTION: Allows the UI to connect itself ---
    public void RegisterTimerText(TextMeshProUGUI newText)
    {
        timerText = newText;
        UpdateDisplay(); // Force an immediate update so it doesn't say "TIMER"
    }
    // ----------------------------------------------------

    public void StartTimer() { isRunning = true; }
    public void PauseTimer() { isRunning = false; }
    
    public void ResetTimer() 
    { 
        elapsedTime = 0f; 
        isRunning = false; 
        UpdateDisplay(); 
    }

    public void RestartTimer()
    {
        elapsedTime = 0f;
        isRunning = true;
        UpdateDisplay();
    }

    public float GetElapsedTime() { return elapsedTime; }

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