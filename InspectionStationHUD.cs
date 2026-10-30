using TMPro;
using UnityEngine;

public sealed class InspectionStationHUD : MonoBehaviour
{
    [SerializeField] private InspectionStationGame station;
    [SerializeField] private ShiftManager shiftDirector;
    [SerializeField] private GameObject intercomPanel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text scoreText;

    private void Start() // Hides the score during training and listens for updates
    {
        scoreText.gameObject.SetActive(!shiftDirector.TutorialMode);
        shiftDirector.Changed += RefreshScore;
        RefreshScore();
    }

    private void OnDestroy() // Unsubscribes from score updates when destroyed
    {
        shiftDirector.Changed -= RefreshScore;
    }

    private void LateUpdate() // Updates dialogue and hides the intercom when there is no message
    {
        string speech = station.DisplayedMessage;
        messageText.text = speech;
        intercomPanel.SetActive(speech.Length > 0);
    }

    private void RefreshScore() // Updates the shift, painting count, bank, and quota
    {
        scoreText.text = "SHIFT " + shiftDirector.ShiftNumber +
            "   |   FILED " + shiftDirector.Inspected + "/" + shiftDirector.PaintingsThisShift +
            "   |   BANK $" + shiftDirector.Bank +
            "   |   QUOTA $" + shiftDirector.Quota;
    }
}
