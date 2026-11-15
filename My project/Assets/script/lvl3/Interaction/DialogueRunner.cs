using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shows a dialogue line by line at the bottom of the screen (E or Space: next line).
/// When a line has choices, the player answers with 1, 2 or 3: his line and the NPC's reaction follow,
/// and the answers add up to pick the ending.
/// Can open with a short fade to black, used to stage the scene (player facing the NPC).
/// The player can't move or look around while it is open.
/// Created automatically the first time a dialogue starts.
/// </summary>
public class DialogueRunner : MonoBehaviour
{
    const float FadeDuration = 0.25f;
    const string PlayerName = "You";

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
    private GUIStyle speakerStyle, textStyle, hintStyle, sourceStyle, choiceStyle;

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

    /// <summary>Same as E / Space (for buttons, a gamepad or tests). Ignored while an answer is expected.</summary>
    public static void Continue()
    {
        if (IsOpen && instance.shown && !instance.WaitingForChoice) instance.Next();
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
        AnnounceLine();
    }

    private void AnnounceLine()
    {
        bool npcSpeaking = string.IsNullOrEmpty(Current.speaker) || Current.speaker == defaultSpeaker;
        LineShown?.Invoke(Current, npcSpeaking);
    }

    private void Update()
    {
        if (dialogue == null || !shown || Time.frameCount == shownFrame) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        if (WaitingForChoice)
        {
            Answer(PickedChoice(kb));
            return;
        }
        if (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Continue();
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
        if (index < lines.Count) { AnnounceLine(); return; }

        dialogue = null;
        closedFrame = Time.frameCount;
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

    private void LockPlayer(bool locked)
    {
        if (lockedPlayer == null)
        {
            var controller = FindFirstObjectByType<playercontroller>();
            if (controller == null) return;
            lockedPlayer = new Behaviour[] { controller, controller.GetComponent<playerinputcontroller>() };
        }
        foreach (var b in lockedPlayer)
            if (b != null) b.enabled = !locked;
    }

    private void OnGUI()
    {
        if (dialogue == null || !shown) return;
        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true, normal = { textColor = Color.white } };
            speakerStyle = new GUIStyle(textStyle) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.4f) } };
            hintStyle = new GUIStyle(textStyle) { fontSize = 16, alignment = TextAnchor.LowerRight, normal = { textColor = new Color(1f, 1f, 1f, 0.6f) } };
            sourceStyle = new GUIStyle(textStyle) { fontSize = 15, fontStyle = FontStyle.Italic, alignment = TextAnchor.LowerLeft, normal = { textColor = new Color(0.6f, 0.85f, 1f, 0.9f) } };
            choiceStyle = new GUIStyle(textStyle) { fontSize = 20, normal = { textColor = new Color(0.85f, 0.95f, 1f) } };
        }

        DialogueLine line = Current;
        string speaker = string.IsNullOrEmpty(line.speaker) ? defaultSpeaker : line.speaker;
        bool choosing = WaitingForChoice;
        float choicesHeight = choosing ? 34f * line.choices.Length + 10f : 0f;

        float w = Mathf.Min(900f, Screen.width - 40f), h = 170f + choicesHeight;
        var box = new Rect((Screen.width - w) / 2f, Screen.height - h - 30f, w, h);
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var inner = new Rect(box.x + 20f, box.y + 14f, box.width - 40f, box.height - 28f);
        GUI.Label(new Rect(inner.x, inner.y, inner.width, 32f), speaker, speakerStyle);
        GUI.Label(new Rect(inner.x, inner.y + 36f, inner.width, 170f - 64f), line.text, textStyle);

        if (choosing)
        {
            float y = inner.y + 170f - 28f;
            for (int i = 0; i < line.choices.Length; i++, y += 34f)
                GUI.Label(new Rect(inner.x, y, inner.width, 32f), $"{i + 1}.  {line.choices[i].label}", choiceStyle);
            GUI.Label(inner, "1 / 2 / 3: answer", hintStyle);
            return;
        }
        if (!string.IsNullOrEmpty(line.source)) GUI.Label(inner, "Source: " + line.source, sourceStyle);
        GUI.Label(inner, index < lines.Count - 1 || !endingAdded && dialogue.endings != null && dialogue.endings.Length > 0
            ? "E / Space: next" : "E / Space: close", hintStyle);
    }
}
