using System;
using UnityEngine;

/// <summary>The lanes' rock wall, framed by cliffs on each side, with small rocks for relief.</summary>
[Serializable]
public class WallLayer : SceneryLayer
{
    [Tooltip("Lane materials, alternating (e.g. MountainRidge_Rock / MountainRidge_RockLight).")]
    public Material[] laneMaterials;
    [Tooltip("Cliffs placed on each side of the lanes (e.g. Cliff_1 to Cliff_8).")]
    public GameObject[] sideCliffs;
    [Tooltip("Material forced on the cliffs (empty = the prefab's).")]
    public Material cliffMaterial;
    [SerializeField] private float cliffSpacing = 4.5f;
    [SerializeField] private float cliffSize = 6f;
    [Tooltip("Small rocks embedded in the wall to give it relief (e.g. Rock_Small_1 to 5).")]
    public GameObject[] wallDetails;
    public Material wallDetailMaterial;
    [SerializeField] private float detailsPerMeter = 1.2f;

    public override void Build(Transform root, float summitHeight)
    {
        float bottom = -4f, top = summitHeight + 6f;
        float height = top - bottom;
        float wallZ = ClimbScenery.WallZ;

        // One strip of rock per lane.
        for (int i = 0; i < Climber.laneCount; i++)
        {
            var lane = ModelFactory.Create($"Lane{i}", root, null, PrimitiveType.Cube,
                                           i % 2 == 0 ? new Color(0.5f, 0.47f, 0.44f) : new Color(0.56f, 0.53f, 0.5f),
                                           new Vector3(Climber.laneWidth, height, 1f));
            lane.transform.position = new Vector3(Climber.LaneX(i), bottom + height / 2f, wallZ + 0.5f);
            if (laneMaterials != null && laneMaterials.Length > 0)
                ModelFactory.SetMaterial(lane, laneMaterials[i % laneMaterials.Length]);
        }

        // Staggered cliffs on each side, to frame the lanes.
        float halfWidth = Climber.laneCount * Climber.laneWidth / 2f;
        var random = new System.Random(3);
        for (float y = bottom; y < top; y += cliffSpacing)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject prefab = Pick(sideCliffs, random);
                float size = cliffSize * (0.85f + 0.3f * (float)random.NextDouble());
                var cliff = ModelFactory.Create("Cliff", root, prefab, PrimitiveType.Cube,
                                                new Color(0.45f, 0.42f, 0.4f), new Vector3(size, size, size));
                cliff.transform.position = new Vector3(side * (halfWidth + size * 0.5f),
                                                       y + (side > 0 ? cliffSpacing / 2f : 0f), wallZ + size * 0.4f);
                // Flipped: their flat face (meant to sit on the ground) points up,
                // where the low-angle camera can't see it.
                cliff.transform.rotation = Quaternion.Euler(180f, random.Next(4) * 90f, 0f);
                ModelFactory.SetMaterial(cliff, cliffMaterial);
            }
        }

        // Small rocks half sunk into the lanes' face.
        int details = Mathf.RoundToInt(height * detailsPerMeter);
        for (int i = 0; i < details; i++)
        {
            float size = 0.15f + 0.25f * (float)random.NextDouble();
            var rock = ModelFactory.Create("WallDetail", root, Pick(wallDetails, random), PrimitiveType.Cube,
                                           new Color(0.4f, 0.37f, 0.35f), Vector3.one * size);
            float x = ((float)random.NextDouble() * 2f - 1f) * (halfWidth - 0.15f);
            rock.transform.position = new Vector3(x, bottom + height * (float)random.NextDouble(), wallZ);
            rock.transform.rotation = Quaternion.Euler(random.Next(360), random.Next(360), random.Next(360));
            ModelFactory.SetMaterial(rock, wallDetailMaterial);
        }
    }
}
