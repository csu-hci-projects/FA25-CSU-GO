using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float _sensitivity = 1f;

    private Vector2 _mouseInput;
    private float _pitch; // up/down (x-axis rotation)
    private float _yaw;   // left/right (y-axis rotation)

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Initialize from current rotation
        Vector3 angles = transform.eulerAngles;
        _yaw = angles.y;
        _pitch = angles.x;
        if (_pitch > 180f) _pitch -= 360f;
    }

    // Use LateUpdate for camera rotation to run after physics interpolation
    private void LateUpdate()
    {
        // Accumulate yaw and pitch from mouse input
        // Use unscaled delta time for consistent feel regardless of frame rate
        _yaw += _mouseInput.x * _sensitivity * Time.deltaTime;
        _pitch -= _mouseInput.y * _sensitivity * Time.deltaTime;
        _pitch = Mathf.Clamp(_pitch, -90f, 90f);

        // Apply rotation directly (no Rotate calls which can accumulate errors)
        transform.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    // Hook this to an Input Action (Vector2) for mouse delta
    public void OnMouseMove(InputAction.CallbackContext context)
    {
        _mouseInput = context.ReadValue<Vector2>();
    }
}
