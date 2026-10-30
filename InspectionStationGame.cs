using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class InspectionStationGame : MonoBehaviour
{
    public const int Slots = 3;
    private const float Overscan = 0.2f;
    private const int TutorialReference = 0;
    private const int TutorialFake = 1;
    private const int TutorialReal = 2;
    private const int TutorialFinished = 3;
    private const int NewReferencesPerShift = 2;
    private const float AuthenticChance = 0.5f;

    [SerializeField] private Camera sceneCamera;
    public Camera SceneCamera => sceneCamera;
    [SerializeField] private InspectionConveyorSurface conveyorSurface;
    [SerializeField] private GameObject paintingTemplate;
    [SerializeField] private Sprite tutorialPainting;
    [SerializeField] private Shader elasticWavesShader;
    [SerializeField] private ShiftManager shiftDirector;
    [SerializeField] private InspectionStationStamps stamps;
    [SerializeField] private InspectionStationAudio audioDirector;
    public InspectionStationAudio AudioDirector => audioDirector;
    [SerializeField] private float conveyorSpeed = 0.82f;
    [SerializeField] private float paintingGap = 0.42f;
    [SerializeField] private float textCharactersPerSecond = 72f;
    [SerializeField] private int blipEveryCharacters = 5;
    [SerializeField] private float maximumSpeedIncrease = 0.65f;
    [SerializeField] private float speedIncreasePerRank = 0.14f;
    [SerializeField] private float tutorialStartDelay = 2f;
    [SerializeField] private float shiftEndAnnouncementDuration = 5f;
    [SerializeField] private Vector2 counterfeitSeed = new Vector2(37f, 17f);

    [System.NonSerialized] public SpriteRenderer[] framedPaintings;
    [System.NonSerialized] public SpriteRenderer[] artworkImages;
    private InspectionAutoFrame[] fits;

    private readonly bool[] artworkIsAuthentic = new bool[Slots];
    private readonly bool[] referenceSamples = new bool[Slots];
    private readonly Sprite[] originalsBySlot = new Sprite[Slots];
    private readonly Sprite[] counterfeitSprites = new Sprite[Slots];
    // Track which originals have already appeared as REAL references
    private readonly HashSet<Sprite> introducedOriginals = new HashSet<Sprite>();
    private struct Appearance
    {
        public Sprite painting;
        public bool reference;
        public bool authentic;

        public Appearance(Sprite painting, bool reference, bool authentic)
        {
            this.painting = painting;
            this.reference = reference;
            this.authentic = authentic;
        }
    }

    private readonly Queue<Appearance> paidPaintingQueue = new Queue<Appearance>();

    private InspectionMagnifier magnifier;
    private Material waveMaterial;
    private bool shiftEnded;
    private bool training;
    private float currentSpeed;
    private int nextPainting;
    private bool nextPaintingPending;
    private float firstPaintingReleaseAt;
    private int tutorialStage;

    private string message = "";
    private string displayedMessage = "";
    private int displayedCharacters;
    private float messageStartedAt;
    private float messageExpiresAt;

    public int TutorialStage => tutorialStage;
    public bool TutorialMagnifierUsed => magnifier.WasPickedUpDuringTutorial;
    public bool IsShiftEnded => shiftEnded;
    public string DisplayedMessage => displayedMessage;
    public bool IsReferenceSample(int index) => referenceSamples[index];
    public Bounds ArtworkBounds(int index) => artworkImages[index].bounds;
    public void ResetReferenceMemory() => introducedOriginals.Clear();

    private void Awake() // Sets up the magnifier, shader, and painting slots
    {
        magnifier = GetComponent<InspectionMagnifier>();
        waveMaterial = new Material(elasticWavesShader);
        GeneratePaintingSlots();
    }

    // Reuse three painting objects as artwork enters and leaves the belt
    private void GeneratePaintingSlots()
    {
        framedPaintings = new SpriteRenderer[Slots];
        artworkImages = new SpriteRenderer[Slots];
        fits = new InspectionAutoFrame[Slots];
        stamps.paintingMarks = new SpriteRenderer[Slots];

        Transform parent = new GameObject("Runtime paintings").transform;
        parent.SetParent(transform.parent, false);

        for (int i = 0; i < Slots; i++)
        {
            GameObject painting = Instantiate(paintingTemplate, parent);
            painting.name = "Painting " + (i + 1);

            SpriteRenderer frame = painting.GetComponent<SpriteRenderer>();
            InspectionAutoFrame fit = painting.GetComponent<InspectionAutoFrame>();
            fit.conveyerBelt = conveyorSurface.GetComponent<SpriteRenderer>();

            foreach (SpriteRenderer child in painting.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (child != frame && child != fit.Artwork)
                {
                    stamps.paintingMarks[i] = child;
                    break;
                }
            }

            framedPaintings[i] = frame;
            artworkImages[i] = fit.Artwork;
            fits[i] = fit;
            painting.SetActive(false);
        }
    }

    private void Update() // Moves the belt, releases paintings, and updates messages
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene("Gallery");
            return;
        }

        if (!shiftEnded)
        {
            MoveConveyor();
            if (training)
            {
                if (nextPaintingPending && Time.time >= firstPaintingReleaseAt)
                {
                    ReleaseNextPainting();
                }
            }
            else
            {
                FeedPaidPainting();
            }
        }
        UpdateMessageTyping();
    }

    private void MoveConveyor() // Moves active paintings and checks when they leave
    {
        // Move paintings at the same rate as the motor's spin-up
        float shift = currentSpeed * audioDirector.ConveyorMotion * Time.deltaTime;
        if (shift <= 0f)
        {
            return;
        }

        conveyorSurface.Scroll(shift);
        for (int i = 0; i < Slots; i++)
        {
            SpriteRenderer frame = framedPaintings[i];
            if (!frame.gameObject.activeSelf)
            {
                continue;
            }

            Vector3 position = frame.transform.position;
            position.x += shift;
            if (position.x > ExitCenterX(frame))
            {
                OnPaintingExit(i);
                if (shiftEnded)
                {
                    return;
                }
            }
            else
            {
                frame.transform.position = position;
            }
        }
    }

    // Spawn and remove paintings just beyond the visible camera area
    private float ScreenHalfWidth => sceneCamera.orthographicSize * sceneCamera.aspect;

    private float EntryCenterX(SpriteRenderer frame)
    {
        return sceneCamera.transform.position.x - ScreenHalfWidth - frame.bounds.extents.x - Overscan;
    }

    private float ExitCenterX(SpriteRenderer frame)
    {
        return sceneCamera.transform.position.x + ScreenHalfWidth + frame.bounds.extents.x + Overscan;
    }

    public void Announce(string text, float duration) // Starts a timed message that types onto the screen
    {
        message = text;
        displayedMessage = "";
        displayedCharacters = 0;
        messageStartedAt = Time.unscaledTime;
        messageExpiresAt = messageStartedAt + Mathf.Max(1f, duration);
    }

    private void UpdateMessageTyping() // Reveals dialogue one character at a time
    {
        if (Time.unscaledTime >= messageExpiresAt)
        {
            displayedMessage = "";
            return;
        }

        int reveal = Mathf.Min(message.Length, Mathf.FloorToInt((Time.unscaledTime - messageStartedAt) * textCharactersPerSecond));
        if (reveal == displayedCharacters)
        {
            return;
        }

        for (int i = displayedCharacters; i < reveal; i++)
        {
            if (i % blipEveryCharacters == 0 && char.IsLetterOrDigit(message[i]))
            {
                audioDirector.PlayDialogueBlip();
            }
        }
        displayedCharacters = reveal;
        displayedMessage = message.Substring(0, reveal);
    }

    public void StartShift() // Resets the belt and prepares the next shift
    {
        shiftEnded = false;
        training = shiftDirector.TutorialMode;
        tutorialStage = TutorialReference;
        nextPainting = 0;
        nextPaintingPending = false;
        paidPaintingQueue.Clear();

        currentSpeed = conveyorSpeed;
        if (!training)
        {
            currentSpeed *= 1f + Mathf.Min(maximumSpeedIncrease, shiftDirector.Rank * speedIncreasePerRank);
        }

        stamps.ResetShift();
        for (int i = 0; i < Slots; i++)
        {
            framedPaintings[i].gameObject.SetActive(false);
            referenceSamples[i] = false;
        }
        conveyorSurface.ResetScroll();

        if (training)
        {
            PrepareTutorial();
            firstPaintingReleaseAt = Time.time + tutorialStartDelay;
            nextPaintingPending = true;
        }
        else
        {
            PreparePaidShift();
        }

        audioDirector.SetConveyor(true);
        if (!training)
        {
            FeedPaidPainting();
        }
    }

    public void EndShift() // Stops inspections and announces the shift result
    {
        shiftEnded = true;
        stamps.CancelDrag();
        audioDirector.SetConveyor(false);
        Announce("SHIFT COMPLETE. OPENING THE ACCOUNTS LEDGER.", shiftEndAnnouncementDuration);
    }

    private void FeedPaidPainting() // Places the next queued painting when there is room
    {
        if (shiftEnded || paidPaintingQueue.Count == 0)
        {
            return;
        }

        int slot = -1;
        for (int i = 0; i < Slots; i++)
        {
            if (!framedPaintings[i].gameObject.activeSelf)
            {
                slot = i;
                break;
            }
        }
        if (slot < 0)
        {
            return;
        }

        Appearance next = paidPaintingQueue.Peek();

        // Let a reference move into view before releasing unmarked paintings
        if (!next.reference)
        {
            for (int i = 0; i < Slots; i++)
            {
                if (referenceSamples[i] && framedPaintings[i].gameObject.activeSelf &&
                    framedPaintings[i].bounds.center.x < sceneCamera.transform.position.x)
                {
                    return;
                }
            }
        }

        SpriteRenderer frame = framedPaintings[slot];
        // Keep frames from overlapping when they enter the belt
        float requiredLeft = EntryCenterX(frame) + frame.bounds.extents.x + paintingGap;
        foreach (SpriteRenderer other in framedPaintings)
        {
            if (other.gameObject.activeSelf && other.bounds.min.x < requiredLeft)
            {
                return;
            }
        }

        paidPaintingQueue.Dequeue();
        stamps.ClearVotes(slot);
        referenceSamples[slot] = next.reference;
        artworkIsAuthentic[slot] = next.authentic;
        originalsBySlot[slot] = next.painting;

        artworkImages[slot].sprite = next.painting;
        DisposeCounterfeit(slot);
        if (!next.reference)
        {
            ApplyArtworkVersion(slot);
        }
        SpawnSlot(slot);
    }

    private void ReleaseNextPainting() // Releases the next tutorial painting when ready
    {
        if (nextPainting >= Slots)
        {
            nextPaintingPending = false;
            return;
        }

        // Wait for the player to try the magnifying glass
        if (nextPainting == TutorialFake && !TutorialMagnifierUsed)
        {
            return;
        }

        SpawnSlot(nextPainting);
        nextPainting++;
        nextPaintingPending = false;
    }

    private void SpawnSlot(int index) // Places a painting at the belt entrance and shows it
    {
        fits[index].AutoFitPainting();
        SpriteRenderer frame = framedPaintings[index];
        Vector3 position = frame.transform.position;
        position.x = EntryCenterX(frame);
        frame.transform.position = position;
        frame.gameObject.SetActive(true);
    }

    private void OnPaintingExit(int index) // Grades a painting or advances the tutorial
    {
        framedPaintings[index].gameObject.SetActive(false);

        // Record the original once its reference has left the belt
        if (referenceSamples[index])
        {
            introducedOriginals.Add(originalsBySlot[index]);
            referenceSamples[index] = false;
            stamps.ClearVotes(index);
            ApplyArtworkVersion(index);
            if (training)
            {
                tutorialStage = TutorialFake;
                nextPaintingPending = true;
            }
            return;
        }

        bool right = stamps.IsCorrect(index, artworkIsAuthentic[index]);
        shiftDirector.RecordJudgement(right);

        // Repeat tutorial paintings until the player grades them correctly
        if (training && !right)
        {
            stamps.ClearVotes(index);
            SpawnSlot(index);
            return;
        }

        if (training)
        {
            if (index == TutorialFake)
            {
                tutorialStage = TutorialReal;
            }
            else
            {
                tutorialStage = TutorialFinished;
            }
            nextPaintingPending = true;
        }
        stamps.ClearVotes(index);
    }

    private void PrepareTutorial() // Sets up the reference and tutorial test paintings
    {
        DisposeCounterfeits();
        for (int i = 0; i < Slots; i++)
        {
            originalsBySlot[i] = tutorialPainting;
            artworkIsAuthentic[i] = i != TutorialFake;
            referenceSamples[i] = i == TutorialReference;
            artworkImages[i].sprite = tutorialPainting;
            if (!referenceSamples[i])
            {
                ApplyArtworkVersion(i);
            }
            else
            {
                fits[i].AutoFitPainting();
            }
        }
    }

    private void PreparePaidShift() // Chooses the paintings and their order for a shift
    {
        Sprite[] catalogue = Resources.LoadAll<Sprite>("Paintings");
        List<Sprite> unseen = new List<Sprite>();
        List<Sprite> known = new List<Sprite>();
        HashSet<Sprite> unique = new HashSet<Sprite>();

        foreach (Sprite painting in catalogue)
        {
            if (!unique.Add(painting))
            {
                continue;
            }
            if (introducedOriginals.Contains(painting))
            {
                known.Add(painting);
            }
            else
            {
                unseen.Add(painting);
            }
        }

        // Queue new REAL reference paintings before any unmarked copies
        List<Sprite> shiftPaintings = new List<Sprite>();
        for (int i = 0; i < NewReferencesPerShift && unseen.Count > 0; i++)
        {
            int choice = Random.Range(0, unseen.Count);
            Sprite original = unseen[choice];
            unseen.RemoveAt(choice);
            shiftPaintings.Add(original);
            paidPaintingQueue.Enqueue(new Appearance(original, true, true));
        }

        // Reuse known originals when no new paintings remain
        while (shiftPaintings.Count < NewReferencesPerShift && known.Count > 0)
        {
            int choice = Random.Range(0, known.Count);
            shiftPaintings.Add(known[choice]);
            known.RemoveAt(choice);
        }
        if (shiftPaintings.Count == 0)
        {
            Debug.LogError("No paintings found in Resources/Paintings.", this);
            return;
        }

        for (int i = 0; i < shiftDirector.PaintingsThisShift; i++)
        {
            Sprite original = shiftPaintings[i % shiftPaintings.Count];
            bool authentic = Random.value > AuthenticChance;
            paidPaintingQueue.Enqueue(new Appearance(original, false, authentic));
        }
    }

    private void ApplyArtworkVersion(int index) // Shows either the original or a fake version
    {
        if (artworkIsAuthentic[index])
        {
            artworkImages[index].sprite = originalsBySlot[index];
        }
        else
        {
            artworkImages[index].sprite = MakeCounterfeit(originalsBySlot[index], index);
        }
        fits[index].AutoFitPainting();
    }

    private Sprite MakeCounterfeit(Sprite original, int index) // Creates a distorted copy of the original painting
    {
        Rect rect = original.rect;
        Texture2D source = original.texture;
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);

        waveMaterial.SetFloat("_Seed", index * counterfeitSeed.x + counterfeitSeed.y + Random.Range(0f, 10000f));
        waveMaterial.SetFloat("_Strength", Random.Range(0.5f, 1f));
        // Tell the shader which region of the texture contains this sprite
        waveMaterial.SetVector("_UvRect", new Vector4(
            rect.x / source.width, rect.y / source.height,
            rect.width / source.width, rect.height / source.height));

        // Bake the shader effect into a separate counterfeit sprite
        RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(source, target, waveMaterial);
        RenderTexture.active = target;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply(false, true);

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);

        Vector2 pivot = new Vector2(original.pivot.x / rect.width, original.pivot.y / rect.height);
        Sprite copy = Sprite.Create(texture, new Rect(0, 0, width, height), pivot,
            original.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        counterfeitSprites[index] = copy;
        return copy;
    }

    // Free textures created for counterfeits when a slot is reused
    private void DisposeCounterfeit(int index)
    {
        Sprite sprite = counterfeitSprites[index];
        if (sprite != null)
        {
            Destroy(sprite.texture);
            Destroy(sprite);
            counterfeitSprites[index] = null;
        }
    }

    private void DisposeCounterfeits()
    {
        for (int i = 0; i < Slots; i++)
        {
            DisposeCounterfeit(i);
        }
    }

    private void OnDestroy() // Cleans up generated counterfeits and the shader material
    {
        DisposeCounterfeits();
        Destroy(waveMaterial);
    }
}
