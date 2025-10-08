using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class BhopIndicator : MonoBehaviour
{
    [Tooltip("PlayerMovementFPSBhop on your Player root")]
    public PlayerMovementFPSBhop player;

    [Tooltip("Color while the bhop window is open")]
    public Color windowOpenColor = Color.green;

    [Tooltip("Color when the window is closed")]
    public Color windowClosedColor = Color.red;

    Renderer rend;
    MaterialPropertyBlock mpb;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        mpb  = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        if (!player) return;

        // "Green only for the time that the bhop window is open"
        bool windowOpen =
            player.IsGrounded() &&
            (Time.time - player.GroundedSince()) <= player.bhopWindow;

        // set color via MPB
        rend.GetPropertyBlock(mpb);
        mpb.SetColor("_BaseColor", windowOpen ? windowOpenColor : windowClosedColor); // URP Lit
        // If using Built-in/Standard, use "_Color" instead:
        // mpb.SetColor("_Color", windowOpen ? windowOpenColor : windowClosedColor);
        rend.SetPropertyBlock(mpb);
    }
}
