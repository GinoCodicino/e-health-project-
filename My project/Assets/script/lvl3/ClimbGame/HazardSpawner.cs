using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>Assets 2D des dangers. Champ vide = forme de couleur par défaut.</summary>
[Serializable]
public class HazardSprites
{
    public Sprite rock;
    public Sprite wet;
    public Sprite water;
    public Sprite lightning;
    [Tooltip("Signal affiché avant la chute d'une pierre.")]
    public Sprite warning;
}

/// <summary>
/// Fait apparaître les dangers selon la météo du tronçon courant :
/// - par tranche de paroi au-dessus du grimpeur : plaques mouillées, gourdes ;
/// - au fil du temps : pierres, éclairs, rafales (annoncées 1 s avant).
/// </summary>
public class HazardSpawner : MonoBehaviour
{
    public LaneClimber climber;
    public ClimbWeather weather;
    public HazardSprites sprites = new HazardSprites();

    [Tooltip("Hauteur d'une tranche de paroi (m).")]
    [SerializeField] private float sliceHeight = 2.5f;
    [Tooltip("On remplit la paroi jusqu'à cette distance au-dessus du grimpeur (m).")]
    [SerializeField] private float spawnAhead = 9f;
    [SerializeField] private float gustWarningTime = 1f;

    /// <summary>Rien n'apparaît au-dessus (le sommet).</summary>
    public float TopLimit { get; set; } = float.MaxValue;
    public bool Running { get; set; }

    /// <summary>Rafale annoncée : direction (-1/+1), 0 si aucune.</summary>
    public int PendingGustDirection { get; private set; }
    public float GustWarning { get; private set; }

    private readonly List<Hazard> hazards = new List<Hazard>();
    private float nextSliceY, rockTimer, lightningTimer, gustTimer;

    /// <summary>Efface tout (après une chute) et repart de la hauteur actuelle du grimpeur.</summary>
    public void ResetSpawns()
    {
        foreach (Hazard h in hazards)
            if (h != null) Destroy(h.gameObject);
        hazards.Clear();
        nextSliceY = climber.Height + 4f;
        rockTimer = lightningTimer = gustTimer = 2f;   // un peu de répit au départ
        PendingGustDirection = 0;
        GustWarning = 0f;
    }

    private void Update()
    {
        if (!Running || weather.Current == null) return;
        WeatherSection w = weather.Current;

        while (nextSliceY < climber.Height + spawnAhead && nextSliceY < TopLimit - 2f)
        {
            SpawnSlice(nextSliceY, w);
            nextSliceY += sliceHeight;
        }

        rockTimer = Tick(rockTimer, w.rockRate, () => Spawn(HazardType.Rock, RandomLane(), 0f));
        lightningTimer = Tick(lightningTimer, w.lightningRate, () => Spawn(HazardType.Lightning, RandomLane(), 0f));

        if (GustWarning > 0f)
        {
            GustWarning -= Time.deltaTime;
            if (GustWarning <= 0f)
            {
                climber.Push(PendingGustDirection);
                PendingGustDirection = 0;
            }
        }
        else
        {
            gustTimer = Tick(gustTimer, w.gustRate, () =>
            {
                PendingGustDirection = Random.value < 0.5f ? -1 : 1;
                GustWarning = gustWarningTime;
            });
        }

        hazards.RemoveAll(h => h == null);
    }

    private void SpawnSlice(float y, WeatherSection w)
    {
        int wetLane = -1;
        if (Random.value < w.wetChance)
        {
            wetLane = RandomLane();
            Spawn(HazardType.Wet, wetLane, y);
        }
        if (Random.value < w.waterChance)
        {
            int lane = RandomLane();
            if (lane == wetLane) lane = (lane + 1) % climber.laneCount;
            Spawn(HazardType.Water, lane, y + sliceHeight / 2f);
        }
    }

    // Décompte jusqu'au prochain événement ; rate = nombre moyen par seconde.
    private float Tick(float timer, float rate, Action spawn)
    {
        if (rate <= 0f) return 1f;
        timer -= Time.deltaTime;
        if (timer > 0f) return timer;
        spawn();
        return Random.Range(0.7f, 1.3f) / rate;
    }

    private int RandomLane() => Random.Range(0, climber.laneCount);

    private void Spawn(HazardType type, int lane, float y)
    {
        var go = new GameObject(type.ToString());
        go.transform.SetParent(transform, false);
        var hazard = go.AddComponent<Hazard>();
        hazard.Init(type, climber, lane, y, sprites);
        hazards.Add(hazard);
    }
}
