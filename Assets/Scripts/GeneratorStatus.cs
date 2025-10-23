using UnityEngine;

public class GeneratorStatus : MonoBehaviour
{
    void Start()
    {
        if (GameData.isGeneratorActivated)
        {
            gameObject.SetActive(false);
        }
    }
}
