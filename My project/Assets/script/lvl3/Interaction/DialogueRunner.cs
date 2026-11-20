using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Shows a dialogue line by line in a panel at the bottom of the screen, with the look of the level 2
/// dialogues (MushroomUI: dark green panel, TextMeshPro, green buttons, text typed letter by letter).
/// E / Space or the Next button: show the whole line, then the next one.
/// When a line has choices, the player answers with 1, 2, 3 or a click: his line and the NPC's reaction follow,
/// and the answers add up to pick the ending.
/// Can open with a short fade to black, used to stage the scene (player facing the NPC).
/// The player can't move or look around while it is open; the mouse cursor is free to click.
/// Created automatically the first time a dialogue starts.
/// </summary>
public class DialogueRunner : MonoBehaviour
{
    const float FadeDuration = 0.25f;
    const float TypingDelay = 0.018f; // seconds per letter
    const string PlayerName = "You";

    // panel layout, in the 1280x720 reference of MushroomUI.Canvas
    const float PanelWidth = 1100f, PanelHeight = 210f, PanelHeightWithChoices = 306f;
    const float TextLeft = -520f, TextWidth = 1040f, ButtonHeight = 40f, ButtonGap = 6f;

    static readonly Color PanelColor = new Color(0.025f, 0.065f, 0.04f, 0.85f);
    static readonly Color ButtonColor = new Color(0.20f, 0.35f, 0.23f);
    static readonly Color NpcNameColor = new Color(1f, 0.85f, 0.4f);
    static readonly Color PlayerNameColor = new Color(0.62f, 0.9f, 0.62f);
    static readonly Color SourceColor = new Color(0.6f, 0.85f, 1f, 0.9f);
    static readonly Color HintColor = new Color(1f, 1f, 1f, 0.55f);

    private static DialogueRunner instance;

    public static bool IsOpen => instance != null && instance.dialogue != null;

    /// <summary>Open, or closed this frame (the closing key must not reopen a dialogue).</summary>
    public static bool IsBusy => IsOpen || (instance != null && instance.closedFrame == Time.frameCount);

    /// <summary>A line appears on screen. bool = true when the NPC who started the dialogue is speaking.</summary>
    public static event Action<DialogueLine, bool> LineShown;

    /// <summary>The player picked an answer.</summary>
    public static event Action<DialogueChoice> ChoiceMade;

    /// <summary>The dialogue was closed.</summary>
    public static event Action Closed;

    private DialogueData dialogue;
    private readonly List<DialogueLine> lines = new List<DialogueLine>();
    private string defaultSpeaker;
    private Action onFinished;
    private int index, convinced;
    private bool shown, endingAdded;
    private int shownFrame, closedFrame = -1;
    private Behaviour[] lockedPlayer;
    private CursorLockMode oldCursorLock;
    private bool oldCursorVisible;

    // interface
    private GameObject uiRoot;
    private RectTransform canvas, panel;
    private TMP_Text speakerText, speechText, sourceText, hintText;
    private readonly List<GameObject> buttons = new List<GameObject>();
    private Coroutine typing;
    private bool isTyping;

    private DialogueLine Current => lines[index];
    private bool WaitingForChoice => Current.choices != null && Current.choices.Length > 0;

    /// <summary>
    /// Plays a dialogue. With stage: fade to black, call stage (move the player, turn the NPC...),
    /// fade back in, then show the first line.
    /// </summary>
    public static void Play(DialogueData data, string defaultSpeaker, Action onFinished = null, Action stage = null)
    {
        if (data == null || data.lines == null || data.lines.Length == 0) { onFinished?.Invoke(); return; }
        if (instance == null) instance = new GameObject("DialogueRunner").AddComponent<DialogueRunner>();
        instance.Open(data, defaultSpeaker, onFinished, stage);
    }

    /// <summary>Same as E / Space: shows the whole line if it is being typed, otherwise the next line.
    /// Ignored while an answer is expected.</summary>
    public static void Continue()
    {
        if (!IsOpen || !instance.shown || instance.WaitingForChoice && !instance.isTyping) return;
        if (instance.isTyping) instance.ShowWholeLine();
        else instance.Next();
    }

    /// <summary>Same as the keys 1, 2, 3 (number = 0, 1, 2). Ignored if no answer is expected.</summary>
    public static void Answer(int number)
    {
        if (IsOpen && instance.shown && instance.WaitingForChoice && number >= 0 && number < instance.Current.choices.Length)
            instance.Choose(instance.Current.choices[number]);
    }

