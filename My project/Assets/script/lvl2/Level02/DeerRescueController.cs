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

        [Header("Gioco delle leve davanti al cervo")]
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
        [Tooltip("Trascina qui SOLO il modello dell accetta di Rowan, se separato.")]

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
        bool gathering;
        CharacterController gatherController;

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
            if (player != null) view = player.GetComponentInChildren<Camera>(true);
            if (view == null) view = Camera.main;
            if (testStartFromMiaEncounter) encounterStarted = true;
        }
        bool PreviousComplete { get { return testStartFromMiaEncounter || (bridgeCondition != null && bridgeCondition.IsRepaired && bridgeQuest != null && bridgeQuest.CurrentStage == BridgeStage.Complete); } }
        float Distance(Transform t) { return t == null || player == null ? float.PositiveInfinity : Vector3.Distance(t.position, player.transform.position); }
        bool Near(Transform t) { return Distance(t) <= interactionDistance; }
        void Update()
        {
            if (player == null) return;

            // Conflitto di merge risolto: tengo la versione "Updated upstream" (logica completa),
            // senza i segnaposto <<<<<<< ======= >>>>>>>.
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
            if (progress.Stage == RescueStage.WaitingForCrossing)
            {
                marker.Hide();
                if (!PreviousComplete) return;
                if (!encounterStarted && Distance(encounterPoint) <= encounterRadius) encounterStarted = true;
                if (encounterStarted && !encounterArrived)
                {
                    if (ranger == null) { encounterArrived = true; Intro(); return; }
                    Vector3 target = player.transform.position; target.y = ranger.position.y;
                    Vector3 delta = target - ranger.position;
                    if (delta.sqrMagnitude > 1.9f * 1.9f)
                    {
                        ranger.position = Vector3.MoveTowards(ranger.position, target, Mathf.Max(.5f, rangerSpeed) * Time.deltaTime);
                        if (delta.sqrMagnitude > .01f) ranger.rotation = Quaternion.Slerp(ranger.rotation, Quaternion.LookRotation(delta), Time.deltaTime * 8);
                    }
                    else { encounterArrived = true; Intro(); return; }
                }
                if (encounterArrived && Near(ranger) && PressE()) Intro();
                return;
            }
            UpdateMarker();
            if (!PressE()) return;
            switch (progress.Stage)
            {

                case RescueStage.FreeDeer:
                    if (Near(deerPoint)) StartRelease(); break;

                case RescueStage.TreatDeer:
                    if (Near(deerPoint)) OpenTreatment(); break;
                case RescueStage.Complete:
                    if (Near(deerPoint)) Speak(rangerName, new[] { "The deer can rest here now. Thank you for helping!" }, Close); break;
            }
        }
        // Prende temporaneamente in carico le interazioni, mantenendo intatti gli script precedenti.


        bool PressE() { return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame; }
        void UpdateMarker()
        {
            Transform target = null; string text = "";
            switch (progress.Stage)
            {

                case RescueStage.FreeDeer: target = deerPoint; text = "Free the trapped deer"; break;

                case RescueStage.TreatDeer: target = deerPoint; text = "Help the injured deer"; break;
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

                        Close();
                        StartRelease();   // prima ci si sposta davanti al cervo, poi parte il gioco
                    }
                );

                Button(
                    "I will come back.",
                    280, -104, 520, 46,
                    Close
                );
            });
        }

        // ---------- Giocatore e Mia si spostano insieme davanti al cervo, poi parte il gioco ----------
        void StartRelease()
        {
            if (gathering) return;
            if (deerPoint == null || ranger == null || player == null) { OpenRelease(); return; }
            StartCoroutine(GatherInFrontOfDeer());
        }

        // Sposta un transform verso la meta' (solo sul piano orizzontale, mantenendo la sua quota).
        // Restituisce true quando e' arrivato.
        bool StepTo(Transform t, Vector3 goal, float speed)
        {
            Vector3 to = goal - t.position; to.y = 0;
            if (to.sqrMagnitude < .0025f) return true;
            Vector3 next = Vector3.MoveTowards(t.position, new Vector3(goal.x, t.position.y, goal.z), speed * Time.deltaTime);
            t.position = next;
            t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), Time.deltaTime * 8f);
            return false;
        }

        void FaceDeer(Transform t)
        {
            Vector3 look = deerPoint.position - t.position; look.y = 0;
            if (look.sqrMagnitude > .0001f) t.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }

        IEnumerator GatherInFrontOfDeer()
        {
            gathering = true;
            marker.Hide();
            player.SetUiMode(this, true);   // blocca i comandi del giocatore mentre si sposta

            Vector3 deerPos = deerPoint.position;
            Vector3 away = player.transform.position - deerPos; away.y = 0;
            if (away.sqrMagnitude < .01f) away = -deerPoint.forward;
            away.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, away);

            Vector3 playerGoal = playerStandPoint != null ? playerStandPoint.position : deerPos + away * standDistance + side * sideOffset;
            Vector3 rangerGoal = rangerStandPoint != null ? rangerStandPoint.position : deerPos + away * standDistance - side * sideOffset;
            if (playerStandPoint == null) playerGoal.y = player.transform.position.y;
            if (rangerStandPoint == null) rangerGoal.y = ranger.position.y;

            // Se il giocatore usa un CharacterController va spento mentre lo spostiamo a mano.
            gatherController = player.GetComponent<CharacterController>();
            if (gatherController != null) gatherController.enabled = false;

            float timeout = 8f;
            bool playerDone = false, rangerDone = false;
            while (timeout > 0f && !(playerDone && rangerDone))
            {
                timeout -= Time.deltaTime;
                if (!playerDone) playerDone = StepTo(player.transform, playerGoal, Mathf.Max(.5f, gatherPlayerSpeed));
                if (!rangerDone) rangerDone = StepTo(ranger, rangerGoal, Mathf.Max(.5f, rangerSpeed));
                yield return null;
            }

            // Posizione finale esatta e sguardo verso il cervo.
            player.transform.position = playerGoal;
            ranger.position = rangerGoal;
            FaceDeer(player.transform);
            FaceDeer(ranger);

            if (gatherController != null) { gatherController.enabled = true; gatherController = null; }
            player.SetUiMode(this, false);
            gathering = false;

            OpenRelease();
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
            if (typing != null) StopCoroutine(typing); typing = null; isTyping = false;
            if (uiRoot != null) uiRoot.SetActive(false);
            if (player != null) player.SetUiMode(this, false);
            closedFrame = Time.frameCount;
        }
        void OnGUI()
        {
            if (IsOpen || gathering || player == null || player.IsGameplayBlocked) return;
            string text = "";
            if (progress.Stage == RescueStage.WaitingForCrossing && encounterArrived && Near(ranger)) text = "E - Talk to " + rangerName;


            else if (progress.Stage == RescueStage.FreeDeer && Near(deerPoint)) text = "E - Free the deer";

            else if (progress.Stage == RescueStage.TreatDeer && Near(deerPoint)) text = "E - Help the injured deer";
            if (text != "") GUI.Box(new Rect(Screen.width / 2 - 220, Screen.height - 95, 440, 50), text);

        }
        void OnDisable()
        {
            gathering = false;
            if (gatherController != null) { gatherController.enabled = true; gatherController = null; }
            Close(); if (marker != null) marker.Hide(); if (equipment != null) equipment.Hide();
            if (bridgeQuest != null) bridgeQuest.showLegacyInventory = true;

            if (releaseGame != null && releaseGame.IsOpen) releaseGame.Close();
            if (careGame != null && careGame.IsOpen) careGame.Close();

        }
        void OnDestroy() { if (uiRoot != null) Destroy(uiRoot); }
    }
}