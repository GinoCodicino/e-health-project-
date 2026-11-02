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
    public Sprite dropSprite;

    private Camera cam;
    private Transform[] drops;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        drops = new Transform[dropCount];
        for (int i = 0; i < dropCount; i++)
        {
            var sr = SpriteFactory.Create("Drop", transform, dropSprite, SpriteFactory.Square,
                                          new Color(0.8f, 0.9f, 1f, 0.55f), new Vector2(0.04f, 0.45f), 20);
            sr.transform.localRotation = Quaternion.FromToRotation(Vector3.down, direction);
            drops[i] = sr.transform;
            drops[i].localPosition = RandomPoint();
        }
    }

    private void LateUpdate()
    {
        bool raining = weather.Current != null && weather.Current.rain;
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
        Vector3 step = (Vector3)direction.normalized * speed * Time.deltaTime;

        foreach (Transform d in drops)
        {
            d.gameObject.SetActive(raining);
            if (!raining) continue;
            Vector3 p = d.localPosition + step;
            if (p.y < -halfH - 0.5f) p = new Vector3(Random.Range(-halfW, halfW + 3f), halfH + 0.5f, 5f);
            d.localPosition = p;
        }
    }

    private Vector3 RandomPoint()
    {
        float halfH = Camera.main != null ? Camera.main.orthographicSize : 6f;
        return new Vector3(Random.Range(-halfH * 2f, halfH * 2f), Random.Range(-halfH, halfH), 5f);
    }
}
