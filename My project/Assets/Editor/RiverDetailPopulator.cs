/*
 FIUME V2 - LETTO FITTO, HABITAT E PESCI PER UNITY 6
 ---------------------------------------
 INSTALLAZIONE
 1. Sostituisci Assets/Editor/RiverDetailPopulator.cs con questo file.
    Aggiungi anche Assets/Scripts/RiverHabitatRuntime.cs dal pacchetto V2.
 2. Aspetta la compilazione e apri Tools > Fiume > Decora fondo e rive.
 3. Assegna il Terrain e il MeshRenderer di Water Plane dalla Hierarchy.
 4. Trascina prefab di rocce e fiori DAL PROJECT nelle rispettive liste.
    Puoi usare gli stessi Rock sia per il fondo sia per la riva.
    Se la lista Sassi del fondo e' vuota, usa i prefab Rocce della riva.
 5. Premi ANALIZZA: blu = fondo sommerso, arancione = fascia di riva.
 6. Premi GENERA, poi salva la scena normalmente. Non serve premere Play.

 FUNZIONAMENTO
 Legge le altezze del Terrain sotto un piano d'acqua orizzontale.
 Non usa il rettangolo dell'acqua come se fosse tutto un fiume: individua
 le depressioni sotto il suo livello e calcola la distanza dalle zone sommerse.
 Riconosce quindi curve e diramazioni, senza dover disegnare un percorso.
 Non legge il colore sullo schermo o eventuali maschere dello shader.

 Se altri Ground coprono il Terrain, aggiungi i loro Collider nelle opzioni
 avanzate: viene usata la superficie piu' alta. Non aggiungere alberi o acqua.
 Puoi riservare l'area del ponte con BoxCollider nelle Zone da lasciare libere:
 viene esclusa la loro proiezione orizzontale, anche se sono disattivati.

 Ogni generazione sostituisce solo il gruppo Fiume_Dettagli_Generati di questo
 Terrain. Cmd+Z / Ctrl+Z annulla la generazione. Annullare con la barra di
 avanzamento o un errore mantiene il risultato precedente.
 Non scolpisce il Terrain, non dipinge texture e non cambia il materiale acqua.
 Non garantisce di evitare oggetti preesistenti: usa le zone libere per
 sentiero, ponte e altre aree da proteggere. Evita sovrapposizioni fra le
 nuove decorazioni usando ingombri orizzontali conservativi.

 LETTO FITTO E HABITAT
 La copertura fitta e' una griglia esagonale irregolare di ciottoli low poly,
 ricostruiti in blocchi di mesh dal componente RiverHabitatRuntime.
 Le ricette sono salvate nella scena: la geometria si ricrea anche dopo
 riapertura, Undo/Redo e in una build. Tieni entrambi gli script nel progetto.
 Con il limite ciottoli viene aumentato il passo dell'intero letto, anziche'
 lasciare l'ultima parte del fiume vuota. La copertura e' un valore desiderato:
 pendenze, bordi e zone protette possono ridurla.
 I pesci inclusi sono modelli low poly generici, sostituibili con prefab.
 Nuotano in Play Mode su percorsi controllati, dentro il volume d'acqua.
 Le nuove categorie di vegetazione/fauna usano prefab forniti dall'utente:
 una lista vuota viene semplicemente saltata. La fauna di riva resta statica.
 Un nuovo seme a ogni generazione e' attivo per default; disattivalo per
 riprodurre un risultato. I prefab gia' assegnati restano nelle rispettive liste.
 La visibilita' sott'acqua dipende dalla trasparenza e dallo shader dell'acqua.
 Le dimensioni qui indicate sono unita' Unity, normalmente metri.
 Il riconoscimento delle rive e' approssimato alla risoluzione della griglia:
 diminuisci Passo analisi per canali stretti, senza aspettarti che disegni rive.
*/

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public sealed class RiverDetailPopulator : EditorWindow
{
    private const string OutputName = "Fiume_Dettagli_Generati";
    private const string UndoName = "Decora fondo e rive del fiume";
    private const int MaxCells = 300000;
    private const int MaxObjects = 10000;
    private static readonly string[] FolderNames =
        { "Sassi_Fondo", "Rocce_Riva", "Fiori_Riva", "Erba_Riva", "Piante_Acquatiche",
          "Felci_e_Carici", "Legni_Riva", "Fauna_Riva", "Habitat_Fitto_e_Pesci" };

    [Serializable]
    private sealed class ScatterGroup
    {
        public List<GameObject> prefabs = new List<GameObject> { null };
        public int maximum;
        public Vector2 size;
        public bool expanded = true;
        public ScatterGroup(int count, float minimumSize, float maximumSize)
        { maximum = count; size = new Vector2(minimumSize, maximumSize); }
    }

    [SerializeField] private Terrain terrain;
    [SerializeField] private MeshRenderer water;
    [SerializeField] private bool automaticWaterLevel = true;
    [SerializeField] private float manualWaterLevel = 7.8f;
    [SerializeField] private float bankWidth = 8f;
    [SerializeField] private float gridStep = 2f;
    [SerializeField] private float maximumDepth = 30f;
    [SerializeField] private float maximumBankRise = 10f;
    [SerializeField] private float plantsShoreDistance = 1f;
    [SerializeField] private float clusterSize = 12f;
    [SerializeField] private int seed = 4837;
    [SerializeField] private bool freshSeedEachGeneration = true;
    [SerializeField] private bool denseBed = true;
    [SerializeField] private float pebbleStep = 2.4f;
    [SerializeField] private float bedCoverage = 0.98f;
    [SerializeField] private int maximumPebbles = 40000;
    [SerializeField] private Material pebbleMaterial;
    [SerializeField] private bool includeFish = true;
    [SerializeField] private int fishCount = 25;
    [SerializeField] private Vector2 fishLength = new Vector2(0.6f, 1.1f);
    [SerializeField] private Vector2 fishSpeed = new Vector2(0.45f, 1.1f);
    [SerializeField] private GameObject fishPrefab;
    [SerializeField] private Vector3 fishModelEuler;
    [SerializeField] private Material fishBodyMaterial, fishFinMaterial;
    [SerializeField] private float aquaticMaximumDepth = 2.5f;
    [SerializeField] private bool disableColliders = true;
    [SerializeField] private bool smallObjectShadows;
    [SerializeField] private float exclusionMargin = 0.5f;
    [SerializeField] private List<Collider> additionalGrounds = new List<Collider>();
    [SerializeField] private List<BoxCollider> exclusionZones = new List<BoxCollider>();
    [SerializeField] private ScatterGroup bed = new ScatterGroup(1800, 0.35f, 0.85f);
    [SerializeField] private ScatterGroup banks = new ScatterGroup(150, 0.8f, 2.3f);
    [SerializeField] private ScatterGroup flowers = new ScatterGroup(450, 0.2f, 0.5f);
    [SerializeField] private ScatterGroup grass = new ScatterGroup(250, 0.3f, 0.8f);
    [SerializeField] private ScatterGroup aquaticPlants = new ScatterGroup(180, 0.25f, 0.8f);
    [SerializeField] private ScatterGroup ferns = new ScatterGroup(200, 0.35f, 1.2f);
    [SerializeField] private ScatterGroup driftwood = new ScatterGroup(25, 0.8f, 2.5f);
    [SerializeField] private ScatterGroup bankFauna = new ScatterGroup(20, 0.15f, 0.35f);
    [SerializeField] private bool advanced;
    [SerializeField] private bool showPreview;
    private readonly List<Bounds> generatedObstacles = new List<Bounds>();
    private readonly Dictionary<Mesh, Vector3[]> anchorVertices = new Dictionary<Mesh, Vector3[]>();
    private Vector2 scroll;
    private Map map;
    private string message = "Assegna Terrain, Water Plane e i prefab. Poi premi ANALIZZA.";
    private MessageType messageType = MessageType.Info;

    [MenuItem("Tools/Fiume/Decora fondo e rive")]
    public static void OpenWindow()
    {
        GetWindow<RiverDetailPopulator>("Decora fiume").minSize = new Vector2(390, 430);
    }

    private void OnEnable() { SceneView.duringSceneGui += DrawPreview; }
    private void OnDisable() { SceneView.duringSceneGui -= DrawPreview; }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Fiume V2: fondo fitto e habitat", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Lavora fuori dal Play Mode. Usa prefab dal Project. " +
            "Le dimensioni vengono normalizzate: anche un prefab grande puo' diventare un sassolino.", MessageType.Info);

        if (GUILayout.Button("Rileva Terrain e acqua nella scena")) DetectSceneReferences();
        EditorGUI.BeginChangeCheck();
        terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
        water = (MeshRenderer)EditorGUILayout.ObjectField("Acqua (Water Plane)", water, typeof(MeshRenderer), true);
        automaticWaterLevel = EditorGUILayout.Toggle("Quota acqua automatica", automaticWaterLevel);
        if (automaticWaterLevel)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.FloatField("Quota Y rilevata", GetWaterLevel());
        }
        else manualWaterLevel = EditorGUILayout.FloatField("Quota Y acqua", manualWaterLevel);
        bankWidth = Mathf.Clamp(EditorGUILayout.FloatField("Larghezza fascia riva", bankWidth), 0.5f, 100f);
        seed = EditorGUILayout.IntField("Seme casuale", seed);
        freshSeedEachGeneration = EditorGUILayout.Toggle("Nuovo seme a ogni GENERA", freshSeedEachGeneration);

        EditorGUILayout.Space();
        denseBed = EditorGUILayout.ToggleLeft("LETTO MOLTO COPERTO DI CIOTTOLI", denseBed, EditorStyles.boldLabel);
        if (denseBed)
        {
            EditorGUI.indentLevel++;
            pebbleStep = Mathf.Clamp(EditorGUILayout.FloatField("Passo ciottoli", pebbleStep), 0.3f, 10f);
            bedCoverage = EditorGUILayout.Slider("Copertura desiderata", bedCoverage, 0.5f, 1f);
            maximumPebbles = Mathf.Clamp(EditorGUILayout.IntField("Limite ciottoli fitti", maximumPebbles), 1000, 60000);
            pebbleMaterial = (Material)EditorGUILayout.ObjectField("Materiale (facoltativo)", pebbleMaterial, typeof(Material), false);
            EditorGUILayout.HelpBox("Ciottoli generati automaticamente, con toni e forme variabili. " +
                "Passo minore = ciottoli piu' piccoli e numerosi. Al limite viene adattato tutto il letto. " +
                "Il materiale puo' essere ricavato dai prefab delle rocce.", MessageType.None);
            EditorGUI.indentLevel--;
        }

        DrawGroup("1. Rocce singole sul fondo (extra)", bed, false);
        DrawGroup("2. Rocce della riva", banks, false);
        DrawGroup("3. Fiori della riva", flowers, true);
        DrawGroup("4. Erba della riva (facoltativa)", grass, true);
        DrawGroup("5. Piante acquatiche (acqua bassa)", aquaticPlants, true);
        DrawGroup("6. Felci / carici della riva", ferns, true);
        DrawGroup("7. Legni e rametti sulla riva", driftwood, false);
        DrawGroup("8. Fauna della riva (es. rane)", bankFauna, true);
        includeFish = EditorGUILayout.ToggleLeft("PESCI CON NUOTO IN PLAY MODE", includeFish, EditorStyles.boldLabel);
        if (includeFish)
        {
            EditorGUI.indentLevel++;
            fishCount = Mathf.Clamp(EditorGUILayout.IntField("Numero massimo pesci", fishCount), 0, 100);
            fishLength = PositiveRange("Lunghezza pesci min / max", fishLength, 0.1f, 4f);
            fishSpeed = PositiveRange("Velocita' min / max", fishSpeed, 0.05f, 5f);
            fishPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab pesce (facoltativo)", fishPrefab, typeof(GameObject), false);
            if (fishPrefab) fishModelEuler = EditorGUILayout.Vector3Field("Correzione rotazione modello", fishModelEuler);
            fishBodyMaterial = (Material)EditorGUILayout.ObjectField("Materiale corpo incluso", fishBodyMaterial, typeof(Material), false);
            fishFinMaterial = (Material)EditorGUILayout.ObjectField("Materiale pinne incluse", fishFinMaterial, typeof(Material), false);
            EditorGUILayout.HelpBox("Senza prefab vengono creati pesci low poly generici. " +
                "I percorsi restano sotto la superficie, sopra fondo e ciottoli, lontani dalle decorazioni. " +
                "Le categorie vuote di flora e fauna vengono saltate.", MessageType.None);
            EditorGUI.indentLevel--;
        }

        advanced = EditorGUILayout.Foldout(advanced, "Opzioni avanzate e zone libere", true);
        if (advanced)
        {
            EditorGUI.indentLevel++;
            gridStep = Mathf.Clamp(EditorGUILayout.FloatField("Passo analisi", gridStep), 0.25f, 20f);
            maximumDepth = Mathf.Clamp(EditorGUILayout.FloatField("Profondita' massima sassi", maximumDepth), 0.1f, 1000f);
            maximumBankRise = Mathf.Clamp(EditorGUILayout.FloatField("Dislivello massimo riva", maximumBankRise), 0.1f, 100f);
            plantsShoreDistance = Mathf.Clamp(EditorGUILayout.FloatField("Piante: distanza dall'acqua", plantsShoreDistance), 0f, bankWidth);
            clusterSize = Mathf.Clamp(EditorGUILayout.FloatField("Ampiezza gruppetti piante", clusterSize), 1f, 100f);
            aquaticMaximumDepth = Mathf.Clamp(EditorGUILayout.FloatField("Profondita' massima piante", aquaticMaximumDepth), 0.1f, 10f);
            disableColliders = EditorGUILayout.Toggle("Disattiva Collider generati", disableColliders);
            smallObjectShadows = EditorGUILayout.Toggle("Ombre anche per sassi/piante", smallObjectShadows);
            DrawList("Collider dei Ground aggiuntivi", additionalGrounds, true);
            EditorGUILayout.HelpBox("Aggiungi solo superfici che coprono il suolo. " +
                "I Collider devono essere attivi; acqua, alberi e rocce non vanno in questa lista.", MessageType.None);
            DrawList("Zone da lasciare libere", exclusionZones, true);
            exclusionMargin = Mathf.Clamp(EditorGUILayout.FloatField("Margine zone libere", exclusionMargin), 0f, 20f);
            EditorGUILayout.HelpBox("Per ponte e sentiero: crea uno o piu' GameObject con BoxCollider " +
                "e trascina qui il componente. La zona e' esclusa in pianta (XZ). " +
                "Puoi disattivare i Collider: restano validi come zone libere.", MessageType.None);
            EditorGUI.indentLevel--;
        }
        if (EditorGUI.EndChangeCheck()) { map = null; SceneView.RepaintAll(); }

        bool preview = EditorGUILayout.Toggle("Mostra anteprima in Scene", showPreview);
        if (preview != showPreview) { showPreview = preview; SceneView.RepaintAll(); }
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("ANALIZZA fondo e rive", GUILayout.Height(28))) Analyze();
            if (GUILayout.Button("GENERA / RIGENERA decorazioni", GUILayout.Height(34))) Generate();
            using (new EditorGUI.DisabledScope(!FindOutput()))
                if (GUILayout.Button("Rimuovi le decorazioni del fiume")) RemoveOutput();
        }
        EditorGUILayout.HelpBox(message, messageType);
        EditorGUILayout.LabelField("Blu: fondo. Arancione: riva. Cmd+Z / Ctrl+Z: annulla.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndScrollView();
    }

    private static Vector2 PositiveRange(string label, Vector2 range, float minimum, float maximum)
    {
        range = EditorGUILayout.Vector2Field(label, range);
        range.x = Mathf.Clamp(range.x, minimum, maximum);
        range.y = Mathf.Clamp(range.y, range.x, maximum);
        return range;
    }

    private static void DrawGroup(string title, ScatterGroup group, bool plant)
    {
        group.expanded = EditorGUILayout.Foldout(group.expanded, title, true);
        if (!group.expanded) return;
        EditorGUI.indentLevel++;
        DrawList("Prefab", group.prefabs, false);
        group.maximum = Mathf.Clamp(EditorGUILayout.IntField("Numero massimo", group.maximum), 0, MaxObjects);
        group.size = EditorGUILayout.Vector2Field(plant ? "Altezza min / max" : "Larghezza min / max", group.size);
        group.size.x = Mathf.Clamp(group.size.x, 0.03f, 50f);
        group.size.y = Mathf.Clamp(group.size.y, group.size.x, 50f);
        EditorGUI.indentLevel--;
    }

    private static void DrawList<T>(string label, List<T> list, bool sceneObjects) where T : UnityEngine.Object
    {
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        int remove = -1;
        for (int i = 0; i < list.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            list[i] = (T)EditorGUILayout.ObjectField(list[i], typeof(T), sceneObjects);
            if (GUILayout.Button("-", GUILayout.Width(24))) remove = i;
            EditorGUILayout.EndHorizontal();
        }
        if (remove >= 0) list.RemoveAt(remove);
        if (GUILayout.Button("+ Aggiungi elemento", EditorStyles.miniButton)) list.Add(null);
    }

    private void DetectSceneReferences()
    {
        if (Selection.activeGameObject)
        {
            Terrain selected = Selection.activeGameObject.GetComponentInParent<Terrain>();
            if (selected) terrain = selected;
        }
        if (!terrain) terrain = Terrain.activeTerrain;
        if (!water)
        {
            foreach (MeshRenderer renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (terrain && renderer.gameObject.scene != terrain.gameObject.scene) continue;
                if (!renderer.gameObject.activeInHierarchy) continue;
                string lower = renderer.name.ToLowerInvariant();
                if (!lower.Contains("water") && !lower.Contains("acqua")) continue;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh || filter.sharedMesh.bounds.size.y > 0.05f) continue;
                water = renderer;
                if (lower.Contains("plane")) break;
            }
        }
        map = null;
        message = "Controlla i riferimenti rilevati, poi assegna i prefab dal Project.";
        messageType = MessageType.Info;
        SceneView.RepaintAll();
    }

    private float GetWaterLevel()
    {
        if (!automaticWaterLevel || !water) return manualWaterLevel;
        MeshFilter filter = water.GetComponent<MeshFilter>();
        return filter && filter.sharedMesh
            ? water.transform.TransformPoint(filter.sharedMesh.bounds.center).y
            : water.transform.position.y;
    }

    private void ValidateReferences()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Esci dal Play Mode prima di generare.");
        if (!terrain || !terrain.terrainData || !water)
            throw new InvalidOperationException("Assegna il Terrain e il MeshRenderer di Water Plane.");
        if (!terrain.gameObject.scene.IsValid() || EditorUtility.IsPersistent(terrain) || EditorUtility.IsPersistent(water))
            throw new InvalidOperationException("Usa Terrain e Water Plane dalla Hierarchy, non asset dal Project.");
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Esci dalla modifica del prefab e torna alla scena.");
        if (!terrain.isActiveAndEnabled || !water.enabled || !water.gameObject.activeInHierarchy)
            throw new InvalidOperationException("Terrain e acqua devono essere attivi e visibili.");
        if (terrain.gameObject.scene != water.gameObject.scene)
            throw new InvalidOperationException("Terrain e acqua devono appartenere alla stessa scena.");
        if (Quaternion.Angle(terrain.transform.rotation, Quaternion.identity) > 0.01f ||
            (terrain.transform.lossyScale - Vector3.one).sqrMagnitude > 0.0001f)
            throw new InvalidOperationException("Il Terrain deve avere rotazione zero e scala mondiale 1,1,1.");
        MeshFilter filter = water.GetComponent<MeshFilter>();
        if (!filter || !filter.sharedMesh || filter.sharedMesh.bounds.size.y > 0.05f ||
            Mathf.Abs(Vector3.Dot(water.transform.up, Vector3.up)) < 0.9999f)
            throw new InvalidOperationException("Assegna la mesh piatta e orizzontale Water Plane.");
        if (!IsFinite(GetWaterLevel())) throw new InvalidOperationException("La quota dell'acqua non e' valida.");
        if (fishPrefab && !PrefabUtility.IsPartOfPrefabAsset(fishPrefab))
            throw new InvalidOperationException("Usa un prefab pesce dal Project oppure lascia il campo vuoto per i pesci inclusi.");
        foreach (Collider col in additionalGrounds)
        {
            if (!col) continue;
            if (EditorUtility.IsPersistent(col) || col.gameObject.scene != terrain.gameObject.scene ||
                !col.enabled || !col.gameObject.activeInHierarchy || col.isTrigger)
                throw new InvalidOperationException("Il Ground aggiuntivo " + col.name + " deve avere un Collider attivo, non Trigger, nella stessa scena.");
            if (col.transform == water.transform || col.transform.IsChildOf(water.transform) ||
                (FindOutput() && col.transform.IsChildOf(FindOutput())))
                throw new InvalidOperationException("La lista Ground non puo' contenere acqua o decorazioni generate.");
        }
        foreach (BoxCollider zone in exclusionZones)
            if (zone && (EditorUtility.IsPersistent(zone) || zone.gameObject.scene != terrain.gameObject.scene))
                throw new InvalidOperationException("Le zone libere devono essere BoxCollider nella stessa scena.");
    }

    private static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

    private sealed class SurfaceSampler
    {
        public Terrain terrain;
        public MeshRenderer water;
        public Bounds waterLocalBounds;
        public Collider[] grounds;
        public Bounds[] groundBounds;
        public float rayTop, rayLength;

        public bool Sample(float x, float z, out Vector3 point, out Vector3 normal)
        {
            point = new Vector3(x, float.NegativeInfinity, z);
            normal = Vector3.up;
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            float nx = (x - origin.x) / data.size.x;
            float nz = (z - origin.z) / data.size.z;
            Vector3 local = water.transform.InverseTransformPoint(new Vector3(x, water.transform.position.y, z));
            if (nx < 0 || nx > 1 || nz < 0 || nz > 1 ||
                local.x < waterLocalBounds.min.x || local.x > waterLocalBounds.max.x ||
                local.z < waterLocalBounds.min.z || local.z > waterLocalBounds.max.z) return false;
            int resolution = data.holesResolution;
            bool valid = resolution < 1 || !data.IsHole(
                Mathf.Clamp(Mathf.FloorToInt(nx * resolution), 0, resolution - 1),
                Mathf.Clamp(Mathf.FloorToInt(nz * resolution), 0, resolution - 1));
            if (valid)
            {
                point.y = origin.y + data.GetInterpolatedHeight(nx, nz);
                normal = data.GetInterpolatedNormal(nx, nz);
            }
            Ray ray = new Ray(new Vector3(x, rayTop, z), Vector3.down);
            for (int i = 0; i < grounds.Length; i++)
            {
                Bounds b = groundBounds[i];
                if (x < b.min.x || x > b.max.x || z < b.min.z || z > b.max.z) continue;
                if (grounds[i].Raycast(ray, out RaycastHit hit, rayLength) && hit.normal.y > 0.05f && hit.point.y > point.y)
                { point = hit.point; normal = hit.normal; valid = true; }
            }
            return valid && IsFinite(point.y);
        }
    }

    private sealed class Map
    {
        public int width, height;
        public float minX, minZ, maxX, maxZ, step, level;
        public float[] heights, distance;
        public bool[] valid, wet;
        public SurfaceSampler sampler;
        public List<int> waterCells = new List<int>();
        public List<int> bedCells = new List<int>();
        public List<int> bankCells = new List<int>();
        public List<int> plantCells = new List<int>();
        public Vector3 PointInCell(int index, float rx, float rz)
        {
            float x = minX + (index % width) * step;
            float z = minZ + (index / width) * step;
            return new Vector3(Mathf.Lerp(x, Mathf.Min(maxX, x + step), rx), heights[index],
                Mathf.Lerp(z, Mathf.Min(maxZ, z + step), rz));
        }
    }

    private Map BuildMap()
    {
        ValidateReferences();
        Physics.SyncTransforms();
        TerrainData data = terrain.terrainData;
        Bounds wb = water.bounds;
        Vector3 origin = terrain.transform.position;
        Map m = new Map
        {
            minX = Mathf.Max(origin.x, wb.min.x), minZ = Mathf.Max(origin.z, wb.min.z),
            maxX = Mathf.Min(origin.x + data.size.x, wb.max.x),
            maxZ = Mathf.Min(origin.z + data.size.z, wb.max.z), step = gridStep, level = GetWaterLevel()
        };
        if (m.maxX <= m.minX || m.maxZ <= m.minZ)
            throw new InvalidOperationException("Il rettangolo del piano d'acqua non interseca il Terrain.");
        do
        {
            m.width = Mathf.CeilToInt((m.maxX - m.minX) / m.step);
            m.height = Mathf.CeilToInt((m.maxZ - m.minZ) / m.step);
            if ((long)m.width * m.height <= MaxCells) break;
            m.step *= 1.15f;
        } while (true);
        int count = m.width * m.height;
        m.heights = new float[count]; m.distance = new float[count];
        m.valid = new bool[count]; m.wet = new bool[count];
        List<Collider> grounds = additionalGrounds.FindAll(c => c && !(c is TerrainCollider));
        m.sampler = new SurfaceSampler
        {
            terrain = terrain, water = water,
            waterLocalBounds = water.GetComponent<MeshFilter>().sharedMesh.bounds,
            grounds = grounds.ToArray(), groundBounds = new Bounds[grounds.Count],
            rayTop = origin.y + data.size.y + 100f
        };
        for (int i = 0; i < grounds.Count; i++)
        {
            m.sampler.groundBounds[i] = grounds[i].bounds;
            m.sampler.rayTop = Mathf.Max(m.sampler.rayTop, grounds[i].bounds.max.y + 10f);
        }
        m.sampler.rayLength = m.sampler.rayTop - origin.y + 100f;
        for (int z = 0; z < m.height; z++)
        {
            if (z % 8 == 0) CheckCancel("Analisi del fondo e dei Ground", 0.3f * z / m.height);
            for (int x = 0; x < m.width; x++)
            {
                int k = z * m.width + x;
                Vector3 center = m.PointInCell(k, 0.5f, 0.5f);
                m.valid[k] = m.sampler.Sample(center.x, center.z, out Vector3 p, out _);
                m.heights[k] = p.y;
                m.wet[k] = m.valid[k] && p.y <= m.level - 0.04f;
                m.distance[k] = m.wet[k] ? 0f : float.PositiveInfinity;
                if (m.wet[k]) m.waterCells.Add(k);
            }
        }
        if (m.waterCells.Count == 0)
            throw new InvalidOperationException("Nessun fondo sotto il livello dell'acqua. Controlla Water Plane, quota Y e Ground aggiuntivi. " +
                "Se il canale e' stretto, riduci Passo analisi.");

        // Distanza approssimata in XZ, con passi ortogonali e diagonali.
        // I semi sono tutte le celle sommerse: non serve un percorso centrale.
        float diagonal = m.step * Mathf.Sqrt(2f);
        for (int z = 0; z < m.height; z++)
        {
            if (z % 32 == 0) CheckCancel("Riconoscimento delle rive", 0.3f + 0.1f * z / m.height);
            for (int x = 0; x < m.width; x++)
            {
                int k = z * m.width + x;
                if (!m.valid[k] || m.wet[k]) continue;
                Relax(m, k, x - 1, z, m.step);
                Relax(m, k, x - 1, z - 1, diagonal);
                Relax(m, k, x, z - 1, m.step);
                Relax(m, k, x + 1, z - 1, diagonal);
            }
        }
        for (int z = m.height - 1; z >= 0; z--)
        {
            for (int x = m.width - 1; x >= 0; x--)
            {
                int k = z * m.width + x;
                if (!m.valid[k] || m.wet[k]) continue;
                Relax(m, k, x + 1, z, m.step);
                Relax(m, k, x + 1, z + 1, diagonal);
                Relax(m, k, x, z + 1, m.step);
                Relax(m, k, x - 1, z + 1, diagonal);
            }
        }
        for (int k = 0; k < count; k++)
        {
            if (!m.valid[k]) continue;
            if (m.wet[k])
            {
                if (m.level - m.heights[k] <= maximumDepth) m.bedCells.Add(k);
                continue;
            }
            m.distance[k] = Mathf.Max(0f, m.distance[k] - m.step * 0.5f);
            if (m.heights[k] < m.level + 0.02f || m.heights[k] > m.level + maximumBankRise ||
                m.distance[k] > bankWidth) continue;
            m.bankCells.Add(k);
            if (m.distance[k] >= plantsShoreDistance) m.plantCells.Add(k);
        }
        return m;
    }

    private static void Relax(Map m, int target, int x, int z, float cost)
    {
        if (x < 0 || z < 0 || x >= m.width || z >= m.height) return;
        int source = z * m.width + x;
        if (m.valid[source]) m.distance[target] = Mathf.Min(m.distance[target], m.distance[source] + cost);
    }

    private void Analyze()
    {
        try
        {
            map = BuildMap(); showPreview = true;
            message = "Analisi: " + map.waterCells.Count + " campioni sommersi, " + map.bankCells.Count +
                " sulla riva. Passo effettivo: " + map.step.ToString("0.##") + ". " +
                "Controlla i punti in Scene prima di generare.";
            if (map.waterCells.Count > map.width * map.height * 0.6f)
                message += " Oltre il 60% dell'area risulta sommerso: verifica la quota e i Ground che coprono il Terrain.";
            messageType = MessageType.Info;
        }
        catch (OperationCanceledException) { message = "Analisi annullata."; messageType = MessageType.Info; }
        catch (Exception ex) { map = null; message = ex.Message; messageType = MessageType.Error; }
        finally { EditorUtility.ClearProgressBar(); SceneView.RepaintAll(); }
    }

    private void Generate()
    {
        int undoGroup = -1;
        try
        {
            if (freshSeedEachGeneration) seed = Guid.NewGuid().GetHashCode() & int.MaxValue;
            generatedObstacles.Clear(); anchorVertices.Clear();
            map = BuildMap(); // Rilegge anche un Terrain modificato dopo l'anteprima.
            ScatterGroup[] groups = { bed, banks, flowers, grass, aquaticPlants, ferns, driftwood, bankFauna };
            List<GameObject>[] prefabs = new List<GameObject>[groups.Length];
            int requested = 0, usable = 0;
            for (int i = 0; i < groups.Length; i++)
            {
                prefabs[i] = ValidPrefabs(groups[i].prefabs);
                if (i == 0 && prefabs[i].Count == 0) prefabs[i] = ValidPrefabs(banks.prefabs);
                if (prefabs[i].Count == 0) continue;
                requested += groups[i].maximum;
                if (groups[i].maximum > 0) usable++;
            }
            if (usable == 0 && !denseBed && !(includeFish && fishCount > 0))
                throw new InvalidOperationException("Aggiungi prefab, attiva il letto fitto oppure i pesci.");
            if (requested > MaxObjects)
                throw new InvalidOperationException("Riduci i numeri massimi: il limite totale e' " + MaxObjects + " oggetti.");
            Transform previous = FindOutput();
            if (!previous && terrain.transform.Find(OutputName))
                throw new InvalidOperationException("Esiste un oggetto chiamato " + OutputName + " che non riconosco. Rinominalo prima di generare.");

            Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            Transform root = CreateFolder(OutputName + "_InPreparazione", terrain.transform);
            Transform[] folders = new Transform[FolderNames.Length];
            for (int i = 0; i < folders.Length; i++) folders[i] = CreateFolder(FolderNames[i], root);
            SpatialHash occupied = new SpatialHash();
            int[] placed = new int[groups.Length];
            for (int i = 0; i < groups.Length; i++)
                placed[i] = Scatter(map, groups[i], prefabs[i], i, folders[i], occupied,
                    new System.Random(unchecked(seed ^ (7919 * (i + 1)))));
            int pebbleCount, placedFish;
            float actualPebbleStep;
            BuildHabitat(map, folders[8], occupied, out pebbleCount, out placedFish, out actualPebbleStep);
            int total = pebbleCount + placedFish;
            foreach (int number in placed) total += number;
            if (total == 0)
                throw new InvalidOperationException("Nessun oggetto collocabile. Controlla anteprima, prefab, dimensioni e zone libere. " +
                    "Per le rive puoi aumentare Larghezza fascia riva o Dislivello massimo riva.");

            // Elimina il risultato precedente soltanto quando il nuovo e' pronto.
            if (previous) Undo.DestroyObjectImmediate(previous.gameObject);
            Undo.RecordObject(root.gameObject, UndoName); root.name = OutputName;
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(undoGroup); undoGroup = -1;
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            Selection.activeGameObject = root.gameObject;
            showPreview = false;
            message = "Creati: " + pebbleCount + " ciottoli fitti (passo " + actualPebbleStep.ToString("0.##") + "), " +
                placed[0] + " rocce fondo, " + placed[1] + " rocce riva, " + placed[2] + " fiori, " + placed[3] +
                " ciuffi, " + placed[4] + " piante acquatiche, " + placed[5] + " felci/carici, " + placed[6] +
                " legni, " + placed[7] + " animali riva, " + placedFish + " pesci. Seme: " + seed + ". Salva la scena.";
            if (includeFish && placedFish < fishCount)
                message += " Pesci limitati dallo spazio e dalla profondita' disponibili.";
            messageType = MessageType.Info;
            Debug.Log(message, root.gameObject);
        }
        catch (OperationCanceledException)
        {
            if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup);
            message = "Generazione annullata. Il risultato precedente e' stato mantenuto.";
            messageType = MessageType.Info;
        }
        catch (Exception ex)
        {
            if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup);
            message = ex.Message; messageType = MessageType.Error;
        }
        finally { EditorUtility.ClearProgressBar(); SceneView.RepaintAll(); Repaint(); }
    }

    private static List<GameObject> ValidPrefabs(List<GameObject> input)
    {
        List<GameObject> result = new List<GameObject>();
        foreach (GameObject item in input)
            if (item && EditorUtility.IsPersistent(item) && PrefabUtility.IsPartOfPrefabAsset(item) && !result.Contains(item))
                result.Add(item);
        return result;
    }

    private Transform CreateFolder(string name, Transform parent)
    {
        GameObject folder = new GameObject(name);
        SceneManager.MoveGameObjectToScene(folder, terrain.gameObject.scene);
        folder.transform.SetParent(parent, true); // Mantiene scala/rotazione mondiali unitarie.
        Undo.RegisterCreatedObjectUndo(folder, UndoName);
        return folder.transform;
    }

    private int Scatter(Map m, ScatterGroup group, List<GameObject> prefabs, int kind,
        Transform parent, SpatialHash occupied, System.Random random)
    {
        if (prefabs.Count == 0 || group.maximum <= 0) return 0;
        List<int> candidates = kind == 0 || kind == 4 ? m.bedCells :
            kind == 1 || kind == 6 || kind == 7 ? m.bankCells : m.plantCells;
        if (candidates.Count == 0) return 0;
        int placed = 0;
        int maxAttempts = group.maximum * 35 + 100;
        for (int attempt = 0; attempt < maxAttempts && placed < group.maximum; attempt++)
        {
            if (attempt % 32 == 0)
                CheckCancel("Distribuzione " + FolderNames[kind] + ": " + placed + "/" + group.maximum,
                    0.4f + 0.04f * kind + 0.04f * attempt / maxAttempts);
            int cell = candidates[random.Next(candidates.Count)];
            Vector3 p = m.PointInCell(cell, (float)random.NextDouble(), (float)random.NextDouble());
            if (!m.sampler.Sample(p.x, p.z, out p, out Vector3 normal)) continue;
            float maxSlope = kind < 2 ? 48f : kind == 7 ? 18f : 28f;
            if (Vector3.Angle(normal, Vector3.up) > maxSlope) continue;
            if (kind == 0 || kind == 4)
            {
                float depth = m.level - p.y;
                if (depth < 0.04f || depth > (kind == 4 ? aquaticMaximumDepth : maximumDepth)) continue;
            }
            else if (p.y < m.level + 0.02f || p.y > m.level + maximumBankRise) continue;
            if (kind >= 2 && kind <= 5)
            {
                float offset = (seed % 100000) * 0.013f + kind * 81.7f;
                float patch = Mathf.PerlinNoise(p.x / clusterSize + offset, p.z / clusterSize + offset);
                if (patch < 0.50f || random.NextDouble() > patch) continue;
            }
            float size = Mathf.Lerp(group.size.x, group.size.y, (float)random.NextDouble());
            float estimate = size * (kind < 2 || kind == 6 ? 0.4f : 0.15f);
            if (IsExcluded(p, estimate) || !occupied.IsFree(p, estimate)) continue;
            GameObject prefab = prefabs[random.Next(prefabs.Count)];
            float yaw = (float)random.NextDouble() * 360f;
            GameObject instance = CreateDecoration(prefab, parent, p, normal, size, yaw, kind, m, occupied, out float radius);
            if (!instance) continue;
            Undo.RegisterCreatedObjectUndo(instance, UndoName);
            occupied.Add(new Vector3(p.x, 0f, p.z), radius);
            if (GetBounds(instance.GetComponentsInChildren<Renderer>(), out Bounds finalBounds))
                generatedObstacles.Add(finalBounds);
            placed++;
        }
        return placed;
    }

    private GameObject CreateDecoration(GameObject prefab, Transform parent, Vector3 p,
        Vector3 normal, float size, float yaw, int kind, Map m, SpatialHash occupied, out float radius)
    {
        radius = 0;
        GameObject instance = null;
        bool keep = false;
        try
        {
            instance = PrefabUtility.InstantiatePrefab(prefab, terrain.gameObject.scene) as GameObject;
            if (!instance) return null;
            instance.transform.SetParent(parent, true);
            instance.SetActive(true);
            Transform t = instance.transform;
            t.position = p;
            Quaternion tilt = Quaternion.FromToRotation(Vector3.up, normal);
            if (kind >= 2 && kind <= 5) tilt = Quaternion.Slerp(Quaternion.identity, tilt, 0.35f);
            t.rotation = tilt * Quaternion.Euler(0, yaw, 0) * prefab.transform.localRotation;
            t.localScale = prefab.transform.localScale;
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (!GetBounds(renderers, out Bounds bounds)) return null;
            float originalSize = kind < 2 || kind == 6 ? Mathf.Max(bounds.size.x, bounds.size.z) : bounds.size.y;
            if (originalSize < 0.00001f || !IsFinite(originalSize)) return null;
            t.localScale *= size / originalSize;
            if (!GetBounds(renderers, out bounds)) return null;
            // Centra il volume visivo, anche con pivot spostati nel prefab originale.
            t.position += new Vector3(p.x - bounds.center.x, 0f, p.z - bounds.center.z);
            GetBounds(renderers, out bounds);
            radius = 0.5f * new Vector2(bounds.size.x, bounds.size.z).magnitude;
            if (IsExcluded(p, radius) || !occupied.IsFree(p, radius)) return null;
            if (!FootprintFits(m, p, radius, kind)) return null;

            // Appoggia le bounds locali al piano tangente del terreno.
            // La piccola immersione rende meno visibile la giunzione con il suolo.
            float lowestProjection = float.PositiveInfinity;
            foreach (Renderer renderer in renderers)
            {
                if (!VisibleMesh(renderer)) continue;
                Bounds local = renderer.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 q = local.center + Vector3.Scale(local.extents, new Vector3(
                        (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    lowestProjection = Mathf.Min(lowestProjection,
                        Vector3.Dot(renderer.transform.TransformPoint(q) - p, normal));
                }
            }
            if (!IsFinite(lowestProjection)) return null;
            float sink = bounds.size.y * (kind < 2 || kind == 6 ? 0.22f : 0.035f);
            t.position += Vector3.up * ((-sink - lowestProjection) / Mathf.Max(0.1f, normal.y));
            RefineGroundContact(instance, renderers, m, p, normal, bounds.size.y, kind);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            if (disableColliders)
                foreach (Collider col in instance.GetComponentsInChildren<Collider>(true))
                { col.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(col); }
            foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true))
            { body.isKinematic = true; PrefabUtility.RecordPrefabInstancePropertyModifications(body); }
            foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
            { animator.applyRootMotion = false; PrefabUtility.RecordPrefabInstancePropertyModifications(animator); }
            if (!smallObjectShadows && kind != 1)
                foreach (Renderer renderer in renderers)
                { renderer.shadowCastingMode = ShadowCastingMode.Off; PrefabUtility.RecordPrefabInstancePropertyModifications(renderer); }
            keep = true;
            return instance;
        }
        finally { if (!keep && instance) UnityEngine.Object.DestroyImmediate(instance); }
    }

    private void RefineGroundContact(GameObject instance, Renderer[] renderers, Map m,
        Vector3 anchor, Vector3 normal, float height, int kind)
    {
        // Prefer actual mesh vertices, so an empty corner of a bounding box does
        // not become a false support point. The Editor-only API also reads
        // imported meshes without changing their Read/Write import settings.
        List<Vector3> points = new List<Vector3>();
        foreach (Renderer renderer in renderers)
        {
            if (!VisibleMesh(renderer)) continue;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh) continue;
            Mesh mesh = filter.sharedMesh;
            if (!anchorVertices.TryGetValue(mesh, out Vector3[] vertices))
            { vertices = ReadAnchorVertices(mesh); anchorVertices.Add(mesh, vertices); }
            int stride = Mathf.Max(1, vertices.Length / 256);
            for (int i = 0; i < vertices.Length; i += stride)
                points.Add(filter.transform.TransformPoint(vertices[i]));
        }
        if (points.Count == 0) return;
        float lowest = float.PositiveInfinity;
        foreach (Vector3 point in points) lowest = Mathf.Min(lowest, Vector3.Dot(point - anchor, normal));
        float minDelta = float.PositiveInfinity;
        int contacts = 0;
        foreach (Vector3 point in points)
        {
            if (Vector3.Dot(point - anchor, normal) > lowest + height * 0.08f) continue;
            if (!m.sampler.Sample(point.x, point.z, out Vector3 surface, out _)) continue;
            minDelta = Mathf.Min(minDelta, surface.y - point.y); contacts++;
        }
        if (contacts == 0) return;
        float burial = height * (kind < 2 || kind == 6 ? 0.06f : 0.015f);
        // Only lower: refinement must not undo an intentional burial.
        instance.transform.position += Vector3.up * Mathf.Min(0f, minDelta - burial);
    }

    private static Vector3[] ReadAnchorVertices(Mesh mesh)
    {
        if (mesh.isReadable) return mesh.vertices;
        try
        {
            using (Mesh.MeshDataArray data = MeshUtility.AcquireReadOnlyMeshData(mesh))
            using (Unity.Collections.NativeArray<Vector3> output = new Unity.Collections.NativeArray<Vector3>(
                data[0].vertexCount, Unity.Collections.Allocator.Temp))
            {
                data[0].GetVertices(output);
                return output.ToArray();
            }
        }
        catch (Exception) { return new Vector3[0]; } // Retain the bounds-based support.
    }

    private void BuildHabitat(Map m, Transform parent, SpatialHash occupied,
        out int stoneCount, out int placedFish, out float actualStep)
    {
        actualStep = pebbleStep;
        List<RiverHabitatRuntime.Stone> stones = new List<RiverHabitatRuntime.Stone>();
        if (denseBed)
        {
            float approximateArea = m.bedCells.Count * m.step * m.step;
            actualStep = Mathf.Max(actualStep, Mathf.Sqrt(approximateArea * bedCoverage /
                (maximumPebbles * 0.8660254f)) * 1.05f);
            // Retry the COMPLETE bed if the estimate undercounts it. Never cut
            // generation halfway along the river when reaching the budget.
            do
            {
                stones = CreateDenseStones(m, parent, occupied, actualStep);
                if (stones.Count <= maximumPebbles) break;
                actualStep *= 1.2f;
            } while (true);
        }
        stoneCount = stones.Count;
        RiverHabitatRuntime.WaterVolume volume = new RiverHabitatRuntime.WaterVolume
        {
            terrain = terrain, water = water, automaticLevel = automaticWaterLevel,
            waterLevel = m.level, extraGrounds = additionalGrounds.FindAll(c => c).ToArray(),
            obstacles = generatedObstacles.ToArray(), bottomClearance = 0.4f,
            gravelHeight = stoneCount > 0 ? actualStep * 0.6f : 0f
        };
        List<Bounds> exclusions = new List<Bounds>();
        foreach (BoxCollider zone in exclusionZones)
        {
            if (!zone) continue;
            Bounds b = WorldBoxBounds(zone); b.Expand(exclusionMargin * 2f); exclusions.Add(b);
        }
        volume.exclusions = exclusions.ToArray(); volume.BuildIndex();
        List<RiverHabitatRuntime.Fish> fishes = includeFish ? CreateFishRoutes(m, parent, volume) :
            new List<RiverHabitatRuntime.Fish>();
        placedFish = fishes.Count;
        Material gravel = null, body = null, fins = null;
        if (stoneCount > 0)
        {
            gravel = pebbleMaterial;
            if (!gravel)
                foreach (GameObject rock in ValidPrefabs(bed.prefabs).Count > 0 ? ValidPrefabs(bed.prefabs) : ValidPrefabs(banks.prefabs))
                {
                    MeshRenderer renderer = rock.GetComponentInChildren<MeshRenderer>(true);
                    if (renderer && renderer.sharedMaterial) { gravel = renderer.sharedMaterial; break; }
                }
            if (!gravel) gravel = GetDefaultMaterial("Ciottoli", new Color(0.42f, 0.43f, 0.39f));
        }
        if (placedFish > 0 && !fishPrefab)
        {
            body = fishBodyMaterial ? fishBodyMaterial : GetDefaultMaterial("Pesci_Corpo", new Color(0.42f, 0.52f, 0.46f));
            fins = fishFinMaterial ? fishFinMaterial : GetDefaultMaterial("Pesci_Pinne", new Color(0.25f, 0.31f, 0.25f));
        }
        RiverHabitatRuntime habitat = Undo.AddComponent<RiverHabitatRuntime>(parent.gameObject);
        Undo.RecordObject(habitat, UndoName);
        habitat.Configure(stones.ToArray(), fishes.ToArray(), volume, gravel, body, fins);
        EditorUtility.SetDirty(habitat);
        habitat.RebuildNow();
    }

    private List<RiverHabitatRuntime.Stone> CreateDenseStones(Map m, Transform parent, SpatialHash occupied, float spacing)
    {
        System.Random random = new System.Random(unchecked(seed ^ 654897));
        List<RiverHabitatRuntime.Stone> result = new List<RiverHabitatRuntime.Stone>();
        float rowStep = spacing * 0.8660254f;
        int rows = Mathf.CeilToInt((m.maxZ - m.minZ) / rowStep);
        int columns = Mathf.CeilToInt((m.maxX - m.minX) / spacing);
        float offsetX = (float)random.NextDouble() * spacing;
        float offsetZ = (float)random.NextDouble() * rowStep;
        for (int row = 0; row <= rows; row++)
        {
            if (row % 8 == 0) CheckCancel("Copertura fitta del letto: " + result.Count + " ciottoli", 0.73f + 0.14f * row / Mathf.Max(1, rows));
            for (int column = 0; column <= columns; column++)
            {
                if (random.NextDouble() > bedCoverage) continue;
                float x = m.minX + offsetX + (column + (row % 2) * 0.5f) * spacing +
                    ((float)random.NextDouble() - 0.5f) * spacing * 0.12f;
                float z = m.minZ + offsetZ + row * rowStep +
                    ((float)random.NextDouble() - 0.5f) * spacing * 0.12f;
                int gx = Mathf.FloorToInt((x - m.minX) / m.step), gz = Mathf.FloorToInt((z - m.minZ) / m.step);
                if (gx < 0 || gz < 0 || gx >= m.width || gz >= m.height || !m.wet[gz * m.width + gx]) continue;
                if (!m.sampler.Sample(x, z, out Vector3 p, out Vector3 normal) ||
                    m.level - p.y < 0.04f || m.level - p.y > maximumDepth || Vector3.Angle(Vector3.up, normal) > 60f) continue;
                float rx = spacing * Mathf.Lerp(0.62f, 0.72f, (float)random.NextDouble());
                float rz = spacing * Mathf.Lerp(0.62f, 0.72f, (float)random.NextDouble());
                float h = spacing * Mathf.Lerp(0.19f, 0.31f, (float)random.NextDouble());
                float radius = Mathf.Max(rx, rz);
                if (IsExcluded(p, radius) || !occupied.IsFree(p, radius * 0.75f)) continue;
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal) *
                    Quaternion.Euler(0, (float)random.NextDouble() * 360f, 0);
                Vector3 scale = new Vector3(rx, h, rz);
                Matrix4x4 matrix = Matrix4x4.TRS(p, rotation, scale);
                float delta = float.PositiveInfinity;
                bool fits = true;
                for (int side = 0; side < 6; side++)
                {
                    Vector3 low = matrix.MultiplyPoint3x4(RiverHabitatRuntime.StoneLowerPoint(side));
                    Vector3 edge = matrix.MultiplyPoint3x4(new Vector3(
                        Mathf.Cos(side * Mathf.PI / 3f), 0.16f, Mathf.Sin(side * Mathf.PI / 3f)));
                    if (!m.sampler.Sample(low.x, low.z, out Vector3 surface, out _) ||
                        !m.sampler.Sample(edge.x, edge.z, out Vector3 outer, out _) ||
                        outer.y > m.level - 0.02f || surface.y > m.level - 0.02f) { fits = false; break; }
                    delta = Mathf.Min(delta, surface.y - low.y);
                }
                if (!fits) continue;
                p.y += delta - h * 0.12f;
                result.Add(new RiverHabitatRuntime.Stone
                {
                    position = parent.InverseTransformPoint(p), rotation = Quaternion.Inverse(parent.rotation) * rotation,
                    scale = scale, shade = random.Next(3)
                });
                if (result.Count > maximumPebbles) return result;
            }
        }
        return result;
    }

    private List<RiverHabitatRuntime.Fish> CreateFishRoutes(Map m, Transform parent, RiverHabitatRuntime.WaterVolume volume)
    {
        List<RiverHabitatRuntime.Fish> result = new List<RiverHabitatRuntime.Fish>();
        System.Random random = new System.Random(unchecked(seed ^ 8675309));
        SpatialHash reservations = new SpatialHash();
        for (int fishIndex = 0; fishIndex < fishCount; fishIndex++)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                if (attempt % 8 == 0) CheckCancel("Percorsi sicuri per i pesci: " + result.Count,
                    0.88f + 0.11f * fishIndex / Mathf.Max(1, fishCount));
                int cell = m.waterCells[random.Next(m.waterCells.Count)];
                Vector3 center = m.PointInCell(cell, (float)random.NextDouble(), (float)random.NextDouble());
                if (!volume.SampleGround(center.x, center.z, out float bottom)) continue;
                float length = Mathf.Lerp(fishLength.x, fishLength.y, (float)random.NextDouble());
                float guard = length * 0.90f;
                float low = bottom + volume.bottomClearance + volume.gravelHeight + guard;
                float high = m.level - 0.12f - guard;
                if (low > high) continue;
                center.y = Mathf.Clamp(m.level - 0.7f - guard - (float)random.NextDouble() * 0.6f, low, high);
                float radius = Mathf.Lerp(1.5f, 4.5f, (float)random.NextDouble()) + guard;
                if (!reservations.IsFree(center, radius + guard)) continue;
                List<Vector3> route = new List<Vector3>();
                float phase = (float)random.NextDouble() * Mathf.PI * 2f;
                bool safe = true;
                for (int i = 0; i < 24 && safe; i++)
                {
                    float angle = phase + i * Mathf.PI / 12f;
                    Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                    if (!volume.FishFits(point, guard)) { safe = false; break; }
                    route.Add(point);
                }
                // Validate the straight interpolation as well as the waypoints.
                for (int i = 0; i < route.Count && safe; i++)
                {
                    Vector3 from = route[i], to = route[(i + 1) % route.Count];
                    int samples = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(from, to) / 0.25f));
                    for (int j = 1; j < samples; j++)
                        if (!volume.FishFits(Vector3.Lerp(from, to, j / (float)samples), guard)) { safe = false; break; }
                }
                if (!safe) continue;
                for (int i = 0; i < route.Count; i++) route[i] = parent.InverseTransformPoint(route[i]);
                result.Add(new RiverHabitatRuntime.Fish
                {
                    prefab = fishPrefab, modelEuler = fishModelEuler, route = route.ToArray(), length = length,
                    speed = Mathf.Lerp(fishSpeed.x, fishSpeed.y, (float)random.NextDouble()), phase = phase
                });
                reservations.Add(center, radius + guard);
                break;
            }
        }
        return result;
    }

    private static Material GetDefaultMaterial(string name, Color color)
    {
        const string folder = "Assets/Generated/RiverHabitat";
        string path = folder + "/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing) return existing;
        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Generated", "RiverHabitat");
        string pipeline = GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.GetType().Name : "";
        string shaderName = pipeline.Contains("Universal") ? "Universal Render Pipeline/Lit" :
            pipeline.Contains("HDRender") ? "HDRP/Lit" : "Standard";
        Shader shader = Shader.Find(shaderName);
        if (!shader) throw new InvalidOperationException("Shader " + shaderName + " non trovato. Assegna i materiali nell'interfaccia.");
        Material material = new Material(shader) { name = name };
        foreach (string property in new[] { "_BaseColor", "_Color" })
            if (material.HasProperty(property)) material.SetColor(property, color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.2f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.2f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Bounds WorldBoxBounds(BoxCollider zone)
    {
        Bounds b = new Bounds(zone.transform.TransformPoint(zone.center), Vector3.zero);
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 q = zone.center + Vector3.Scale(zone.size * 0.5f, new Vector3(
                (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
            b.Encapsulate(zone.transform.TransformPoint(q));
        }
        return b;
    }

    private bool FootprintFits(Map m, Vector3 p, float radius, int kind)
    {
        // Le rocce di riva possono sporgere parzialmente in acqua.
        if (kind == 1) return true;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 0.25f;
            if (!m.sampler.Sample(p.x + Mathf.Cos(angle) * radius,
                p.z + Mathf.Sin(angle) * radius, out Vector3 q, out _)) return false;
            if (kind == 0 || kind == 4)
            {
                if (q.y > m.level - 0.02f) return false;
                if (kind == 4 && m.level - q.y > aquaticMaximumDepth) return false;
            }
            else if (q.y < m.level + 0.02f) return false;
        }
        return true;
    }

    private static bool VisibleMesh(Renderer renderer)
    {
        return renderer && renderer.enabled && renderer.gameObject.activeInHierarchy &&
            (renderer is MeshRenderer || renderer is SkinnedMeshRenderer);
    }

    private static bool GetBounds(Renderer[] renderers, out Bounds bounds)
    {
        bounds = new Bounds(); bool found = false;
        foreach (Renderer renderer in renderers)
        {
            if (!VisibleMesh(renderer)) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found;
    }

    private bool IsExcluded(Vector3 point, float radius)
    {
        foreach (BoxCollider zone in exclusionZones)
        {
            if (!zone) continue;
            // Le bounds sono calcolate dai vertici: funzionano anche a Collider spento.
            // AABB in XZ conservativa anche con BoxCollider ruotati.
            Bounds b = new Bounds(zone.transform.TransformPoint(zone.center), Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 q = zone.center + Vector3.Scale(zone.size * 0.5f, new Vector3(
                    (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                b.Encapsulate(zone.transform.TransformPoint(q));
            }
            float dx = Mathf.Max(b.min.x - point.x, 0f, point.x - b.max.x);
            float dz = Mathf.Max(b.min.z - point.z, 0f, point.z - b.max.z);
            float margin = radius + exclusionMargin;
            if (dx * dx + dz * dz <= margin * margin) return true;
        }
        return false;
    }

    private sealed class SpatialHash
    {
        private const float CellSize = 4f;
        private struct Entry { public Vector2 position; public float radius; }
        private readonly Dictionary<Vector2Int, List<Entry>> cells = new Dictionary<Vector2Int, List<Entry>>();
        private float largestRadius;
        private static Vector2Int Key(Vector3 p)
        { return new Vector2Int(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize)); }
        public bool IsFree(Vector3 p, float radius)
        {
            Vector2Int key = Key(p);
            int reach = Mathf.CeilToInt((radius + largestRadius + 0.04f) / CellSize);
            Vector2 point = new Vector2(p.x, p.z);
            for (int z = -reach; z <= reach; z++)
                for (int x = -reach; x <= reach; x++)
                    if (cells.TryGetValue(key + new Vector2Int(x, z), out List<Entry> entries))
                        foreach (Entry entry in entries)
                        {
                            float gap = radius + entry.radius + 0.04f;
                            if ((point - entry.position).sqrMagnitude < gap * gap) return false;
                        }
            return true;
        }
        public void Add(Vector3 p, float radius)
        {
            Vector2Int key = Key(p);
            if (!cells.TryGetValue(key, out List<Entry> entries))
            { entries = new List<Entry>(); cells.Add(key, entries); }
            entries.Add(new Entry { position = new Vector2(p.x, p.z), radius = radius });
            largestRadius = Mathf.Max(largestRadius, radius);
        }
    }

    private Transform FindOutput()
    {
        if (!terrain) return null;
        Transform root = terrain.transform.Find(OutputName);
        if (!root) return null;
        // Accept the original four folders as well, so V2 replaces V1 safely.
        for (int i = 0; i < 4; i++) if (!root.Find(FolderNames[i])) return null;
        foreach (Transform child in root)
            if (Array.IndexOf(FolderNames, child.name) < 0) return null;
        return root;
    }

    private void RemoveOutput()
    {
        Transform root = FindOutput();
        if (!root) return;
        Undo.DestroyObjectImmediate(root.gameObject);
        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        message = "Decorazioni rimosse. Cmd+Z / Ctrl+Z per ripristinarle.";
        messageType = MessageType.Info;
    }

    private static void CheckCancel(string label, float progress)
    {
        if (EditorUtility.DisplayCancelableProgressBar("Decora fiume", label, Mathf.Clamp01(progress)))
            throw new OperationCanceledException();
    }

    private void DrawPreview(SceneView view)
    {
        if (!showPreview || map == null || !terrain || !water || Event.current.type != EventType.Repaint) return;
        Color oldColor = Handles.color;
        CompareFunction oldTest = Handles.zTest;
        Handles.zTest = CompareFunction.Always;
        DrawPreviewCells(map.waterCells, new Color(0.1f, 0.65f, 1f, 0.8f));
        DrawPreviewCells(map.bankCells, new Color(1f, 0.65f, 0.15f, 0.8f));
        Handles.color = oldColor; Handles.zTest = oldTest;
    }

    private void DrawPreviewCells(List<int> cells, Color color)
    {
        Handles.color = color;
        int stride = Mathf.Max(1, Mathf.CeilToInt(cells.Count / 1600f));
        for (int i = 0; i < cells.Count; i += stride)
        {
            Vector3 p = map.PointInCell(cells[i], 0.5f, 0.5f);
            p.y = Mathf.Max(p.y, map.level) + 0.1f;
            Handles.DotHandleCap(0, p, Quaternion.identity, Mathf.Max(0.15f, map.step * 0.28f), EventType.Repaint);
        }
    }
}
#endif
