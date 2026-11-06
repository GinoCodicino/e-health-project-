using UnityEngine;

/// <summary>
/// Particles carried by the WindSystem around the player: long faint streaks and small dust motes.
/// More of them, and faster, during gusts. Built in code at startup.
/// </summary>
public class WindParticles : MonoBehaviour
{
    [Tooltip("Particle material (e.g. URP's ParticlesUnlit). Empty = created from the URP particle shader.")]
    [SerializeField] private Material material;
    [Tooltip("Size of the area filled with particles around the player (m).")]
    [SerializeField] private Vector3 area = new Vector3(40f, 8f, 40f);

    [Header("Streaks")]
    [SerializeField] private Color streakColor = new Color(1f, 1f, 1f, 0.18f);
    [SerializeField] private Vector2 streakRate = new Vector2(3f, 30f);     // calm, gust
    [SerializeField] private Vector2 streakSpeed = new Vector2(6f, 14f);

    [Header("Dust")]
    [SerializeField] private Color dustColor = new Color(0.95f, 0.9f, 0.8f, 0.45f);
    [SerializeField] private Vector2 dustRate = new Vector2(20f, 60f);
    [SerializeField] private Vector2 dustSpeed = new Vector2(1.5f, 5f);

    private Transform player;
    private ParticleSystem streaks, dust;

    private void Awake()
    {
        var controller = FindFirstObjectByType<playercontroller>();
        player = controller != null ? controller.transform : Camera.main != null ? Camera.main.transform : transform;
        if (material == null) material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));

        streaks = Create("WindStreaks", streakColor, new Vector2(0.03f, 0.06f), 1.2f, true);
        dust = Create("WindDust", dustColor, new Vector2(0.03f, 0.08f), 4f, false);
    }

    private void LateUpdate()
    {
        var wind = WindSystem.Instance;
        if (wind == null) return;
        float gust = wind.Gust01;

        // The area follows the player, shifted upwind so particles cross the view.
        Vector3 center = player.position + Vector3.up * area.y * 0.3f - wind.Direction * area.z * 0.25f;
        Drive(streaks, center, wind.Direction, Mathf.Lerp(streakRate.x, streakRate.y, gust), Mathf.Lerp(streakSpeed.x, streakSpeed.y, gust));
        Drive(dust, center, wind.Direction, Mathf.Lerp(dustRate.x, dustRate.y, gust), Mathf.Lerp(dustSpeed.x, dustSpeed.y, gust));
    }

    private static void Drive(ParticleSystem ps, Vector3 center, Vector3 direction, float rate, float speed)
    {
        ps.transform.position = center;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        // Applies to the particles already flying too: the whole cloud speeds up with a gust.
        // x, y and z must use the same curve mode (two constants, like the vertical drift).
        var velocity = ps.velocityOverLifetime;
        velocity.x = new ParticleSystem.MinMaxCurve(direction.x * speed * 0.8f, direction.x * speed * 1.2f);
        velocity.z = new ParticleSystem.MinMaxCurve(direction.z * speed * 0.8f, direction.z * speed * 1.2f);
    }

    private ParticleSystem Create(string name, Color color, Vector2 size, float lifetime, bool stretched)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime * 1.3f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1000;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = area;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        // A little vertical drift so the motion isn't perfectly flat.
        velocity.y = new ParticleSystem.MinMaxCurve(-0.3f, 0.4f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = stretched ? 0.3f : 0.8f;
        noise.frequency = 0.4f;

        // Fade in and out instead of popping.
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (stretched)
        {
            // Long thin lines aligned with their motion.
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.12f;
            renderer.lengthScale = 2f;
        }

        ps.Play();
        return ps;
    }
}
