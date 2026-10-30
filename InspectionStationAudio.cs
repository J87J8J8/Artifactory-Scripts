//DONE1

using UnityEngine;

public sealed class InspectionStationAudio : MonoBehaviour
{
    [SerializeField] private AudioSource conveyorSource;
    [SerializeField] private AudioSource effectSource;
    [SerializeField] private AudioSource dialogueSource;

    [SerializeField] private AudioClip stampPickup;
    [SerializeField] private AudioClip stampReturn;
    [SerializeField] private AudioClip stampImpact;
    [SerializeField] private AudioClip stampMechanical;
    [SerializeField] private AudioClip dialogueBlip;

    [SerializeField] private float motorSpinUpSeconds = 1.45f;
    [SerializeField] private float idleMotorPitch = 0.48f;
    [SerializeField] private float motorVolume = 0.43f;
    [SerializeField] private float ambience = 0.75f;
    [SerializeField] private float effects = 0.85f;
    [SerializeField] private float dialogue = 0.5f;

    [SerializeField] private float masterVolumeDefault = 0.85f;
    [SerializeField] private float pickupVolume = 0.77f;
    [SerializeField] private float returnVolume = 0.58f;
    [SerializeField] private float stampImpactVolume = 0.95f;
    [SerializeField] private float stampMechanicalVolume = 0.26f;
    [SerializeField] private float blipVolume = 0.32f;
    [SerializeField] private float blipCooldown = 0.065f;
    [SerializeField] private Vector2 blipPitchRange = new Vector2(0.97f, 1.04f);

    private bool conveyorRunning;
    private float speed;
    private float lastBlipTime = -100f;
    private float master;

    public float ConveyorMotion => speed;

    private void Awake() // Loads the saved volume and sets the sound levels
    {
        master = PlayerPrefs.GetFloat("QuietShopping.Audio.Master", masterVolumeDefault);
        effectSource.volume = master * effects;
        dialogueSource.volume = master * dialogue;
    }

    private void Update() // Adjusts the motor sound as the belt speeds up
    {
        if (conveyorRunning)
        {
            speed = Mathf.MoveTowards(speed, 1f, Time.deltaTime / motorSpinUpSeconds);
        }
        // Ease in the motor's sound as the belt speeds up
        float smooth = speed * speed * (3f - 2f * speed);
        conveyorSource.pitch = Mathf.Lerp(idleMotorPitch, 1f, smooth);
        conveyorSource.volume = master * ambience * motorVolume * smooth;
    }

    public void SetConveyor(bool running) // Starts or stops the conveyor motor sound
    {
        conveyorRunning = running;
        if (running)
        {
            if (!conveyorSource.isPlaying)
            {
                conveyorSource.Play();
            }
        }
        else
        {
            speed = 0f;
            conveyorSource.Stop();
        }
    }

    public void PlayPickup()
    {
        effectSource.PlayOneShot(stampPickup, pickupVolume);
    }

    public void PlayReturn()
    {
        effectSource.PlayOneShot(stampReturn, returnVolume);
    }

    public void PlayStamp() // Plays both stamp impact sounds together
    {
        effectSource.PlayOneShot(stampImpact, stampImpactVolume);
        effectSource.PlayOneShot(stampMechanical, stampMechanicalVolume);
    }

    public void PlayDialogueBlip()
    {
        // Prevent dialogue sound effects from playing too rapidly
        if (Time.unscaledTime - lastBlipTime < blipCooldown)
        {
            return;
        }
        lastBlipTime = Time.unscaledTime;
        dialogueSource.pitch = Random.Range(blipPitchRange.x, blipPitchRange.y);
        dialogueSource.PlayOneShot(dialogueBlip, blipVolume);
    }
}
