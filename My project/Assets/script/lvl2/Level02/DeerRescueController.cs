using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Level02.BridgeMission;

namespace Level02.DeerRescue
{
    // Esegue le nuove interazioni prima dei vecchi NPC; SetUiMode impedisce aperture doppie.
    [DefaultExecutionOrder(-150)]
    public sealed class DeerRescueController : MonoBehaviour
    {
        [Header("Missioni precedenti")]
        public playercontroller player;
        public ForestQuest mushroomQuest;
        public BridgeQuestController bridgeQuest;
        public BridgeCondition bridgeCondition;
        [Header("Punti del terzo gioco")]
        public Transform encounterPoint;
        public Transform ranger;

        [Header("Test")]
        [Tooltip("Salta ponte e missioni precedenti: Mia si avvicina subito all'avvio, con rete e kit già dati.")]
        public bool testStartFromMiaEncounter;

        public Transform deerPoint;
        public DeerRescueView deerView;

        [Header("Incontro con Mia")]
        [Min(1f)] public float dialogueDistance = 1.9f;
        [Tooltip("Correzione Y se il modello importato guarda di lato.")]
        public float miaModelYaw;
        [Header("Camminata fino al cervo")]
        [Tooltip("Opzionale. Se vuoto, Mia segue le cinque parti del sentiero già presenti in scene2.")]
        public Transform[] escortWaypoints;
        [Min(1f)] public float followDistance = 2.1f;
        [Tooltip("Secondi di campo largo fra i due raccordi di camera.")]
        [Min(0f)] public float wideShotSeconds = 2f;

        [Header("Gioco delle funi davanti al cervo")]
        [Tooltip("OPZIONALE. Dove si mette il giocatore davanti al cervo. Se vuoto viene calcolato in automatico.")]
        public Transform playerStandPoint;
        [Tooltip("OPZIONALE. Dove si mette Mia davanti al cervo. Se vuoto viene calcolato in automatico.")]
        public Transform rangerStandPoint;
        [Tooltip("Distanza dal cervo (automatico).")]
        public float standDistance = 2.2f;
        [Tooltip("Distanza laterale tra giocatore e Mia (automatico).")]
        public float sideOffset = .9f;
        [Tooltip("Velocità con cui il giocatore viene portato davanti al cervo.")]
        public float gatherPlayerSpeed = 3.5f;

        public ScrewRescueGame releaseGame;
        public BacteriaBattleGame careGame;
        public string rangerName = "Mia";
        public float interactionDistance = 3;
        public float encounterRadius = 4;
        public float rangerSpeed = 3;
        [Range(0, .1f)] public float typingSpeed = .025f;
        public RescueStage CurrentStage { get { return progress.Stage; } }
        public bool IsOpen { get { return uiRoot != null && uiRoot.activeSelf; } }
        readonly RescueProgress progress = new RescueProgress();
        QuestEquipmentHud equipment;
        BridgeQuestMarker marker;
        Camera view;
        bool encounterStarted, encounterArrived;
        bool gathering, framingMia, escortPending, escortFinished;
        MiaMotion miaMotion;
        MiaEscortShot escortShot;
        Vector3 lastPlayerPosition, shotHeading;
        bool trackingPlayer;
        float miaSpeed, shotBlend, dialoguePitch;
        // Figli diretti e univoci di Path in scene2. Se il gruppo sposta il sentiero,
        // si leggono le posizioni correnti dei pezzi, senza salvare coordinate assolute.
        static readonly string[] Scene2PathPieces = {
            "Path Piece_7 (25)", "Path Piece_9 (29)", "Path Piece_8 (54)",
            "Path Piece_9 (34)", "Path Piece_16 (37)"
        };
        readonly RaycastHit[] groundHits = new RaycastHit[24];
        CharacterController gatherController;
        Rigidbody gatherBody;
        bool controllerWasEnabled, bodyWasKinematic;

        GameObject uiRoot, page;
        RectTransform canvas;
        TMP_Text speech;
        int lineIndex, closedFrame = -1;
        string[] lines;
        Action afterLines;
        Coroutine typing;
        bool isTyping;

