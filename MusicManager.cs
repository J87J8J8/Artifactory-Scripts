//DONE
using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static AudioSource SourceAudio;

    private void Awake()
    {
        SourceAudio = GetComponent<AudioSource>(); // So the SettingsButton script can access AudioSource
        DontDestroyOnLoad(gameObject); // So the music continues when changing scenes
    }
}
