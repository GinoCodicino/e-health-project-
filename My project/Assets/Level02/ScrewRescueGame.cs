using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Level02.DeerRescue
{
    // Tiro alla fune con fisica, tutto in scena (nessuna schermata).
    // Giocatore e Mia tirano il tronco dallo stesso lato. Da solo nessuno dei due
    // riesce a smuoverlo (attrito di primo distacco): bisogna tirare insieme a Mia
    // mentre lei "dà uno strappo". Il tronco poi scivola finche' la spinta supera l'attrito.
    // Mantiene l'API richiesta da DeerRescueController (Open, IsOpen, Close).
    public sealed class ScrewRescueGame : MonoBehaviour
    {
        [Header("Riferimenti scena")]
        public Transform fallenTree;
        public Transform mia;
        public Transform ropeOnTree;
        [Tooltip("Opzionale: punto dove Mia va a mettersi per tirare. Se vuoto resta dove si trova.")]
        public Transform miaPullPoint;

        [Header("Direzione e distanza")]
        [Tooltip("Direzione in cui scivola il tronco: verso il lato dove tirate tu e Mia.")]
        public Vector3 treeMoveDirection = Vector3.forward;
        [Min(0.1f)] public float totalMoveDistance = 2.2f;

        [Header("Fisica del tronco")]
        [Min(1f)] public float trunkMass = 60f;
        [Tooltip("Forza minima per smuovere il tronco da fermo. Deve essere maggiore della forza di UNO solo dei due.")]
        public float breakawayForce = 400f;
        [Tooltip("Attrito mentre scivola (tienilo un po' sotto la forza di primo distacco).")]
        public float slideFriction = 380f;
        [Min(0f)] public float drag = 40f;

        [Header("Forze")]
        public float playerPullForce = 300f;
        public float miaPullForce = 330f;
        [Tooltip("Ogni quanti secondi Mia ripete lo strappo.")]
        [Min(0.5f)] public float miaHeavePeriod = 2.2f;
        [Tooltip("Quota del ciclo in cui Mia tira forte.")]
        [Range(0.2f, 0.8f)] public float miaHeaveFraction = 0.55f;
        [Tooltip("Forza di Mia a riposo, in percentuale della sua forza massima.")]
        [Range(0f, 0.5f)] public float miaRestForceFraction = 0.1f;

        [Header("Fiato del giocatore")]
        [Min(0.05f)] public float staminaDrainPerSecond = 0.30f;
        [Min(0.05f)] public float staminaRegenPerSecond = 0.55f;
        [Range(0f, 1f)] public float staminaRecoverThreshold = 0.3f;

        [Header("Fune")]
        [Min(0.01f)] public float ropeWidth = 0.035f;
        public Color ropeColor = new Color(0.72f, 0.57f, 0.32f);
        public Vector3 playerHandOffset = new Vector3(0f, 1.15f, 0.25f);
        public Vector3 miaHandOffset = new Vector3(0f, 1.15f, 0.25f);
        public Vector3 treeRopeOffset = new Vector3(0f, 0.7f, 0f);
        [Tooltip("Quanto pende la fune quando e' allentata.")]
        [Min(0f)] public float ropeSag = 0.35f;

        [Header("Mia")]
        [Min(0.1f)] public float miaWalkSpeed = 2.5f;
        [Tooltip("Quanto Mia si piega all'indietro quando tira al massimo.")]
        [Min(0f)] public float miaLeanDistance = 0.25f;

        public bool IsOpen { get { return active; } }

        const int RopePoints = 14;

        playercontroller currentPlayer;
        Transform playerTransform;
        Action<bool> resultCallback;

        LineRenderer playerRope;
        LineRenderer miaRope;
        GameObject ropeRoot;

        Rigidbody trunkBody;
        bool bodyWasKinematic;
        Vector3 treeOrigin;
        bool treeOriginSaved;
        Vector3 moveDirection;
        Vector3 miaBasePosition;

        // Stato della simulazione
        float distance;          // quanto ha scivolato il tronco (m)
        float velocity;          // velocita' lungo la direzione (m/s)
        float heaveClock;
        float playerForceNow, miaForceNow;
        bool miaHeaving;
        float ropeTension;       // 0 fune lenta, 1 fune tesa (solo grafica)

        // Giocatore
        float stamina = 1f;
        bool exhausted, pulling, armed;

        bool active, finished, callbackSent, miaInPlace;
        GUIStyle labelStyle, smallStyle;

        Vector3 TreePoint
        {
            get
            {
                if (fallenTree == null) return Vector3.zero;
                return ropeOnTree != null
                    ? ropeOnTree.position
                    : fallenTree.position + fallenTree.TransformDirection(treeRopeOffset);
            }
        }

        public void Open(playercontroller who, Action<bool> callback)
        {
            if (active || who == null) return;

            DeerRescueController controller = GetComponent<DeerRescueController>();
            if (mia == null && controller != null) mia = controller.ranger;

            if (fallenTree == null)
            {
                Debug.LogError("ScrewRescueGame: assegna il tronco nel campo Fallen Tree dell'Inspector.");
                return;
            }

            if (!treeOriginSaved) { treeOrigin = fallenTree.position; treeOriginSaved = true; }
            moveDirection = treeMoveDirection.sqrMagnitude > 0.001f ? treeMoveDirection.normalized : Vector3.forward;

            // Se il tronco ha un Rigidbody lo muovo io: lo rendo cinematico per la durata del gioco.
            trunkBody = fallenTree.GetComponent<Rigidbody>();
            if (trunkBody != null) { bodyWasKinematic = trunkBody.isKinematic; trunkBody.isKinematic = true; }

            currentPlayer = who;
            playerTransform = who.transform;
            resultCallback = callback;
            callbackSent = false;
            finished = false;
            velocity = 0f;
            stamina = 1f;
            exhausted = false;
            pulling = false;
            armed = false; // il tasto E che ha aperto il gioco non deve contare come trazione
            heaveClock = -0.8f; // piccola pausa prima del primo strappo di Mia
            playerForceNow = miaForceNow = 0f;

            miaInPlace = mia == null || miaPullPoint == null;
            if (mia != null) miaBasePosition = mia.position;

            active = true;
            currentPlayer.SetUiMode(this, true);
            CreateRopes();
        }

        void Update()
        {
            if (!active) return;
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame) { Close(); return; }

            if (!miaInPlace) { WalkMia(); return; }
            if (finished) { pulling = false; return; }

            bool held = kb.eKey.isPressed;
            if (!armed)
            {
                if (!held) armed = true;
                pulling = false;
                return;
            }

            if (exhausted && stamina >= staminaRecoverThreshold) exhausted = false;
            pulling = held && !exhausted && stamina > 0f;

            if (pulling)
            {
                stamina -= staminaDrainPerSecond * Time.deltaTime;
                if (stamina <= 0f) { stamina = 0f; exhausted = true; pulling = false; }
            }
            else
            {
                stamina = Mathf.Min(1f, stamina + staminaRegenPerSecond * Time.deltaTime);
            }
        }

        void WalkMia()
        {
            Vector3 target = miaPullPoint.position;
            target.y = mia.position.y;
            Vector3 delta = target - mia.position;
            if (delta.sqrMagnitude < 0.05f * 0.05f)
            {
                miaInPlace = true;
                miaBasePosition = mia.position;
                return;
            }
            mia.position = Vector3.MoveTowards(mia.position, target, miaWalkSpeed * Time.deltaTime);
            mia.rotation = Quaternion.Slerp(mia.rotation, Quaternion.LookRotation(delta), Time.deltaTime * 8f);
        }

        // La fisica gira a passo fisso: forze -> attrito -> velocita' -> posizione.
        void FixedUpdate()
        {
            if (!active || !miaInPlace || finished) return;

            float dt = Time.fixedDeltaTime;
            heaveClock += dt;

            // Mia tira a ondate: a riposo poco, durante lo strappo con una curva a campana.
            float phase = Mathf.Repeat(heaveClock, miaHeavePeriod) / miaHeavePeriod;
            miaHeaving = heaveClock >= 0f && phase < miaHeaveFraction;
            float rest = miaPullForce * miaRestForceFraction;
            miaForceNow = miaHeaving
                ? Mathf.Max(rest, miaPullForce * Mathf.Sin(Mathf.PI * phase / miaHeaveFraction))
                : rest;
            playerForceNow = pulling ? playerPullForce : 0f;

            float total = playerForceNow + miaForceNow;

            if (velocity <= 0.001f)
            {
                // Fermo: serve superare l'attrito di primo distacco.
                if (total <= breakawayForce) velocity = 0f;
                else velocity = Mathf.Max(0.02f, (total - slideFriction) / trunkMass * dt);
            }
            else
            {
                // In movimento: attrito di scivolamento + resistenza proporzionale alla velocita'.
                float net = total - slideFriction - drag * velocity;
                velocity = Mathf.Max(0f, velocity + net / trunkMass * dt);
            }

            distance = Mathf.Min(totalMoveDistance, distance + velocity * dt);
            ApplyTrunk();

            if (distance >= totalMoveDistance) StartCoroutine(Win());
        }

        void ApplyTrunk()
        {
            Vector3 p = treeOrigin + moveDirection * distance;
            if (trunkBody != null) trunkBody.MovePosition(p);
            else fallenTree.position = p;
        }

        IEnumerator Win()
        {
            finished = true;
            velocity = 0f;
            yield return new WaitForSeconds(1.5f);
            FinishSuccess();
        }

        void LateUpdate()
        {
            if (!active) return;

            float total = playerForceNow + miaForceNow;
            float target = Mathf.Clamp01(total / Mathf.Max(1f, breakawayForce));
            ropeTension = Mathf.Lerp(ropeTension, finished ? 0.2f : target, Time.deltaTime * 10f);

            if (mia != null && miaInPlace)
            {
                Vector3 toTree = TreePoint - miaBasePosition;
                toTree.y = 0f;
                if (toTree.sqrMagnitude > 0.001f)
                {
                    Vector3 dir = toTree.normalized;
                    float lean = finished ? 0f : (miaForceNow / Mathf.Max(1f, miaPullForce)) * miaLeanDistance;
                    mia.position = miaBasePosition - dir * lean;
                    mia.rotation = Quaternion.Slerp(mia.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
                }
            }
            UpdateRopes();
        }

        void CreateRopes()
        {
            if (ropeRoot != null) Destroy(ropeRoot);
            ropeRoot = new GameObject("Fune_Salvataggio");
            playerRope = MakeRope("Fune_Giocatore");
            miaRope = mia != null ? MakeRope("Fune_Mia") : null;
            UpdateRopes();
        }

        LineRenderer MakeRope(string objectName)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(ropeRoot.transform, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.positionCount = RopePoints;
            line.useWorldSpace = true;
            line.startWidth = ropeWidth;
            line.endWidth = ropeWidth;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = ropeColor;
            line.endColor = ropeColor;
            return line;
        }

        void UpdateRopes()
        {
            if (fallenTree == null || playerTransform == null || playerRope == null) return;
            Vector3 treePoint = TreePoint;
            DrawRope(playerRope, playerTransform.position + playerTransform.TransformDirection(playerHandOffset), treePoint);
            if (mia != null && miaRope != null)
                DrawRope(miaRope, mia.position + mia.TransformDirection(miaHandOffset), treePoint);
        }

        // La fune pende quando e' allentata e si tende con la forza applicata.
        void DrawRope(LineRenderer line, Vector3 a, Vector3 b)
        {
            float sag = (1f - ropeTension) * ropeSag;
            for (int i = 0; i < RopePoints; i++)
            {
                float k = i / (RopePoints - 1f);
                Vector3 p = Vector3.Lerp(a, b, k);
                p.y -= Mathf.Sin(k * Mathf.PI) * sag;
                line.SetPosition(i, p);
            }
        }

        string StatusText()
        {
            if (!miaInPlace) return "Mia is getting into position...";
            if (finished) return "You pulled together and freed the deer!";
            if (exhausted) return "Out of breath! Let go and recover.";
            if (miaHeaving) return pulling ? "Together! Keep pulling!" : "Mia is heaving! Hold E now!";
            return "Mia is catching her breath... get ready";
        }

        void Fill(Rect area, float from, float to, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(area.x + area.width * from, area.y, area.width * Mathf.Max(0f, to - from), area.height), Texture2D.whiteTexture);
        }

        // Solo un piccolo pannello in basso: il mondo di gioco resta sempre visibile.
        void OnGUI()
        {
            if (!active) return;

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.alignment = TextAnchor.MiddleCenter;
                labelStyle.fontSize = 20;
                labelStyle.fontStyle = FontStyle.Bold;
                labelStyle.normal.textColor = Color.white;
                smallStyle = new GUIStyle(GUI.skin.label);
                smallStyle.alignment = TextAnchor.MiddleRight;
                smallStyle.fontSize = 13;
                smallStyle.normal.textColor = Color.white;
            }

            float w = 440f;
            float x = (Screen.width - w) * 0.5f;
            float y0 = Screen.height - 160f;
            Color old = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x - 80f, y0 - 10f, w + 160f, 150f), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(x - 80f, y0, w + 160f, 28f), StatusText(), labelStyle);

            // Forza: blu = tu, arancio = Mia. La linea bianca e' la forza che serve per smuovere il tronco.
            float maxForce = Mathf.Max(1f, playerPullForce + miaPullForce);
            Rect force = new Rect(x, y0 + 34f, w, 22f);
            Fill(force, 0f, 1f, new Color(0.18f, 0.18f, 0.18f, 1f));
            Fill(force, 0f, playerForceNow / maxForce, new Color(0.3f, 0.6f, 1f, 1f));
            Fill(force, playerForceNow / maxForce, (playerForceNow + miaForceNow) / maxForce, new Color(1f, 0.6f, 0.2f, 1f));
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(force.x + force.width * Mathf.Clamp01(breakawayForce / maxForce) - 1.5f, force.y - 3f, 3f, force.height + 6f), Texture2D.whiteTexture);
            GUI.Label(new Rect(x - 80f, force.y, 75f, force.height), "Force", smallStyle);

            Rect trunk = new Rect(x, y0 + 64f, w, 10f);
            Fill(trunk, 0f, 1f, new Color(0.18f, 0.18f, 0.18f, 1f));
            Fill(trunk, 0f, distance / Mathf.Max(0.01f, totalMoveDistance), new Color(0.3f, 0.85f, 0.35f, 1f));
            GUI.color = Color.white;
            GUI.Label(new Rect(x - 80f, trunk.y - 8f, 75f, 26f), "Trunk", smallStyle);

            Rect breath = new Rect(x, y0 + 80f, w, 10f);
            Fill(breath, 0f, 1f, new Color(0.18f, 0.18f, 0.18f, 1f));
            Fill(breath, 0f, stamina, exhausted ? new Color(0.9f, 0.25f, 0.2f, 1f) : new Color(1f, 0.85f, 0.3f, 1f));
            GUI.color = Color.white;
            GUI.Label(new Rect(x - 80f, breath.y - 8f, 75f, 26f), "Breath", smallStyle);

            GUI.Label(new Rect(x - 80f, y0 + 100f, w + 160f, 26f),
                "Hold E to pull with Mia   (Esc to stop)", labelStyle);
            GUI.color = old;
        }

        void FinishSuccess()
        {
            if (!active || callbackSent) return;
            callbackSent = true;
            EndSession();
            Action<bool> callback = resultCallback;
            resultCallback = null;
            callback?.Invoke(true);
        }

        public void Close()
        {
            if (!active) return;
            if (finished) { FinishSuccess(); return; }

            EndSession();
            if (!callbackSent)
            {
                callbackSent = true;
                Action<bool> callback = resultCallback;
                resultCallback = null;
                callback?.Invoke(false);
            }
        }

        void EndSession()
        {
            active = false;
            velocity = 0f;
            pulling = false;
            if (fallenTree != null && treeOriginSaved) fallenTree.position = treeOrigin + moveDirection * distance;
            if (trunkBody != null) trunkBody.isKinematic = bodyWasKinematic;
            if (mia != null && miaInPlace) mia.position = miaBasePosition;
            if (ropeRoot != null) ropeRoot.SetActive(false);
            if (currentPlayer != null) currentPlayer.SetUiMode(this, false);
        }

        void OnDisable()
        {
            if (active) Close();
        }

        void OnDestroy()
        {
            if (ropeRoot != null) Destroy(ropeRoot);
        }
    }
}