        void Awake()
        {
            if (player == null) player = FindFirstObjectByType<playercontroller>();
            if (bridgeQuest == null) bridgeQuest = FindFirstObjectByType<BridgeQuestController>();
            if (bridgeCondition == null && bridgeQuest != null) bridgeCondition = bridgeQuest.bridge;
            if (mushroomQuest == null && bridgeQuest != null) mushroomQuest = bridgeQuest.mushroomQuest;
            if (mushroomQuest == null) mushroomQuest = FindFirstObjectByType<ForestQuest>();

            if (releaseGame == null) releaseGame = GetComponent<ScrewRescueGame>();
            if (releaseGame == null) releaseGame = gameObject.AddComponent<ScrewRescueGame>();
            if (careGame == null) careGame = GetComponent<BacteriaBattleGame>();
            if (careGame == null) careGame = gameObject.AddComponent<BacteriaBattleGame>();
            equipment = gameObject.AddComponent<QuestEquipmentHud>();
            if (bridgeQuest != null) bridgeQuest.showLegacyInventory = false;
            marker = gameObject.AddComponent<BridgeQuestMarker>();
            view = Camera.main;
            if (player && (!view || !view.transform.IsChildOf(player.transform)))
            {
                Camera playerView = player.GetComponentInChildren<Camera>();
                if (playerView) view = playerView;
            }
            if (testStartFromMiaEncounter) encounterStarted = true;
        }
        bool PreviousComplete { get { return testStartFromMiaEncounter || (bridgeCondition != null && bridgeCondition.IsRepaired && bridgeQuest != null && bridgeQuest.CurrentStage == BridgeStage.Complete); } }
        float Distance(Transform t) { return t == null || player == null ? float.PositiveInfinity : Vector3.Distance(t.position, player.transform.position); }
        bool Near(Transform t) { return Distance(t) <= interactionDistance; }
        Transform RescueMarker => escortFinished && playerStandPoint ? playerStandPoint : deerPoint;
        bool AtRescue => Near(deerPoint) || (escortFinished && Near(RescueMarker));
        void Update()
        {
            miaMotion?.ClearFrame();
            miaSpeed = 0f;
            if (player == null) return;

            progress.GrantPeterGifts(testStartFromMiaEncounter || (mushroomQuest != null ));

            if (bridgeQuest != null) bridgeQuest.showLegacyInventory = false;

            equipment.Show(progress.HasAxe, progress.HasMedicalKit, progress.HasNet, progress.Fish,
                bridgeQuest == null ? 0 : bridgeQuest.RemainingPlanks, !IsOpen && !player.IsGameplayBlocked);
            if (IsOpen)
            {
                marker.Hide();
                if (playerinputcontroller.ConsumeEscape()) { Close(); return; }
                if (isTyping && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ShowAll();
                return;
            }
            if (gathering) { marker.Hide(); return; }
            if (player.IsGameplayBlocked || releaseGame.IsOpen || careGame.IsOpen || closedFrame == Time.frameCount) { marker.Hide(); return; }
            if (escortPending && progress.Stage == RescueStage.FreeDeer) { StartRelease(); return; }
            if (progress.Stage == RescueStage.WaitingForCrossing)
            {
                marker.Hide();
                if (!PreviousComplete) return;
                if (!encounterStarted && Distance(encounterPoint) <= encounterRadius) encounterStarted = true;
                if (encounterStarted && !encounterArrived)
                {
                    if (ranger == null) { encounterArrived = true; Intro(); return; }
                    if (ApproachPlayer()) { encounterArrived = true; Intro(); }
                    return;
                }
                if (encounterArrived && Near(ranger) && PressE()) Intro();
                return;
            }
            UpdateMarker();
            if (!PressE()) return;
            switch (progress.Stage)
            {

                case RescueStage.FreeDeer:
                    if (AtRescue) StartRelease(); break;

                case RescueStage.TreatDeer:
                    if (AtRescue) OpenTreatment(); break;
                case RescueStage.Complete:
                    if (AtRescue) Speak(rangerName, new[] { "The deer can rest here now. Thank you for helping!" }, Close); break;
            }
        }
        // Prende temporaneamente in carico le interazioni, mantenendo intatti gli script precedenti.


        bool PressE() { return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame; }
        void UpdateMarker()
        {
            Transform target = null; string text = "";
            switch (progress.Stage)
            {

                case RescueStage.FreeDeer: target = RescueMarker; text = "Free the trapped deer"; break;

                case RescueStage.TreatDeer: target = RescueMarker; text = "Help the injured deer"; break;
            }
            marker.Show(target, text, view);
        }
        void Page(bool full)
        {
            if (uiRoot == null) { canvas = MushroomUI.Canvas("DeerRescue_UI"); uiRoot = canvas.gameObject; uiRoot.SetActive(false); }
            if (!uiRoot.activeSelf) { player.SetUiMode(this, true); uiRoot.SetActive(true); }
            if (typing != null) StopCoroutine(typing); typing = null; isTyping = false;
            if (page != null) { page.SetActive(false); Destroy(page); }
            Image image = MushroomUI.Image("Page", canvas, new Color(.045f, .10f, .07f, full ? 1 : .82f)); page = image.gameObject;
            if (full) MushroomUI.Place(image.rectTransform, 0, 0, 1200, 680);
            else
            {
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, 0);
                image.rectTransform.pivot = new Vector2(.5f, 0); image.rectTransform.anchoredPosition = new Vector2(0, 20);
                image.rectTransform.sizeDelta = new Vector2(1200, 300);
            }
        }
        TMP_Text Label(string text, float x, float y, float w, float h, int size = 24)
        {
            var t = MushroomUI.Text("Text", page.transform, text, size, Color.white);
            t.rectTransform.pivot = new Vector2(0, .5f); MushroomUI.Place(t.rectTransform, x, y, w, h);
            t.alignment = TextAlignmentOptions.MidlineLeft; t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Truncate; return t;
        }
        Button Button(string caption, float x, float y, float w, float h, Action action)
        {
            TMP_Text t; var b = MushroomUI.Button("Button", page.transform, caption, new Color(.23f, .40f, .28f), out t);
            t.color = Color.white; t.fontSize = 21; MushroomUI.Place((RectTransform)b.transform, x, y, w, h);
            Navigation n = b.navigation; n.mode = Navigation.Mode.None; b.navigation = n;
            b.onClick.AddListener(() => { if (!isTyping) action(); }); return b;
        }
        RescueArtwork Art(Transform parent, RescueArtwork.Picture type, float x, float y, float w, float h)
        {
            var a = MushroomUI.Rect(type.ToString(), parent).gameObject.AddComponent<RescueArtwork>();
            a.picture = type; a.raycastTarget = false; MushroomUI.Place(a.rectTransform, x, y, w, h); a.SetVerticesDirty();
            return a;
        }
        void Speak(string name, string[] text, Action finish)
        {
            Page(false); lines = text; lineIndex = 0; afterLines = finish;
            Label(name, -550, 116, 1000, 32, 22); speech = Label("", -550, 25, 1100, 116, 25);
            Button("Close", -390, -104, 300, 46, Close);
            Button next = Button("Next / Show all", 270, -104, 520, 46, Next);
            next.onClick.RemoveAllListeners(); next.onClick.AddListener(Next); TypeLine();
        }
        void TypeLine()
        { speech.text = lines[lineIndex]; speech.maxVisibleCharacters = 0; speech.ForceMeshUpdate(); typing = StartCoroutine(Type(speech.textInfo.characterCount)); }
        IEnumerator Type(int length)
        {
            isTyping = true;
            for (int i = 0; i <= length; i++) { speech.maxVisibleCharacters = i; if (typingSpeed > 0) yield return new WaitForSecondsRealtime(typingSpeed); }
            isTyping = false; typing = null;
        }
        void ShowAll() { if (typing != null) StopCoroutine(typing); typing = null; isTyping = false; if (speech != null) speech.maxVisibleCharacters = int.MaxValue; }
        void Next() { if (isTyping) { ShowAll(); return; } if (++lineIndex < lines.Length) TypeLine(); else afterLines?.Invoke(); }

