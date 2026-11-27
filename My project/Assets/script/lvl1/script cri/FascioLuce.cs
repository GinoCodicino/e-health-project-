using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class FascioLuceSfumato : MonoBehaviour
{
    public Color coloreLuce = new Color(1f, 0.9f, 0.4f, 0.35f);
    public float velocitaPulsazione = 1.5f;

    private Material mat;
    private float alphaBase;

    void Start()
    {
        // Crea una texture 1x64 pixel sfumata dall'alto (trasparente) al basso (luminoso)
        Texture2D gradiente = new Texture2D(1, 64, TextureFormat.RGBA32, false);
        gradiente.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < 64; y++)
        {
            float t = y / 63f; // 0 = base a terra, 1 = punta in alto
            // Sfuma leggermente alla base (primi 10%) e svanisce completamente verso l'alto
            float fadeBase = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.12f));
            float fadeAlto = Mathf.Pow(1f - t, 1.8f);
            float alpha = fadeBase * fadeAlto;

            gradiente.SetPixel(0, y, new Color(1f, 1f, 1f, alpha));
        }
        gradiente.Apply();

        mat = GetComponent<MeshRenderer>().material;
        mat.mainTexture = gradiente;
        alphaBase = coloreLuce.a;
    }

    void Update()
    {
        if (mat == null) return;
        // Leggera respirazione luminosa
        float pulse = 0.85f + Mathf.Sin(Time.time * velocitaPulsazione) * 0.15f;
        Color c = coloreLuce;
        c.a = alphaBase * pulse;
        mat.color = c;
    }
}