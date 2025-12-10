using UnityEngine;
using TMPro;

public class TimerConnector : MonoBehaviour
{
    void Start()
    {
        // 1. Get the Text component on THIS object
        TextMeshProUGUI myText = GetComponent<TextMeshProUGUI>();

        // 2. Find the GameTimer and tell it "Hey, use me as the display!"
        if (GameTimer.Instance != null)
        {
            GameTimer.Instance.RegisterTimerText(myText);
        }
    }
}