    /// <summary>How much the player convinced the NPC so far in this dialogue.</summary>
    public static int Convinced => instance != null ? instance.convinced : 0;

    /// <summary>True when the dialogue waits for the player to pick an answer.</summary>
    public static bool IsWaitingForAnswer => IsOpen && instance.shown && instance.WaitingForChoice;

    private void Open(DialogueData data, string speaker, Action finished, Action stage)
    {
        dialogue = data;
        lines.Clear();
        lines.AddRange(data.lines);
        defaultSpeaker = speaker;
        onFinished = finished;
        index = 0;
        convinced = 0;
        endingAdded = false;
        shown = false;
        LockPlayer(true);
        StartCoroutine(Intro(stage));
    }

    private IEnumerator Intro(Action stage)
    {
        if (stage != null)
        {
            var fader = FindFirstObjectByType<ScreenFader>();
            if (fader != null) yield return fader.FadeTo(1f, FadeDuration);
            stage();
            if (fader != null) yield return fader.FadeTo(0f, FadeDuration);
        }
        shown = true;
        // The key that opened the dialogue must not also skip the first line.
        shownFrame = Time.frameCount;
        BuildInterface();
        uiRoot.SetActive(true);
        ShowLine();
    }

    private void ShowLine()
    {
        bool npcSpeaking = string.IsNullOrEmpty(Current.speaker) || Current.speaker == defaultSpeaker;
        DrawLine(npcSpeaking);
        LineShown?.Invoke(Current, npcSpeaking);
    }

