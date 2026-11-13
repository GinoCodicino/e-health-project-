using UnityEngine;

public class VillageThemeApplier : MonoBehaviour
{
    [Header("Riferimenti Scena")]
    public Light sunLight;
    public GameObject groundObject; // Il tuo prato/terreno
    public GameObject[] existingHouses; // Trascina qui le case che hai già in scena

    [Header("Colori Prato e Luce")]
    public Color grassColor = new Color(0.42f, 0.72f, 0.14f); // Verde prato acceso
    public Color skyAmbientColor = new Color(0.55f, 0.68f, 0.75f); // Luce ombra azzurrina
    public Color sunColor = new Color(1.0f, 0.96f, 0.86f);

    // Palette per le case: { Muri, Tetto, Bordi/Legno }
    private readonly Color[,] housePalettes = new Color[,]
    {
        { new Color(0.78f, 0.18f, 0.16f), new Color(0.12f, 0.45f, 0.48f), new Color(0.85f, 0.65f, 0.35f) }, // Muri Rossi, Tetto Ottanio
        { new Color(0.18f, 0.42f, 0.78f), new Color(0.24f, 0.46f, 0.52f), new Color(0.92f, 0.82f, 0.60f) }, // Muri Blu, Tetto Blu-Verde
        { new Color(0.22f, 0.55f, 0.24f), new Color(0.75f, 0.32f, 0.15f), new Color(0.88f, 0.68f, 0.38f) }, // Muri Verdi, Tetto Terracotta
        { new Color(0.88f, 0.68f, 0.20f), new Color(0.18f, 0.48f, 0.28f), new Color(0.95f, 0.90f, 0.78f) }  // Muri Gialli, Tetto Verde Bosco
    };

    void Start()
    {
        ApplyVibrantTheme();
    }

    // Permette di eseguirlo anche nell'Editor facendo click destro sul componente
    [ContextMenu("Applica Colori Villaggio")]
    public void ApplyVibrantTheme()
    {
        SetupLighting();
        ColorGround();
        ColorExistingHouses();
    }

    void SetupLighting()
    {
        if (sunLight != null)
        {
            sunLight.color = sunColor;
            sunLight.intensity = 1.35f;
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = skyAmbientColor;
    }

    void ColorGround()
    {
        if (groundObject == null) return;

        MeshRenderer mr = groundObject.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sharedMaterial = CreateMatteMaterial(grassColor);
        }
    }

    void ColorExistingHouses()
    {
        if (existingHouses == null) return;

        for (int i = 0; i < existingHouses.Length; i++)
        {
            if (existingHouses[i] == null) continue;

            int p = i % housePalettes.GetLength(0);
            Color wallCol = housePalettes[p, 0];
            Color roofCol = housePalettes[p, 1];
            Color trimCol = housePalettes[p, 2];

            MeshRenderer[] renderers = existingHouses[i].GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers)
            {
                Material[] mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] == null) continue;
                    string matName = mats[m].name.ToLower();

                    // Assegna i colori in base al nome del materiale o all'indice
                    if (matName.Contains("roof") || matName.Contains("tetto") || m == 1)
                        mats[m] = CreateMatteMaterial(roofCol);
                    else if (matName.Contains("trim") || matName.Contains("wood") || matName.Contains("bord") || m == 2)
                        mats[m] = CreateMatteMaterial(trimCol);
                    else
                        mats[m] = CreateMatteMaterial(wallCol);
                }
                r.sharedMaterials = mats;
            }
        }
    }

    public static Material CreateMatteMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.05f);
        return mat;
    }
}