using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Level02.DeerRescue
{
    // Minigioco in scena: giocatore e Mia tirano la stessa fune dallo stesso lato.
    // Non eredita da RescueGamePanel: usa solo Unity UI e mantiene l'API
    // richiesta da DeerRescueController (Open, IsOpen, Close).
    public sealed class ScrewRescueGame : MonoBehaviour
    {
        [Header("Riferimenti scena")]
        public Transform fallenTree;
        public Transform mia;
        public Transform ropeOnTree;

        [Header("Spostamento tronco")]
        public Vector3 treeMoveDirection = Vector3.forward;
        [Min(0.1f)] public float totalMoveDistance = 2.2f;
        [Range(3, 8)] public int pullsNeeded = 5;
        [Min(0.1f)] public float pullAnimationTime = 0.65f;

        [Header("Fune")]
        [Min(0.01f)] public float ropeWidth = 0.035f;
        public Color ropeColor = new Color(0.72f, 0.57f, 0.32f);
        public Vector3 playerHandOffset = new Vector3(0f, 1.15f, 0.25f);
        public Vector3 miaHandOffset = new Vector3(0f, 1.15f, 0.25f);
        public Vector3 treeRopeOffset = new Vector3(0f, 0.7f, 0f);

        [Header("Tempismo")]
        [Range(0.1f, 0.9f)] public float greenZoneStart = 0.62f;
        [Range(0.2f, 1f)] public float greenZoneEnd = 0.84f;
        [Min(0.2f)] public float chargeSeconds = 1.35f;

        public bool IsOpen { get { return uiRoot != null && uiRoot.activeSelf; } }

        playercontroller currentPlayer;
        Transform playerTransform;
        Action<bool> resultCallback;
        GameObject uiRoot;
        TMP_Text titleText;
        TMP_Text instructionText;
        TMP_Text meterText;
        TMP_Text progressText;
        TMP_Text statusText;

        LineRenderer playerRope;
        LineRenderer miaRope;
        GameObject ropeRoot;

        Vector3 treeStartPosition;
        Vector3 moveDirection;
        float tension;
        int successfulPulls;
        bool roundRunning;
        bool keyWasHeld;
        bool animating;
        bool callbackSent;
        Coroutine moveRoutine;

        public void Open(playercontroller who, Action<bool> callback)
        {
            if (IsOpen || who == null) return;

            currentPlayer = who;
            playerTransform = who.transform;
            resultCallback = callback;
            callbackSent = false;
            completedWaitingForKey = false;

            DeerRescueController controller = GetComponent<DeerRescueController>();
            if (mia == null && controller != null) mia = controller.ranger;

            if (fallenTree == null)
            {
                Debug.LogError("ScrewRescueGame: assegna il tronco nel campo Fallen Tree dell'Inspector.");
                return;
            }

            treeStartPosition = fallenTree.position;
            moveDirection = treeMoveDirection.sqrMagnitude > 0.001f
                ? treeMoveDirection.normalized
                : Vector3.forward;

            successfulPulls = 0;
            tension = 0f;
            keyWasHeld = false;
            animating = false;
            roundRunning = false;

            currentPlayer.SetUiMode(this, true);
            CreateUI();
            ShowInstructions();
            CreateRopes();
        }

        void CreateUI()
        {
            if (uiRoot != null) Destroy(uiRoot);

            uiRoot = new GameObject("ScrewRescue_UI");
            Canvas canvas = uiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            uiRoot.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            uiRoot.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280, 720);
            uiRoot.AddComponent<GraphicRaycaster>();

            Image background = uiRoot.AddComponent<Image>();
            background.color = new Color(0.025f, 0.07f, 0.055f, 0.94f);

            titleText = MakeText("Titolo", 34, new Vector2(0.08f, 0.76f), new Vector2(0.84f, 0.14f));
            titleText.alignment = TextAlignmentOptions.Center;

            instructionText = MakeText("Istruzioni", 24, new Vector2(0.12f, 0.42f), new Vector2(0.76f, 0.28f));
            instructionText.alignment = TextAlignmentOptions.Center;

            meterText = MakeText("Indicatore", 30, new Vector2(0.12f, 0.28f), new Vector2(0.76f, 0.10f));
            meterText.alignment = TextAlignmentOptions.Center;

            progressText = MakeText("Progressi", 23, new Vector2(0.12f, 0.18f), new Vector2(0.76f, 0.08f));
            progressText.alignment = TextAlignmentOptions.Center;

            statusText = MakeText("Stato", 21, new Vector2(0.10f, 0.08f), new Vector2(0.80f, 0.09f));
            statusText.alignment = TextAlignmentOptions.Center;
        }

        TMP_Text MakeText(string objectName, int fontSize, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(uiRoot.transform, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            TMP_Text text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.alignment = TextAlignmentOptions.Center;
            return text;
        }

        void ShowInstructions()
        {
            titleText.text = "LIBERIAMO IL CERVO";
            instructionText.text =
                "Tu e Mia siete dalla stessa parte del tronco e tirate insieme la fune.\\n\\n" +
                "Tieni premuto E per caricare la trazione. RILASCIA E quando l'indicatore è nella zona verde.\\n\\n" +
                "Ogni strappo riuscito sposta il tronco. Premi E per iniziare.";
            meterText.text = "";
            progressText.text = "";
            statusText.text = "Premi E per cominciare";
        }

        void StartChallenge()
        {
            roundRunning = true;
            tension = 0f;
            keyWasHeld = false;
            instructionText.text = "TIENI PREMUTO E, POI RILASCIA NEL VERDE";
            statusText.text = "Mia sta tirando insieme a te!";
            RefreshUI();
        }

        void Update()
        {
            if (!IsOpen) return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (completedWaitingForKey)
            {
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                    FinishSuccess();
                return;
            }

            if (!roundRunning)
            {
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                    StartChallenge();
                return;
            }

            if (animating || Keyboard.current == null) return;

            bool held = Keyboard.current.eKey.isPressed;
            if (held)
            {
                tension = Mathf.Clamp01(tension + Time.unscaledDeltaTime / Mathf.Max(0.2f, chargeSeconds));
            }

            if (keyWasHeld && !held)
                ReleasePull();

            keyWasHeld = held;
            RefreshUI();
        }

        void ReleasePull()
        {
            if (tension >= greenZoneStart && tension <= greenZoneEnd)
            {
                successfulPulls++;
                statusText.text = "Ottimo strappo! Tu e Mia avete spostato il tronco.";
                if (moveRoutine != null) StopCoroutine(moveRoutine);
                moveRoutine = StartCoroutine(MoveTree());
            }
            else if (tension < greenZoneStart)
            {
                statusText.text = "Troppo presto! Aspetta la zona verde.";
            }
            else
            {
                statusText.text = "Troppo tardi! Riprova a coordinarti con Mia.";
            }

            tension = 0f;
            keyWasHeld = false;
            RefreshUI();
        }

        IEnumerator MoveTree()
        {
            animating = true;
            Vector3 from = fallenTree.position;
            Vector3 target = treeStartPosition + moveDirection *
                (totalMoveDistance * successfulPulls / Mathf.Max(1, pullsNeeded));

            float elapsed = 0f;
            while (elapsed < pullAnimationTime)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / pullAnimationTime);
                t = t * t * (3f - 2f * t);
                fallenTree.position = Vector3.Lerp(from, target, t);
                UpdateRopes();
                yield return null;
            }

            fallenTree.position = target;
            animating = false;
            moveRoutine = null;
            RefreshUI();

            if (successfulPulls >= pullsNeeded)
                Win();
            else
                statusText.text = "Ancora " + (pullsNeeded - successfulPulls) + " strappi per liberare il cervo.";
        }

        void RefreshUI()
        {
            if (meterText != null)
            {
                int percent = Mathf.RoundToInt(tension * 100f);
                int filled = Mathf.RoundToInt(tension * 10f);
                string bar = new string('|', filled) + new string('-', 10 - filled);
                meterText.text = "TENSIONE  [" + bar + "]  " + percent + "%";
                meterText.color = tension >= greenZoneStart && tension <= greenZoneEnd
                    ? new Color(0.35f, 1f, 0.4f)
                    : Color.white;
            }

            if (progressText != null)
                progressText.text = "STRAPPI RIUSCITI: " + successfulPulls + " / " + pullsNeeded;
        }

        void CreateRopes()
        {
            if (ropeRoot != null) Destroy(ropeRoot);
            ropeRoot = new GameObject("Fune_Salvataggio");
            playerRope = MakeRope("Fune_Giocatore");
            if (mia != null) miaRope = MakeRope("Fune_Mia");
            UpdateRopes();
        }

        LineRenderer MakeRope(string objectName)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(ropeRoot.transform, false);

            LineRenderer line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = ropeWidth;
            line.endWidth = ropeWidth;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = ropeColor;
            line.endColor = ropeColor;
            return line;
        }

        void LateUpdate()
        {
            if (IsOpen) UpdateRopes();
        }

        void UpdateRopes()
        {
            if (fallenTree == null || playerTransform == null || playerRope == null) return;

            Vector3 treePoint = ropeOnTree != null
                ? ropeOnTree.position
                : fallenTree.position + fallenTree.TransformDirection(treeRopeOffset);

            Vector3 playerHand = playerTransform.position +
                playerTransform.TransformDirection(playerHandOffset);
            playerRope.SetPosition(0, playerHand);
            playerRope.SetPosition(1, treePoint);

            if (mia != null && miaRope != null)
            {
                Vector3 miaHand = mia.position + mia.TransformDirection(miaHandOffset);
                miaRope.SetPosition(0, miaHand);
                miaRope.SetPosition(1, treePoint);
            }
        }

        void Win()
        {
            roundRunning = false;
            instructionText.text = "Avete tirato insieme e liberato il cervo!";
            meterText.text = "";
            progressText.text = "IL CERVO È LIBERO";
            statusText.text = "Premi E per continuare";
            completedWaitingForKey = true;
        }

        bool completedWaitingForKey;

        void FinishSuccess()
        {
            if (callbackSent) return;
            callbackSent = true;
            Action<bool> callback = resultCallback;
            resultCallback = null;
            CleanupUI();
            if (currentPlayer != null) currentPlayer.SetUiMode(this, false);
            if (ropeRoot != null) ropeRoot.SetActive(false);
            callback?.Invoke(true);
        }


        void OnGUI()
        {
            // Nessuna GUI legacy: input finale gestito qui solo come fallback.
        }

        void CleanupUI()
        {
            if (uiRoot != null)
            {
                Destroy(uiRoot);
                uiRoot = null;
            }
        }

        public void Close()
        {
            if (!IsOpen && callbackSent) return;

            roundRunning = false;
            completedWaitingForKey = false;
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = null;
            CleanupUI();
            if (ropeRoot != null) ropeRoot.SetActive(false);
            if (currentPlayer != null) currentPlayer.SetUiMode(this, false);

            if (!callbackSent)
            {
                callbackSent = true;
                Action<bool> callback = resultCallback;
                resultCallback = null;
                callback?.Invoke(false);
            }
        }

        void OnDisable()
        {
            if (IsOpen) Close();
        }

        void OnDestroy()
        {
            if (ropeRoot != null) Destroy(ropeRoot);
        }
    }
}
