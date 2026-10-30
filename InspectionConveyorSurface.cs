//
using UnityEngine;

public sealed class InspectionConveyorSurface : MonoBehaviour
{
    [SerializeField] private SpriteRenderer loopSurface;

    private Vector3 startPosition;
    private float width;
    private float offset;

    private void Awake()
    {
        startPosition = transform.localPosition;
        width = GetComponent<SpriteRenderer>().sprite.bounds.size.x * Mathf.Abs(transform.localScale.x);
    }

    public void Scroll(float distance)
    {
        // Wrap the belt position so the two sprites loop seamlessly
        offset = Mathf.Repeat(offset + distance / transform.parent.lossyScale.x, width); // scroll consistently with parent scale
        PositionTiles();
    }

    public void ResetScroll()
    {
        offset = 0f;
        PositionTiles();
    }

    private void PositionTiles() // Places the two belt sprites so scrolling loops
    {
        Vector3 position = startPosition + Vector3.right * offset;
        transform.localPosition = position;
        loopSurface.transform.localPosition = position - Vector3.right * width;
    }
}
