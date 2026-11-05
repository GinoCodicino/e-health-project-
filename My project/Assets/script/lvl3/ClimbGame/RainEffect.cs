using UnityEngine;

/// <summary>
/// Pluie à l'écran : des traits qui tombent en biais, enfants de la caméra.
/// Visible seulement quand le tronçon courant a de la pluie.
/// </summary>
[RequireComponent(typeof(Camera))]
public class RainEffect : MonoBehaviour
{
    [SerializeField] private ClimbWeather weather;
    [SerializeField] private int dropCount = 90;
    [SerializeField] private float speed = 14f;
    [SerializeField] private Vector2 direction = new Vector2(-0.25f, -1f);
    [Tooltip("Distance des gouttes devant la caméra (m) : elles passent devant la paroi.")]
    [SerializeField] private float depth = 5f;
    [SerializeField] private Vector2 dropSize = new Vector2(0.015f, 0.16f);
    public Sprite dropSprite;

    private Camera cam;
    private Transform[] drops;

    // Demi-hauteur visible à la distance des gouttes.
    private float HalfHeight => cam.orthographic ? cam.orthographicSize
                                                 : depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

    private void Awake()
    {
        cam = GetComponent<Camera>();
        drops = new Transform[dropCount];
        for (int i = 0; i < dropCount; i++)
        {
            var sr = SpriteFactory.Create("Drop", transform, dropSprite, SpriteFactory.Square,
                                          new Color(0.8f, 0.9f, 1f, 0.55f), dropSize, 20);
            sr.transform.localRotation = Quaternion.FromToRotation(Vector3.down, direction);
            drops[i] = sr.transform;
            drops[i].localPosition = RandomPoint();
        }
    }

    private void LateUpdate()
    {
        bool raining = weather.Current != null && weather.Current.rain;
        float halfH = HalfHeight, halfW = halfH * cam.aspect;
        Vector3 step = (Vector3)direction.normalized * (speed * halfH / 6f) * Time.deltaTime;

        foreach (Transform d in drops)
        {
            d.gameObject.SetActive(raining);
            if (!raining) continue;
            Vector3 p = d.localPosition + step;
            if (p.y < -halfH - 0.3f) p = new Vector3(Random.Range(-halfW, halfW + halfH * 0.5f), halfH + 0.3f, depth);
            d.localPosition = p;
        }
    }

    private Vector3 RandomPoint()
    {
        float halfH = HalfHeight;
        return new Vector3(Random.Range(-halfH * 2f, halfH * 2f), Random.Range(-halfH, halfH), depth);
    }
}
