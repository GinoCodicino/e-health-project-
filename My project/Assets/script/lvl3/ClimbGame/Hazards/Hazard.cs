using UnityEngine;

/// <summary>
/// Base class for everything that appears in a lane (hazards and bonuses). Created by HazardSpawner.
/// Each subclass builds its own visuals and decides what happens when it touches the climber.
/// </summary>
public abstract class Hazard : MonoBehaviour
{
    protected LaneClimber climber;
    protected ClimbCamera view;
    protected HazardVisuals visuals;
    protected int lane;
    /// <summary>Time since the hazard appeared (subclasses may reset it between phases).</summary>
    protected float timer;
    /// <summary>The part that collides with the climber. Null = cannot touch him.</summary>
    protected GameObject visual;

    /// <summary>Destroyed once this far below the bottom of the screen (m).</summary>
    protected virtual float DespawnMargin => 2f;

    public void Init(LaneClimber target, ClimbCamera camera, int laneIndex, float y, HazardVisuals set)
    {
        climber = target;
        view = camera;
        lane = laneIndex;
        visuals = set;
        transform.position = new Vector3(climber.LaneX(lane), y, 0f);
        Build();
    }

    /// <summary>Creates the visuals, once the hazard is placed.</summary>
    protected abstract void Build();

    /// <summary>Per-frame behaviour (movement, warning, animation).</summary>
    protected virtual void Tick() { }

    /// <summary>Called every frame while the visual overlaps the climber.</summary>
    protected virtual void OnTouch() { }

    private void Update()
    {
        timer += Time.deltaTime;
        Tick();
        if (Touches()) OnTouch();
        if (transform.position.y < view.Bottom - DespawnMargin) Destroy(gameObject);
    }

    private bool Touches() =>
        climber.Active && visual != null && ModelFactory.OverlapsXY(ModelFactory.WorldBounds(visual), climber.Bounds);
}
