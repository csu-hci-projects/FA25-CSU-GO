using UnityEngine;
using UnityEngine.SceneManagement; // Required to change scenes

public class MainMenuController : MonoBehaviour
{
    // Call this function to load the game scene
    public void PlayGame()
    {
        // Replace "GameScene" with the exact name of your playable scene
        SceneManager.LoadScene("Level1");
    }

    // Call this function to quit the game
    public void QuitGame()
    {
        Debug.Log("Quit Game triggered!"); // This shows in the editor so you know it works
        Application.Quit();
    }
}