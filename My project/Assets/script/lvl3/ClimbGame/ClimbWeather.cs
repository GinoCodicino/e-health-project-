using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Un tronçon de la voie avec sa météo. Toutes ces valeurs sont des « leviers » :
/// un futur système météo peut les modifier pendant la partie.
/// </summary>
[Serializable]
public class WeatherSection
{
    public string name = "Beau temps";
    [Tooltip("Altitude où commence ce tronçon (m). C'est aussi le point de contrôle après une chute.")]
    public float startHeight;
    public Color sky = new Color(0.55f, 0.78f, 0.98f);
    [Tooltip("Pluie visible à l'écran.")]
    public bool rain;

    [Header("Grimpeur")]
    [Tooltip("Multiplie la perte d'endurance au fil du temps (froid, chaleur, pluie).")]
    public float drainMultiplier = 1f;
    [Tooltip("Multiplie la vitesse de montée (pluie = plus lent).")]
    public float climbSpeedMultiplier = 1f;

    [Header("Dangers (par seconde ou par tranche de paroi)")]
    [Tooltip("Pierres qui tombent, par seconde.")]
    public float rockRate = 0.25f;
    [Tooltip("Rafales qui poussent d'un couloir, par seconde.")]
    public float gustRate;
    [Tooltip("Éclairs, par seconde.")]
    public float lightningRate;
    [Tooltip("Probabilité d'une plaque mouillée / verglacée à chaque tranche de paroi.")]
    [Range(0f, 1f)] public float wetChance;
    [Tooltip("Probabilité d'une gourde d'eau à chaque tranche de paroi.")]
    [Range(0f, 1f)] public float waterChance = 0.12f;
}

/// <summary>
/// La météo de la voie, découpée en tronçons par altitude.
/// Le tronçon courant est choisi selon la hauteur du grimpeur.
/// </summary>
public class ClimbWeather : MonoBehaviour
{
    public List<WeatherSection> sections = new List<WeatherSection>
    {
        new WeatherSection { name = "Beau temps", startHeight = 0f,  sky = new Color(0.55f, 0.78f, 0.98f), rockRate = 0.25f, waterChance = 0.12f },
        new WeatherSection { name = "Vent",       startHeight = 30f, sky = new Color(0.62f, 0.74f, 0.86f), drainMultiplier = 1.1f, rockRate = 0.3f, gustRate = 0.25f, waterChance = 0.15f },
        new WeatherSection { name = "Pluie",      startHeight = 60f, sky = new Color(0.42f, 0.5f, 0.6f),   rain = true, drainMultiplier = 1.3f, climbSpeedMultiplier = 0.85f, rockRate = 0.35f, gustRate = 0.1f, wetChance = 0.35f, waterChance = 0.18f },
        new WeatherSection { name = "Orage",      startHeight = 90f, sky = new Color(0.24f, 0.26f, 0.34f), rain = true, drainMultiplier = 1.5f, climbSpeedMultiplier = 0.8f, rockRate = 0.4f, gustRate = 0.3f, lightningRate = 0.4f, wetChance = 0.3f, waterChance = 0.2f },
    };

    public WeatherSection Current { get; private set; }
    public int CurrentIndex { get; private set; } = -1;

    /// <summary>Levé à l'entrée dans un nouveau tronçon.</summary>
    public event Action<WeatherSection> OnSectionChanged;

    /// <summary>
    /// Met à jour le tronçon d'après l'altitude. On ne redescend jamais de tronçon pendant
    /// une montée (un recul après un choc ne doit pas relancer la météo précédente).
    /// </summary>
    public void UpdateForHeight(float height)
    {
        int index = IndexAt(height);
        if (index > CurrentIndex) SetSection(index);
    }

    /// <summary>Repart du tronçon correspondant à cette altitude (début ou point de contrôle).</summary>
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
