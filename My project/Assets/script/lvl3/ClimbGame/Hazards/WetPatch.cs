using UnityEngine;

/// <summary>Slippery patch (snow, ice...) stuck to the wall: the climber slides down once.</summary>
public class WetPatch : Hazard
{
    private bool slipped;

    protected override void Build()
    {
        // Without a model: a flat blue-gray block. Pushed back until its back touches the wall.
        visual = ModelFactory.Create("Wet", transform, visuals.wet, PrimitiveType.Cube,
                                     new Color(0.6f, 0.75f, 0.85f), new Vector3(climber.laneWidth * 0.85f, 1.2f, 0.1f));
        float back = ModelFactory.WorldBounds(visual).max.z;
        visual.transform.position += Vector3.forward * (ClimbScenery.WallZ - back);
    }

    protected override void OnTouch()
    {
        if (slipped) return;
        slipped = true;
        climber.Slip();
    }
}
