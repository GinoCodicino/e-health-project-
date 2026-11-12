using UnityEngine;

[ExecuteInEditMode]
public class RedHouseDecorator : MonoBehaviour
{
    [Header("Orientamento e Scala")]
    [Tooltip("Attiva se il fronte della casa risulta invertito")]
    public bool invertiFronte = true;

    [Tooltip("Larghezza reale del fronte della casa")]
    public float houseWidth = 3.5f;
    [Tooltip("Profondità reale della casa")]
    public float houseDepth = 3.0f;
    [Tooltip("Altezza della parete prima dell'inizio del triangolo del tetto")]
    public float wallHeight = 2.4f;

    [Header("Parametri Tetto Principale")]
    [Tooltip("Angolo di inclinazione del tetto")]
    public float roofPitchAngle = 26f;
    [Tooltip("Spessore dei pannelli del tetto")]
    public float roofThickness = 0.15f;
    [Tooltip("Sporgenza laterale e frontale del tetto")]
    public float roofOverhang = 0.25f;

    [Header("Colori Materiali")]
    public Color mainRoofColor = new Color(0.24f, 0.24f, 0.26f);
    public Color woodColor = new Color(0.42f, 0.26f, 0.15f);
    public Color porchRoofColor = new Color(0.35f, 0.22f, 0.14f);
    public Color stoneColor = new Color(0.55f, 0.55f, 0.58f);
    public Color fenceColor = new Color(0.5f, 0.35f, 0.2f);
    public Color trimColor = new Color(0.85f, 0.85f, 0.85f);
    public Color foliageColor = new Color(0.2f, 0.55f, 0.2f);
    public Color flowerColor = new Color(0.95f, 0.3f, 0.3f);

    [ContextMenu("Genera Decorazioni")]
    public void Generate()
    {
        ClearDecorations();

        GameObject container = new GameObject("House_Decorations");
        container.transform.SetParent(transform);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;

        if (invertiFronte)
        {
            container.transform.localRotation = Quaternion.Euler(0, 180f, 0);
        }

        BuildMainRoof(container.transform);
        BuildPorch(container.transform);
        BuildChimney(container.transform);
        BuildWindowTrim(container.transform);
        BuildFlowerBox(container.transform);
        BuildFirewoodPile(container.transform);
        BuildSteppingStones(container.transform);
        BuildGardenFence(container.transform);
    }

