using UnityEngine;

/// <summary>
/// One part of the climb scenery (wall, backdrop, clouds, summit).
/// Plain class, edited in the ClimbScenery inspector. Any empty field is replaced by a simple colored shape.
/// </summary>
public abstract class SceneryLayer
{
    protected ClimbScenery Scenery { get; private set; }
    protected LaneClimber Climber => Scenery.Climber;

    public void Bind(ClimbScenery scenery) => Scenery = scenery;

    /// <summary>Built once; can be saved in the scene (ClimbScenery ⋮ menu → "Generate scenery in scene").</summary>
    public virtual void Build(Transform root, float summitHeight) { }

    /// <summary>Rebuilt at every startup, never saved in the scene.</summary>
    public virtual void BuildRuntime(Transform root, float summitHeight) { }

    /// <summary>Every frame in play mode.</summary>
    public virtual void Tick(float dt) { }

    protected static GameObject Pick(GameObject[] prefabs, System.Random random) =>
        prefabs != null && prefabs.Length > 0 ? prefabs[random.Next(prefabs.Length)] : null;
}
