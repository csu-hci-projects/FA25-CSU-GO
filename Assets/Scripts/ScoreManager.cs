using UnityEngine;
using TMPro; 

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public int Score { get; private set; }

    [SerializeField] private TextMeshProUGUI scoreText;
    // If using legacy UI Text, use:
    // using UnityEngine.UI;
    // [SerializeField] private Text scoreText;

    private void Awake()
    {
        // Singleton pattern so we can easily access ScoreManager.Instance
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        Score = 0;
        UpdateScoreText();
    }

    public void AddPoints(int amount)
    {
        Score += amount;
        UpdateScoreText();
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + Score;
        }
    }
}
