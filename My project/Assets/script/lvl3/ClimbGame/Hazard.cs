using UnityEngine;

public enum HazardType { Rock, Wet, Lightning, Water }

/// <summary>
/// Un danger (ou bonus) dans un couloir. Créé par HazardSpawner.
/// Pierre et éclair sont annoncés avant d'arriver ; plaque mouillée et gourde sont posées sur la paroi.
/// Pierre, gourde, plaque glissante et signal sont des modèles 3D ; zone d'éclair et éclair restent plats (effets transparents).
/// </summary>
public class Hazard : MonoBehaviour
{
    const float RockWarnTime = 1f;
    const float RockSpeed = 9f;
    const float RockSpin = 220f;
    const float LightningWarnTime = 1.2f;
    const float LightningStrikeTime = 0.25f;
    const float WaterAmount = 30f;

    public HazardType Type { get; private set; }

    private LaneClimber climber;
    private ClimbCamera view;
    private HazardVisuals visuals;
    private int lane;
    private float timer;
    private bool armed;            // pierre : tombe ; éclair : a frappé
    private GameObject visual, warning;
    private Vector3 spinAxis;

    public void Init(HazardType type, LaneClimber target, ClimbCamera camera, int laneIndex, float y, HazardVisuals set)
    {
        Type = type;
        climber = target;
        view = camera;
        lane = laneIndex;
        visuals = set;
        float x = climber.LaneX(lane);
        float w = climber.laneWidth;
        transform.position = new Vector3(x, y, 0f);

        switch (type)
        {
            case HazardType.Rock:
                warning = ModelFactory.Create("Warning", transform, visuals.warning, PrimitiveType.Sphere,
                                              new Color(1f, 0.25f, 0.2f), Vector3.one * 0.45f);
                break;
            case HazardType.Wet:
                // Sans modèle : un bloc plat bleu-gris. On le recule jusqu'à ce que son arrière touche la paroi.
                visual = ModelFactory.Create("Wet", transform, visuals.wet, PrimitiveType.Cube,
                                             new Color(0.6f, 0.75f, 0.85f), new Vector3(w * 0.85f, 1.2f, 0.1f));
                float back = ModelFactory.WorldBounds(visual).max.z;
                visual.transform.position += Vector3.forward * (ClimbScenery.WallZ - back);
                break;
            case HazardType.Water:
                visual = ModelFactory.Create("Water", transform, visuals.water, PrimitiveType.Cylinder,
                                             new Color(0.2f, 0.6f, 1f), new Vector3(0.35f, 0.25f, 0.35f));
                break;
            case HazardType.Lightning:
                warning = SpriteFactory.Create("LaneFlash", transform, null, SpriteFactory.Square,
                                               new Color(1f, 0.95f, 0.3f, 0.25f), new Vector2(w, view.HalfHeight * 2f + 2f), 7).gameObject;
                warning.transform.localPosition = new Vector3(0f, 0f, ClimbScenery.WallZ - 0.03f);
                break;
        }
    }

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
            transform.position = new Vector3(transform.position.x, view.Top - 0.6f, 0f);
            ModelFactory.SetVisible(warning, Mathf.Repeat(timer * 6f, 1f) > 0.3f);
            if (timer < RockWarnTime) return;
            armed = true;
            Destroy(warning);
            visual = ModelFactory.Create("Rock", transform, PickRock(), PrimitiveType.Sphere,
                                         new Color(0.45f, 0.4f, 0.35f), Vector3.one * 0.7f);
            spinAxis = Random.onUnitSphere;
            transform.position += Vector3.up * 1.2f;
        }

        transform.position += Vector3.down * RockSpeed * Time.deltaTime;
        visual.transform.Rotate(spinAxis, RockSpin * Time.deltaTime, Space.World);
        if (Touches()) { climber.Hit(); Destroy(gameObject); return; }
        if (transform.position.y < view.Bottom - 1f) Destroy(gameObject);
    }

    private void UpdateLightning()
    {
        transform.position = new Vector3(transform.position.x, view.FocusY, 0f);
        if (!armed)
        {
            warning.SetActive(Mathf.Repeat(timer * 5f, 1f) > 0.4f);
            if (timer < LightningWarnTime) return;
            armed = true;
            timer = 0f;
            warning.SetActive(false);
            visual = CreateBolt();
            if (climber.Active && climber.Lane == lane) climber.Hit();
        }
        else if (timer > LightningStrikeTime) Destroy(gameObject);
    }

    private void UpdateStatic()
    {
        // La gourde tourne sur elle-même pour attirer l'œil.
        if (Type == HazardType.Water) visual.transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);

        if (Touches())
        {
            if (Type == HazardType.Water) { climber.Refill(WaterAmount); Destroy(gameObject); return; }
            if (!armed) { armed = true; climber.Slip(); }
        }
        if (transform.position.y < view.Bottom - 2f) Destroy(gameObject);
    }

    // Éclair en zigzag sur toute la hauteur de l'écran, dans le couloir.
    private GameObject CreateBolt()
    {
        var go = new GameObject("Bolt");
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        // Matériau des sprites : transparent et sans éclairage, disponible même dans un build.
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
        return go;
    }

    private GameObject PickRock() =>
        visuals.rocks != null && visuals.rocks.Length > 0 ? visuals.rocks[Random.Range(0, visuals.rocks.Length)] : null;

    private bool Touches() =>
        climber.Active && visual != null && ModelFactory.OverlapsXY(ModelFactory.WorldBounds(visual), climber.Bounds);
}
