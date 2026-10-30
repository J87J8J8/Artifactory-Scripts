//
using UnityEngine;

public sealed class ExitButton : MonoBehaviour
{
    public void Exit() // Stops play mode in the editor or quits the game
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