    private void Update()
    {
        if (dialogue == null || !shown || Time.frameCount == shownFrame) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) { Continue(); return; }
        if (WaitingForChoice && !isTyping) Answer(PickedChoice(kb));
    }

    private static int PickedChoice(Keyboard kb)
    {
        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return 0;
        if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return 1;
        if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return 2;
        return -1;
    }

    // The player's line and the NPC's reaction come right after the question.
    private void Choose(DialogueChoice choice)
    {
        convinced += choice.convince;
        ChoiceMade?.Invoke(choice);
        lines.Insert(index + 1, new DialogueLine { speaker = PlayerName, text = choice.playerLine, source = choice.source });
        lines.Insert(index + 2, new DialogueLine { text = choice.reaction });
        Next();
    }

    private void Next()
    {
        index++;
        if (index >= lines.Count && !endingAdded) AddEnding();
        if (index < lines.Count) { ShowLine(); return; }

        StopTyping();
        dialogue = null;
        closedFrame = Time.frameCount;
        if (uiRoot != null) uiRoot.SetActive(false);
        LockPlayer(false);
        Closed?.Invoke();
        var finished = onFinished;
        onFinished = null;
        finished?.Invoke();
    }

    private void AddEnding()
    {
        endingAdded = true;
        if (dialogue.endings == null) return;
        DialogueEnding best = null;
        foreach (var ending in dialogue.endings)
            if (convinced >= ending.minConvince && (best == null || ending.minConvince > best.minConvince)) best = ending;
        if (best != null) lines.Add(new DialogueLine { text = best.text });
    }

    private bool IsLastLine => index >= lines.Count - 1 && (endingAdded || dialogue.endings == null || dialogue.endings.Length == 0);

    // The player stops moving and the mouse cursor is freed to click the buttons.
    private void LockPlayer(bool locked)
    {
        if (locked)
        {
            oldCursorLock = Cursor.lockState;
            oldCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = oldCursorLock;
            Cursor.visible = oldCursorVisible;
        }

        if (lockedPlayer == null)
        {
            var controller = FindFirstObjectByType<playercontroller>();
            if (controller == null) return;
            lockedPlayer = new Behaviour[] { controller, controller.GetComponent<playerinputcontroller>() };
        }
        foreach (var b in lockedPlayer)
            if (b != null) b.enabled = !locked;
    }

    // ---------------- interface ----------------

    private void BuildInterface()
    {
        if (uiRoot != null) return;
        canvas = MushroomUI.Canvas("Dialogue_UI");
        canvas.SetParent(transform, false);
        uiRoot = canvas.gameObject;

        Image background = MushroomUI.Image("Panel", canvas, PanelColor);
        panel = background.rectTransform;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 20f);

        speakerText = Label("Speaker", 22f, NpcNameColor);
        speakerText.fontStyle = FontStyles.Bold;
        speechText = Label("Speech", 23f, Color.white);
        speechText.alignment = TextAlignmentOptions.TopLeft;
        sourceText = Label("Source", 16f, SourceColor);
        sourceText.fontStyle = FontStyles.Italic;
        hintText = Label("Hint", 15f, HintColor);
        hintText.alignment = TextAlignmentOptions.MidlineRight;
        uiRoot.SetActive(false);
    }

    private TMP_Text Label(string name, float size, Color color)
    {
        TMP_Text text = MushroomUI.Text(name, panel, "", size, color);
        text.rectTransform.pivot = new Vector2(0f, 0.5f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private static void Place(TMP_Text text, float top, float height, float panelHeight)
    {
        // top = distance from the top of the panel
        MushroomUI.Place(text.rectTransform, TextLeft, panelHeight / 2f - top - height / 2f, TextWidth, height);
    }

    private void DrawLine(bool npcSpeaking)
    {
        DialogueLine line = Current;
        bool choosing = WaitingForChoice;
        float height = choosing ? PanelHeightWithChoices : PanelHeight;
        panel.sizeDelta = new Vector2(PanelWidth, height);

        speakerText.text = npcSpeaking ? defaultSpeaker : line.speaker;
        speakerText.color = npcSpeaking ? NpcNameColor : PlayerNameColor;
        Place(speakerText, 10f, 28f, height);
        Place(speechText, 40f, 88f, height);
        sourceText.text = string.IsNullOrEmpty(line.source) ? "" : "Source: " + line.source;
        Place(sourceText, 130f, 20f, height);

        foreach (var button in buttons) Destroy(button);
        buttons.Clear();
        if (choosing)
        {
            for (int i = 0; i < line.choices.Length; i++)
            {
                int number = i;
                float y = height / 2f - 158f - i * (ButtonHeight + ButtonGap) - ButtonHeight / 2f;
                AddButton($"{i + 1}.  {line.choices[i].label}", 0f, y, TextWidth, () => Answer(number), TextAlignmentOptions.MidlineLeft);
            }
            hintText.text = "";
        }
        else
        {
            float y = -height / 2f + 14f + ButtonHeight / 2f;
            AddButton(IsLastLine ? "Close" : "Next", TextWidth / 2f - 110f, y, 220f, () => Continue(), TextAlignmentOptions.Center);
            hintText.text = IsLastLine ? "E / Space: close" : "E / Space: next";
            MushroomUI.Place(hintText.rectTransform, TextLeft, y, TextWidth - 240f, ButtonHeight);
        }
        SetButtonsInteractable(false);
        Type(line.text);
    }

    private void AddButton(string caption, float x, float y, float width, Action action, TextAlignmentOptions alignment)
    {
        Button button = MushroomUI.Button("Button", panel, caption, ButtonColor, out TMP_Text label);
        label.color = Color.white;
        label.fontSize = 20;
        label.alignment = alignment;
        label.margin = new Vector4(14f, 0f, 14f, 0f);
        label.overflowMode = TextOverflowModes.Overflow;
        MushroomUI.Place((RectTransform)button.transform, x, y, width, ButtonHeight);
        button.onClick.AddListener(() => action());
        buttons.Add(button.gameObject);
    }

    // The choices can only be clicked once the question is fully written; Next also works while typing.
    private void SetButtonsInteractable(bool interactable)
    {
        if (!WaitingForChoice) return;
        foreach (var button in buttons) button.GetComponent<Button>().interactable = interactable;
    }

    private void Type(string text)
    {
        StopTyping();
        speechText.text = text;
        speechText.maxVisibleCharacters = 0;
        speechText.ForceMeshUpdate();
        typing = StartCoroutine(TypeLetters(speechText.textInfo.characterCount));
    }

    private IEnumerator TypeLetters(int count)
    {
        isTyping = true;
        for (int i = 0; i <= count; i++)
        {
            speechText.maxVisibleCharacters = i;
            yield return new WaitForSecondsRealtime(TypingDelay);
        }
        ShowWholeLine();
    }

    private void ShowWholeLine()
    {
        StopTyping();
        speechText.maxVisibleCharacters = int.MaxValue;
        SetButtonsInteractable(true);
    }

    private void StopTyping()
    {
        if (typing != null) StopCoroutine(typing);
        typing = null;
        isTyping = false;
    }
}
