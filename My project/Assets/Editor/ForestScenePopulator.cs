using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// Unity 6 - strumento per l'Editor. Salvare in Assets/Editor/ForestScenePopulator.cs.
// Aprire Tools > Bosco > Completa scena.
// 1. Indicare Terrain/Ground, montagne e percorso (i pulsanti aiutano a trovarli).
// 2. Inserire almeno un prefab/modello nelle categorie desiderate, Ground inclusi.
// 3. Tracciare un poligono semplice all'interno della valle, in vista dall'alto.
// 4. Terminare il disegno e premere Genera negli spazi liberi.
// Ogni esecuzione AGGIUNGE un gruppo Bosco_Generato; gli oggetti esistenti restano
// al loro posto. Modifica > Annulla / Cmd+Z annulla l'intera generazione.
// I controlli sono conservativi: ingombri rettangolari delle mesh, margini,
// Collider delle montagne e campioni del suolo. Non sono un test esatto fra
// tutti i triangoli dei modelli. Mesh deformate da shader e animazioni richiedono
// controllo visivo. Tutte le distanze sono espresse in unita della scena.
// I Ground sono generati prima degli altri oggetti, parzialmente interrati.
// Diventano superfici di appoggio; i loro ingombri non bloccano la vegetazione.
// Il Terrain originale non viene scolpito. I Ground devono essere modelli del suolo,
// non contenitori che includano anche alberi, rocce o altri oggetti di scena.
public class ForestScenePopulator : EditorWindow
{
    private const string GeneratedGroundName = "Ground_Generati";
    [Serializable]
    private class Category
    {
        public string label;
        public List<GameObject> models = new List<GameObject>();
        public int count;
        public Vector2 scale = new Vector2(0.9f, 1.1f);
        public float maxSlope = 25f;
        public bool alignToGround;
        public bool expanded;
        public Category(string name, int amount, bool align)
        {
            label = name;
            count = amount;
            alignToGround = align;
        }
    }

    private class Template
    {
        public GameObject source;
        public Bounds localBounds;
        public Vector3 scale;
        public Quaternion rotation;
    }

    [SerializeField] private List<GameObject> surfaces = new List<GameObject>();
    [SerializeField] private List<GameObject> mountains = new List<GameObject>();
    [SerializeField] private List<GameObject> pathAndZones = new List<GameObject>();
    [SerializeField] private List<Vector3> area = new List<Vector3>();
    [SerializeField] private Category groundPatches = new Category("Ground / macchie di terreno", 8, true)
    {
        scale = new Vector2(0.9f, 1.1f), maxSlope = 15f, expanded = true
    };
    [SerializeField] private Vector2 groundRise = new Vector2(0.03f, 0.15f);
    [SerializeField] private float groundEmbedDepth = 0.02f;
    [SerializeField] private float groundSupportVariation = 0.25f;
    [SerializeField] private float groundOverlap = 0.1f;
    [SerializeField] private Category trees = new Category("Alberi", 40, false);
    [SerializeField] private Category rocks = new Category("Rocce", 20, true);
    [SerializeField] private Category grass = new Category("Erba", 100, true);
    [SerializeField] private Category flowers = new Category("Fiori", 60, true);
    [SerializeField] private int seed = 12345;
    [SerializeField] private float pathMargin = 1f;
    [SerializeField] private float objectGap = 0.15f;
    [SerializeField] private float borderMargin = 0.3f;
    [SerializeField] private float mountainMargin = 0.2f;
    [SerializeField] private float maxGroundVariation = 0.6f;
    private bool drawing;
    private bool showSurfaces = true;
    private bool showMountains;
    private bool showPath;
    private Vector2 scroll;
    private string status = "";

    [MenuItem("Tools/Bosco/Completa scena")]
    private static void Open()
    {
        ForestScenePopulator window = GetWindow<ForestScenePopulator>("Completa bosco");
        window.minSize = new Vector2(460f, 480f);
        window.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui -= DrawScene;
        SceneView.duringSceneGui += DrawScene;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DrawScene;
    }

    private void OnSelectionChange() { Repaint(); }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox(
            "Aggiunge Ground, alberi, rocce, erba e fiori negli spazi liberi. " +
            "Mantiene gli oggetti esistenti e crea un gruppo separato, annullabile con Cmd+Z. " +
            "I modelli e le superfici devono appartenere alla configurazione della tua scena.",
            MessageType.Info);

