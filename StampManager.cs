using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class InspectionStationStamps : MonoBehaviour
{
    private const int DraggedStampSortingOrder = 110;
    private const int DraggedFaceSortingOrder = 111;
    private static readonly Color DraggedStampColor = new Color(1f, 0.97f, 0.86f, 1f);

    // Remember the original stamp appearance so dragging can be undone
    private sealed class Stamp
    {
        public readonly SpriteRenderer body;
        public readonly SpriteRenderer face;
        public readonly bool authentic;
        public readonly Vector3 restPosition;
        private readonly Vector3 scale;
        private readonly Color color;
        private readonly int bodyOrder;
        private readonly int faceOrder;

        public Stamp(SpriteRenderer body, SpriteRenderer face, bool authentic)
        {
            this.body = body;
            this.face = face;
            this.authentic = authentic;
            restPosition = body.transform.localPosition;
            scale = body.transform.localScale;
            color = body.color;
            bodyOrder = body.sortingOrder;
            faceOrder = face.sortingOrder;
        }

        public void PickUp(float scaleBoost)
        {
            body.sortingOrder = DraggedStampSortingOrder;
            face.sortingOrder = DraggedFaceSortingOrder;
            body.transform.localScale = scale * scaleBoost;
            body.color = DraggedStampColor;
        }

        public void PutDown()
        {
            body.sortingOrder = bodyOrder;
            face.sortingOrder = faceOrder;
            body.transform.localScale = scale;
            body.color = color;
        }
    }

    [SerializeField] private SpriteRenderer realStamp;
    [SerializeField] private SpriteRenderer fakeStamp;
    [SerializeField] private SpriteRenderer realStampFace;
    [SerializeField] private SpriteRenderer fakeStampFace;
    [SerializeField] private Sprite realMarkSprite;
    [SerializeField] private Sprite fakeMarkSprite;
    [SerializeField] private float pickedUpStampScale = 1.13f;
    [SerializeField] private float stampReturnSpeed = 24f;
    [SerializeField] private RectTransform intercomBox;
    [SerializeField] private float stampHitPadding = 0.22f;
    [SerializeField] private int firstExtraMarkSortingOrder = 6;
    [SerializeField] private int maximumMarkSortingOrder = 9;
    [SerializeField] private float markDepthOffset = 0.002f;

    [System.NonSerialized] public SpriteRenderer[] paintingMarks;

    private InspectionStationGame station;
    private InspectionMagnifier magnifier;
    private Stamp real;
    private Stamp fake;
    private Stamp dragged;
    private Vector3 dragOffset;

    private readonly int[] realVotes = new int[InspectionStationGame.Slots];
    private readonly int[] fakeVotes = new int[InspectionStationGame.Slots];
    private readonly bool[] firstVoteWasReal = new bool[InspectionStationGame.Slots];
    private readonly Vector2[] firstVoteOffset = new Vector2[InspectionStationGame.Slots];
    private readonly List<SpriteRenderer>[] extraMarks = new List<SpriteRenderer>[InspectionStationGame.Slots];

    private void Awake() // Sets up both stamps and their painting marks
    {
        station = GetComponent<InspectionStationGame>();
        magnifier = GetComponent<InspectionMagnifier>();
        real = new Stamp(realStamp, realStampFace, true);
        fake = new Stamp(fakeStamp, fakeStampFace, false);
        for (int i = 0; i < extraMarks.Length; i++)
        {
            extraMarks[i] = new List<SpriteRenderer>();
        }
    }

    private void LateUpdate() // Handles stamp dragging and placement with the mouse
    {
        if (station.IsShiftEnded)
        {
            return;
        }

        RefreshMarks();
        MoveStampHome(real);
        MoveStampHome(fake);
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        Vector2 screenPoint = mouse.position.ReadValue();
        Vector3 worldPoint = PointerWorld(screenPoint);

        if (dragged != null)
        {
            if (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame)
            {
                dragged.body.transform.position = worldPoint + dragOffset;
                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    ReleaseStampDrag(worldPoint);
                }
            }
            else
            {
                CancelDrag();
            }
            return;
        }

        if (magnifier.IsPointerOver(screenPoint))
        {
            return;
        }
        if (!mouse.leftButton.wasPressedThisFrame ||
            RectTransformUtility.RectangleContainsScreenPoint(intercomBox, screenPoint, station.SceneCamera))
        {
            return;
        }

        if (Contains(real.body.bounds, worldPoint) || Contains(real.face.bounds, worldPoint))
        {
            BeginStampDrag(real, worldPoint);
        }
        else if (Contains(fake.body.bounds, worldPoint) || Contains(fake.face.bounds, worldPoint))
        {
            BeginStampDrag(fake, worldPoint);
        }
    }

    private Vector3 PointerWorld(Vector2 screenPosition) // Converts the cursor position into world coordinates
    {
        Camera camera = station.SceneCamera;
        Vector3 point = camera.ScreenToWorldPoint(new Vector3(
            screenPosition.x, screenPosition.y, -camera.transform.position.z));
        point.z = 0f;
        return point;
    }

    private static bool Contains(Bounds bounds, Vector3 point) // Checks a point inside a sprite's bounds, ignoring depth
    {
        return bounds.Contains(new Vector3(point.x, point.y, bounds.center.z));
    }

    private void BeginStampDrag(Stamp stamp, Vector3 pointer)
    {
        dragged = stamp;
        dragOffset = stamp.body.transform.position - pointer;
        stamp.PickUp(pickedUpStampScale);
        station.AudioDirector.PlayPickup();
    }

    private void ReleaseStampDrag(Vector3 pointer) // Places a mark if the stamp was dropped on a painting
    {
        Stamp stamp = dragged;
        Vector3 imprintPoint = pointer;
        int target = FindPaintingAt(pointer);
        // Try the stamp face if the cursor missed the painting
        if (target < 0)
        {
            imprintPoint = stamp.face.bounds.center;
            target = FindPaintingAt(imprintPoint);
        }

        stamp.PutDown();
        dragged = null;

        if (target < 0)
        {
            station.AudioDirector.PlayReturn();
            return;
        }
        StampPainting(target, stamp.authentic, imprintPoint);
    }

    public void CancelDrag()
    {
        if (dragged == null)
        {
            return;
        }
        dragged.PutDown();
        dragged = null;
        station.AudioDirector.PlayReturn();
    }

    private void MoveStampHome(Stamp stamp) // Moves a released stamp back to its starting position
    {
        if (stamp == dragged)
        {
            return;
        }
        Transform stampTransform = stamp.body.transform;
        stampTransform.localPosition = Vector3.MoveTowards(
            stampTransform.localPosition, stamp.restPosition, stampReturnSpeed * Time.deltaTime);
    }

    private int FindPaintingAt(Vector3 point) // Finds the visible painting under the cursor
    {
        Vector3 viewport = station.SceneCamera.WorldToViewportPoint(point);
        if (viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
        {
            return -1;
        }

        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < station.framedPaintings.Length; i++)
        {
            SpriteRenderer frame = station.framedPaintings[i];
            if (!frame.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds area = station.ArtworkBounds(i);
            area.Expand(new Vector3(stampHitPadding, stampHitPadding, 0f));
            if (!Contains(area, point) && !Contains(frame.bounds, point))
            {
                continue;
            }

            float distance = (frame.bounds.center - point).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }
        return best;
    }

    private void StampPainting(int index, bool authentic, Vector3 dropPoint) // Records a vote and adds the visible stamp mark
    {
        if (station.IsReferenceSample(index))
        {
            station.AudioDirector.PlayReturn();
            return;
        }

        bool alreadyStamped = realVotes[index] + fakeVotes[index] > 0;

        if (authentic)
        {
            realVotes[index]++;
        }
        else
        {
            fakeVotes[index]++;
        }

        if (!alreadyStamped)
        {
            Bounds art = station.ArtworkBounds(index);
            // Store the mark relative to the artwork so it follows the belt
            firstVoteOffset[index] = new Vector2(
                (dropPoint.x - art.center.x) / art.size.x,
                (dropPoint.y - art.center.y) / art.size.y);
            firstVoteWasReal[index] = authentic;
        }
        else
        {
            // Add another visible mark without replacing the first
            SpriteRenderer extra = Instantiate(paintingMarks[index], station.framedPaintings[index].transform);
            if (authentic)
            {
                extra.sprite = realMarkSprite;
            }
            else
            {
                extra.sprite = fakeMarkSprite;
            }
            int layer = extraMarks[index].Count + 1;
            extra.sortingOrder = Mathf.Min(maximumMarkSortingOrder, firstExtraMarkSortingOrder + layer - 1);
            extra.transform.position = new Vector3(dropPoint.x, dropPoint.y, -markDepthOffset * layer);
            extra.enabled = true;
            extraMarks[index].Add(extra);
        }

        station.AudioDirector.PlayStamp();
    }

    // A tie or no stamps counts as an incorrect decision
    public bool IsCorrect(int index, bool authentic)
    {
        if (realVotes[index] == fakeVotes[index])
        {
            return false;
        }
        return (realVotes[index] > fakeVotes[index]) == authentic;
    }

    public void ClearVotes(int index) // Removes all marks and votes from a painting
    {
        foreach (SpriteRenderer mark in extraMarks[index])
        {
            Destroy(mark.gameObject);
        }
        extraMarks[index].Clear();
        realVotes[index] = 0;
        fakeVotes[index] = 0;
        firstVoteWasReal[index] = true;
        firstVoteOffset[index] = Vector2.zero;
    }

    public void ResetShift()
    {
        if (dragged != null)
        {
            dragged.PutDown();
            dragged = null;
        }
        real.body.transform.localPosition = real.restPosition;
        fake.body.transform.localPosition = fake.restPosition;
        for (int i = 0; i < extraMarks.Length; i++)
        {
            ClearVotes(i);
        }
    }

    private void RefreshMarks() // Keeps stamp marks visible and aligned as paintings move
    {
        for (int i = 0; i < paintingMarks.Length; i++)
        {
            SpriteRenderer mark = paintingMarks[i];
            bool visible = station.framedPaintings[i].gameObject.activeInHierarchy;
            bool hasVote = realVotes[i] + fakeVotes[i] > 0;
            mark.enabled = visible && (station.IsReferenceSample(i) || hasVote);
            if (!visible)
            {
                continue;
            }

            if (firstVoteWasReal[i])
            {
                mark.sprite = realMarkSprite;
            }
            else
            {
                mark.sprite = fakeMarkSprite;
            }

            // Keep the first stamp mark aligned with the moving artwork
            Bounds art = station.ArtworkBounds(i);
            mark.transform.position = new Vector3(
                art.center.x + firstVoteOffset[i].x * art.size.x,
                art.center.y + firstVoteOffset[i].y * art.size.y, 0f);
        }
    }
}
