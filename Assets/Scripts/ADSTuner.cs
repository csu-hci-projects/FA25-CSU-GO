using UnityEngine;

[RequireComponent(typeof(AdsController))]
public class AdsTuner : MonoBehaviour
{
    public float posStep = 0.0015f;   // meters per tap
    public float rotStep = 0.2f;      // degrees per tap
    public KeyCode aimKey = KeyCode.Mouse1; // same key you use to aim

    AdsController ads;

    void Awake() => ads = GetComponent<AdsController>();

    void Update()
    {
        if (ads == null || !Input.GetKey(aimKey)) return; // only adjust while aiming

        // --- POSITION: IJKL/UO (local X/Y/Z) ---
        if (Input.GetKeyDown(KeyCode.J)) ads.aimLocalPosition.x -= posStep;
        if (Input.GetKeyDown(KeyCode.L)) ads.aimLocalPosition.x += posStep;

        if (Input.GetKeyDown(KeyCode.I)) ads.aimLocalPosition.y += posStep;
        if (Input.GetKeyDown(KeyCode.K)) ads.aimLocalPosition.y -= posStep;

        if (Input.GetKeyDown(KeyCode.U)) ads.aimLocalPosition.z += posStep;
        if (Input.GetKeyDown(KeyCode.O)) ads.aimLocalPosition.z -= posStep;

        // --- ROTATION: Arrow keys (pitch/yaw), [ ] for roll ---
        if (Input.GetKeyDown(KeyCode.UpArrow))    ads.aimLocalEuler.x -= rotStep; // look up
        if (Input.GetKeyDown(KeyCode.DownArrow))  ads.aimLocalEuler.x += rotStep; // look down

        if (Input.GetKeyDown(KeyCode.LeftArrow))  ads.aimLocalEuler.y -= rotStep; // yaw left
        if (Input.GetKeyDown(KeyCode.RightArrow)) ads.aimLocalEuler.y += rotStep; // yaw right

        if (Input.GetKeyDown(KeyCode.LeftBracket))  ads.aimLocalEuler.z -= rotStep; // roll
        if (Input.GetKeyDown(KeyCode.RightBracket)) ads.aimLocalEuler.z += rotStep;

        // print current values so you can paste them
        if (Input.GetKeyDown(KeyCode.Return))
        {
            Debug.Log($"ADS POS = ({ads.aimLocalPosition.x:F4}, {ads.aimLocalPosition.y:F4}, {ads.aimLocalPosition.z:F4})");
            Debug.Log($"ADS EUL = ({ads.aimLocalEuler.x:F2}, {ads.aimLocalEuler.y:F2}, {ads.aimLocalEuler.z:F2})");
        }
    }
}
