using System;
using UnityEngine;

/// <summary>The summit ledge at the top of the wall, with its flag.</summary>
[Serializable]
public class SummitLayer : SceneryLayer
{
    public Material flagPoleMaterial;
    public Material flagClothMaterial;

    public override void Build(Transform root, float summitHeight)
    {
        float wallZ = ClimbScenery.WallZ;
        float width = Climber.laneCount * Climber.laneWidth + 2f;
        var ledge = ModelFactory.Create("Summit", root, null, PrimitiveType.Cube,
                                        new Color(0.92f, 0.94f, 0.97f), new Vector3(width, 0.8f, 3f));
        ledge.transform.position = new Vector3(0f, summitHeight + 0.4f, wallZ);

        var pole = ModelFactory.Create("FlagPole", root, null, PrimitiveType.Cylinder,
                                       new Color(0.4f, 0.3f, 0.2f), new Vector3(0.08f, 1.4f, 0.08f));
        pole.transform.position = new Vector3(width / 2f - 1f, summitHeight + 2.2f, wallZ);
        ModelFactory.SetMaterial(pole, flagPoleMaterial);

        var cloth = ModelFactory.Create("FlagCloth", root, null, PrimitiveType.Cube,
                                        new Color(0.9f, 0.2f, 0.2f), new Vector3(0.9f, 0.55f, 0.04f));
        cloth.transform.position = new Vector3(width / 2f - 1f - 0.48f, summitHeight + 3.2f, wallZ);
        ModelFactory.SetMaterial(cloth, flagClothMaterial);
    }
}
