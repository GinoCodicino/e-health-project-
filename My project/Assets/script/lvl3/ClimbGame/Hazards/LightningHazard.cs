using UnityEngine;

/// <summary>
/// Lightning: the lane flashes, then a bolt strikes the whole lane.
/// The zone and bolt stay flat (transparent effects). Hits by lane, not by overlap.
/// </summary>
public class LightningHazard : TelegraphedHazard
{
    const float StrikeTime = 0.25f;

    protected override float WarnTime => 1.2f;
    protected override float BlinkRate => 5f;
    protected override float BlinkOff => 0.4f;

    protected override void Build()
    {
        warning = SpriteFactory.Create("LaneFlash", transform, null, SpriteFactory.Square,
                                       new Color(1f, 0.95f, 0.3f, 0.25f),
                                       new Vector2(climber.laneWidth, view.HalfHeight * 2f + 2f), 7).gameObject;
        warning.transform.localPosition = new Vector3(0f, 0f, ClimbScenery.WallZ - 0.03f);
    }

    // The flash and the bolt cover the whole screen height: they follow the camera.
    protected override void TickWarning() => FollowView();

    protected override void Strike()
    {
        CreateBolt();
        if (climber.Active && climber.Lane == lane) climber.Hit();
    }

    protected override void TickStrike()
    {
        FollowView();
        if (timer > StrikeTime) Destroy(gameObject);
    }

    private void FollowView() =>
        transform.position = new Vector3(transform.position.x, view.FocusY, 0f);

    // Zigzag lightning bolt over the full screen height, in the lane.
    // Not stored in "visual": the bolt hits by lane, not by overlap.
    private void CreateBolt()
    {
        var go = new GameObject("Bolt");
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        // Sprite material: transparent and unlit, available even in a build.
        line.sharedMaterial = SpriteFactory.DefaultMaterial;
        line.startColor = line.endColor = new Color(1f, 1f, 0.75f);
        line.widthMultiplier = 0.12f;
        line.useWorldSpace = false;

        const int points = 12;
        float half = view.HalfHeight + 1f;
        float zigzag = climber.laneWidth * 0.3f;
        line.positionCount = points;
        for (int i = 0; i < points; i++)
        {
            float t = i / (points - 1f);
            float x = i == 0 || i == points - 1 ? 0f : Random.Range(-zigzag, zigzag);
            line.SetPosition(i, new Vector3(x, Mathf.Lerp(half, -half, t), -0.3f));
        }
    }
}
