//DONE
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private SpriteRenderer tutorialCompleteStamp;

    private void Start() // Shows the completion stamp if the tutorial was finished
    {
        tutorialCompleteStamp.enabled = PlayerPrefs.GetInt("Tutorial.Complete", 0) != 0;
    }

    public void LaunchGameScene(bool tutorial) // Launches the game scene and puts it in tutorial mode if the tutorial was selected
    {
        ShiftManager.Tutorial = tutorial;
        SceneManager.LoadScene("Inspection Floor");
    }
}
