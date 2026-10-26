using UnityEngine;

/// <summary>
/// Sonidos de un tanque
///   · Fire Clip: suena cada vez que este tanque dispara
///   · Hit Clip:  suena cada vez que este tanque recibe un impacto
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioController : MonoBehaviour
{
    [Header("Disparo")]
    public AudioClip fireClip;
    [Range(0f, 1f)] public float fireVolume = 0.7f;

    [Header("Impacto recibido")]
    public AudioClip hitClip;
    [Range(0f, 1f)] public float hitVolume = 1f;

    [Header("Variedad")]
    [Range(0f, 0.3f)] public float pitchVariation = 0.08f;

    private TankController tank;
    private AudioSource audioSource;
    private float lastHealth;

    private void Awake()
    {
        tank = GetComponent<TankController>();
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;   
    }

    private void OnEnable()
    {
        if (tank == null)
        {
            return;
        }

        lastHealth = tank.maxHealth;
        tank.Fired += OnFired;
        tank.HealthChanged += OnHealthChanged;
    }

    private void OnDisable()
    {
        if (tank == null)
        {
            return;
        }

        tank.Fired -= OnFired;
        tank.HealthChanged -= OnHealthChanged;
    }

    private void OnFired(Rigidbody bullet)
    {
        Play(fireClip, fireVolume);
    }

    private void OnHealthChanged(float current, float max)
    {
        // Solo suena si la vida ha bajado
        if (current < lastHealth)
        {
            Play(hitClip, hitVolume);
        }

        lastHealth = current;
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            return;
        }

        audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        // PlayOneShot permite que dos sonidos se solapen sin cortarse
        audioSource.PlayOneShot(clip, volume);
    }
}