using UnityEngine;
using TMPro;

public class WinScreenScore : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] TextMeshProUGUI finalScoreText;

    void OnEnable()
    {
        // Search for the timer, even if it is disabled/inactive
        GameTimer timer = Object.FindAnyObjectByType<GameTimer>(FindObjectsInactive.Include);

        if (timer != null)
        {
            timer.PauseTimer();
            float finalTime = timer.GetElapsedTime();

            int minutes = Mathf.FloorToInt(finalTime / 60f);
            int seconds = Mathf.FloorToInt(finalTime % 60f);
            int hundredths = Mathf.FloorToInt((finalTime * 100f) % 100f);

            // UPDATED: Now says "Final Time"
            finalScoreText.text = string.Format("Final Time: {0:00}:{1:00}.{2:00}", minutes, seconds, hundredths);
        }
        else
        {
            // If you see this, the Timer object no longer exists in the hierarchy
            Debug.LogError("WinScreenScore: Timer object is missing! It was likely destroyed.");
            finalScoreText.text = "Final Time: --:--.--";
        }
    }
}