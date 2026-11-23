using UnityEngine;

/// <summary>
/// Draws the Farland Skies sun disc where the directional light actually is.
/// The skybox shader reads the sun direction from a matrix that is not saved in the material,
/// so it has to be sent from a script.
/// </summary>
[ExecuteAlways]
public class SkySunAligner : MonoBehaviour
{
    [Tooltip("Directional light used as the sun. Empty = RenderSettings.sun.")]
    [SerializeField] private Light sun;

    private static readonly int SunMatrixId = Shader.PropertyToID("sunMatrix");

    private void LateUpdate()
    {
        Light light = sun != null ? sun : RenderSettings.sun;
        Material sky = RenderSettings.skybox;
        if (light == null || sky == null) return;

        // Rotation only: the sky is infinitely far, the light's position must not shift the disc.
        sky.SetMatrix(SunMatrixId, Matrix4x4.Rotate(Quaternion.Inverse(light.transform.rotation)));
    }
}
