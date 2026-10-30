using UnityEngine;
using UnityEngine.EventSystems;

public sealed class GalleryPaintingHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Transform painting;
    [SerializeField] private float hoverHeight = 0.13f;
    [SerializeField] private float hoverSizeMultiplier = 1.055f;
    [SerializeField] private float hoverSpeed = 11f;

    private Vector3 originalPosition;
    private Vector3 originalScale;
    private bool isHovering;

    private void Awake()
    {
        originalPosition = painting.localPosition;
        originalScale = painting.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
    }

    private void Update() // Smoothly lifts and enlarges a hovered painting
    {
        Vector3 targetPosition = originalPosition;
        Vector3 targetScale = originalScale;
        if (isHovering)
        {
            targetPosition += Vector3.up * hoverHeight;
            targetScale *= hoverSizeMultiplier;
        }

        // Keep the hover animation smooth regardless of frame rate
        float smoothAmount = 1f - Mathf.Exp(-hoverSpeed * Time.unscaledDeltaTime);
        painting.localPosition = Vector3.Lerp(painting.localPosition, targetPosition, smoothAmount);
        painting.localScale = Vector3.Lerp(painting.localScale, targetScale, smoothAmount);
    }
}
