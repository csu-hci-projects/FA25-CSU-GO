using UnityEngine;
using UnityEngine.SceneManagement; // Required for Scene switching

public class LevelLoader : MonoBehaviour
{
    [Tooltip("The exact name of the scene you want to load")]
    public string levelName = "Level2";

    // This function runs when something enters the trigger zone
    private void OnTriggerEnter(Collider other)
    {
        // Check if the object hitting the generator is actually the Player
        // (You don't want enemies or crates triggering the level change)
        if (other.CompareTag("Player"))
        {
            LoadNextLevel();
        }
    }

    void LoadNextLevel()
    {
        // LoadSceneMode.Single is the default. 
        // It closes the current scene and loads the new one.
        SceneManager.LoadScene(levelName);
    }
}