using UnityEngine;

/// <summary>
/// Wind sound generated in code (no audio file): low rumbling noise that gets louder
/// and brighter with the gusts of the WindSystem.
/// </summary>
[RequireComponent(typeof(AudioSource), typeof(AudioLowPassFilter))]
public class WindAudio : MonoBehaviour
{
    [Range(0f, 1f)] [SerializeField] private float volume = 0.4f;
    [Tooltip("Share of the volume kept when the wind is calm.")]
    [Range(0f, 1f)] [SerializeField] private float calmVolume = 0.35f;
    [Tooltip("Low-pass cutoff when calm / at the peak of a gust (Hz): higher = whistling.")]
    [SerializeField] private Vector2 cutoff = new Vector2(350f, 1600f);
    [Tooltip("How fast the sound follows the gusts.")]
    [SerializeField] private float response = 2f;

    private AudioSource source;
    private AudioLowPassFilter lowPass;
    private float level;

    // Audio thread state.
    private readonly System.Random random = new System.Random();
    private float brownL, brownR;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        lowPass = GetComponent<AudioLowPassFilter>();

        int rate = AudioSettings.outputSampleRate;
        source.clip = AudioClip.Create("Wind", rate * 2, 2, rate, true, Generate);
        source.loop = true;
        source.spatialBlend = 0f;   // heard everywhere, like the wind around you
        source.playOnAwake = false;
        source.volume = 0f;
    }

    private void Start() => source.Play();

    private void Update()
    {
        var wind = WindSystem.Instance;
        float gust = wind != null ? wind.Gust01 : 0f;
        level = Mathf.MoveTowards(level, gust, response * Time.deltaTime);

        source.volume = volume * Mathf.Lerp(calmVolume, 1f, level);
        lowPass.cutoffFrequency = Mathf.Lerp(cutoff.x, cutoff.y, level);
    }

    // Brown noise (integrated white noise): a soft, deep rumble. Left and right differ for width.
    private void Generate(float[] data)
    {
        for (int i = 0; i + 1 < data.Length; i += 2)
        {
            brownL = Mathf.Clamp(brownL * 0.995f + ((float)random.NextDouble() * 2f - 1f) * 0.06f, -1f, 1f);
            brownR = Mathf.Clamp(brownR * 0.995f + ((float)random.NextDouble() * 2f - 1f) * 0.06f, -1f, 1f);
            data[i] = brownL * 2.5f;
            data[i + 1] = brownR * 2.5f;
        }
    }
}
