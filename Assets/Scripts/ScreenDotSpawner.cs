using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Place this on a GameObject and assign a `Canvas` and a small UI Image/RectTransform prefab (dot).
/// Call ShowDotAtWorldPosition(...) with the hit point to render a 2D UI dot where the bullet hit.
/// Works for ScreenSpace-Overlay and ScreenSpace-Camera canvases.
/// </summary>
public class ScreenDotSpawner : MonoBehaviour
{
    [Tooltip("Canvas to parent dots under (prefer a Screen Space canvas).")]
    public Canvas targetCanvas;

    [Tooltip("Prefab for the UI dot. Should be a RectTransform with an Image component.")]
    public RectTransform dotPrefab;

    [Tooltip("Seconds the dot stays visible (<=0 means keep).")]
    public float dotLifetime = 1f;

    [Tooltip("If true, the script will reuse a single dot instance instead of spawning multiple.")]
    public bool reuseSingleDot = true;

    RectTransform singleDot;
    Coroutine hideCoroutine;

    void Awake()
    {
        if (targetCanvas == null)
        {
            // Use the newer API to avoid obsolete warnings in recent Unity versions.
            targetCanvas = Object.FindAnyObjectByType<Canvas>();
            if (targetCanvas == null)
                Debug.LogWarning("ScreenDotSpawner: No Canvas assigned and none found in scene.");
        }

        if (reuseSingleDot && dotPrefab != null && targetCanvas != null)
        {
            singleDot = Instantiate(dotPrefab, targetCanvas.transform, false);
            singleDot.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Shows a UI dot at the world position. Camera can be null to use Camera.main.
    /// </summary>
    public void ShowDotAtWorldPosition(Vector3 worldPos, Camera cam = null)
    {
        if (targetCanvas == null || dotPrefab == null)
            return;

        if (cam == null) cam = Camera.main;
        if (cam == null)
            return;

        RectTransform canvasRect = targetCanvas.transform as RectTransform;

        Camera canvasCamera = null;
        if (targetCanvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            canvasCamera = targetCanvas.worldCamera != null ? targetCanvas.worldCamera : cam;
        }
        // Use the same camera for both conversions. In builds the UI may render with a different
        // camera/FOV than the gameplay camera; mixing them causes a horizontal offset.
        Camera screenPointCamera = canvasCamera != null ? canvasCamera : cam;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(screenPointCamera, worldPos);
        
        Vector2 localPoint;
        bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, canvasCamera, out localPoint);

        if (!ok)
            return;

        if (reuseSingleDot)
        {
            if (singleDot == null)
            {
                singleDot = Instantiate(dotPrefab, targetCanvas.transform, false);
            }

            // If the dot is already active and very close to the desired position,
            // don't update it or restart the hide coroutine every frame—this avoids
            // repeated Start/Stop of coroutines which can cause visible flicker.
            bool needUpdate = true;
            if (singleDot.gameObject.activeSelf)
            {
                Vector2 current = singleDot.anchoredPosition;
                if (Vector2.Distance(current, localPoint) < 0.5f)
                    needUpdate = false;
            }

            if (needUpdate)
            {
                singleDot.anchoredPosition = localPoint;
                singleDot.gameObject.SetActive(true);

                if (dotLifetime > 0f)
                {
                    // Cancel any previous hide coroutine so multiple overlapping coroutines
                    // don't cause the dot to be hidden while it's still being updated every frame.
                    if (hideCoroutine != null)
                        StopCoroutine(hideCoroutine);

                    hideCoroutine = StartCoroutine(HideAfter(singleDot.gameObject, dotLifetime));
                }
            }
        }
        else
        {
            RectTransform go = Instantiate(dotPrefab, targetCanvas.transform, false);
            go.anchoredPosition = localPoint;
            if (dotLifetime > 0f)
                Destroy(go.gameObject, dotLifetime);
        }
    }

    IEnumerator HideAfter(GameObject go, float t)
    {
        yield return new WaitForSeconds(t);
        if (go)
            go.SetActive(false);
        // Clear the stored coroutine reference when the hide completes.
        hideCoroutine = null;
    }

    /// <summary>
    /// Hides the single reused dot (if using reuseSingleDot).
    /// </summary>
    public void HideDot()
    {
        if (reuseSingleDot && singleDot != null)
        {
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
                hideCoroutine = null;
            }

            singleDot.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Sets the color of the reused dot (if using reuseSingleDot).
    /// </summary>
    public void SetDotColor(Color color)
    {
        if (reuseSingleDot && singleDot != null)
        {
            Image img = singleDot.GetComponent<Image>();
            if (img != null)
            {
                img.color = color;
            }
        }
    }
}
