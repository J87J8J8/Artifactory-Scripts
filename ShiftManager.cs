using UnityEngine;
using UnityEngine.SceneManagement;

public class ShiftManager : MonoBehaviour
{
    public enum State { Inspecting, ShiftReport, PromotionOffer, Fired }

    [SerializeField] private InspectionStationGame station;

    [SerializeField] private int initialPaintingsPerShift = 5;
    [SerializeField] private int paintingsAddedEachShift = 2;
    [SerializeField] private int maximumPaintingsPerShift = 15;
    [SerializeField] private int initialBank = 0;
    [SerializeField] private int correctPaintingPay = 34;
    [SerializeField] private int initialQuota = 85;
    [SerializeField] private int quotaIncreasePerShift = 5;
    [SerializeField] private int maximumQuotaIncrease = 30;
    [SerializeField] private int raiseBonus = 9;
    [SerializeField] private int quotaPerAdditionalPainting = 12;
    [SerializeField] private float welcomeAnnouncementDuration = 14f;
    public int PromotionRewardBonus => raiseBonus;

    private const int realReferencePaintings = 2;
    // Carry the tutorial choice from the menu into the inspection scene
    public static bool Tutorial;

    public State CurrentGameState { get; private set; }
    public int ShiftNumber { get; private set; }
    public int Bank { get; private set; }
    public int Rank { get; private set; }
    public int Inspected { get; private set; }
    public int Correct { get; private set; }
    public int BankBeforeShift { get; private set; }
    public int LastShiftQuota { get; private set; }
    public int LastBankChange { get; private set; }
    public bool TutorialMode { get; private set; }

    public int PaintingsThisShift => Mathf.Min(maximumPaintingsPerShift, initialPaintingsPerShift + (ShiftNumber - 1) * paintingsAddedEachShift);
    public int Wrong => Inspected - Correct;

    public int Quota => initialQuota + (PaintingsThisShift - initialPaintingsPerShift) * quotaPerAdditionalPainting + Mathf.Min(ShiftNumber - 1, maximumQuotaIncrease / quotaIncreasePerShift) * quotaIncreasePerShift;
    public int PayPerCorrect => correctPaintingPay + Rank * raiseBonus;
    public int Gross => Correct * PayPerCorrect;
    public int Net => Gross;

    public event System.Action Changed;

    private void Awake() // Reads the tutorial choice and resets starting values
    {
        TutorialMode = Tutorial;
        Tutorial = false;
        Bank = initialBank;
        ShiftNumber = 1;
    }

    private void Start()
    {
        BeginShift();
    }

    private void BeginShift() // Resets the counters and starts inspecting paintings
    {
        CurrentGameState = State.Inspecting;
        Inspected = 0;
        Correct = 0;
        BankBeforeShift = Bank;
        station.StartShift();
        if (!TutorialMode)
        {
            if (ShiftNumber == 1)
            {
                station.Announce("Welcome to your first shift, good luck!", welcomeAnnouncementDuration);
            }
            else
            {
                station.Announce("Congratulations on making it to shift " + ShiftNumber + "!", welcomeAnnouncementDuration);
            }
        }
        Changed?.Invoke();
    }

    public void RecordJudgement(bool isCorrect) // Counts a grading result and checks if the shift is over
    {
        Inspected++;
        if (isCorrect)
        {
            Correct++;
        }

        // Training counts correct answers; paid shifts count all paintings
        bool shiftOver = TutorialMode ? Correct >= realReferencePaintings : Inspected >= PaintingsThisShift;

        if (shiftOver)
        {
            CompleteShift();
        }
        else
        {
            Changed?.Invoke();
        }
    }

    private void CompleteShift() // Updates the bank and checks if the player is fired
    {
        station.EndShift();
        // Subtract the quota from earnings before updating the bank
        LastShiftQuota = Quota;
        LastBankChange = Net - LastShiftQuota;
        Bank += LastBankChange;

        CurrentGameState = State.ShiftReport;
        // Fire the player if their bank cannot cover the quota
        if (Bank < 0 && !TutorialMode)
        {
            Bank = 0;
            CurrentGameState = State.Fired;
        }
        Changed?.Invoke();
    }

    public void Continue() // Opens the raise offer after the shift report
    {
        CurrentGameState = State.PromotionOffer;
        Changed?.Invoke();
    }

    public void ChoosePromotion(bool accept) // Applies the raise choice or finishes training
    {
        if (TutorialMode)
        {
            PlayerPrefs.SetInt("Tutorial.Complete", 1);
            SceneManager.LoadScene("Gallery");
            return;
        }

        // A raise increases both pay and conveyor difficulty
        if (accept)
        {
            Rank++;
        }
        ShiftNumber++;
        BeginShift();
    }

    public void RestartRun() // Resets progress and starts again from shift one
    {
        Bank = initialBank;
        Rank = 0;
        ShiftNumber = 1;
        station.ResetReferenceMemory();
        BeginShift();
    }
}
