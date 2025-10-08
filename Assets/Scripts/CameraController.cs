using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float _sensitivity = 1f;

    private Vector2 _mouseInput;
    private float _pitch; // up/down (x-axis rotation)

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // Yaw (left/right) around world up
        transform.Rotate(Vector3.up, _mouseInput.x * _sensitivity * Time.deltaTime, Space.World);

        // Pitch (up/down) with clamp
        _pitch -= _mouseInput.y * _sensitivity * Time.deltaTime;
        _pitch = Mathf.Clamp(_pitch, -90f, 90f);

        Vector3 e = transform.localEulerAngles;
        e.x = _pitch;
        e.z = 0f; // keep roll zeroed
        transform.localEulerAngles = e;
    }

    // Hook this to an Input Action (Vector2) for mouse delta
    public void OnMouseMove(InputAction.CallbackContext context)
    {
        _mouseInput = context.ReadValue<Vector2>();
    }
}
