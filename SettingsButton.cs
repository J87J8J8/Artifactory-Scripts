//DONE
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SettingsButton : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private TMP_Text masterVolumePercent;
    [SerializeField] private TMP_Text musicVolumePercent;

    private void Start() //Gets the PlayerPrefs or sets them to 1f as default.
    {
        masterVolumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("Volume.Master", 1f));
        musicVolumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("Volume.Music", 1f));
        Refresh();
    }

    public void OpenPanel()
    {
        panel.SetActive(true);
    }

    public void ClosePanel()
    {
        panel.SetActive(false);
    }

    public void SetMasterVolume(float volume)
    {
        PlayerPrefs.SetFloat("Volume.Master", volume);
        Refresh();
    }

    public void SetMusicVolume(float volume)
    {
        PlayerPrefs.SetFloat("Volume.Music", volume);
        Refresh();
    }

    private void Refresh() // Refreshes the UI, updating the text and volumes
    {
        masterVolumePercent.text = Mathf.RoundToInt(masterVolumeSlider.value * 100f) + "%";
        musicVolumePercent.text = Mathf.RoundToInt(musicVolumeSlider.value * 100f) + "%";
        MusicManager.SourceAudio.volume = masterVolumeSlider.value * musicVolumeSlider.value;
    }
}
