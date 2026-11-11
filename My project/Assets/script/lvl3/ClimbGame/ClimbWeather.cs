using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A section of the route with its weather. All these values are "levers":
/// a future weather system can change them during the game.
/// </summary>
[Serializable]
public class WeatherSection
{
    public string name = "The weather is fine...";
    [Tooltip("Altitude where this section starts (m). It is also the checkpoint after a fall.")]
    public float startHeight;
    [Tooltip("Sky color at the horizon, also used by the fog.")]
    public Color sky = new Color(0.55f, 0.78f, 0.98f);
    [Tooltip("Sky color at the very top (zenith).")]
    public Color skyTop = new Color(0.2f, 0.42f, 0.8f);
    [Tooltip("Rain visible on screen.")]
    public bool rain;

    [Header("Climber")]
    [Tooltip("Multiplies stamina loss over time (cold, heat, rain).")]
    public float drainMultiplier = 1f;
    [Tooltip("Multiplies climbing speed (rain = slower).")]
    public float climbSpeedMultiplier = 1f;

    [Header("Hazards (per second or per wall slice)")]
    [Tooltip("Falling rocks, per second.")]
    public float rockRate = 0.25f;
    [Tooltip("Gusts that push one lane over, per second.")]
    public float gustRate;
    [Tooltip("Lightning strikes, per second.")]
    public float lightningRate;
    [Tooltip("Chance of a wet / icy patch on each wall slice.")]
    [Range(0f, 1f)] public float wetChance;
    [Tooltip("Chance of a water bottle on each wall slice.")]
    [Range(0f, 1f)] public float waterChance = 0.12f;

    /// <summary>Zenith; if skyTop was never set (transparent), a darker version of the horizon.</summary>
    public Color SkyTop => skyTop.a > 0f ? skyTop : sky * 0.55f;
}

/// <summary>
/// The route's weather, split into sections by altitude.
/// The current section is chosen from the climber's height.
/// </summary>
public class ClimbWeather : MonoBehaviour
{
    public List<WeatherSection> sections = new List<WeatherSection>
    {
        new WeatherSection { name = "Clear skies", startHeight = 0f,  sky = new Color(0.55f, 0.78f, 0.98f), skyTop = new Color(0.2f, 0.42f, 0.8f), rockRate = 0.25f, waterChance = 0.12f },
        new WeatherSection { name = "Wind",       startHeight = 30f, sky = new Color(0.62f, 0.74f, 0.86f), skyTop = new Color(0.3f, 0.45f, 0.7f), drainMultiplier = 1.1f, rockRate = 0.3f, gustRate = 0.25f, waterChance = 0.15f },
        new WeatherSection { name = "Rain",       startHeight = 60f, sky = new Color(0.42f, 0.5f, 0.6f), skyTop = new Color(0.25f, 0.3f, 0.4f),   rain = true, drainMultiplier = 1.3f, climbSpeedMultiplier = 0.85f, rockRate = 0.35f, gustRate = 0.1f, wetChance = 0.35f, waterChance = 0.18f },
        new WeatherSection { name = "Storm",      startHeight = 90f, sky = new Color(0.24f, 0.26f, 0.34f), skyTop = new Color(0.08f, 0.09f, 0.14f), rain = true, drainMultiplier = 1.5f, climbSpeedMultiplier = 0.8f, rockRate = 0.4f, gustRate = 0.3f, lightningRate = 0.4f, wetChance = 0.3f, waterChance = 0.2f },
    };

    public WeatherSection Current { get; private set; }
    public int CurrentIndex { get; private set; } = -1;

    /// <summary>Raised when entering a new section.</summary>
    public event Action<WeatherSection> OnSectionChanged;

    /// <summary>
    /// Updates the section from the altitude. Never goes back down a section during
    /// a climb (being knocked back after a hit must not restart the previous weather).
    /// </summary>
    public void UpdateForHeight(float height)
    {
        int index = IndexAt(height);
        if (index > CurrentIndex) SetSection(index);
    }

    /// <summary>Restarts from the section matching this altitude (start or checkpoint).</summary>
    public void ResetTo(float height) => SetSection(IndexAt(height));

    private int IndexAt(float height)
    {
        int index = 0;
        for (int i = 0; i < sections.Count; i++)
            if (height >= sections[i].startHeight) index = i;
        return index;
    }

    private void SetSection(int index)
    {
        CurrentIndex = index;
        Current = sections[index];
        OnSectionChanged?.Invoke(Current);
    }
}