        void Intro()
        {
            if (ranger)
            {
                EnsureMiaMotion();
                framingMia = true;
                dialoguePitch = view ? -Mathf.Asin(Mathf.Clamp(view.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg : 0f;
            }
            Speak(rangerName, new[]
            {
                "Please, can you help? A deer is trapped under a fallen tree!",
                "The heavy rain brought down the old pine. The deer cannot get out.",
                "We can move the trunk together with this rope.",
                "I have a first-aid kit with me. Once the deer is free, we can treat its injured leg."
            },
            () =>
            {
                Page(false);

                Label(rangerName, -550, 116, 1000, 32, 22);
                Label(
                    "Will you help me pull the trunk away?",
                    -550, 30, 1100, 90
                );

                Button(
                    "Let's pull together!",
                    -280, -104, 520, 46,
                    () =>
                    {
                        if (!progress.AcceptRescue())
                            return;

                        escortPending = true;
                        Close();
                        StartRelease();
                    }
                );

                Button(
                    "I will come back.",
                    280, -104, 520, 46,
                    Close
                );
            });
        }

        void EnsureMiaMotion()
        {
            if (miaMotion == null && ranger) miaMotion = new MiaMotion(ranger, MiaForward);
        }

        Vector3 MiaForward => Flat(Quaternion.Euler(0f, -miaModelYaw, 0f) * ranger.forward).normalized;
        static Vector3 Flat(Vector3 direction) => MiaTrail.Flat(direction);

        void Turn(Transform actor, Vector3 towards, float degrees)
        {
            Vector3 dir = Flat(towards);
            if (dir.sqrMagnitude < .000001f) return;
            Quaternion target = Quaternion.LookRotation(dir);
            if (actor == ranger) target *= Quaternion.Euler(0f, miaModelYaw, 0f);
            actor.rotation = Quaternion.RotateTowards(actor.rotation, target, degrees);
        }

        Vector3 OnGround(Vector3 candidate, float currentY)
        {
            // Un raggio corto segue suolo, passerelle e pendii senza salire sulle chiome.
            Vector3 origin = new Vector3(candidate.x, currentY + .85f, candidate.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 2.8f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            float y = candidate.y;
            for (int i = 0; i < count; ++i)
            {
                var hit = groundHits[i];
                Transform t = hit.collider.transform;
                if (hit.normal.y < .62f || t.IsChildOf(ranger) || t.IsChildOf(player.transform)
                    || (deerView && t.IsChildOf(deerView.transform))) continue;
                float change = Mathf.Abs(hit.point.y - currentY);
                if (change >= best) continue;
                best = change;
                y = hit.point.y;
            }
            candidate.y = Mathf.MoveTowards(currentY, y, 5f * Time.deltaTime);
            return candidate;
        }

        bool WalkTo(Transform actor, Vector3 goal, float maxStep)
        {
            Vector3 start = actor.position;
            Vector3 delta = Flat(goal - start);
            float distance = delta.magnitude;
            if (distance < .003f) return true;
            float step = Mathf.Min(maxStep, distance);
            Vector3 next = Vector3.Lerp(start, goal, step / distance);
            actor.position = OnGround(next, start.y);
            Turn(actor, delta, 350f * Time.deltaTime);
            if (actor == ranger && Time.deltaTime > 0f) miaSpeed = step / Time.deltaTime;
            return step >= distance;
        }

        bool ApproachPlayer()
        {
            EnsureMiaMotion();
            Vector3 target = player.transform.position;
            Vector3 delta = Flat(target - ranger.position);
            float distance = delta.magnitude;
            float stop = Mathf.Max(1f, dialogueDistance);
            if (distance <= stop + .04f) return true;

            // Se il giocatore scappa, Mia guadagna terreno anche contro lo sprint.
            float dt = Time.deltaTime;
            float escapingSpeed = 0f;
            if (trackingPlayer && dt > 0f)
                escapingSpeed = Mathf.Clamp(Vector3.Dot(Flat(target - lastPlayerPosition), delta / distance) / dt, 0f, 12f);
            lastPlayerPosition = target;
            trackingPlayer = true;
            float pace = Mathf.Max(.6f, rangerSpeed);
            float speed = Mathf.Max(pace * Mathf.Lerp(.8f, 1.7f, Mathf.InverseLerp(stop, stop + 6f, distance)), escapingSpeed + pace);
            WalkTo(ranger, target - delta / distance * stop, speed * dt);
            return Flat(target - ranger.position).sqrMagnitude <= (stop + .04f) * (stop + .04f);
        }

        void LateUpdate()
        {
            if (!player || !ranger || miaMotion == null) return;
            if (framingMia)
            {
                float dt = Time.unscaledDeltaTime;
                Vector3 toMia = Flat(ranger.position - player.transform.position);
                // Anche riaprendo il dialogo con E a distanza zero, il volto entra in campo.
                float minimum = Mathf.Max(1f, dialogueDistance) * .92f;
                if (toMia.magnitude < minimum - .01f)
                {
                    Vector3 outwards = toMia.sqrMagnitude > .01f ? toMia.normalized : player.transform.forward;
                    Vector3 stand = player.transform.position + outwards * minimum;
                    WalkTo(ranger, stand, 3.5f * dt);
                    toMia = Flat(ranger.position - player.transform.position);
                }
                Turn(ranger, -toMia, 460f * dt);
                if (view)
                {
                    Vector3 aim = miaMotion.DialogueTarget - view.transform.position;
                    Vector3 lensForward = Flat(view.transform.forward);
                    if (lensForward.sqrMagnitude > .0001f && Flat(aim).sqrMagnitude > .0001f)
                    {
                        float yaw = Vector3.SignedAngle(lensForward, Flat(aim), Vector3.up);
                        player.transform.rotation = Quaternion.AngleAxis(Mathf.Clamp(yaw, -460f * dt, 460f * dt),
                            Vector3.up) * player.transform.rotation;
                    }
                    float pitch = -Mathf.Atan2(aim.y, Mathf.Max(.01f, Flat(aim).magnitude)) * Mathf.Rad2Deg;
                    dialoguePitch = Mathf.MoveTowardsAngle(dialoguePitch, pitch, 260f * dt);
                    player.SetPitch(dialoguePitch);
                }
                else Turn(player.transform, toMia, 460f * dt);
            }

            if (framingMia || gathering || (encounterStarted && !encounterArrived))
            {
                miaMotion.Animate(miaSpeed, framingMia, isTyping, MiaForward,
                    framingMia ? Time.unscaledDeltaTime : Time.deltaTime);
            }
            if (gathering && escortShot != null) escortShot.Frame(shotBlend, shotHeading);
        }

        void StartRelease()
        {
            if (gathering || releaseGame.IsOpen) return;
            if (escortFinished || !deerPoint || !ranger || !player)
            {
                escortPending = false;
                OpenRelease();
                return;
            }
            escortPending = true;
            StartCoroutine(GatherInFrontOfDeer());
        }

        MiaTrail MakeTrail(out Vector3 playerGoal)
        {
            Vector3 start = player.transform.position;
            var path = new MiaTrail();
            Vector3 entry = start;
            Transform[] waypoints = escortWaypoints;
            if (waypoints == null || !Array.Exists(waypoints, point => point != null))
            {
                var trailObject = GameObject.Find("Path");
                if (trailObject) waypoints = Array.ConvertAll(Scene2PathPieces, name => trailObject.transform.Find(name));
            }
            if (waypoints != null && Array.Exists(waypoints, point => point != null))
            {
                path.Add(encounterPoint ? encounterPoint.position : start);
                foreach (Transform waypoint in waypoints)
                    if (waypoint) { entry = waypoint.position; path.Add(entry); }
            }
            bool shared = playerStandPoint && rangerStandPoint && playerStandPoint == rangerStandPoint;
            Vector3 away = shared ? Flat(playerStandPoint.position - RescueLookPoint).normalized
                                  : Flat(entry - deerPoint.position).normalized;
            if (away.sqrMagnitude < .01f) away = -Flat(deerPoint.forward).normalized;
            if (away.sqrMagnitude < .01f) away = Vector3.back;
            Vector3 side = Vector3.Cross(Vector3.up, away);
            Vector3 center = shared ? playerStandPoint.position : deerPoint.position + away * Mathf.Max(.8f, standDistance);
            float spacing = Mathf.Max(.7f, sideOffset);
            // Nella scena ricevuta la stessa roccia indica il centro della postazione.
            playerGoal = playerStandPoint && !shared ? playerStandPoint.position : center + side * spacing;
            Vector3 rangerGoal = rangerStandPoint && !shared ? rangerStandPoint.position : center - side * spacing;
            if (Flat(playerGoal - rangerGoal).sqrMagnitude < 1f) playerGoal = rangerGoal + side * (2f * spacing);
            path.Add(rangerGoal);
            return path.From(start);
        }

        Vector3 RescueLookPoint => releaseGame && releaseGame.ropeOnTree
            ? releaseGame.ropeOnTree.position : deerPoint.position + Vector3.up * .65f;

        IEnumerator GatherInFrontOfDeer()
        {
            gathering = true;
            EnsureMiaMotion();
            marker.Hide();
            player.SetUiMode(this, true);
            gatherController = player.GetComponent<CharacterController>();
            gatherBody = player.GetComponent<Rigidbody>();
            if (gatherBody) { bodyWasKinematic = gatherBody.isKinematic; gatherBody.isKinematic = true; }
            if (gatherController) { controllerWasEnabled = gatherController.enabled; gatherController.enabled = false; }

            MiaTrail path = MakeTrail(out Vector3 playerGoal);
            float gap = Mathf.Min(Mathf.Max(1f, followDistance), path.Length);
            float walkSpeed = Mathf.Max(.55f, Mathf.Min(rangerSpeed, gatherPlayerSpeed));
            Vector3 firstLead = path.At(gap);
            Vector3 heading = Flat(firstLead - player.transform.position).normalized;
            if (heading.sqrMagnitude < .01f) heading = player.transform.forward;
            shotHeading = heading;
            shotBlend = 0f;
            if (view && view.transform != player.transform)
                escortShot = new MiaEscortShot(view, player.transform, ranger);

            // Mia passa di fianco al giocatore se, per mettersi alla guida, dovrebbe attraversarlo.
            Vector3 crossing = Flat(firstLead - ranger.position);
            if (crossing.sqrMagnitude > .05f)
            {
                float along = Vector3.Dot(Flat(player.transform.position - ranger.position), crossing) / crossing.sqrMagnitude;
                if (along > 0f && along < 1f && Flat(ranger.position + crossing * along - player.transform.position).sqrMagnitude < .95f)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, heading);
                    if (Vector3.Dot(ranger.position - player.transform.position, side) < 0f) side = -side;
                    Vector3 around = player.transform.position + side * 1.4f;
                    while (Flat(around - ranger.position).sqrMagnitude > .00001f)
                    {
                        WalkTo(ranger, around, walkSpeed * Time.deltaTime);
                        FaceGuide();
                        yield return null;
                    }
                }
            }
            while (Flat(firstLead - ranger.position).sqrMagnitude > .00001f)
            {
                WalkTo(ranger, firstLead, walkSpeed * Time.deltaTime);
                FaceGuide();
                yield return null;
            }

            float lead = gap, elapsed = 0f;
            const float blendTime = .55f;
            while (lead < path.Length)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                lead = Mathf.MoveTowards(lead, path.Length, walkSpeed * dt);
                Vector3 guide = path.At(lead);
                Vector3 follower = path.At(lead - gap);
                // Due distanze sulla stessa polilinea: il giocatore segue anche nelle curve.
                WalkTo(ranger, guide, walkSpeed * dt);
                WalkTo(player.transform, follower, walkSpeed * dt);
                Vector3 direction = Flat(ranger.position - player.transform.position);
                if (direction.sqrMagnitude > .01f) shotHeading = direction.normalized;
                dialoguePitch = Mathf.MoveTowardsAngle(dialoguePitch, 2f, 160f * dt);
                player.SetPitch(dialoguePitch);

                float fadeOut = Mathf.Min(wideShotSeconds + 2f * blendTime - elapsed, (path.Length - lead) / walkSpeed);
                shotBlend = wideShotSeconds <= 0f ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.Min(elapsed, fadeOut) / blendTime);
                yield return null;
            }
            shotBlend = 0f;
            escortShot?.Restore();
            while (!WalkTo(player.transform, playerGoal, walkSpeed * Time.deltaTime))
            {
                Turn(ranger, RescueLookPoint - ranger.position, 320f * Time.deltaTime);
                yield return null;
            }

