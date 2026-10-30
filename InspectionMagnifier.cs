using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class InspectionMagnifier : MonoBehaviour
{
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private Camera lensCamera;
    [SerializeField] private RectTransform magnifierVisual;
    [SerializeField] private RectTransform handleHitArea;
    [SerializeField] private RawImage lensImage;
    [SerializeField] private Shader lensShader;
    [SerializeField] private float magnification = 2.3f;
    [SerializeField] private int lensResolution = 512;
    [SerializeField] private int lensDepthBits = 16;

    private ShiftManager shiftDirector;
    private RenderTexture image;
    private Material lensMaterial;
    private Vector2 restPosition;
    private Vector3 dragOffset;
    private bool holding;

    public bool WasPickedUpDuringTutorial { get; private set; }

    private void Awake() // Sets up the magnifying glass camera and texture
    {
        shiftDirector = GetComponent<ShiftManager>();
        restPosition = magnifierVisual.anchoredPosition;

        // Draw the zoomed scene into a texture used by the glass
        image = new RenderTexture(lensResolution, lensResolution, lensDepthBits, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        image.wrapMode = TextureWrapMode.Clamp;
        lensMaterial = new Material(lensShader);
        lensImage.texture = image;
        lensImage.material = lensMaterial;
        lensCamera.targetTexture = image;
    }

    // Use the glass's actual position even if it moves in the scene
    private Vector2 LensCenter
    {
        get
        {
            RectTransform rect = lensImage.rectTransform;
            return RectTransformUtility.WorldToScreenPoint(sceneCamera, rect.TransformPoint(rect.rect.center));
        }
    }

    private bool IsOverGlass(Vector2 pointer) // Checks if the cursor is on the glass or handle
    {
        RectTransform rect = lensImage.rectTransform;
        Vector3 left = rect.TransformPoint(new Vector3(rect.rect.xMin, rect.rect.center.y));
        Vector3 right = rect.TransformPoint(new Vector3(rect.rect.xMax, rect.rect.center.y));
        float radius = Vector2.Distance(sceneCamera.WorldToScreenPoint(left), sceneCamera.WorldToScreenPoint(right)) * 0.5f;
        return Vector2.Distance(pointer, LensCenter) <= radius || RectTransformUtility.RectangleContainsScreenPoint(handleHitArea, pointer, sceneCamera);
    }

    public bool IsPointerOver(Vector2 point) // Stops stamp clicks while using the magnifying glass
    {
        return shiftDirector.CurrentGameState == ShiftManager.State.Inspecting && (holding || IsOverGlass(point));
    }

    private Vector3 PointerWorld(Vector2 pointer) // Converts the cursor position to the glass's world position
    {
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            magnifierVisual, pointer, sceneCamera, out Vector3 world);
        return world;
    }

    private void LateUpdate() // Handles dragging and keeps the zoom camera under the glass
    {
        if (shiftDirector.CurrentGameState != ShiftManager.State.Inspecting)
        {
            holding = false;
            magnifierVisual.anchoredPosition = restPosition;
            lensCamera.enabled = false;
            return;
        }
        if (!lensCamera.enabled)
        {
            lensCamera.enabled = true;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        Vector2 pointer = mouse.position.ReadValue();
        if (!holding && mouse.leftButton.wasPressedThisFrame && IsOverGlass(pointer))
        {
            holding = true;
            WasPickedUpDuringTutorial = true;
            // Keep the glass from jumping to the cursor when picked up
            dragOffset = magnifierVisual.position - PointerWorld(pointer);
        }

        if (holding)
        {
            if (mouse.leftButton.isPressed)
            {
                magnifierVisual.position = PointerWorld(pointer) + dragOffset;
            }
            else
            {
                holding = false;
                magnifierVisual.anchoredPosition = restPosition;
            }
        }

        // Point the zoom camera at the world position under the glass
        Vector2 center = LensCenter;
        Vector3 world = sceneCamera.ScreenToWorldPoint(new Vector3(center.x, center.y, -sceneCamera.transform.position.z));
        lensCamera.transform.position = new Vector3(world.x, world.y, sceneCamera.transform.position.z);

        RectTransform rect = lensImage.rectTransform;
        Vector3 bottom = rect.TransformPoint(new Vector3(rect.rect.center.x, rect.rect.yMin));
        Vector3 top = rect.TransformPoint(new Vector3(rect.rect.center.x, rect.rect.yMax));
        // Match the zoom level to the glass's on-screen size
        float glassHeight = Vector2.Distance(sceneCamera.WorldToScreenPoint(bottom), sceneCamera.WorldToScreenPoint(top));
        lensCamera.orthographicSize = sceneCamera.orthographicSize * glassHeight / sceneCamera.pixelHeight / magnification;
    }

    // Release the temporary camera texture and material
    private void OnDestroy()
    {
        lensCamera.targetTexture = null;
        Destroy(image);
        Destroy(lensMaterial);
    }
}
