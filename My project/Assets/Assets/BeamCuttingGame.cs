using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Level02.BridgeMission
{
    /// <summary>
    /// Minigioco "Help the woodcutter cut the beams!" tutto dentro la scena (nessuna scena/schermata nuova).
    /// Un'unica trave poggia su due cavalletti con dei segni rossi nei punti di taglio.
    /// Un cursore va avanti e indietro sulla trave con l'ascia sospesa sopra: SPAZIO / click lo ferma
    /// e fa calare l'ascia. Se il cursore e dentro la zona verde del prossimo segno, la trave viene tagliata lì.
    /// Dopo l'ultimo taglio scatta onCompleted / Completed.
    /// Mettere questo file in Assets/Level02/BridgeQuest/Scripts (NON in una cartella Editor).
    /// </summary>
    public class BeamCuttingGame : MonoBehaviour
    {
        [Header("References")]
        public playercontroller player;
        [Tooltip("Materiale base (es. BrokenWood). Se vuoto ne viene creato uno automatico.")]
        public Material woodMaterial;

        [Header("Beam")]
        public float length = 3.5f;
        public float thickness = .28f;
        public float supportHeight = .6f;
        [Tooltip("Posizioni dei tagli lungo la trave: 0 = estremo sinistro, 1 = destro. 3 valori = 3 tagli = 4 travi; 2 valori = 3 travi.")]
        public float[] cutFractions = { .25f, .5f, .75f };
        public float kerf = .06f;
        public float axeHover = .9f;

        [Header("Timing game")]
        [Tooltip("Velocità del cursore in lunghezze-trave al secondo.")]
        public float baseSpeed = .65f;
        public float speedIncrease = .2f;
        [Tooltip("Mezza larghezza della zona verde, in frazione della lunghezza della trave.")]
        [Range(.01f, .2f)] public float hitTolerance = .05f;
        public float missPause = .5f;

        [Header("Interaction")]
        public float interactDistance = 4f;
        public float labelDistance = 14f;
        public bool freezePlayer = true;
        public bool available = true;
        [Tooltip("Se falso, E non avvia il gioco da solo: lo avvia il controller della missione chiamando Begin().")]
        public bool selfInteract = true;
        [Tooltip("Opzionale: se impostato decide quando l'utente vuole uscire (es. ConsumeEscape del controller).")]
        public System.Func<bool> cancelCheck;

        [Header("Texts")]
        public string title = "Help the woodcutter cut the beams!";
        public string promptStart = "Press E to help";
        public string playHint = "SPACE / click: drop the axe on the green zone  (Esc: leave)";
        public string okText = "Nice cut!";
        public string missText = "Missed! Wait for the green zone.";
        public string doneText = "Well done! The beams are ready.";

        [Header("Events")]
        public UnityEvent onCompleted;
        public event System.Action Completed;
        public event System.Action Started;
        public event System.Action Stopped;
        /// <summary>Numero di travi ottenute alla fine (tagli + 1).</summary>
        public int PieceCount { get { return Mathf.Max(1, cutFractions.Length) + 1; } }

        public bool IsCompleted { get; private set; }
        public bool IsPlaying { get; private set; }
        public IReadOnlyList<Transform> Pieces { get { return segments; } }

        static readonly Color cursorColor = new Color(1f, .85f, .1f);

        Transform visual, cursor, axe;
        Material cursorMat, chipMat;
        TMPro.TextMeshPro label;
        readonly List<Transform> segments = new List<Transform>();
        GameObject[] zones, marks;
        float[] targets, edges, kerfNow;
        float top, phase, cursorFraction = .5f, feedbackUntil, doneAt;
        int cutIndex;
        bool busy, prevPlayerEnabled;
        string feedback = "";

        void Awake() { Build(); }

        void OnDisable() { if (IsPlaying) EndPlay(); }

        // ---------- API pubblica ----------
        public void SetAvailable(bool value) { available = value; }

        /// <summary>Avvia il minigioco (chiamabile anche da dialogo/controller).</summary>
        public void Begin()
        {
            if (IsCompleted || IsPlaying) return;
            if (player == null) player = FindFirstObjectByType<playercontroller>();
            IsPlaying = true; busy = false;
            if (freezePlayer && player != null) { prevPlayerEnabled = player.enabled; player.enabled = false; }
            cursor.gameObject.SetActive(true);
            axe.gameObject.SetActive(true);
            RefreshMarks();
            if (Started != null) Started();
        }

        public void ResetGame()
        {
            if (IsPlaying) EndPlay();
            Build();
        }

        // ---------- Costruzione ----------
        void Build()
        {
            StopAllCoroutines();
            if (visual != null) Destroy(visual.gameObject);
            if (label != null) Destroy(label.gameObject);
            segments.Clear();
            cutIndex = 0; phase = 0; busy = false; IsCompleted = false; feedback = "";

            int n = Mathf.Max(1, cutFractions.Length);
            targets = new float[n];
            for (int i = 0; i < n; i++)
                targets[i] = Mathf.Clamp(i < cutFractions.Length ? cutFractions[i] : .5f, .08f, .92f);
            System.Array.Sort(targets);
            edges = new float[n + 2];
            edges[0] = -length * .5f; edges[n + 1] = length * .5f;
            for (int i = 0; i < n; i++) edges[i + 1] = (targets[i] - .5f) * length;
            kerfNow = new float[n];
            top = supportHeight + thickness;

            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);

            Material wood = Tint(woodMaterial, new Color(.62f, .40f, .20f));
            Material dark = Tint(woodMaterial, new Color(.28f, .16f, .07f));
            Material green = Tint(woodMaterial, new Color(.25f, .85f, .30f));
            Material red = Tint(woodMaterial, new Color(.80f, .12f, .10f));
            Material steel = Tint(woodMaterial, new Color(.62f, .64f, .68f));
            cursorMat = Tint(woodMaterial, cursorColor);
            chipMat = wood;

            for (int i = 0; i <= n; i++) segments.Add(Part("Beam piece " + i, visual, Vector3.zero, Vector3.one, wood));
            RefreshSegments();

            float legX = length * .5f - .4f;
            Part("Support A", visual, new Vector3(-legX, supportHeight * .5f, 0), new Vector3(.2f, supportHeight, thickness * .9f), dark);
            Part("Support B", visual, new Vector3(legX, supportHeight * .5f, 0), new Vector3(.2f, supportHeight, thickness * .9f), dark);

            zones = new GameObject[n]; marks = new GameObject[n];
            float zoneWidth = hitTolerance * 2f * length;
            for (int i = 0; i < n; i++)
            {
                zones[i] = Part("Cut zone " + i, visual, new Vector3(edges[i + 1], top + .002f, 0), new Vector3(zoneWidth, .004f, thickness * .94f), green).gameObject;
                marks[i] = Part("Cut mark " + i, visual, new Vector3(edges[i + 1], top + .005f, 0), new Vector3(.025f, .006f, thickness), red).gameObject;
            }

            cursor = Part("Cursor", visual, Vector3.zero, new Vector3(.035f, .03f, thickness * 1.3f), cursorMat);
            axe = new GameObject("Axe").transform;
            axe.SetParent(visual, false);
            // Lama con il taglio verso il basso, perpendicolare alla trave.
            Part("Axe head", axe, new Vector3(0, .09f, 0), new Vector3(.03f, .18f, thickness * 1.1f), steel);
            Part("Axe handle", axe, new Vector3(0, .63f, 0), new Vector3(.05f, .9f, .05f), dark);
            cursor.gameObject.SetActive(false);
            axe.gameObject.SetActive(false);
            RefreshMarks();
            PlaceCursor(.5f);

            GameObject go = new GameObject("Label");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, top + 1.9f, 0);
            label = go.AddComponent<TMPro.TextMeshPro>();
            label.fontSize = 3.2f;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.color = Color.white;
            label.outlineColor = new Color32(0, 0, 0, 255);
            label.outlineWidth = .25f;
            label.rectTransform.sizeDelta = new Vector2(7, 2.5f);
            go.SetActive(false);
        }

        Transform Part(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c); // niente collider: non blocca il giocatore
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        static Material Tint(Material source, Color color)
        {
            Material m;
            if (source != null) m = new Material(source);
            else
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit");
                if (s == null) s = Shader.Find("Standard");
                m = new Material(s);
            }
            m.color = color;
            return m;
        }

        void RefreshSegments()
        {
            int n = segments.Count - 1; // numero di tagli
            for (int i = 0; i <= n; i++)
            {
                float l = edges[i] + (i > 0 ? kerfNow[i - 1] * .5f : 0f);
                float r = edges[i + 1] - (i < n ? kerfNow[i] * .5f : 0f);
                Transform s = segments[i];
                s.localPosition = new Vector3((l + r) * .5f, supportHeight + thickness * .5f, 0);
                s.localScale = new Vector3(Mathf.Max(.01f, r - l), thickness, thickness);
            }
        }

        void RefreshMarks()
        {
            for (int i = 0; i < marks.Length; i++)
            {
                marks[i].SetActive(i >= cutIndex);
                zones[i].SetActive(i == cutIndex);
            }
        }

        void PlaceCursor(float fraction)
        {
            float x = (fraction - .5f) * length;
            cursor.localPosition = new Vector3(x, top + .02f, 0);
            axe.localPosition = new Vector3(x, top + axeHover, 0);
        }

        // ---------- Loop ----------
        void Update()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<playercontroller>();
                if (player == null) return;
            }
            float dist = Vector3.Distance(player.transform.position, transform.position);

            if (IsCompleted)
            {
                bool show = Time.time < doneAt + 5f && dist <= labelDistance;
                label.gameObject.SetActive(show);
                if (show) label.text = doneText;
                return;
            }
            if (!available) { label.gameObject.SetActive(false); return; }

            label.gameObject.SetActive(IsPlaying || dist <= labelDistance);

            if (IsPlaying) { PlayUpdate(); return; }

            bool near = dist <= interactDistance;
            label.text = near && selfInteract ? title + "\n<size=70%>" + promptStart + "</size>" : title;
            if (selfInteract && near && InteractPressed()) Begin();
        }

        void PlayUpdate()
        {
            if (!busy && CancelRequested()) { EndPlay(); return; }

            string status = Time.time < feedbackUntil
                ? feedback
                : "Cut " + (cutIndex + 1) + "/" + targets.Length + "\n" + playHint;
            label.text = title + "\n<size=70%>" + status + "</size>";

            if (busy) return;
            float speed = baseSpeed + speedIncrease * cutIndex;
            phase += speed * Time.deltaTime;
            cursorFraction = Mathf.PingPong(phase, 1f);
            PlaceCursor(cursorFraction);
            if (ActionPressed()) StartCoroutine(Drop());
        }

        void LateUpdate()
        {
            // Etichetta sempre rivolta verso la camera (solo rotazione orizzontale).
            Camera cam = Camera.main;
            if (cam == null || label == null || !label.gameObject.activeSelf) return;
            Vector3 d = label.transform.position - cam.transform.position;
            d.y = 0;
            if (d.sqrMagnitude > .001f) label.transform.rotation = Quaternion.LookRotation(d);
        }

        // ---------- Taglio ----------
        IEnumerator Drop()
        {
            busy = true;
            float x0 = (cursorFraction - .5f) * length;
            bool hit = Mathf.Abs(cursorFraction - targets[cutIndex]) <= hitTolerance;
            float x1 = hit ? edges[cutIndex + 1] : x0; // se colpisce, l'ascia si assesta esattamente sul segno
            float startY = top + axeHover, endY = top - .06f;

            for (float t = 0; t < 1f; t += Time.deltaTime / .12f)
            {
                axe.localPosition = new Vector3(Mathf.Lerp(x0, x1, t), Mathf.Lerp(startY, endY, t * t), 0);
                yield return null;
            }
            axe.localPosition = new Vector3(x1, endY, 0);

            if (hit)
            {
                SpawnChips(visual.TransformPoint(new Vector3(x1, top, 0)));
                StartCoroutine(OpenKerf(cutIndex));
                SetFeedback(okText);
                cutIndex++;
                RefreshMarks();
                yield return new WaitForSeconds(.3f);
            }
            else
            {
                SetFeedback(missText);
                cursorMat.color = Color.red;
                yield return new WaitForSeconds(.2f);
            }

            yield return StartCoroutine(Raise(x1));

            if (hit && cutIndex >= targets.Length) { Finish(); yield break; }
            if (!hit)
            {
                yield return new WaitForSeconds(missPause);
                cursorMat.color = cursorColor;
            }
            busy = false;
        }

        IEnumerator Raise(float x)
        {
            float fromY = top - .06f, toY = top + axeHover;
            for (float t = 0; t < 1f; t += Time.deltaTime / .2f)
            {
                axe.localPosition = new Vector3(x, Mathf.Lerp(fromY, toY, t), 0);
                yield return null;
            }
            axe.localPosition = new Vector3(x, toY, 0);
        }

        IEnumerator OpenKerf(int k)
        {
            for (float t = 0; t < 1f; t += Time.deltaTime / .2f)
            {
                kerfNow[k] = Mathf.Lerp(0f, kerf, t);
                RefreshSegments();
                yield return null;
            }
            kerfNow[k] = kerf;
            RefreshSegments();
        }

        void SpawnChips(Vector3 worldPos)
        {
            for (int i = 0; i < 8; i++)
            {
                Transform c = Part("Chip", visual, Vector3.zero, Vector3.one * Random.Range(.03f, .07f), chipMat);
                c.position = worldPos;
                Vector3 v = transform.TransformDirection(new Vector3(Random.Range(-1f, 1f), Random.Range(1.5f, 3f), Random.Range(-1.5f, 1.5f)));
                StartCoroutine(Chip(c, v));
            }
        }

        IEnumerator Chip(Transform t, Vector3 velocity)
        {
            float life = 0f;
            while (life < .8f && t != null)
            {
                velocity += Physics.gravity * Time.deltaTime;
                t.position += velocity * Time.deltaTime;
                t.Rotate(300f * Time.deltaTime, 200f * Time.deltaTime, 0f);
                life += Time.deltaTime;
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        void SetFeedback(string text) { feedback = text; feedbackUntil = Time.time + 1.1f; }

        void Finish()
        {
            IsCompleted = true;
            doneAt = Time.time;
            EndPlay();
            if (Completed != null) Completed();
            if (onCompleted != null) onCompleted.Invoke();
        }

        void EndPlay()
        {
            bool wasPlaying = IsPlaying;
            IsPlaying = false;
            busy = false;
            if (freezePlayer && player != null) player.enabled = prevPlayerEnabled;
            if (cursor != null) cursor.gameObject.SetActive(false);
            if (axe != null) axe.gameObject.SetActive(false);
            if (wasPlaying && Stopped != null) Stopped();
        }

        // ---------- Input (vecchio e nuovo Input System) ----------
        static bool InteractPressed()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }

        static bool ActionPressed()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
#endif
        }

        bool CancelRequested()
        {
            if (cancelCheck != null) return cancelCheck();
            return CancelPressedDefault();
        }

        static bool CancelPressedDefault()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