            Vector3 deer = RescueLookPoint;
            Turn(ranger, deer - ranger.position, 360f);
            Turn(player.transform, deer - player.transform.position, 360f);
            if (view)
            {
                Vector3 look = deer - view.transform.position;
                player.SetPitch(-Mathf.Atan2(look.y, Mathf.Max(.01f, Flat(look).magnitude)) * Mathf.Rad2Deg);
            }
            escortFinished = true;
            escortPending = false;
            EndEscort();
            OpenRelease();
        }

        void FaceGuide()
        {
            Turn(player.transform, ranger.position - player.transform.position, 310f * Time.deltaTime);
            dialoguePitch = Mathf.MoveTowardsAngle(dialoguePitch, 2f, 140f * Time.deltaTime);
            player.SetPitch(dialoguePitch);
        }

        void EndEscort()
        {
            if (!gathering) return;
            gathering = false;
            escortShot?.Restore();
            escortShot = null;
            shotBlend = 0f;
            miaMotion?.Rest();
            if (gatherBody)
            {
                gatherBody.position = player.transform.position;
                gatherBody.rotation = player.transform.rotation;
                gatherBody.isKinematic = bodyWasKinematic;
            }
            if (gatherController) gatherController.enabled = controllerWasEnabled;
            gatherController = null;
            gatherBody = null;
            if (player) player.SetUiMode(this, false);
        }