        showSurfaces = EditorGUILayout.Foldout(showSurfaces, "1. Suolo: Terrain e Ground", true);
        if (showSurfaces)
        {
            DrawObjects(surfaces);
            if (GUILayout.Button("Trova Terrain e Ground nella scena"))
                FindNamed(surfaces, 0);
        }
        showMountains = EditorGUILayout.Foldout(showMountains, "2. Montagne da evitare", true);
        if (showMountains)
        {
            EditorGUILayout.HelpBox(
                "Indica le montagne separatamente dal suolo. Devono avere Collider " +
                "che seguano la geometria; per le mesh statiche irregolari usa Mesh Collider non Convex.",
                MessageType.None);
            DrawObjects(mountains);
            if (GUILayout.Button("Trova MountainRidge nella scena"))
                FindNamed(mountains, 1);
        }
        showPath = EditorGUILayout.Foldout(showPath, "3. Percorso e zone da tenere libere", true);
        if (showPath)
        {
            EditorGUILayout.HelpBox(
                "Aggiungi i Path Piece, il loro contenitore, o zone di esclusione con Box Collider. " +
                "Puoi riservare qui anche lo spazio per il futuro fiume.", MessageType.None);
            DrawObjects(pathAndZones);
            if (GUILayout.Button("Trova Path Piece / Road_Rocks nella scena"))
                FindNamed(pathAndZones, 2);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("4. Area da riempire", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Vertici del contorno: " + area.Count);
        EditorGUILayout.HelpBox(
            "Mettiti in vista dall'alto. Attiva il disegno e clicca almeno tre punti " +
            "sul suolo lungo il contorno interno della valle, in ordine. " +
            "Il contorno si chiude automaticamente. Invio o Fine disegno termina l'inserimento.",
            MessageType.None);
        if (GUILayout.Button(drawing ? "Fine disegno" : "Disegna / aggiungi punti al contorno"))
        {
            drawing = !drawing;
            SceneView.RepaintAll();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Togli ultimo punto") && area.Count > 0)
            {
                Undo.RecordObject(this, "Modifica area bosco");
                area.RemoveAt(area.Count - 1);
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("Svuota contorno"))
            {
                Undo.RecordObject(this, "Svuota area bosco");
                area.Clear();
                SceneView.RepaintAll();
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("5. Ground da distribuire", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Inserisci qui i modelli Ground da COPIARE. Vengono distribuiti prima della vegetazione, " +
            "con rotazione e scala casuali, bordi inferiori interrati e rilievo variabile. " +
            "Il punto 1 indica invece il suolo gia presente su cui appoggiarli. " +
            "Quantita 0 disattiva l'aggiunta di Ground.", MessageType.None);
        DrawCategory(groundPatches);
        if (groundPatches.expanded)
        {
            EditorGUI.indentLevel++;
            groundRise = EditorGUILayout.Vector2Field("Rilievo min / max", groundRise);
            groundRise.x = Mathf.Max(0f, groundRise.x);
            groundRise.y = Mathf.Max(groundRise.x, groundRise.y);
            groundEmbedDepth = PositiveField("Interramento minimo", groundEmbedDepth);
            groundSupportVariation = PositiveField("Dislivello sotto i Ground", groundSupportVariation);
            groundOverlap = EditorGUILayout.Slider("Sovrapposizione fra Ground", groundOverlap, 0f, 0.35f);
            EditorGUILayout.HelpBox(
                "Il rilievo e l'altezza massima visibile rispetto al suolo piu basso sotto il pezzo. " +
                "Viene limitato dallo spessore del modello per non sollevarne la base. " +
                "I modelli quasi piatti vengono tenuti aderenti al suolo e richiedono una zona uniforme. " +
                "Una piccola sovrapposizione fra Ground aiuta a unire le macchie; percorso e altri oggetti " +
                "restano esclusi. Le mesh rigide non vengono deformate: verifica visivamente i bordi.",
                MessageType.None);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("6. Vegetazione, rocce e quantita", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Trascina i prefab dal Project, oppure seleziona alcuni esempi nella Hierarchy " +
            "e premi Aggiungi selezione nella categoria giusta. Basta un esempio per variante. " +
            "Quantita 0 disattiva quella categoria.", MessageType.None);
        DrawCategory(trees);
        DrawCategory(rocks);
        DrawCategory(grass);
        DrawCategory(flowers);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("7. Distanze e variazioni", EditorStyles.boldLabel);
        seed = EditorGUILayout.IntField("Disposizione (seed)", seed);
        pathMargin = PositiveField("Margine dal percorso", pathMargin);
        objectGap = PositiveField("Spazio fra ingombri", objectGap);
        borderMargin = PositiveField("Margine dal contorno", borderMargin);
        mountainMargin = PositiveField("Margine dalle montagne", mountainMargin);
        maxGroundVariation = PositiveField("Dislivello massimo sotto il modello", maxGroundVariation);
        EditorGUILayout.HelpBox(
            "Gli ingombri includono anche le chiome: il risultato puo essere prudente. " +
            "Se lo spazio non basta, vengono creati meno elementi di quelli richiesti. " +
            "Cambia seed per un'altra disposizione dopo aver annullato la precedente.", MessageType.None);

        using (new EditorGUI.DisabledScope(drawing || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Genera negli spazi liberi", GUILayout.Height(36f)))
                Generate();
        }
        if (!string.IsNullOrEmpty(status))
            EditorGUILayout.HelpBox(status, MessageType.Info);
        EditorGUILayout.EndScrollView();
    }

    private static float PositiveField(string label, float value)
    {
        return Mathf.Max(0f, EditorGUILayout.FloatField(label, value));
    }

    private static void DrawObjects(List<GameObject> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                list[i] = (GameObject)EditorGUILayout.ObjectField(list[i], typeof(GameObject), true);
                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    list.RemoveAt(i);
                    i--;
                }
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Aggiungi campo")) list.Add(null);
            if (GUILayout.Button("Aggiungi selezione"))
                foreach (GameObject selected in Selection.gameObjects)
                    if (!list.Contains(selected)) list.Add(selected);
        }
    }

    private static void DrawCategory(Category category)
    {
        category.expanded = EditorGUILayout.Foldout(category.expanded,
            category.label + " - " + category.models.Count + " modelli", true);
        if (!category.expanded) return;
        EditorGUI.indentLevel++;
        category.count = Mathf.Clamp(EditorGUILayout.IntField("Nuovi elementi", category.count), 0, 2000);
        category.scale = EditorGUILayout.Vector2Field("Scala casuale min / max", category.scale);
        category.scale.x = Mathf.Max(0.01f, category.scale.x);
        category.scale.y = Mathf.Max(category.scale.x, category.scale.y);
        category.maxSlope = EditorGUILayout.Slider("Pendenza massima", category.maxSlope, 0f, 60f);
        category.alignToGround = EditorGUILayout.Toggle("Segui inclinazione suolo", category.alignToGround);
        for (int i = 0; i < category.models.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                category.models[i] = (GameObject)EditorGUILayout.ObjectField(
                    category.models[i], typeof(GameObject), true);
                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    category.models.RemoveAt(i);
                    i--;
                }
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Aggiungi campo")) category.models.Add(null);
            if (GUILayout.Button("Aggiungi selezione"))
            {
                foreach (GameObject selected in Selection.gameObjects)
                {
                    GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(selected);
                    bool duplicate = category.models.Contains(selected);
                    if (source != null)
                        foreach (GameObject existing in category.models)
                            if (existing != null && (existing == source ||
                                PrefabUtility.GetCorrespondingObjectFromSource(existing) == source))
                                duplicate = true;
                    // Conserva l'esempio di scena per mantenerne scala e modifiche.
                    if (!duplicate) category.models.Add(selected);
                }
            }
        }
        EditorGUI.indentLevel--;
    }

    private static void FindNamed(List<GameObject> list, int kind)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        foreach (Transform t in root.GetComponentsInChildren<Transform>())
        {
            string n = t.name;
            bool match = kind == 0 ? t.GetComponent<Terrain>() != null || n.StartsWith("Ground", StringComparison.OrdinalIgnoreCase)
                : kind == 1 ? n.StartsWith("MountainRidge", StringComparison.OrdinalIgnoreCase)
                : n.StartsWith("Path Piece", StringComparison.OrdinalIgnoreCase) || n.StartsWith("Road_Rocks", StringComparison.OrdinalIgnoreCase);
            if (match && kind == 1)
                for (Transform ancestor = t.parent; ancestor != null; ancestor = ancestor.parent)
                    if (ancestor.name.StartsWith("MountainRidge", StringComparison.OrdinalIgnoreCase))
                    { match = false; break; }
            if (match && !list.Contains(t.gameObject)) list.Add(t.gameObject);
        }
    }

    private static List<Collider> GetColliders(List<GameObject> roots, bool groundOnly)
    {
        var result = new HashSet<Collider>();
        foreach (GameObject root in roots)
        {
            if (!ValidSceneObject(root)) continue;
            if (groundOnly && root.GetComponent<Terrain>() != null)
            {
                Collider col = root.GetComponent<TerrainCollider>();
                if (Usable(col)) result.Add(col);
            }
            else
                foreach (Collider col in root.GetComponentsInChildren<Collider>())
                    if (Usable(col)) result.Add(col);
        }
        return new List<Collider>(result);
    }

    private static bool Usable(Collider col)
    {
        return col != null && col.enabled && col.gameObject.activeInHierarchy && !col.isTrigger;
    }

    private static bool ValidSceneObject(GameObject go)
    {
        return go != null && go.scene == SceneManager.GetActiveScene() && go.activeInHierarchy;
    }

    private static bool IsGeneratedGround(Transform transform)
    {
        for (Transform current = transform; current != null; current = current.parent)
            if (current.name == GeneratedGroundName && current.parent != null &&
                current.parent.name.StartsWith("Bosco_Generato", StringComparison.Ordinal)) return true;
        return false;
    }

    private List<GameObject> GroundRoots()
    {
        var roots = new List<GameObject>(surfaces);
        // Riconosce anche Ground generati in esecuzioni precedenti, senza dipendere
        // dai riferimenti conservati dalla finestra dopo una ricompilazione.
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        foreach (Transform child in root.GetComponentsInChildren<Transform>())
            if (child.name == GeneratedGroundName && IsGeneratedGround(child) && !roots.Contains(child.gameObject))
                roots.Add(child.gameObject);
        return roots;
    }

    private static bool Hit(Ray ray, List<Collider> colliders, float distance, out RaycastHit nearest)
    {
        nearest = default(RaycastHit);
        float closest = float.PositiveInfinity;
        bool found = false;
        foreach (Collider col in colliders)
        {
            if (col.Raycast(ray, out RaycastHit hit, distance) && hit.distance < closest)
            {
                closest = hit.distance;
                nearest = hit;
                found = true;
            }
        }
        return found;
    }

    private void DrawScene(SceneView view)
    {
        Color old = Handles.color;
        Handles.color = new Color(0.1f, 1f, 0.6f, 1f);
        for (int i = 0; i < area.Count; i++)
        {
            Handles.Label(area[i] + Vector3.up * 0.15f, (i + 1).ToString());
            if (i > 0) Handles.DrawAAPolyLine(3f, area[i - 1], area[i]);
        }
        if (area.Count >= 3) Handles.DrawAAPolyLine(3f, area[area.Count - 1], area[0]);
        Handles.color = old;
        if (!drawing) return;
        Event ev = Event.current;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if (ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.Escape))
        {
            drawing = false;
            ev.Use();
            Repaint();
        }
        if (ev.type == EventType.MouseDown && ev.button == 0 && !ev.alt)
        {
            Physics.SyncTransforms();
            Ray ray = HandleUtility.GUIPointToWorldRay(ev.mousePosition);
            if (Hit(ray, GetColliders(GroundRoots(), true), float.MaxValue, out RaycastHit hit))
            {
                Undo.RecordObject(this, "Punto area bosco");
                area.Add(hit.point);
                status = "Punto aggiunto. Continua lungo il contorno, poi premi Fine disegno.";
            }
            else status = "Il clic non incontra il suolo: indica prima Terrain e Ground con Collider attivi.";
            ev.Use();
            Repaint();
            SceneView.RepaintAll();
        }
    }

