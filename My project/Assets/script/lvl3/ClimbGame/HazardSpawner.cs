using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>Hazard visuals. Empty field = default colored shape.</summary>
[Serializable]
public class HazardVisuals
{
    [Tooltip("Falling 3D rocks (a random one each time).")]
    public GameObject[] rocks;
    [Tooltip("3D water bottle that restores stamina.")]
    public GameObject water;
    [Tooltip("3D warning shown before a rock falls.")]
    public GameObject warning;
    [Tooltip("3D slippery patch (e.g. snow, ice), stuck to the wall.")]
    public GameObject wet;
}

/// <summary>
/// Spawns hazards based on the current section's weather:
/// - per wall slice above the climber: wet patches, water bottles;
/// - over time: rocks, lightning, gusts (announced 1 s ahead).
/// </summary>
public class HazardSpawner : MonoBehaviour
{
    public LaneClimber climber;
    public ClimbWeather weather;
    public ClimbCamera view;
    public HazardVisuals visuals = new HazardVisuals();

    [Tooltip("Height of a wall slice (m).")]
    [SerializeField] private float sliceHeight = 2.5f;
    [Tooltip("The wall is filled up to this distance above the climber (m).")]
    [SerializeField] private float spawnAhead = 9f;

    /// <summary>Nothing spawns above this (the summit).</summary>
    public float TopLimit { get; set; } = float.MaxValue;
    public bool Running { get; set; }

    /// <summary>The gust in progress (only one at a time), null if none.</summary>
    public GustHazard ActiveGust { get; private set; }

    private readonly List<Hazard> hazards = new List<Hazard>();
    private float nextSliceY, rockTimer, lightningTimer, gustTimer;

    /// <summary>Clears everything (after a fall) and restarts from the climber's current height.</summary>
    public void ResetSpawns()
    {
        foreach (Hazard h in hazards)
            if (h != null) Destroy(h.gameObject);
        hazards.Clear();
        nextSliceY = climber.Height + 4f;
        rockTimer = lightningTimer = gustTimer = 2f;   // a short breather at the start
        ActiveGust = null;
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

        rockTimer = Tick(rockTimer, w.rockRate, () => Spawn<RockHazard>(RandomLane(), 0f));
        lightningTimer = Tick(lightningTimer, w.lightningRate, () => Spawn<LightningHazard>(RandomLane(), 0f));

        // The next gust is only counted down once the previous one has blown.
        if (ActiveGust == null)
            gustTimer = Tick(gustTimer, w.gustRate, () => ActiveGust = Spawn<GustHazard>(climber.Lane, 0f));

        hazards.RemoveAll(h => h == null);
    }

    private void SpawnSlice(float y, WeatherSection w)
    {
        int wetLane = -1;
        if (Random.value < w.wetChance)
        {
            wetLane = RandomLane();
            Spawn<WetPatch>(wetLane, y);
        }
        if (Random.value < w.waterChance)
        {
            int lane = RandomLane();
            if (lane == wetLane) lane = (lane + 1) % climber.laneCount;
            Spawn<WaterBottle>(lane, y + sliceHeight / 2f);
        }
    }

    // Countdown to the next event; rate = average count per second.
    private float Tick(float timer, float rate, Action spawn)
    {
        if (rate <= 0f) return 1f;
        timer -= Time.deltaTime;
        if (timer > 0f) return timer;
        spawn();
        return Random.Range(0.7f, 1.3f) / rate;
    }

    private int RandomLane() => Random.Range(0, climber.laneCount);

    private T Spawn<T>(int lane, float y) where T : Hazard
    {
        var go = new GameObject(typeof(T).Name);
        go.transform.SetParent(transform, false);
        var hazard = go.AddComponent<T>();
        hazard.Init(climber, view, lane, y, visuals);
        hazards.Add(hazard);
        return hazard;
    }
}
