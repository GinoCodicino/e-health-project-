using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shows a dialogue line by line at the bottom of the screen (E or Space: next line).
/// Can open with a short fade to black, used to stage the scene (player facing the NPC).
/// The player can't move or look around while it is open.
/// Created automatically the first time a dialogue starts.
/// </summary>
public class DialogueRunner : MonoBehaviour
{
    const float FadeDuration = 0.25f;

    private static DialogueRunner instance;

    public static bool IsOpen => instance != null && instance.dialogue != null;

    /// <summary>Open, or closed this frame (the closing key must not reopen a dialogue).</summary>
    public static bool IsBusy => IsOpen || (instance != null && instance.closedFrame == Time.frameCount);

    /// <summary>A line appears on screen. bool = true when the NPC who started the dialogue is speaking.</summary>
    public static event Action<DialogueLine, bool> LineShown;

    /// <summary>The dialogue was closed.</summary>
    public static event Action Closed;

    private DialogueData dialogue;
    private string defaultSpeaker;
    private Action onFinished;
    private int index;
    private bool shown;
    private int shownFrame, closedFrame = -1;
    private Behaviour[] lockedPlayer;
    private GUIStyle speakerStyle, textStyle, hintStyle;

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

    private void Open(DialogueData data, string speaker, Action finished, Action stage)
    {
        dialogue = data;
        defaultSpeaker = speaker;
        onFinished = finished;
        index = 0;
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
        DialogueLine line = dialogue.lines[index];
        bool npcSpeaking = string.IsNullOrEmpty(line.speaker) || line.speaker == defaultSpeaker;
        LineShown?.Invoke(line, npcSpeaking);
    }

    private void Update()
    {
        if (dialogue == null || !shown || Time.frameCount == shownFrame) return;
        var kb = Keyboard.current;
        if (kb == null || !(kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) return;

        index++;
        if (index < dialogue.lines.Length) { AnnounceLine(); return; }

        dialogue = null;
        closedFrame = Time.frameCount;
        LockPlayer(false);
        Closed?.Invoke();
        var finished = onFinished;
        onFinished = null;
        finished?.Invoke();
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
        }

        DialogueLine line = dialogue.lines[index];
        string speaker = string.IsNullOrEmpty(line.speaker) ? defaultSpeaker : line.speaker;

        float w = Mathf.Min(900f, Screen.width - 40f), h = 170f;
        var box = new Rect((Screen.width - w) / 2f, Screen.height - h - 30f, w, h);
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var inner = new Rect(box.x + 20f, box.y + 14f, box.width - 40f, box.height - 28f);
        GUI.Label(new Rect(inner.x, inner.y, inner.width, 32f), speaker, speakerStyle);
        GUI.Label(new Rect(inner.x, inner.y + 36f, inner.width, inner.height - 36f), line.text, textStyle);
        GUI.Label(inner, index < dialogue.lines.Length - 1 ? "E / Space: next" : "E / Space: close", hintStyle);
    }
}