    private static Rect Footprint(Bounds b, float padding)
    {
        return Rect.MinMaxRect(b.min.x - padding, b.min.z - padding, b.max.x + padding, b.max.z + padding);
    }

    private static bool Under(Transform t, GameObject root)
    {
        return root != null && (t == root.transform || t.IsChildOf(root.transform));
    }

    private static bool UnderAny(Transform t, List<GameObject> roots)
    {
        foreach (GameObject root in roots) if (Under(t, root)) return true;
        return false;
    }

    private bool IsGroundRenderer(Renderer r)
    {
        if (IsGeneratedGround(r.transform)) return true;
        foreach (GameObject root in surfaces)
        {
            if (root == null) continue;
            // Un Terrain non ha Mesh Renderer: i suoi figli possono essere alberi esistenti.
            if (root.GetComponent<Terrain>() == null && Under(r.transform, root)) return true;
        }
        return false;
    }

    private List<Rect> PathFootprints()
    {
        var result = new List<Rect>();
        var seenRenderers = new HashSet<Renderer>();
        var seenColliders = new HashSet<Collider>();
        foreach (GameObject root in pathAndZones)
        {
            if (!ValidSceneObject(root)) continue;
            // Un singolo Path Piece puo contenere due file di sassi:
            // il suo ingombro complessivo protegge anche lo spazio fra le file.
            if ((root.name.StartsWith("Path Piece", StringComparison.OrdinalIgnoreCase) ||
                root.name.StartsWith("Road_Rocks", StringComparison.OrdinalIgnoreCase)) &&
                SceneBounds(root, out Bounds pieceBounds))
            {
                result.Add(Footprint(pieceBounds, pathMargin));
                continue;
            }
            // Usa i singoli Renderer, non il rettangolo dell'intero sentiero curvo.
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                if ((r is MeshRenderer || r is SkinnedMeshRenderer) && seenRenderers.Add(r))
                    result.Add(Footprint(r.bounds, pathMargin));
            foreach (Collider col in root.GetComponentsInChildren<Collider>())
                if (col.enabled && seenColliders.Add(col))
                    result.Add(Footprint(col.bounds, pathMargin));
        }
        return result;
    }

