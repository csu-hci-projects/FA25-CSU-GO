using UnityEngine;

[RequireComponent(typeof(Camera))]
public class IgnoreRenderLayer : MonoBehaviour
{
    [Tooltip("The name of the layer to hide from this camera.")]
    public string ignoreLayerName = "IgnoreRender";

    void Start()
    {
        Camera cam = GetComponent<Camera>();
        int layer = LayerMask.NameToLayer(ignoreLayerName);

        if (layer == -1)
        {
            Debug.LogWarning($"Layer \"{ignoreLayerName}\" does not exist. Did you create it in the Tags & Layers settings?");
            return;
        }

        // Remove the layer from the camera’s culling mask
        cam.cullingMask &= ~(1 << layer);
    }
}