    [ContextMenu("Cancella Decorazioni")]
    public void ClearDecorations()
    {
        Transform existing = transform.Find("House_Decorations");
        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
        }
    }

    // --- TETTO PIENO E SIGILLATO ---
    private void BuildMainRoof(Transform parent)
    {
        Material roofMat = CreateMat("Mat_MainRoof", mainRoofColor);

        float halfWidth = (houseWidth / 2f) + roofOverhang;
        float angleRad = roofPitchAngle * Mathf.Deg2Rad;

        // Lunghezza della falda lungo la diagonale
        float slopeLength = (halfWidth / Mathf.Cos(angleRad)) + 0.1f;
        float roofLengthZ = houseDepth + (roofOverhang * 2f);

        // Altezza della punta centrale (vertice del triangolo)
        float apexY = wallHeight + (houseWidth / 2f * Mathf.Tan(angleRad));

        // Punto mediano delle falde inclinate
        float midX = halfWidth / 2f;
        float midY = (wallHeight + apexY) / 2f;

        // Falda Destra
        GameObject rightSlope = CreatePrimitive(PrimitiveType.Cube, "Roof_Slope_Right", parent,
            new Vector3(midX, midY, 0),
            new Vector3(slopeLength, roofThickness, roofLengthZ),
            roofMat);
        rightSlope.transform.localRotation = Quaternion.Euler(0, 0, -roofPitchAngle);

        // Falda Sinistra
        GameObject leftSlope = CreatePrimitive(PrimitiveType.Cube, "Roof_Slope_Left", parent,
            new Vector3(-midX, midY, 0),
            new Vector3(slopeLength, roofThickness, roofLengthZ),
            roofMat);
        leftSlope.transform.localRotation = Quaternion.Euler(0, 0, roofPitchAngle);

        // Trave di colmo centrale per chiudere ermeticamente la linea di giunzione in cima
        GameObject ridge = CreatePrimitive(PrimitiveType.Cube, "Roof_Ridge_Cap", parent,
            new Vector3(0, apexY + (roofThickness * 0.4f), 0),
            new Vector3(roofThickness * 1.8f, roofThickness * 1.1f, roofLengthZ + 0.05f),
            roofMat);
        ridge.transform.localRotation = Quaternion.Euler(0, 0, 45f);
    }

    private void BuildPorch(Transform parent)
    {
        Material woodMat = CreateMat("Mat_Wood", woodColor);
        Material roofMat = CreateMat("Mat_PorchRoof", porchRoofColor);

        float frontZ = -(houseDepth / 2f);
        float porchDepth = 1.3f;
        float porchWidth = 1.8f;

        CreatePrimitive(PrimitiveType.Cube, "Porch_Platform", parent,
            new Vector3(0, 0.1f, frontZ - (porchDepth / 2f)),
            new Vector3(porchWidth, 0.2f, porchDepth), woodMat);

        float postX = (porchWidth / 2f) - 0.1f;
        float postZ = frontZ - porchDepth + 0.1f;

        CreatePrimitive(PrimitiveType.Cylinder, "Post_Left", parent,
            new Vector3(-postX, wallHeight * 0.45f, postZ),
            new Vector3(0.15f, wallHeight * 0.45f, 0.15f), woodMat);

        CreatePrimitive(PrimitiveType.Cylinder, "Post_Right", parent,
            new Vector3(postX, wallHeight * 0.45f, postZ),
            new Vector3(0.15f, wallHeight * 0.45f, 0.15f), woodMat);

        GameObject roof = CreatePrimitive(PrimitiveType.Cube, "Porch_Roof", parent,
            new Vector3(0, wallHeight * 0.9f, frontZ - (porchDepth / 2f)),
            new Vector3(porchWidth + 0.3f, 0.1f, porchDepth + 0.2f), roofMat);
        roof.transform.localRotation = Quaternion.Euler(12f, 0, 0);
    }

    private void BuildChimney(Transform parent)
    {
        Material stoneMat = CreateMat("Mat_Stone", stoneColor);
        CreatePrimitive(PrimitiveType.Cube, "Chimney_Base", parent,
            new Vector3(houseWidth * 0.25f, wallHeight + 0.7f, 0),
            new Vector3(0.6f, 1.6f, 0.6f), stoneMat);

        CreatePrimitive(PrimitiveType.Cube, "Chimney_Top", parent,
            new Vector3(houseWidth * 0.25f, wallHeight + 1.55f, 0),
            new Vector3(0.75f, 0.15f, 0.75f), stoneMat);
    }

    private void BuildWindowTrim(Transform parent)
    {
        Material trimMat = CreateMat("Mat_Trim", trimColor);
        float frontZ = -(houseDepth / 2f) - 0.02f;
        Vector3 winPos = new Vector3(-1.1f, wallHeight * 0.55f, frontZ);

        CreatePrimitive(PrimitiveType.Cube, "Window_Trim_Top", parent, winPos + new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.08f, 0.06f), trimMat);
        CreatePrimitive(PrimitiveType.Cube, "Window_Trim_Bottom", parent, winPos - new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.08f, 0.06f), trimMat);
        CreatePrimitive(PrimitiveType.Cube, "Window_Trim_L", parent, winPos - new Vector3(0.42f, 0, 0), new Vector3(0.08f, 0.9f, 0.06f), trimMat);
        CreatePrimitive(PrimitiveType.Cube, "Window_Trim_R", parent, winPos + new Vector3(0.42f, 0, 0), new Vector3(0.08f, 0.9f, 0.06f), trimMat);
    }

    private void BuildFlowerBox(Transform parent)
    {
        Material woodMat = CreateMat("Mat_Wood", woodColor);
        Material folMat = CreateMat("Mat_Foliage", foliageColor);
        Material flwMat = CreateMat("Mat_Flower", flowerColor);

        Vector3 boxPos = new Vector3(-1.1f, (wallHeight * 0.55f) - 0.5f, -(houseDepth / 2f) - 0.12f);
        CreatePrimitive(PrimitiveType.Cube, "FlowerBox", parent, boxPos, new Vector3(0.85f, 0.22f, 0.25f), woodMat);
        CreatePrimitive(PrimitiveType.Cube, "Flower_Leaves", parent, boxPos + new Vector3(0, 0.14f, 0), new Vector3(0.8f, 0.12f, 0.22f), folMat);

        for (int i = -1; i <= 1; i++)
        {
            CreatePrimitive(PrimitiveType.Cube, $"Flower_{i}", parent,
                boxPos + new Vector3(i * 0.22f, 0.22f, 0),
                new Vector3(0.08f, 0.08f, 0.08f), flwMat);
        }
    }

    private void BuildFirewoodPile(Transform parent)
    {
        Material woodMat = CreateMat("Mat_Wood", woodColor);
        float sideX = (houseWidth / 2f) + 0.2f;

        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                Vector3 pos = new Vector3(sideX, 0.1f + (row * 0.16f), -0.3f + (col * 0.22f));
                GameObject log = CreatePrimitive(PrimitiveType.Cylinder, $"Log_{row}_{col}", parent, pos, new Vector3(0.14f, 0.35f, 0.14f), woodMat);
                log.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            }
        }
    }

    private void BuildSteppingStones(Transform parent)
    {
        Material stoneMat = CreateMat("Mat_Stone", stoneColor);
        float startZ = -(houseDepth / 2f) - 1.6f;

        for (int i = 0; i < 4; i++)
        {
            Vector3 pos = new Vector3(Random.Range(-0.1f, 0.1f), 0.02f, startZ - (i * 0.7f));
            float size = Random.Range(0.45f, 0.65f);
            GameObject stone = CreatePrimitive(PrimitiveType.Cylinder, $"Stone_{i}", parent, pos, new Vector3(size, 0.04f, size), stoneMat);
            stone.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
        }
    }

    private void BuildGardenFence(Transform parent)
    {
        Material fenceMat = CreateMat("Mat_Fence", fenceColor);

        float leftX = -(houseWidth / 2f) - 0.8f;
        float rightX = (houseWidth / 2f) + 0.8f;
        float backZ = 0.5f;
        float frontZ = -(houseDepth / 2f) - 4.2f;

        BuildFenceLine(parent, new Vector3(leftX, 0, backZ), new Vector3(leftX, 0, frontZ), fenceMat);
        BuildFenceLine(parent, new Vector3(rightX, 0, backZ), new Vector3(rightX, 0, frontZ), fenceMat);
        BuildFenceLine(parent, new Vector3(leftX, 0, frontZ), new Vector3(-0.8f, 0, frontZ), fenceMat);
        BuildFenceLine(parent, new Vector3(0.8f, 0, frontZ), new Vector3(rightX, 0, frontZ), fenceMat);
    }

    private void BuildFenceLine(Transform parent, Vector3 start, Vector3 end, Material mat)
    {
        float dist = Vector3.Distance(start, end);
        int posts = Mathf.Max(1, Mathf.CeilToInt(dist / 1.3f));
        Vector3 dir = (end - start).normalized;

        for (int i = 0; i <= posts; i++)
        {
            Vector3 pos = start + dir * (i * (dist / posts)) + new Vector3(0, 0.45f, 0);
            CreatePrimitive(PrimitiveType.Cube, "Fence_Post", parent, pos, new Vector3(0.12f, 0.9f, 0.12f), mat);
        }

        Vector3 center = (start + end) / 2f;
        Quaternion rot = Quaternion.LookRotation(dir);

        GameObject railUpper = CreatePrimitive(PrimitiveType.Cube, "Fence_Rail_Top", parent,
            center + new Vector3(0, 0.7f, 0), new Vector3(0.06f, 0.08f, dist), mat);
        railUpper.transform.localRotation = rot;

        GameObject railLower = CreatePrimitive(PrimitiveType.Cube, "Fence_Rail_Bottom", parent,
            center + new Vector3(0, 0.3f, 0), new Vector3(0.06f, 0.08f, dist), mat);
        railLower.transform.localRotation = rot;
    }

    private GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent);
        obj.transform.localPosition = localPos;
        obj.transform.localScale = localScale;
        if (mat != null)
        {
            obj.GetComponent<Renderer>().sharedMaterial = mat;
        }
        return obj;
    }

    private Material CreateMat(string matName, Color col)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.name = matName;
        mat.color = col;
        return mat;
    }
}