    private static bool SceneBounds(GameObject root, out Bounds bounds)
    {
        bounds = default(Bounds);
        bool found = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (!found) { bounds = r.bounds; found = true; } else bounds.Encapsulate(r.bounds);
        }
        foreach (Collider col in root.GetComponentsInChildren<Collider>())
        {
            if (!col.enabled) continue;
            if (!found) { bounds = col.bounds; found = true; } else bounds.Encapsulate(col.bounds);
        }
        return found;
    }

    private List<Rect> ExistingFootprints(Scene scene)
    {
        var result = new List<Rect>();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (IsGroundRenderer(r) || UnderAny(r.transform, mountains) || UnderAny(r.transform, pathAndZones)) continue;
            result.Add(Footprint(r.bounds, objectGap * 0.5f));
        }
        return result;
    }

    private Rect GroundSpacingFootprint(Bounds bounds)
    {
        Rect footprint = Footprint(bounds, 0f);
        Vector2 size = footprint.size * (1f - groundOverlap);
        return new Rect(footprint.center - size * 0.5f, size);
    }

    private List<Rect> ExistingGroundFootprints(Scene scene)
    {
        var result = new List<Rect>();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            if ((renderer is MeshRenderer || renderer is SkinnedMeshRenderer) && IsGeneratedGround(renderer.transform))
                result.Add(GroundSpacingFootprint(renderer.bounds));
        return result;
    }

    private static Vector3 Corner(Bounds b, int i)
    {
        return b.center + Vector3.Scale(b.extents,
            new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
    }

    private static Bounds TransformBounds(Bounds local, Matrix4x4 matrix)
    {
        Bounds b = new Bounds(matrix.MultiplyPoint3x4(Corner(local, 0)), Vector3.zero);
        for (int i = 1; i < 8; i++) b.Encapsulate(matrix.MultiplyPoint3x4(Corner(local, i)));
        return b;
    }

    private static Template MakeTemplate(GameObject source)
    {
        if (source == null) return null;
        // InstantiatePrefab istanzia la radice: un sotto-oggetto di un asset
        // avrebbe ingombri diversi. Usa il prefab intero o un esempio di scena.
        if (EditorUtility.IsPersistent(source) && source.transform.parent != null) return null;
        bool found = false;
        Bounds local = default(Bounds);
        Matrix4x4 toLocal = source.transform.worldToLocalMatrix;
        foreach (MeshFilter mesh in source.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mesh.sharedMesh == null) continue;
            Bounds b = TransformBounds(mesh.sharedMesh.bounds, toLocal * mesh.transform.localToWorldMatrix);
            if (!found) { local = b; found = true; } else local.Encapsulate(b);
        }
        foreach (SkinnedMeshRenderer skin in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Bounds b = TransformBounds(skin.localBounds, toLocal * skin.transform.localToWorldMatrix);
            if (!found) { local = b; found = true; } else local.Encapsulate(b);
        }
        if (!found) return null;
        bool asset = EditorUtility.IsPersistent(source);
        return new Template { source = source, localBounds = local,
            scale = asset ? source.transform.localScale : source.transform.lossyScale,
            rotation = asset ? source.transform.localRotation : source.transform.rotation };
    }

    private static Vector2 XZ(Vector3 p) { return new Vector2(p.x, p.z); }

    private static float SegmentDistanceSquared(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude < 0.000001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - a - ab * t).sqrMagnitude;
    }

    private bool CircleInsideArea(Vector2 point, float radius)
    {
        bool inside = false;
        float minDistance = float.PositiveInfinity;
        for (int i = 0, j = area.Count - 1; i < area.Count; j = i++)
        {
            Vector2 a = XZ(area[i]);
            Vector2 b = XZ(area[j]);
            minDistance = Mathf.Min(minDistance, SegmentDistanceSquared(point, a, b));
            if ((a.y > point.y) != (b.y > point.y) &&
                point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside && minDistance >= radius * radius;
    }

    private static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }

    private bool ValidPolygon()
    {
        if (area.Count < 3) return false;
        float twiceArea = 0f;
        for (int i = 0; i < area.Count; i++)
        {
            Vector2 a = XZ(area[i]);
            Vector2 b = XZ(area[(i + 1) % area.Count]);
            if ((b - a).sqrMagnitude < 0.000001f) return false;
            twiceArea += Cross(a, b);
            for (int j = i + 1; j < area.Count; j++)
            {
                if ((i + 1) % area.Count == j || (j + 1) % area.Count == i) continue;
                Vector2 c = XZ(area[j]);
                Vector2 d = XZ(area[(j + 1) % area.Count]);
                // Incroci o contatti fra lati non adiacenti rendono ambiguo il contorno.
                if (SegmentsTouch(a, b, c, d)) return false;
            }
        }
        return Mathf.Abs(twiceArea) > 0.001f;
    }

    private static bool SegmentsTouch(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        const float eps = 0.000001f;
        float s1 = Cross(b - a, c - a), s2 = Cross(b - a, d - a);
        float s3 = Cross(d - c, a - c), s4 = Cross(d - c, b - c);
        if (((s1 > eps && s2 < -eps) || (s1 < -eps && s2 > eps)) &&
            ((s3 > eps && s4 < -eps) || (s3 < -eps && s4 > eps))) return true;
        return Mathf.Abs(s1) <= eps && SegmentDistanceSquared(c, a, b) <= eps ||
               Mathf.Abs(s2) <= eps && SegmentDistanceSquared(d, a, b) <= eps ||
               Mathf.Abs(s3) <= eps && SegmentDistanceSquared(a, c, d) <= eps ||
               Mathf.Abs(s4) <= eps && SegmentDistanceSquared(b, c, d) <= eps;
    }

    private static bool OverlapsAny(Rect footprint, List<Rect> obstacles)
    {
        foreach (Rect obstacle in obstacles) if (footprint.Overlaps(obstacle)) return true;
        return false;
    }

    private static bool GroundAt(float x, float z, float startY, float length,
        List<Collider> ground, List<Collider> blockers, out RaycastHit hit)
    {
        Ray ray = new Ray(new Vector3(x, startY, z), Vector3.down);
        if (!Hit(ray, ground, length, out hit)) return false;
        return !Hit(ray, blockers, length, out RaycastHit obstruction) || obstruction.distance > hit.distance + 0.02f;
    }

    private bool FullSupport(Bounds b, float centerY, float startY, float length,
        List<Collider> ground, List<Collider> blockers)
    {
        for (int i = 0; i < 4; i++)
        {
            float x = (i & 1) == 0 ? b.min.x : b.max.x;
            float z = (i & 2) == 0 ? b.min.z : b.max.z;
            if (!GroundAt(x, z, startY, length, ground, blockers, out RaycastHit hit) ||
                Mathf.Abs(hit.point.y - centerY) > maxGroundVariation) return false;
        }
        return true;
    }

    private bool GroundPatchSupport(Bounds bounds, float startY, float length,
        List<Collider> baseGround, List<Collider> blockers, out float lowestY, out float highestY)
    {
        lowestY = float.PositiveInfinity;
        highestY = float.NegativeInfinity;
        // Centro, bordi e angoli: evita di stendere un pezzo rigido su buchi,
        // scarpate o montagne. Sono campioni conservativi, non una deformazione.
        for (int ix = 0; ix < 3; ix++)
        for (int iz = 0; iz < 3; iz++)
        {
            float x = Mathf.Lerp(bounds.min.x, bounds.max.x, ix * 0.5f);
            float z = Mathf.Lerp(bounds.min.z, bounds.max.z, iz * 0.5f);
            if (!GroundAt(x, z, startY, length, baseGround, blockers, out RaycastHit support) ||
                Vector3.Angle(support.normal, Vector3.up) > groundPatches.maxSlope) return false;
            lowestY = Mathf.Min(lowestY, support.point.y);
            highestY = Mathf.Max(highestY, support.point.y);
        }
        return highestY - lowestY <= groundSupportVariation;
    }

    private static List<Collider> PrepareGroundColliders(GameObject instance)
    {
        // Il suolo generato resta fermo anche se l'esempio conteneva un Rigidbody.
        foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>())
        {
            Undo.RecordObject(body, "Ground statico");
            body.isKinematic = true;
            body.useGravity = false;
            if (PrefabUtility.IsPartOfPrefabInstance(body))
                PrefabUtility.RecordPrefabInstancePropertyModifications(body);
        }
        var colliders = GetColliders(new List<GameObject> { instance }, false);
        if (colliders.Count > 0) return colliders;

        // Aggiunge i Collider solo alle COPIE generate, mai ai prefab sorgenti.
        foreach (MeshFilter mesh in instance.GetComponentsInChildren<MeshFilter>())
        {
            if (mesh.sharedMesh == null || mesh.sharedMesh.vertexCount == 0) continue;
            MeshCollider collider = Undo.AddComponent<MeshCollider>(mesh.gameObject);
            collider.sharedMesh = mesh.sharedMesh;
            collider.convex = false;
            if (PrefabUtility.IsPartOfPrefabInstance(collider))
                PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
        }
        return GetColliders(new List<GameObject> { instance }, false);
    }

    private void Generate()
    {
        drawing = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!ValidPolygon()) { status = "Disegna un contorno con almeno tre punti, senza lati incrociati o punti ripetuti."; return; }
        Scene scene = SceneManager.GetActiveScene();
        List<Collider> ground = GetColliders(GroundRoots(), true);
        var baseGround = ground.FindAll(col => !IsGeneratedGround(col.transform));
        List<Collider> blockers = GetColliders(mountains, false);
        Physics.SyncTransforms();
        if (ground.Count == 0) { status = "Indica Terrain e Ground con Collider attivi nella scena corrente."; return; }
        if (blockers.Count == 0) { status = "Indica le montagne e verifica che abbiano Collider attivi aderenti alla geometria."; return; }
        foreach (GameObject mountain in mountains)
        {
            if (mountain != null && (!ValidSceneObject(mountain) ||
                GetColliders(new List<GameObject> { mountain }, false).Count == 0))
            { status = "La montagna '" + mountain.name + "' non ha un Collider attivo nella scena corrente."; return; }
        }
        List<Rect> reserved = PathFootprints();
        if (reserved.Count == 0) { status = "Indica i Path Piece o il loro contenitore per tenere libero il percorso."; return; }

        Category[] categories = { groundPatches, trees, rocks, grass, flowers };
        var templates = new List<Template>[categories.Length];
        int requested = 0;
        for (int i = 0; i < categories.Length; i++)
        {
            templates[i] = new List<Template>();
            foreach (GameObject model in categories[i].models)
            {
                Template template = MakeTemplate(model);
                if (template != null) templates[i].Add(template);
            }
            if (templates[i].Count > 0) requested += categories[i].count;
        }
        if (requested == 0) { status = "Aggiungi modelli ad almeno una categoria e imposta una quantita maggiore di zero."; return; }
        if (groundPatches.count > 0 && templates[0].Count > 0 && baseGround.Count == 0)
        { status = "Per distribuire Ground indica anche il Terrain o il suolo originale al punto 1."; return; }

        float startY = float.NegativeInfinity, bottomY = float.PositiveInfinity;
        foreach (Collider col in ground) { startY = Mathf.Max(startY, col.bounds.max.y); bottomY = Mathf.Min(bottomY, col.bounds.min.y); }
        foreach (Collider col in blockers) startY = Mathf.Max(startY, col.bounds.max.y);
        startY += 2f;
        float rayLength = startY - bottomY + 2f;
        Vector2 min = XZ(area[0]), max = min;
        foreach (Vector3 p in area) { min = Vector2.Min(min, XZ(p)); max = Vector2.Max(max, XZ(p)); }
        List<Rect> occupied = ExistingFootprints(scene);
        List<Rect> groundOccupied = ExistingGroundFootprints(scene);
        var blockerSet = new HashSet<Collider>(blockers);
        var random = new System.Random(seed);
        int total = 0;
        bool cancelled = false;
        var reports = new List<string>();

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Genera bosco");
        GameObject output = new GameObject("Bosco_Generato");
        SceneManager.MoveGameObjectToScene(output, scene);
        Undo.RegisterCreatedObjectUndo(output, "Genera bosco");
        try
        {
            for (int c = 0; c < categories.Length && !cancelled; c++)
            {
                Category category = categories[c];
                if (category.count <= 0) continue;
                if (templates[c].Count == 0)
                {
                    reports.Add(category.label + ": nessun modello valido assegnato");
                    continue;
                }
                bool isGround = c == 0;
                // I Ground si appoggiano al suolo originale, non si accumulano
                // sopra quelli appena generati. La vegetazione usa invece tutti.
                List<Collider> supportGround = isGround ? baseGround : ground;
                int created = 0;
                int budget = Mathf.Clamp(category.count * 40, 200, 20000);
                Transform parent = null;
                for (int attempt = 0; attempt < budget && created < category.count; attempt++)
                {
                    if (attempt % 32 == 0 && EditorUtility.DisplayCancelableProgressBar("Completa bosco",
                        category.label + ": " + created + "/" + category.count,
                        (c + attempt / (float)budget) / categories.Length)) { cancelled = true; break; }
                    float x = Mathf.Lerp(min.x, max.x, (float)random.NextDouble());
                    float z = Mathf.Lerp(min.y, max.y, (float)random.NextDouble());
                    if (!CircleInsideArea(new Vector2(x, z), 0f) ||
                        !GroundAt(x, z, startY, rayLength, supportGround, blockers, out RaycastHit hit) ||
                        Vector3.Angle(hit.normal, Vector3.up) > category.maxSlope) continue;

                    Template template = templates[c][random.Next(templates[c].Count)];
                    float factor = Mathf.Lerp(category.scale.x, category.scale.y, (float)random.NextDouble());
                    Vector3 scale = template.scale * factor;
                    Quaternion rotation = Quaternion.AngleAxis((float)random.NextDouble() * 360f, Vector3.up) * template.rotation;
                    if (category.alignToGround) rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * rotation;
                    Bounds relative = TransformBounds(template.localBounds, Matrix4x4.TRS(Vector3.zero, rotation, scale));
                    Vector3 position = new Vector3(x, hit.point.y - relative.min.y - 0.01f, z);
                    Bounds bounds = relative;
                    bounds.center += position;
                    if (isGround)
                    {
                        if (!GroundPatchSupport(bounds, startY, rayLength, baseGround, blockers,
                            out float lowestSupport, out float highestSupport)) continue;
                        float thickness = template.localBounds.size.y * Mathf.Abs(scale.y);
                        if (thickness <= 0.02f)
                        {
                            // Un piano non puo diventare un rialzo: sulle zone
                            // uniformi resta a pochi millimetri dalla superficie.
                            if (highestSupport - lowestSupport > 0.015f) continue;
                            position.y = (lowestSupport + highestSupport) * 0.5f - relative.center.y + 0.003f;
                        }
                        else
                        {
                            float desiredRise = Mathf.Lerp(groundRise.x, groundRise.y, (float)random.NextDouble());
                            float availableRise = Mathf.Max(0f, Mathf.Min(thickness, relative.size.y) - groundEmbedDepth);
                            float rise = Mathf.Min(desiredRise, availableRise);
                            position.y = lowestSupport + rise - relative.max.y;
                        }
                        bounds = relative;
                        bounds.center += position;
                    }
                    float radius = new Vector2(bounds.extents.x, bounds.extents.z).magnitude + borderMargin;
                    Rect footprint = Footprint(bounds, objectGap * 0.5f);
                    if (!CircleInsideArea(XZ(bounds.center), radius) || OverlapsAny(footprint, reserved) ||
                        OverlapsAny(footprint, occupied)) continue;
                    if (isGround)
                    {
                        if (OverlapsAny(GroundSpacingFootprint(bounds), groundOccupied)) continue;
                    }
                    else if (!FullSupport(bounds, hit.point.y, startY, rayLength, ground, blockers)) continue;

                    bool collision = false;
                    foreach (Collider col in Physics.OverlapBox(bounds.center,
                        bounds.extents + Vector3.one * mountainMargin, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                        if (blockerSet.Contains(col)) { collision = true; break; }
                    if (collision) continue;

                    if (parent == null)
                    {
                        GameObject group = new GameObject(isGround ? GeneratedGroundName : category.label);
                        SceneManager.MoveGameObjectToScene(group, scene);
                        group.transform.SetParent(output.transform, false);
                        Undo.RegisterCreatedObjectUndo(group, "Genera bosco");
                        parent = group.transform;
                    }
                    GameObject instance;
                    if (EditorUtility.IsPersistent(template.source))
                        instance = PrefabUtility.InstantiatePrefab(template.source, scene) as GameObject;
                    else
                    {
                        instance = UnityEngine.Object.Instantiate(template.source);
                        SceneManager.MoveGameObjectToScene(instance, scene);
                    }
                    if (instance == null) continue;
                    instance.name = template.source.name;
                    instance.transform.SetParent(parent, false);
                    instance.transform.SetPositionAndRotation(position, rotation);
                    instance.transform.localScale = scale;
                    instance.SetActive(true);
                    Undo.RegisterCreatedObjectUndo(instance, "Genera bosco");
                    if (PrefabUtility.IsPartOfPrefabInstance(instance))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    if (isGround)
                    {
                        List<Collider> newGround = PrepareGroundColliders(instance);
                        if (newGround.Count == 0)
                        {
                            Undo.DestroyObjectImmediate(instance);
                            continue;
                        }
                        ground.AddRange(newGround);
                        groundOccupied.Add(GroundSpacingFootprint(bounds));
                    }
                    else occupied.Add(footprint);
                    created++;
                    total++;
                }
                reports.Add(category.label + " " + created + "/" + category.count);
                if (isGround && created > 0)
                {
                    // Aggiorna la fisica dopo aver posato i Ground: alberi, rocce,
                    // erba e fiori possono cosi appoggiarsi anche ai nuovi rilievi.
                    Physics.SyncTransforms();
                    foreach (Collider col in ground) startY = Mathf.Max(startY, col.bounds.max.y + 2f);
                    rayLength = startY - bottomY + 2f;
                }
            }
        }
        catch (Exception error)
        {
            cancelled = true;
            Debug.LogException(error);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (total == 0) Undo.DestroyObjectImmediate(output);
            Undo.CollapseUndoOperations(undoGroup);
        }
        if (total > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = output;
        }
        status = (cancelled ? "Generazione interrotta. " : "Generazione completata. ") +
            string.Join("; ", reports) + ". Totale aggiunto: " + total +
            ". Se sono meno del richiesto, i controlli o lo spazio disponibile hanno limitato la distribuzione. " +
            "Cmd+Z annulla questo gruppo.";
        Debug.Log(status);
        SceneView.RepaintAll();
    }
}