        void OpenRelease()
        {
            releaseGame.Open(player, won =>
            {
                closedFrame = Time.frameCount;
                if (!won || !progress.CompleteRelease()) return;
                if (deerView != null) deerView.Free();
                Speak(rangerName, new[]
                {
                    "The deer is free! Its leg is hurt.",
                    "Let's use my first-aid kit to help it."
                }, OpenTreatment);
            });
        }

        void OpenTreatment()
        {
            Close();
            careGame.Open(player, won =>
            {
                closedFrame = Time.frameCount;
                if (!won || !progress.CompleteCare()) return;
                if (deerView != null) deerView.Heal();
                Speak(rangerName, new[]{"The deer is free and its injured leg is dressed. I will stay here while it rests.",
                    "You brought everyone together to help. Your grandfather would have loved that."}, Close);
            });
        }
        // Il vecchio componente di trascinamento puo restare nel progetto senza riferimenti mancanti.
        public bool DropDressing(Vector2 position, Camera eventCamera) { return false; }
        public void Close()
        {
            framingMia = false;
            miaMotion?.Rest();
            if (typing != null) StopCoroutine(typing); typing = null; isTyping = false;
            if (IsOpen)
            {
                uiRoot.SetActive(false);
                if (player) player.SetUiMode(this, false);
            }
            closedFrame = Time.frameCount;
        }
        void OnGUI()
        {
            if (IsOpen || gathering || player == null || player.IsGameplayBlocked) return;
            string text = "";
            if (progress.Stage == RescueStage.WaitingForCrossing && encounterArrived && Near(ranger)) text = "E - Talk to " + rangerName;


            else if (progress.Stage == RescueStage.FreeDeer && AtRescue) text = "E - Free the deer";

            else if (progress.Stage == RescueStage.TreatDeer && AtRescue) text = "E - Help the injured deer";
            if (text != "") GUI.Box(new Rect(Screen.width / 2 - 220, Screen.height - 95, 440, 50), text);

        }
        void OnDisable()
        {
            StopAllCoroutines();
            typing = null;
            trackingPlayer = false;
            EndEscort();
            Close(); if (marker != null) marker.Hide(); if (equipment != null) equipment.Hide();
            if (bridgeQuest != null) bridgeQuest.showLegacyInventory = true;

            if (releaseGame != null && releaseGame.IsOpen) releaseGame.Close();
            if (careGame != null && careGame.IsOpen) careGame.Close();

        }
        void OnDestroy() { if (uiRoot != null) Destroy(uiRoot); }
    }
}
