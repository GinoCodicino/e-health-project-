using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.UI;

public class NpcDialogue : MonoBehaviour
{
    public const int MaximumQuestionCharacters = 4000;
    public const string OriginalIntroSpeech = "Welcome, traveler. Look around you... our precious mushrooms have stopped growing. The soil has fallen ill ever since the ancient roots lost their nourishment. Tell me, what do you wish to know?";
    public const string PreviousPatchIntroSpeech = "Welcome, traveler! Our forest's mushrooms are disappearing. A few remain: help me find them. Select all five mushrooms in the pictures. You can also ask me a question about the forest.";

    [Header("Riferimenti UI esistenti: conservati per compatibilita")]
    public GameObject dialoguePanel;
    public TMP_Text npcText;
    public TMP_InputField playerInputField;

    [Header("Configurazione Dialogo")]
    [TextArea(3, 10)]
    public string introSpeech = OriginalIntroSpeech;
    public float typingSpeed = 0.03f;
    public bool animateIntroduction = true;

    [Header("Aspetto del dialogo nella scena")]
    public string speakerName = "HERBALIST";
    [Range(0.55f, 0.95f)] public float dialogueOpacity = 0.82f;
    [Range(18f, 28f)] public float speechFontSize = 22f;

    [Header("Server LLM")]
    public string apiUrl = "http://127.0.0.1:8000/ask";
    public int requestTimeoutSeconds = 300;

    [Header("Find Mushrooms")]
    public playercontroller player;
    public FindMushroomsGame mushroomGame;
    public MushroomLearningQuest learningQuest;
    [Tooltip("Il menu Tools la attiva per NPC_funghi. Gli altri NPC mantengono la propria UI.")]
    public bool buildMushroomInterface;
    public bool IsOpen { get; private set; }

    private bool hasSpoken;
    private bool isWaitingResponse;
    private bool isTyping;
    private bool initialized;
    private int conversationVersion;
    private GameObject generatedCanvas;
    private TMP_Text statusText;
    private TMP_Text startCaption;
    private Button startButton;
    private Button sendButton;
    private Button whyButton;
    private Button helpButton;
    private Button skipButton;
    private Button quizButton;
    private TMP_Text quizCaption;
    private GameObject mainPanel;
    private GameObject composePanel;
    private TMP_InputField composeInput;
    private TMP_Text composeCounter;
    private bool isComposing;
    private string sessionId;
    private ScrollRect speechScroll;
    private Coroutine introRoutine;
    private UnityWebRequest activeRequest;

