using System;
using UnityEngine;

/// <summary>
/// Everything behind the wall: the starting ground, pine trees, distant mountains (saved in the scene)
/// and the large high peaks (rebuilt at every startup).
/// </summary>
[Serializable]
public class BackdropLayer : SceneryLayer
{
    public GameObject[] farMountains;
    public GameObject[] pines;
    public Material groundMaterial;
    [Tooltip("Background mountains start at this depth (m).")]
    [SerializeField] private float mountainsMinZ = 70f;
    [Tooltip("Material forced on the distant mountains (empty = the prefab's).")]
    public Material mountainMaterial;

    // How deep the high peaks' base is sunk below the ground (m).
    private const float PeakSink = 70f;

    public override void Build(Transform root, float summitHeight)
    {
        var random = new System.Random(7);

        var ground = ModelFactory.Create("Ground", root, null, PrimitiveType.Cube,
                                         new Color(0.35f, 0.6f, 0.3f), new Vector3(120f, 1f, 60f));
        ground.transform.position = new Vector3(0f, -1.1f, -10f);
        ModelFactory.SetMaterial(ground, groundMaterial);

        // Pine trees at the foot of the wall, on both sides.
        for (int i = 0; i < 14; i++)
        {
            float x = (i % 2 == 0 ? -1f : 1f) * (6f + 18f * (float)random.NextDouble());
            float z = -6f + 14f * (float)random.NextDouble();
            float h = 4f + 3f * (float)random.NextDouble();
            var pine = ModelFactory.Create("Pine", root, Pick(pines, random), PrimitiveType.Capsule,
                                           new Color(0.2f, 0.45f, 0.25f), new Vector3(h * 0.5f, h, h * 0.5f));
            pine.transform.position = new Vector3(x, -0.6f + h / 2f, z);
        }

        // Distant mountains behind the wall: visible on the sides.
        // These models are very deep: each is pushed back until its front face is at mountainsMinZ.
        for (int i = 0; i < 7; i++)
        {
            float x = -180f + 60f * i + 20f * (float)random.NextDouble();
            float h = 60f + 50f * (float)random.NextDouble();
            var mountain = ModelFactory.Create("Mountain", root, Pick(farMountains, random), PrimitiveType.Cube,
                                               new Color(0.6f, 0.65f, 0.72f), new Vector3(h * 1.6f, h, h));
            mountain.transform.rotation = Quaternion.Euler(0f, random.Next(360), 0f);
            mountain.transform.position = new Vector3(x, h / 2f - 12f, 0f);
            float front = ModelFactory.WorldBounds(mountain).min.z;
            mountain.transform.position += Vector3.forward * (mountainsMinZ - front + 40f * (float)random.NextDouble());
            ModelFactory.SetMaterial(mountain, mountainMaterial);
        }
    }

    // Large distant peaks, tall enough to stay visible on the sides up to the top of the route.
    // Placed near the end of the fog: they show up as pale, horizon-colored silhouettes.
    public override void BuildRuntime(Transform root, float summitHeight)
    {
        var random = new System.Random(11);
        float depth = Mathf.Max(Scenery.FogRange.y * 0.7f, 40f);
        for (int i = 0; i < 8; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            float x = side * (25f + 30f * (i / 2) + 10f * (float)random.NextDouble());
            float h = summitHeight * (1.2f + 0.6f * (float)random.NextDouble());
            var peak = ModelFactory.Create("Peak", root, Pick(farMountains, random), PrimitiveType.Cube,
                                           new Color(0.6f, 0.65f, 0.72f), new Vector3(h * 4f, h, h));  // generous width: the height sets the scale
            peak.transform.rotation = Quaternion.Euler(0f, random.Next(360), 0f);
            // Base sunk deep: the low-angle camera must never see the models' flat bottom.
            peak.transform.position = new Vector3(x, 0f, 0f);
            Bounds bounds = ModelFactory.WorldBounds(peak);
            peak.transform.position += new Vector3(0f, -PeakSink - bounds.min.y,
                                                   depth - bounds.min.z + 20f * (float)random.NextDouble());
            ModelFactory.SetMaterial(peak, mountainMaterial);
        }
    }
}
