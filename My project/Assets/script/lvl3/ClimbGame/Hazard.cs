using UnityEngine;

public enum HazardType { Rock, Wet, Lightning, Water }

/// <summary>
/// Un danger (ou bonus) dans un couloir. Créé par HazardSpawner.
/// Pierre et éclair sont annoncés avant d'arriver ; plaque mouillée et gourde sont posées sur la paroi.
/// </summary>
public class Hazard : MonoBehaviour
{
    const float RockWarnTime = 1f;
    const float RockSpeed = 9f;
    const float LightningWarnTime = 1.2f;
    const float LightningStrikeTime = 0.25f;
    const float WaterAmount = 30f;

    public HazardType Type { get; private set; }

    private LaneClimber climber;
    private Camera cam;
    private HazardSprites sprites;
    private int lane;
    private float timer;
    private bool armed;            // pierre : tombe ; éclair : a frappé
    private SpriteRenderer visual, warning;

    public void Init(HazardType type, LaneClimber target, int laneIndex, float y, HazardSprites spriteSet)
    {
        Type = type;
        climber = target;
        lane = laneIndex;
        sprites = spriteSet;
        cam = Camera.main;
        float x = climber.LaneX(lane);
        float w = climber.laneWidth;
        transform.position = new Vector3(x, y, 0f);

        switch (type)
        {
            case HazardType.Rock:
                warning = SpriteFactory.Create("Warning", transform, sprites.warning, SpriteFactory.Circle, new Color(1f, 0.25f, 0.2f), new Vector2(0.5f, 0.5f), 9);
                break;
            case HazardType.Wet:
                visual = SpriteFactory.Create("Wet", transform, sprites.wet, SpriteFactory.Square, new Color(0.6f, 0.85f, 1f, 0.7f), new Vector2(w * 0.85f, 1.2f), -1);
                break;
            case HazardType.Water:
                visual = SpriteFactory.Create("Water", transform, sprites.water, SpriteFactory.Circle, new Color(0.2f, 0.6f, 1f), new Vector2(0.5f, 0.5f), 4);
                break;
            case HazardType.Lightning:
                warning = SpriteFactory.Create("LaneFlash", transform, null, SpriteFactory.Square, new Color(1f, 0.95f, 0.3f, 0.25f), new Vector2(w, CamHeight + 2f), 7);
                break;
        }
    }

    private float CamHeight => cam.orthographicSize * 2f;
    private float CamTop => cam.transform.position.y + cam.orthographicSize;
    private float CamBottom => cam.transform.position.y - cam.orthographicSize;

    private void Update()
    {
        timer += Time.deltaTime;
        switch (Type)
        {
            case HazardType.Rock:      UpdateRock(); break;
            case HazardType.Lightning: UpdateLightning(); break;
            case HazardType.Wet:
            case HazardType.Water:     UpdateStatic(); break;
        }
    }

    private void UpdateRock()
    {
        if (!armed)
        {
            // Signal en haut de l'écran, puis la pierre tombe.
            transform.position = new Vector3(transform.position.x, CamTop - 0.6f, 0f);
            warning.enabled = Mathf.Repeat(timer * 6f, 1f) > 0.3f;
            if (timer < RockWarnTime) return;
            armed = true;
            Destroy(warning.gameObject);
            visual = SpriteFactory.Create("Rock", transform, sprites.rock, SpriteFactory.Circle, new Color(0.45f, 0.4f, 0.35f), new Vector2(0.7f, 0.7f), 6);
            transform.position += Vector3.up * 1.2f;
        }

        transform.position += Vector3.down * RockSpeed * Time.deltaTime;
        if (Touches()) { climber.Hit(); Destroy(gameObject); return; }
        if (transform.position.y < CamBottom - 1f) Destroy(gameObject);
    }

    private void UpdateLightning()
    {
        transform.position = new Vector3(transform.position.x, cam.transform.position.y, 0f);
        if (!armed)
        {
            warning.enabled = Mathf.Repeat(timer * 5f, 1f) > 0.4f;
            if (timer < LightningWarnTime) return;
            armed = true;
            timer = 0f;
            warning.enabled = false;
            visual = SpriteFactory.Create("Bolt", transform, sprites.lightning, SpriteFactory.Square, new Color(1f, 1f, 0.75f), new Vector2(0.35f, CamHeight + 2f), 8);
            if (climber.Active && climber.Lane == lane) climber.Hit();
        }
        else if (timer > LightningStrikeTime) Destroy(gameObject);
    }

    private void UpdateStatic()
    {
        if (Touches())
        {
            if (Type == HazardType.Water) { climber.Refill(WaterAmount); Destroy(gameObject); return; }
            if (!armed) { armed = true; climber.Slip(); }
        }
        if (transform.position.y < CamBottom - 2f) Destroy(gameObject);
    }

    private bool Touches() => climber.Active && visual != null && visual.bounds.Intersects(climber.Bounds);
}
