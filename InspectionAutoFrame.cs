
using UnityEngine;

public sealed class InspectionAutoFrame : MonoBehaviour
{
    [SerializeField] private SpriteRenderer artwork;
    public SpriteRenderer Artwork => artwork;
    [System.NonSerialized] public SpriteRenderer conveyerBelt;
    [SerializeField] private float verticalBeltMargin = 0.05f;
    [SerializeField] private Vector2 maxArtworkSize = new Vector2(7.5f, 8f);
    [SerializeField] private float frameBorderSize = 0.31f;

    public void AutoFitPainting() // Resizes and centers the artwork inside its frame
    {
        float border = frameBorderSize * 2f;

        Vector3 position = transform.position;
        position.y = conveyerBelt.bounds.center.y;
        transform.position = position;

        float maxHeight = Mathf.Min(maxArtworkSize.y, conveyerBelt.bounds.size.y * (1f - 2f * verticalBeltMargin) - border);

        Sprite picture = artwork.sprite;
        float pixelsPerUnit = picture.pixelsPerUnit;
        float width = picture.rect.width / pixelsPerUnit;
        float height = picture.rect.height / pixelsPerUnit;
        // Fit both dimensions without stretching the artwork
        float scale = Mathf.Min(maxArtworkSize.x / width, maxHeight / height);

        artwork.transform.localScale = new Vector3(scale, scale, 1f);
        // Compensate for off-center sprite pivots
        Vector2 pivotOffset = (picture.rect.size * 0.5f - picture.pivot) / pixelsPerUnit;
        artwork.transform.localPosition = new Vector3(-pivotOffset.x * scale, -pivotOffset.y * scale, artwork.transform.localPosition.z);

        GetComponent<SpriteRenderer>().size = new Vector2(width * scale + border, height * scale + border);
    }
}
