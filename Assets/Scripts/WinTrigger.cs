using UnityEngine;
using UnityEngine.SceneManagement; // <-- REQUIRED for loading scenes

public class WinTrigger1 : MonoBehaviour
{

    public string sceneToLoad;

    // This runs when another object enters the trigger
    private void OnTriggerEnter(Collider other)
    {
        // Checks if the object has the "Player" tag
        if (other.CompareTag("Player"))
        {
            // Optional: Makes the generator disappear
            gameObject.SetActive(false);

            // Loads the new scene and unloads the current one
            SceneManager.LoadScene(sceneToLoad);
        }
    }
}