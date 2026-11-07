using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class MushroomLearningQuest : MonoBehaviour
{
    public NpcDialogue dialogue;
    public FindMushroomsGame mushroomGame;
    public TextAsset lessonAsset;
    public UnityEvent onCompleted = new UnityEvent();
    public bool IsOpen { get; private set; }
    public bool IsAvailable { get { return LoadLesson(); } }
    public int TopicCount { get { return LoadLesson() ? progress.TopicCount : 0; } }
    public int PassedCount { get { return LoadLesson() ? progress.PassedCount : 0; } }
    public bool IsComplete { get { return LoadLesson() && progress.IsComplete; } }
    public bool CanAssess { get { return LoadLesson() && progress.CanStart(mushroomGame != null && mushroomGame.IsCompleted); } }

    private Lesson data;
    private MushroomQuizState progress;
    private bool loadAttempted;
    private bool completionRaised;
    private playercontroller player;
    private GameObject uiRoot;
    private GameObject discussionPanel;
    private GameObject quizPanel;
    private TMP_Text topicText;
    private TMP_Text topicStatus;
    private TMP_Text quizText;
    private TMP_Text quizStatus;
    private TMP_Text continueCaption;
    private TMP_Text confirmCaption;
    private Button continueTopic;
    private Button confirm;
    private readonly List<Button> tabs = new List<Button>();
    private readonly List<TMP_Text> tabCaptions = new List<TMP_Text>();
    private readonly List<Button> options = new List<Button>();
    private readonly List<TMP_Text> optionCaptions = new List<TMP_Text>();
    private Option[] displayedOptions;
    private int topicIndex;
    private int questionIndex;
    private int selectedOption = -1;
    private bool waitingForNext;

    private bool LoadLesson()
    {
        if (loadAttempted) return data != null;
        loadAttempted = true;
        try
        {
            if (lessonAsset == null) lessonAsset = Resources.Load<TextAsset>("MushroomLesson");
            if (lessonAsset == null) throw new InvalidOperationException("Manca Resources/MushroomLesson.json.");
            Lesson parsed = JsonUtility.FromJson<Lesson>(lessonAsset.text);
            if (parsed == null || parsed.topics == null || parsed.questions == null
                || parsed.topics.Length != 3 || parsed.questions.Length != 3)
                throw new InvalidOperationException("Servono tre argomenti e tre domande.");
            HashSet<string> topicIds = new HashSet<string>();
            Dictionary<string, string> keys = new Dictionary<string, string>();
            foreach (Topic topic in parsed.topics)
            {
                if (string.IsNullOrWhiteSpace(topic.id) || !topicIds.Add(topic.id)
                    || string.IsNullOrWhiteSpace(topic.claim) || string.IsNullOrWhiteSpace(topic.note))
                    throw new InvalidOperationException("Argomento mancante o duplicato.");
            }
            foreach (Question question in parsed.questions)
            {
                if (string.IsNullOrWhiteSpace(question.id) || keys.ContainsKey(question.id)
                    || !topicIds.Contains(question.topicId) || question.options == null || question.options.Length != 3)
                    throw new InvalidOperationException("Domanda non valida.");
                HashSet<string> ids = new HashSet<string>();
                foreach (Option option in question.options)
                    if (string.IsNullOrWhiteSpace(option.id) || !ids.Add(option.id) || string.IsNullOrWhiteSpace(option.text))
                        throw new InvalidOperationException("Opzione mancante o duplicata.");
                if (!ids.Contains(question.correct)) throw new InvalidOperationException("Manca la risposta corretta.");
                keys.Add(question.id, question.correct);
            }
            data = parsed;
            progress = new MushroomQuizState(topicIds, keys);
            return true;
        }
        catch (Exception error)
        {
            data = null;
            Debug.LogError("Find Mushrooms: " + error.Message, this);
            return false;
        }
    }

    private Image Panel(string name, Transform canvas, float height)
    {
        float opacity = dialogue == null ? 0.82f : dialogue.dialogueOpacity;
        Image panel = MushroomUI.Image(name, canvas, new Color(0.035f, 0.075f, 0.055f, opacity));
        RectTransform r = panel.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.pivot = new Vector2(0.5f, 0f);
        r.anchoredPosition = new Vector2(0f, 18f);
        r.sizeDelta = new Vector2(1120f, height);
        return panel;
    }

    private Button Button(string name, Transform parent, string text, float x, float y, float width, float height, out TMP_Text caption)
    {
        Button b = MushroomUI.Button(name, parent, text, new Color(0.25f, 0.40f, 0.29f, 0.95f), out caption);
        caption.color = Color.white;
        caption.fontSize = 18f;
        MushroomUI.Fill(caption.rectTransform, 5f);
        MushroomUI.Place((RectTransform)b.transform, x, y, width, height);
        return b;
    }

    private TMP_Text TextArea(string name, Transform parent, float y, float height, float fontSize)
    {
        Image area = MushroomUI.Image(name, parent, new Color(1f, 1f, 1f, 0f));
        MushroomUI.Place(area.rectTransform, 0f, y, 1080f, height);
        ScrollRect scroll = area.gameObject.AddComponent<ScrollRect>();
        RectTransform viewport = MushroomUI.Rect("Viewport", area.transform);
        MushroomUI.Fill(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        TMP_Text text = MushroomUI.Text("Text", viewport, "", fontSize, Color.white);
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.rectTransform.anchorMin = new Vector2(0f, 1f);
        text.rectTransform.anchorMax = new Vector2(1f, 1f);
        text.rectTransform.pivot = new Vector2(0.5f, 1f);
        text.rectTransform.sizeDelta = new Vector2(0f, height);
        text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = text.rectTransform;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        return text;
    }

    private void SetText(TMP_Text label, string value)
    {
        label.text = value;
        label.rectTransform.anchoredPosition = Vector2.zero;
        ScrollRect scroll = label.GetComponentInParent<ScrollRect>();
        if (scroll != null) { scroll.StopMovement(); scroll.verticalNormalizedPosition = 1f; }
    }

    private void EnsureUI()
    {
        if (uiRoot != null) return;
        RectTransform canvas = MushroomUI.Canvas("Herbalist_Learning_UI");
        uiRoot = canvas.gameObject;
        Image discussion = Panel("Discussion", canvas, 350f);
        discussionPanel = discussion.gameObject;
        TMP_Text title = MushroomUI.Text("Title", discussion.transform, "DISCUSS THE HERBALIST'S DOUBTS", 24f, Color.white);
        MushroomUI.Place(title.rectTransform, -80f, 143f, 920f, 34f);
        TMP_Text caption;
        Button back = Button("Back", discussion.transform, "Back", 475f, 143f, 130f, 34f, out caption);
        back.onClick.AddListener(ReturnToNpc);
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            Button tab = Button("Topic_" + i, discussion.transform, "", -360f + i * 360f, 98f, 340f, 34f, out caption);
            tabs.Add(tab);
            tabCaptions.Add(caption);
            tab.onClick.AddListener(() => ShowTopic(index));
        }
        topicText = TextArea("TopicText", discussion.transform, -6f, 150f, 21f);
        topicStatus = MushroomUI.Text("Progress", discussion.transform, "", 16f, new Color(0.87f, 0.88f, 0.73f));
        MushroomUI.Place(topicStatus.rectTransform, 0f, -98f, 1080f, 24f);
        continueTopic = Button("Read", discussion.transform, "Continue", -365f, -137f, 350f, 44f, out continueCaption);
        continueTopic.onClick.AddListener(MarkTopicAndContinue);
        Button ask = Button("Ask", discussion.transform, "Challenge this view", 0f, -137f, 350f, 44f, out caption);
        ask.onClick.AddListener(ChallengeTopic);
        Button leave = Button("Leave", discussion.transform, "Return to conversation", 365f, -137f, 350f, 44f, out caption);
        leave.onClick.AddListener(ReturnToNpc);

        Image quiz = Panel("Assessment", canvas, 400f);
        quizPanel = quiz.gameObject;
        title = MushroomUI.Text("Title", quiz.transform, "YOUR RESPONSE TO THE HERBALIST", 24f, Color.white);
        MushroomUI.Place(title.rectTransform, 0f, 171f, 1080f, 34f);
        quizText = TextArea("Question", quiz.transform, 108f, 82f, 21f);
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            Button option = Button("Answer_" + i, quiz.transform, "", 0f, 32f - i * 62f, 1080f, 52f, out caption);
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            MushroomUI.Fill(caption.rectTransform, 8f);
            options.Add(option);
            optionCaptions.Add(caption);
            option.onClick.AddListener(() => SelectOption(index));
        }
        quizStatus = MushroomUI.Text("Progress", quiz.transform, "", 16f, new Color(0.87f, 0.88f, 0.73f));
        MushroomUI.Place(quizStatus.rectTransform, -70f, -173f, 420f, 44f);
        back = Button("TalkAgain", quiz.transform, "Discuss again", -420f, -173f, 240f, 44f, out caption);
        back.onClick.AddListener(() => { discussionPanel.SetActive(true); quizPanel.SetActive(false); ShowTopicForCurrentQuestion(); });
        confirm = Button("Confirm", quiz.transform, "Confirm answer", 350f, -173f, 380f, 44f, out confirmCaption);
        confirm.onClick.AddListener(ConfirmAnswer);
        uiRoot.SetActive(false);
    }

    private bool Open(playercontroller who)
    {
        if (!LoadLesson() || who == null) return false;
        player = who;
        EnsureUI();
        player.SetUiMode(this, true);
        uiRoot.SetActive(true);
        IsOpen = true;
        return true;
    }

    public bool OpenDiscussion(playercontroller who)
    {
        if (!Open(who)) return false;
        discussionPanel.SetActive(true);
        quizPanel.SetActive(false);
        int unread = Array.FindIndex(data.topics, topic => !progress.HasRead(topic.id));
        ShowTopic(unread < 0 ? 0 : unread);
        return true;
    }

    private void ShowTopic(int index)
    {
        topicIndex = index;
        Topic topic = data.topics[index];
        SetText(topicText, "HERBALIST - HIS VIEW\n" + topic.claim + "\n\nYOUR FIELD NOTES\n" + topic.note + "\n\nSource: " + topic.source + "\n" + topic.url);
        for (int i = 0; i < tabs.Count; i++)
        {
            tabCaptions[i].text = (progress.HasRead(data.topics[i].id) ? "Read - " : "") + data.topics[i].label;
            tabs[i].GetComponent<Image>().color = i == index ? new Color(0.38f, 0.54f, 0.34f) : new Color(0.19f, 0.30f, 0.22f);
        }
        topicStatus.text = "Topics discussed: " + progress.TopicCount + " / 3. Consider the claim and your notes, then select Continue.";
        continueCaption.text = progress.HasRead(topic.id) ? "Already discussed" : "Continue";
        continueTopic.interactable = !progress.HasRead(topic.id);
    }

    private void MarkTopicAndContinue()
    {
        progress.MarkTopicRead(data.topics[topicIndex].id);
        int unread = Array.FindIndex(data.topics, topic => !progress.HasRead(topic.id));
        ShowTopic(unread < 0 ? topicIndex : unread);
        if (unread < 0) topicStatus.text = "All three topics discussed. Find all five mushrooms, then answer the final questions.";
    }

    private void ChallengeTopic()
    {
        string question = data.topics[topicIndex].discussion;
        progress.MarkTopicRead(data.topics[topicIndex].id);
        ReturnToNpc();
        if (dialogue != null) dialogue.AskSuggestedQuestion(question);
    }

    public bool OpenAssessment(playercontroller who)
    {
        if (!CanAssess || !Open(who)) return false;
        discussionPanel.SetActive(false);
        quizPanel.SetActive(true);
        if (IsComplete) { ReturnToNpc(); return true; }
        NextQuestion();
        return true;
    }

    private void NextQuestion()
    {
        questionIndex = Array.FindIndex(data.questions, question => !progress.HasPassed(question.id));
        if (questionIndex < 0) { ReturnToNpc(); return; }
        Question question = data.questions[questionIndex];
        SetText(quizText, question.prompt);
        displayedOptions = (Option[])question.options.Clone();
        System.Random random = new System.Random();
        for (int i = displayedOptions.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            Option swap = displayedOptions[i]; displayedOptions[i] = displayedOptions[j]; displayedOptions[j] = swap;
        }
        selectedOption = -1;
        waitingForNext = false;
        for (int i = 0; i < options.Count; i++)
        {
            optionCaptions[i].text = ((char)('A' + i)) + ". " + displayedOptions[i].text;
            options[i].interactable = true;
            options[i].GetComponent<Image>().color = new Color(0.18f, 0.29f, 0.21f);
        }
        confirmCaption.text = "Confirm answer";
        confirm.interactable = false;
        quizStatus.text = "Correct responses: " + progress.PassedCount + " / 3";
    }

    private void SelectOption(int index)
    {
        if (waitingForNext) return;
        selectedOption = index;
        for (int i = 0; i < options.Count; i++)
            options[i].GetComponent<Image>().color = i == index ? new Color(0.38f, 0.54f, 0.34f) : new Color(0.18f, 0.29f, 0.21f);
        confirm.interactable = true;
    }

    private void ConfirmAnswer()
    {
        if (waitingForNext) { if (IsComplete) ReturnToNpc(); else NextQuestion(); return; }
        if (selectedOption < 0) return;
        Question question = data.questions[questionIndex];
        bool correct = progress.Submit(question.id, displayedOptions[selectedOption].id, mushroomGame != null && mushroomGame.IsCompleted);
        SetText(quizText, (correct ? "CORRECT. " : "NOT YET. ") + question.feedback + (correct ? "" : "\n\n" + question.prompt));
        if (!correct)
        {
            options[selectedOption].GetComponent<Image>().color = new Color(0.55f, 0.28f, 0.20f);
            selectedOption = -1;
            confirm.interactable = false;
            quizStatus.text = "Try another response. Your mushrooms and correct answers are kept.";
            return;
        }
        foreach (Button option in options) option.interactable = false;
        waitingForNext = true;
        confirmCaption.text = IsComplete ? "Return to herbalist" : "Next question";
        quizStatus.text = "Correct responses: " + progress.PassedCount + " / 3";
        if (IsComplete && !completionRaised) { completionRaised = true; onCompleted.Invoke(); }
    }

    private void ShowTopicForCurrentQuestion()
    {
        int index = Array.FindIndex(data.topics, topic => topic.id == data.questions[questionIndex].topicId);
        ShowTopic(Mathf.Max(index, 0));
    }

    public void ReturnToNpc()
    {
        if (!IsOpen) return;
        if (dialogue != null && dialogue.isActiveAndEnabled) dialogue.OpenFromLearning(player);
        IsOpen = false;
        if (uiRoot != null) uiRoot.SetActive(false);
        if (player != null) player.SetUiMode(this, false);
    }

    private void Update() { if (IsOpen && playerinputcontroller.ConsumeEscape()) ReturnToNpc(); }
    private void OnDisable()
    {
        IsOpen = false;
        if (uiRoot != null) uiRoot.SetActive(false);
        if (player != null) player.SetUiMode(this, false);
    }
    private void OnDestroy() { if (uiRoot != null) Destroy(uiRoot); }

    [Serializable] public class Lesson { public string title; public Topic[] topics; public Question[] questions; }
    [Serializable] public class Topic { public string id; public string label; public string claim; public string note; public string discussion; public string source; public string url; }
    [Serializable] public class Question { public string id; public string topicId; public string prompt; public Option[] options; public string correct; public string feedback; }
    [Serializable] public class Option { public string id; public string text; }
}
