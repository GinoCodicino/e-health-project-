using UnityEngine;

public class HousePaletteRecolorer : MonoBehaviour
{
    public enum HouseTheme
    {
        Casuale,
        MuriRossi_TettoOttanio,
        MuriBlu_TettoAzzurro,
        MuriRossi_TettoBluScuro,
        MuriVerdi_TettoTerracotta,
        MuriTerracotta_TettoVerdeScuro,
        MuriOttanio_TettoVerdePrato
    }

    [Header("Scelta Colori Casetta")]
    public HouseTheme temaColori = HouseTheme.Casuale;

    [Header("Regolazione Stacco Tetto / Muri / Bordi")]
    [Range(0.25f, 0.45f)] public float sogliaTetto = 0.36f;
    [Range(0.48f, 0.70f)] public float sogliaBordi = 0.56f;
    [Range(0f, 1f)] public float opacitaMateriale = 0.05f;

    [HideInInspector] public Material[] originalMaterials;

    void Start()
    {
        ApplyRecolor();
    }

    [ContextMenu("Ricolora Casetta Ora")]
    public void ApplyRecolor()
    {
        GetThemeColors(temaColori, out Color wallColor, out Color roofColor, out Color trimColor);

        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer mr in renderers)
        {
            if (originalMaterials == null || originalMaterials.Length != mr.sharedMaterials.Length)
            {
                originalMaterials = mr.sharedMaterials;
            }

            Material[] mats = new Material[originalMaterials.Length];

            for (int i = 0; i < originalMaterials.Length; i++)
            {
                if (originalMaterials[i] == null) continue;

                Material newMat = new Material(originalMaterials[i]);
                newMat.name = originalMaterials[i].name + "_Recolored";

                string texProp = newMat.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                Texture originalTex = newMat.GetTexture(texProp);

                if (originalTex != null)
                {
                    Texture2D recoloredTex = RecolorHouseTexture(originalTex, wallColor, roofColor, trimColor);
                    newMat.SetTexture(texProp, recoloredTex);
                }
                else
                {
                    newMat.color = wallColor;
                    if (newMat.HasProperty("_BaseColor")) newMat.SetColor("_BaseColor", wallColor);
                }

                if (newMat.HasProperty("_Smoothness")) newMat.SetFloat("_Smoothness", opacitaMateriale);
                if (newMat.HasProperty("_Glossiness")) newMat.SetFloat("_Glossiness", opacitaMateriale);

                mats[i] = newMat;
            }

            mr.sharedMaterials = mats;
        }
    }

    private void GetThemeColors(HouseTheme theme, out Color wall, out Color roof, out Color trim)
    {
        if (theme == HouseTheme.Casuale)
        {
            theme = (HouseTheme)Random.Range(1, 7);
        }

        // Legno caldo miele/quercia uguale per tutte le case (evita bordi grigi)
        trim = new Color(0.82f, 0.56f, 0.24f);

        switch (theme)
        {
            case HouseTheme.MuriRossi_TettoOttanio:
                wall = new Color(0.84f, 0.16f, 0.14f); // Rosso vivo
                roof = new Color(0.08f, 0.44f, 0.48f); // Verde ottanio scuro
                break;

            case HouseTheme.MuriBlu_TettoAzzurro:
                wall = new Color(0.16f, 0.44f, 0.84f); // Blu intenso
                roof = new Color(0.20f, 0.54f, 0.58f); // Azzurro-ottanio
                break;

            case HouseTheme.MuriRossi_TettoBluScuro:
                wall = new Color(0.78f, 0.16f, 0.16f); // Rosso cremisi
                roof = new Color(0.16f, 0.34f, 0.56f); // Blu ardesia
                break;

            case HouseTheme.MuriVerdi_TettoTerracotta:
                wall = new Color(0.22f, 0.52f, 0.20f); // Verde bosco
                roof = new Color(0.78f, 0.34f, 0.14f); // Terracotta caldo
                break;

            case HouseTheme.MuriTerracotta_TettoVerdeScuro:
                wall = new Color(0.74f, 0.24f, 0.14f); // Rosso mattone
                roof = new Color(0.10f, 0.38f, 0.22f); // Verde pino scuro
                break;

            default: // MuriOttanio_TettoVerdePrato
                wall = new Color(0.12f, 0.44f, 0.38f); // Verde ottanio
                roof = new Color(0.28f, 0.62f, 0.18f); // Verde brillante
                break;
        }
    }

    private Texture2D RecolorHouseTexture(Texture source, Color wallColor, Color roofColor, Color trimColor)
    {
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
        Graphics.Blit(source, rt);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D tex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.filterMode = FilterMode.Bilinear;

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        Color[] pixels = tex.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color p = pixels[i];
            Color.RGBToHSV(p, out _, out _, out float v);

            // 1. TETTO (parti più scure della texture originale)
            if (v < sogliaTetto)
            {
                float localV = Mathf.InverseLerp(0.10f, sogliaTetto, v);
                float grain = Mathf.Lerp(0.75f, 1.25f, localV);
                pixels[i] = roofColor * grain;
            }
            // 2. MURI (parti intermedie)
            else if (v < sogliaBordi)
            {
                float localV = Mathf.InverseLerp(sogliaTetto, sogliaBordi, v);
                float grain = Mathf.Lerp(0.82f, 1.20f, localV);
                pixels[i] = wallColor * grain;
            }
            // 3. CORNICI, TRAVI E BASAMENTO (parti chiare)
            else
            {
                float localV = Mathf.InverseLerp(sogliaBordi, 0.85f, v);
                float grain = Mathf.Lerp(0.85f, 1.15f, localV);
                pixels[i] = trimColor * grain;
            }

            pixels[i].a = p.a;
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}