    private void Start() { Initialize(); }

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        sessionId = System.Guid.NewGuid().ToString("N");
        // Migra soltanto l'introduzione inserita dalla prima patch; conserva testi personalizzati.
        if (string.IsNullOrWhiteSpace(introSpeech) || introSpeech == PreviousPatchIntroSpeech)
            introSpeech = OriginalIntroSpeech;
        if (mushroomGame == null) mushroomGame = GetComponent<FindMushroomsGame>();
        if (mushroomGame != null)
        {
            if (learningQuest == null) learningQuest = GetComponent<MushroomLearningQuest>();
            if (learningQuest == null) learningQuest = gameObject.AddComponent<MushroomLearningQuest>();
            learningQuest.dialogue = this;
            learningQuest.mushroomGame = mushroomGame;
        }
        if (buildMushroomInterface || mushroomGame != null)
        {
            if (npcText != null && npcText.font != null) MushroomUI.Font = npcText.font;
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            BuildUI();
        }
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (playerInputField != null)
        {
            playerInputField.gameObject.SetActive(false);
            playerInputField.characterLimit = MaximumQuestionCharacters;
            playerInputField.onSubmit.AddListener(SubmitFromField);
        }
    }

    private void BuildUI()
    {
        RectTransform canvas = MushroomUI.Canvas("Herbalist_Dialogue_UI");
        generatedCanvas = canvas.gameObject;
        Image panel = MushroomUI.Image("DialoguePanel", canvas, new Color(0.035f, 0.075f, 0.055f, dialogueOpacity));
        panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        panel.rectTransform.pivot = new Vector2(0.5f, 0f);
        panel.rectTransform.anchoredPosition = new Vector2(0f, 18f);
        panel.rectTransform.sizeDelta = new Vector2(1120f, 250f);
        mainPanel = panel.gameObject;
        // Nessuno sfondo a tutto schermo: NPC, animazioni e bosco restano visibili.
        dialoguePanel = generatedCanvas;

        TMP_Text title = MushroomUI.Text("Title", panel.transform, speakerName, 20f, new Color(0.91f, 0.88f, 0.66f));
        title.fontStyle = FontStyles.Bold;
        MushroomUI.Place(title.rectTransform, -410f, 99f, 260f, 28f);
        TMP_Text closeLabel;
        skipButton = DialogueButton("ShowAll", panel.transform, "Show all [Space]", -170f, 180f, false, out closeLabel);
        MushroomUI.Place((RectTransform)skipButton.transform, -170f, 99f, 180f, 32f);
        skipButton.onClick.AddListener(SkipIntroduction);
        quizButton = DialogueButton("FinalQuestions", panel.transform, "Final questions (locked)", 127f, 390f, true, out quizCaption);
        MushroomUI.Place((RectTransform)quizButton.transform, 127f, 99f, 390f, 32f);
        quizButton.onClick.AddListener(BeginAssessment);
        Button close = DialogueButton("Close", panel.transform, "Close [Esc]", 437f, 206f, false, out closeLabel);
        MushroomUI.Place((RectTransform)close.transform, 437f, 99f, 206f, 32f);
        close.onClick.AddListener(CloseDialogue);

        npcText = BuildSpeechArea(panel.transform);
        statusText = MushroomUI.Text("ConversationStatus", panel.transform, "", 14f, new Color(0.83f, 0.88f, 0.81f));
        MushroomUI.Place(statusText.rectTransform, 0f, -69f, 1080f, 18f);

        playerInputField = MushroomUI.Input(panel.transform);
        MushroomUI.Place((RectTransform)playerInputField.transform, -79f, -37f, 922f, 42f);
        playerInputField.GetComponent<Image>().color = new Color(1f, 1f, 0.95f, 0.13f);
        playerInputField.textComponent.color = Color.white;
        playerInputField.textComponent.fontSize = 20f;
        playerInputField.textComponent.overflowMode = TextOverflowModes.Overflow;
        playerInputField.characterLimit = MaximumQuestionCharacters;
        // Il viewport piu basso richiede meno margine rispetto al vecchio pannello.
        MushroomUI.Fill(playerInputField.textViewport, 9f);
        TMP_Text placeholder = playerInputField.placeholder as TMP_Text;
        if (placeholder != null)
        {
            placeholder.text = "Discuss his views... Use Write more for a longer argument.";
            placeholder.fontSize = 18f;
            placeholder.color = new Color(0.76f, 0.81f, 0.76f);
        }
        sendButton = DialogueButton("Send", panel.transform, "Send", 467f, 146f, true, out closeLabel);
        MushroomUI.Place((RectTransform)sendButton.transform, 467f, -37f, 146f, 42f);
        sendButton.onClick.AddListener(OnSubmitQuestion);

        whyButton = DialogueButton("Discuss", panel.transform, "Discuss his doubts", -425f, 230f, false, out closeLabel);
        whyButton.onClick.AddListener(BeginDiscussion);
        helpButton = DialogueButton("WriteMore", panel.transform, "Write more...", -183f, 230f, false, out closeLabel);
        helpButton.onClick.AddListener(OpenComposer);
        startButton = DialogueButton("StartGame", panel.transform, "Find Mushrooms", 104f, 320f, true, out startCaption);
        startButton.onClick.AddListener(BeginMushroomGame);
        Button explore = DialogueButton("Explore", panel.transform, "Continue exploring", 408f, 264f, false, out closeLabel);
        explore.onClick.AddListener(CloseDialogue);
        BuildComposer(canvas);
        RefreshQuestStatus();
    }

    private void BuildComposer(Transform canvas)
    {
        Image panel = MushroomUI.Image("LongMessage", canvas, new Color(0.035f, 0.075f, 0.055f, dialogueOpacity));
        panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        panel.rectTransform.pivot = new Vector2(0.5f, 0f);
        panel.rectTransform.anchoredPosition = new Vector2(0f, 18f);
        panel.rectTransform.sizeDelta = new Vector2(1120f, 360f);
        composePanel = panel.gameObject;
        TMP_Text title = MushroomUI.Text("Title", panel.transform, "YOUR ARGUMENT TO THE HERBALIST", 22f, Color.white);
        MushroomUI.Place(title.rectTransform, 0f, 149f, 1080f, 30f);
        TMP_Text hint = MushroomUI.Text("Hint", panel.transform, "Enter: new line. Cmd/Ctrl + Enter: send. Your draft is kept when you go back.", 17f, Color.white);
        MushroomUI.Place(hint.rectTransform, 0f, 119f, 1080f, 24f);
        composeInput = MushroomUI.Input(panel.transform);
        MushroomUI.Place((RectTransform)composeInput.transform, 0f, 2f, 1080f, 196f);
        composeInput.characterLimit = MaximumQuestionCharacters;
        composeInput.lineType = TMP_InputField.LineType.MultiLineNewline;
        composeInput.textComponent.alignment = TextAlignmentOptions.TopLeft;
        composeInput.textComponent.fontSize = 20f;
        composeInput.textComponent.overflowMode = TextOverflowModes.Overflow;
        composeInput.textComponent.enableWordWrapping = true;
        TMP_Text placeholder = composeInput.placeholder as TMP_Text;
        if (placeholder != null)
        {
            placeholder.text = "Explain your point and give an example or evidence...";
            placeholder.alignment = TextAlignmentOptions.TopLeft;
            placeholder.fontSize = 20f;
        }
        MushroomUI.Fill(composeInput.textViewport, 12f);
        composeInput.textViewport.offsetMax = new Vector2(-30f, -12f);
        Image track = MushroomUI.Image("Scrollbar", composeInput.transform, new Color(0.12f, 0.24f, 0.18f, 0.18f));
        MushroomUI.Place(track.rectTransform, 526f, 0f, 12f, 172f);
        Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
        RectTransform slide = MushroomUI.Rect("SlidingArea", track.transform);
        MushroomUI.Fill(slide, 1f);
        Image handle = MushroomUI.Image("Handle", slide, MushroomUI.Green);
        MushroomUI.Fill(handle.rectTransform);
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.value = 1f;
        composeInput.verticalScrollbar = scrollbar;
        composeCounter = MushroomUI.Text("Count", panel.transform, "", 16f, Color.white);
        MushroomUI.Place(composeCounter.rectTransform, 0f, -112f, 1080f, 24f);
        composeInput.onValueChanged.AddListener(UpdateComposeCount);
        TMP_Text label;
        Button back = DialogueButton("KeepDraft", panel.transform, "Back - keep draft", -277f, 526f, false, out label);
        MushroomUI.Place((RectTransform)back.transform, -277f, -149f, 526f, 42f);
        back.onClick.AddListener(CloseComposer);
        Button send = DialogueButton("SendLongMessage", panel.transform, "Send [Cmd/Ctrl + Enter]", 277f, 526f, true, out label);
        MushroomUI.Place((RectTransform)send.transform, 277f, -149f, 526f, 42f);
        send.onClick.AddListener(SubmitComposer);
        composePanel.SetActive(false);
    }

    private void UpdateComposeCount(string value)
    {
        if (composeCounter != null) composeCounter.text = value.Length + " / " + MaximumQuestionCharacters + " characters. Scroll to review longer messages.";
    }

    public void OpenComposer()
    {
        if (!IsOpen || isTyping || isWaitingResponse || composeInput == null) return;
        composeInput.text = playerInputField.text;
        UpdateComposeCount(composeInput.text);
        isComposing = true;
        mainPanel.SetActive(false);
        composePanel.SetActive(true);
        composeInput.ActivateInputField();
        composeInput.caretPosition = composeInput.text.Length;
    }

    public void CloseComposer()
    {
        if (!isComposing) return;
        playerInputField.text = composeInput.text;
        isComposing = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        composePanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    public void SubmitComposer()
    {
        if (!isComposing || isWaitingResponse || string.IsNullOrWhiteSpace(composeInput.text)) return;
        CloseComposer();
        OnSubmitQuestion();
    }

    private Button DialogueButton(string name, Transform parent, string caption, float x, float width, bool primary, out TMP_Text label)
    {
        Color background = primary ? new Color(0.25f, 0.43f, 0.30f, 0.96f) : new Color(1f, 1f, 1f, 0.10f);
        Button button = MushroomUI.Button(name, parent, caption, background, out label);
        label.color = Color.white;
        label.fontSize = 18f;
        MushroomUI.Fill(label.rectTransform, 5f);
        MushroomUI.Place((RectTransform)button.transform, x, -101f, width, 36f);
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.white;
        colors.selectedColor = new Color(0.84f, 0.96f, 0.84f);
        colors.pressedColor = new Color(0.66f, 0.79f, 0.66f);
        colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.60f);
        button.colors = colors;
        return button;
    }

    private TMP_Text BuildSpeechArea(Transform parent)
    {
        Image area = MushroomUI.Image("SpeechScroll", parent, new Color(1f, 1f, 1f, 0f));
        MushroomUI.Place(area.rectTransform, 0f, 38f, 1080f, 82f);
        speechScroll = area.gameObject.AddComponent<ScrollRect>();
        RectTransform viewport = MushroomUI.Rect("Viewport", area.transform);
        MushroomUI.Fill(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        TMP_Text text = MushroomUI.Text("Speech", viewport, "", speechFontSize, Color.white);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.overflowMode = TextOverflowModes.Overflow;
        text.rectTransform.anchorMin = new Vector2(0f, 1f);
        text.rectTransform.anchorMax = new Vector2(1f, 1f);
        text.rectTransform.pivot = new Vector2(0.5f, 1f);
        text.rectTransform.sizeDelta = new Vector2(0f, 82f);
        text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        speechScroll.viewport = viewport;
        speechScroll.content = text.rectTransform;
        speechScroll.horizontal = false;
        speechScroll.vertical = true;
        speechScroll.movementType = ScrollRect.MovementType.Clamped;
        speechScroll.scrollSensitivity = 24f;
        return text;
    }

    private void SetSpeech(string text)
    {
        npcText.text = text;
        npcText.maxVisibleCharacters = int.MaxValue;
        if (speechScroll != null)
        {
            speechScroll.StopMovement();
            speechScroll.content.anchoredPosition = Vector2.zero;
            speechScroll.verticalNormalizedPosition = 1f;
        }
    }

    private void RefreshQuestStatus()
    {
        if (statusText == null) return;
        int found = mushroomGame == null ? 0 : mushroomGame.FoundCount;
        int topics = learningQuest == null ? 0 : learningQuest.TopicCount;
        statusText.text = "Mushrooms: " + found + " / 5. Topics discussed: " + topics + " / 3. "
            + (learningQuest != null && learningQuest.IsComplete ? "Mission complete!"
                : learningQuest != null && learningQuest.CanAssess ? "Final questions are ready."
                : "Discuss his doubts and find five mushrooms to unlock the final questions.");
    }

    private void OnTriggerEnter(Collider other)
    {
        playercontroller who = other.GetComponentInParent<playercontroller>();
        if (who == null || (mushroomGame != null && mushroomGame.IsOpen)
            || (learningQuest != null && learningQuest.IsOpen)) return;
        OpenDialogue(who);
    }

    private void OnTriggerExit(Collider other)
    {
        playercontroller who = other.GetComponentInParent<playercontroller>();
        if (who != null && who == player && IsOpen) CloseDialogue();
    }

    public void OpenDialogue(playercontroller who)
    {
        Initialize();
        if (IsOpen) return;
        if (who != null) player = who;
        if (dialoguePanel == null || npcText == null || player == null)
        {
            Debug.LogError("NpcDialogue: riferimenti mancanti. Usa Tools > Find Mushrooms > Configura scena aperta.", this);
            return;
        }
        IsOpen = true;
        conversationVersion++;
        player.SetUiMode(this, true);
        dialoguePanel.SetActive(true);
        if (mainPanel != null) mainPanel.SetActive(true);
        if (composePanel != null) composePanel.SetActive(false);
        isComposing = false;
        RefreshQuestStatus();
        if (!hasSpoken)
        {
            hasSpoken = true;
            if (animateIntroduction && typingSpeed > 0f) introRoutine = StartCoroutine(TypeIntroRoutine());
            else { SetSpeech(introSpeech); ShowQuestionField(); }
        }
        else
        {
            SetSpeech(string.IsNullOrWhiteSpace(npcText.text) ? introSpeech : npcText.text);
            ShowQuestionField();
        }
        UpdateButtons();
    }

    private IEnumerator TypeIntroRoutine()
    {
        isTyping = true;
        SetSpeech(introSpeech);
        npcText.maxVisibleCharacters = 0;
        npcText.ForceMeshUpdate();
        int length = npcText.textInfo.characterCount;
        UpdateButtons();
        for (int i = 1; i <= length; i++)
        {
            npcText.maxVisibleCharacters = i;
            if (typingSpeed > 0f) yield return new WaitForSecondsRealtime(typingSpeed);
        }
        npcText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;
        introRoutine = null;
        ShowQuestionField();
        UpdateButtons();
    }

    private void ShowQuestionField()
    {
        if (playerInputField == null || !IsOpen) return;
        playerInputField.gameObject.SetActive(true);
        playerInputField.interactable = !isWaitingResponse;
    }

    public void SkipIntroduction()
    {
        if (!IsOpen || !isTyping) return;
        if (introRoutine != null) StopCoroutine(introRoutine);
        introRoutine = null;
        isTyping = false;
        npcText.maxVisibleCharacters = int.MaxValue;
        ShowQuestionField();
        UpdateButtons();
        // Avoid submitting a newly focused input with the same Enter key.
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void UpdateButtons()
    {
        if (startButton != null) startButton.interactable = !isTyping && mushroomGame != null && !mushroomGame.IsCompleted;
        if (sendButton != null) sendButton.interactable = !isTyping && !isWaitingResponse;
        if (whyButton != null) whyButton.interactable = !isTyping && learningQuest != null && learningQuest.IsAvailable;
        if (helpButton != null) helpButton.interactable = !isTyping && !isWaitingResponse;
        if (startCaption != null) startCaption.text = mushroomGame != null && mushroomGame.IsCompleted ? "Found: 5 / 5" : "Find Mushrooms";
        if (skipButton != null) skipButton.interactable = isTyping;
        if (quizButton != null) quizButton.interactable = !isTyping && learningQuest != null && learningQuest.CanAssess && !learningQuest.IsComplete;
        if (quizCaption != null) quizCaption.text = learningQuest != null && learningQuest.IsComplete ? "Mission complete"
            : learningQuest != null && learningQuest.CanAssess ? "Final questions" : "Final questions (locked)";
    }

    private void SubmitFromField(string _) { OnSubmitQuestion(); }

    public void AskSuggestedQuestion(string question)
    {
        if (!IsOpen || isTyping || isWaitingResponse || playerInputField == null) return;
        playerInputField.text = question;
        OnSubmitQuestion();
    }

    public void OnSubmitQuestion()
    {
        if (!IsOpen || isTyping || isWaitingResponse || playerInputField == null) return;
        string question = playerInputField.text.Trim();
        if (string.IsNullOrEmpty(question)) return;
        StartCoroutine(SendQuestionToPython(question, conversationVersion));
    }

    private IEnumerator SendQuestionToPython(string question, int version)
    {
        isWaitingResponse = true;
        playerInputField.interactable = false;
        SetSpeech("Let me think about that...");
        if (statusText != null) statusText.text = "You can continue the search while you wait.";
        UpdateButtons();
        string json = JsonUtility.ToJson(new QuestionData { question = question, session_id = sessionId });
        bool delivered = false;

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            activeRequest = request;
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = Mathf.Clamp(requestTimeoutSeconds, 5, 600);
            yield return request.SendWebRequest();
            if (activeRequest == request) activeRequest = null;
            if (!IsOpen || version != conversationVersion) yield break;

            AnswerData answer = null;
            if (request.result == UnityWebRequest.Result.Success)
            {
                try { answer = JsonUtility.FromJson<AnswerData>(request.downloadHandler.text); }
                catch (System.Exception) { answer = null; }
            }
            if (answer != null && !string.IsNullOrWhiteSpace(answer.answer))
            {
                delivered = true;
                SetSpeech(answer.answer);
                RefreshQuestStatus();
                if (statusText != null && answer.mode == "demo") statusText.text = "DEMO dialogue: scripted reply.";
            }
            else
            {
                SetSpeech(mushroomGame != null && mushroomGame.IsCompleted
                    ? "I cannot find the right words just yet. Your five mushrooms are safe. We can still discuss my doubts and finish the final questions."
                    : "I cannot find the right words just yet. You can still help me find the mushrooms, or ask me again in a moment.");
                if (statusText != null) statusText.text = "Try your question again in a moment.";
                Debug.LogWarning("NPC AI: " + apiUrl + " | HTTP " + request.responseCode + " | " + request.error, this);
            }
        }
        if (version != conversationVersion) yield break;
        isWaitingResponse = false;
        if (delivered) playerInputField.text = "";
        ShowQuestionField();
        UpdateButtons();
    }

    public void BeginMushroomGame()
    {
        if (mushroomGame == null || isTyping || mushroomGame.IsCompleted) return;
        mushroomGame.StartGame(player);
        if (mushroomGame.IsOpen) CloseDialogue();
    }

    public void OpenAfterGame(playercontroller who, bool complete)
    {
        OpenDialogue(who);
        SetSpeech(complete
            ? "You found all five mushrooms! But I still have doubts about climate change and what we can do. Discuss my views, compare them with your field notes, and then choose your responses in the final questions."
            : "Take your time, traveler. The mushrooms you have already found are safe. We can talk, and you can resume your search whenever you are ready.");
        RefreshQuestStatus();
        UpdateButtons();
    }

    public void BeginDiscussion()
    {
        if (isTyping || learningQuest == null) return;
        if (learningQuest.OpenDiscussion(player)) CloseDialogue();
    }

    public void BeginAssessment()
    {
        if (isTyping || learningQuest == null || learningQuest.IsComplete) return;
        if (learningQuest.OpenAssessment(player) && learningQuest.IsOpen) CloseDialogue();
    }

    public void OpenFromLearning(playercontroller who)
    {
        OpenDialogue(who);
        if (learningQuest != null && learningQuest.IsComplete)
            SetSpeech("You have found all five mushrooms and answered my three doubts with evidence. I have something to think about. Your task here is complete.");
        else if (learningQuest != null && learningQuest.CanAssess)
            SetSpeech("We have discussed my three doubts, and your search is complete. Select Final questions to choose how you would respond to my views.");
        else
            SetSpeech("Consider what I have said and compare it with your field notes. You can challenge my views, continue our conversation, or look for the remaining mushrooms.");
        RefreshQuestStatus();
        UpdateButtons();
    }

    public void CloseDialogue()
    {
        if (!IsOpen) return;
        CloseComposer();
        IsOpen = false;
        conversationVersion++;
        if (introRoutine != null) { StopCoroutine(introRoutine); introRoutine = null; }
        isTyping = false;
        isWaitingResponse = false;
        if (activeRequest != null) { activeRequest.Abort(); activeRequest = null; }
        if (EventSystem.current != null && playerInputField != null && EventSystem.current.currentSelectedGameObject == playerInputField.gameObject)
            EventSystem.current.SetSelectedGameObject(null);
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (player != null) player.SetUiMode(this, false);
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (playerinputcontroller.ConsumeEscape())
        {
            if (isComposing) CloseComposer(); else CloseDialogue();
            return;
        }
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        bool enter = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
        if (isComposing)
        {
            bool command = keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed
                || keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            if (command && enter) SubmitComposer();
        }
        else if (isTyping && (keyboard.spaceKey.wasPressedThisFrame || enter)) SkipIntroduction();
    }

    private void OnDisable() { CloseDialogue(); }
    private void OnDestroy()
    {
        if (playerInputField != null) playerInputField.onSubmit.RemoveListener(SubmitFromField);
        if (generatedCanvas != null) Destroy(generatedCanvas);
    }

    [System.Serializable] private class QuestionData { public string question; public string session_id; }
    [System.Serializable] private class AnswerData { public string answer; public string mode; }
}
