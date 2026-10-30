using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class InspectionShiftResultsUI : MonoBehaviour
{
    [SerializeField] private ShiftManager director;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text heading;
    [SerializeField] private TMP_Text details;
    [SerializeField] private TMP_Text leftLabel;
    [SerializeField] private TMP_Text rightLabel;
    [SerializeField] private Button rightButton;

    // TMP position tags keep the ledger amounts aligned
    private const string ValuePosition = "<pos=70%>";
    private const string Rule = "<color=#8D8169>────────────────────────────────────</color>\n";

    private void Start() // Listens for shift changes to update the results screen
    {
        director.Changed += Refresh;
        Refresh();
    }

    private void OnDestroy() // Stops listening for shift changes
    {
        director.Changed -= Refresh;
    }

    private void Refresh() // Shows the report, raise offer, or fired screen
    {
        ShiftManager.State state = director.CurrentGameState;
        panel.SetActive(state != ShiftManager.State.Inspecting);
        if (state == ShiftManager.State.Inspecting)
        {
            return;
        }

        heading.text = state == ShiftManager.State.Fired ? "FIRED" : "SHIFT " + director.ShiftNumber;

        // Use the same panel for the report and promotion choices
        if (state == ShiftManager.State.PromotionOffer)
        {
            rightButton.gameObject.SetActive(true);
            details.text = "MANAGEMENT HAS OFFERED YOU A RAISE.\n\n" + Row("PAY PER CORRECT GRADING", Money(director.PromotionRewardBonus), true);
            leftLabel.text = director.TutorialMode ? "YES / FINISH" : "ACCEPT";
            rightLabel.text = director.TutorialMode ? "NO / FINISH" : "DECLINE";
            return;
        }

        rightButton.gameObject.SetActive(false);

        details.text =
            Row("PAINTINGS GRADED", director.Inspected.ToString()) +
            Row("CORRECT", director.Correct.ToString()) +
            Row("INCORRECT", director.Wrong.ToString()) + "\n" +
            Row("PAY PER CORRECT", Dollar(director.PayPerCorrect)) +
            Row("CORRECT WORK", Money(director.Gross)) +
            Rule +
            Row("TOTAL EARNED", Money(director.Net), true) +
            Row("QUOTA COLLECTED", Money(-director.LastShiftQuota)) +
            Rule +
            Row("PREVIOUS BANK", Dollar(director.BankBeforeShift)) +
            Row("CHANGE TO BANK", Money(director.LastBankChange), true) +
            Rule +
            Row("CURRENT BANK", Dollar(director.Bank), true);

        bool fired = state == ShiftManager.State.Fired;
        if (fired)
        {
            int shortfall = Mathf.Max(0, -director.BankBeforeShift - director.LastBankChange);
            details.text += "\nYOU HAVE BEEN FIRED FOR NOT MEETING THE QUOTA.\n" +
                Row("SHIFTS COMPLETED", (director.ShiftNumber - 1).ToString(), true) +
                Row("UNPAID SHORTFALL", Money(-shortfall));
            leftLabel.text = "RESTART RUN";
        }
        else
        {
            leftLabel.text = "CONTINUE";
        }

    }

    private static string Row(string label, string value, bool bold = false) // Builds an aligned row for the results screen
    {
        string line = label + ValuePosition + value;
        if (bold)
        {
            return "<b>" + line + "</b>\n";
        }
        return line + "\n";
    }

    private static string Dollar(int value) // Formats an amount without a gain or loss color
    {
        return "<color=#E8DCC1>$" + value.ToString("N0") + "</color>";
    }

    // Color earnings green and losses red
    private static string Money(int value)
    {
        if (value < 0)
        {
            return "<color=#E7837A>-$" + (-value).ToString("N0") + "</color>";
        }
        if (value == 0)
        {
            return Dollar(0);
        }
        return "<color=#8ED7A0>+$" + value.ToString("N0") + "</color>";
    }

    public void Left() // Continues, accepts a raise, or restarts depending on the screen
    {
        switch (director.CurrentGameState)
        {
            case ShiftManager.State.ShiftReport:
                director.Continue();
                break;
            case ShiftManager.State.PromotionOffer:
                director.ChoosePromotion(true);
                break;
            case ShiftManager.State.Fired:
                director.RestartRun();
                break;
        }
    }

    public void Right() // Declines a raise or finishes the tutorial
    {
        director.ChoosePromotion(false);
    }
}
