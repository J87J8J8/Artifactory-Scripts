//DONE
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class MenuSpotlightFollow : MonoBehaviour
{
    [SerializeField] private Camera galleryCamera;

    private void Update()
    {
        Vector3 mousePos = galleryCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue()); // Get the mouse position
        transform.position = new Vector3(mousePos.x, mousePos.y, transform.position.z); // Makes the spotlight follow the mouse while staying on same Z (2D) plane
    }
}