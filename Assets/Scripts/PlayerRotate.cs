using UnityEngine;

public class PlayerRotate : MonoBehaviour
{
    private Camera mainCamera;

    private void Start()
    {
        // Find the main camera in the scene
        mainCamera = Camera.main;
    }

    private void Update()
    {
        // Rotate the player towards the camera every frame
        RotatePlayerTowardsCamera();
    }

    private void RotatePlayerTowardsCamera()
    {
        if (mainCamera != null)
        {
            Vector3 cameraForward = mainCamera.transform.forward;
            cameraForward.y = 0f; // Ignore the y-axis rotation

            if (cameraForward != Vector3.zero)
            {
                Quaternion newRotation = Quaternion.LookRotation(cameraForward);
                transform.rotation = newRotation;
            }
        }
    }
}