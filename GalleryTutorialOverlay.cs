using UnityEngine;

public sealed class GalleryTutorialOverlay : MonoBehaviour
{
    private const int FakeStage = 1;
    private const int RealStage = 2;

    [SerializeField] private InspectionStationGame station;
    [SerializeField] private ShiftManager shiftDirector;
    [SerializeField] private float messageDuration = 20f;
    [SerializeField] private float retryDuration = 12f;

    private int explainedStage;
    private int lastWrongCount;
    private bool magnifierPrompted;
    private bool raiseExplained;

    private void Start() // Shows the tutorial introduction only in training mode
    {
        if (!shiftDirector.TutorialMode)
        {
            enabled = false;
            return;
        }

        station.Announce(
            "Welcome to the inspection floor. The conveyor always moves. " +
            "The first Mona Lisa is REAL and already stamped. It is your reference. " +
            "Take a closer look with the magnifying glass.", messageDuration);
    }

    private void Update() // Shows instructions based on the current tutorial step
    {
        if (shiftDirector.CurrentGameState != ShiftManager.State.Inspecting)
        {
            if (!raiseExplained && shiftDirector.CurrentGameState == ShiftManager.State.PromotionOffer)
            {
                raiseExplained = true;
                station.Announce(
                    "After every shift, you can accept or decline a raise. " +
                    "Accepting earns more money, but the conveyor moves faster, making differences harder to spot. " +
                    "Choose to finish training.", messageDuration);
            }
            return;
        }

        int stage = station.TutorialStage;
        if (stage == FakeStage && !station.TutorialMagnifierUsed)
        {
            if (!magnifierPrompted)
            {
                magnifierPrompted = true;
                station.Announce(
                    "Pick up the magnifying glass by its handle and move it around to continue.",
                    messageDuration);
            }
            return;
        }

        // Explain each tutorial step only once
        if (stage > explainedStage && stage <= RealStage)
        {
            explainedStage = stage;
            station.Announce(stage == FakeStage ?
                "Now inspect an unmarked Mona Lisa. Compare it with the REAL reference. " +
                "Drag the green REAL or red FAKE stamp onto it before it leaves." :
                "Correct, that painting was FAKE. Here is another unmarked Mona Lisa. " +
                "Decide if it is REAL or FAKE and stamp your answer.", messageDuration);
        }

        // Prompt the player again after an incorrect tutorial answer
        if (shiftDirector.Wrong > lastWrongCount)
        {
            lastWrongCount = shiftDirector.Wrong;
            station.Announce(
                "Incorrect. That painting was " + (stage == FakeStage ? "FAKE" : "REAL") +
                ". Try again.", retryDuration);
        }
    }
}
