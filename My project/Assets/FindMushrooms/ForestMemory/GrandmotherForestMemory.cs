using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Standalone introduction: does not replace the existing player, NPC or quest scripts.
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public class GrandmotherForestMemory : MonoBehaviour
{
    [Header("Riferimenti")]
    public playercontroller player;
    public NpcDialogue herbalist;
    public TextAsset storyAsset;
    public Texture2D pastIllustration;
    public Texture2D presentIllustration;

    [Header("Avvio del ricordo")]
    [Min(0.5f)] public float approachRadius = 2f;
    [Min(0f)] public float startAfterTravel = 8f;
    public bool playImmediately;
    public bool italianDialogue;
    [Tooltip("Il dialogo dell'erborista viene attivato dopo il prologo.")]
    public bool holdHerbalistUntilFinished = true;

    [Header("Immagini continue - V3")]
    [Tooltip("Leggero zoom e scorrimento dell'illustrazione, senza muovere la camera del gioco.")]
    public bool gentleImageMotion = true;
    [Range(0f, 0.08f)] public float imageZoom = 0.035f;
    [Range(0.25f, 2f)] public float illustrationBlendSeconds = 0.9f;

    [Header("Audio facoltativo")]
    public AudioClip backgroundMusic;
    [Range(0f, 1f)] public float musicVolume = 0.18f;
    [Tooltip("Puoi inserire una registrazione per ciascuna delle cinque pagine.")]
    public AudioClip[] voicePerPage;
    public UnityEvent onFinished = new UnityEvent();

    public bool IsPlaying { get; private set; }
    public bool IsCompleted { get; private set; }

    private Story story;
    private Vector3 initialPosition;
    private float readyAt, pageShownAt, memoryBegan;
    private int pageIndex, lastAdvanceFrame = -1;
    private bool initialized, transitioning, ownsInput, exiting;
    private GameObject uiRoot;
    private CanvasGroup group, narrationGroup, sectionGroup;
    private Image background;
    private RawImage illustration, incomingIllustration;
    private AspectRatioFitter pictureAspect, incomingAspect;
    private TMP_Text section, narration, footer, nextCaption, skipCaption;
    private AudioSource musicSource, voiceSource;
    private readonly List<Collider> pausedNpcTriggers = new List<Collider>();

    private void Start()
    {
        if (player == null) player = UnityEngine.Object.FindFirstObjectByType<playercontroller>();
        if (herbalist == null)
        {
            GameObject npc = GameObject.Find("NPC_funghi");
            if (npc != null) herbalist = npc.GetComponent<NpcDialogue>();
        }
        if (player == null)
        {
            Debug.LogError("Ricordo della nonna: manca il playercontroller. Usa il menu di configurazione dell'apertura.", this);
            enabled = false;
            return;
        }
        try
        {
            if (storyAsset == null) storyAsset = Resources.Load<TextAsset>("ForestMemory/ForestMemoryStory");
            if (storyAsset == null) throw new InvalidOperationException("Manca ForestMemoryStory.json.");
            story = JsonUtility.FromJson<Story>(storyAsset.text);
            if (story == null || story.pages == null || story.pages.Length == 0)
                throw new InvalidOperationException("Il ricordo non contiene pagine.");
            foreach (Page page in story.pages)
                if (page == null || string.IsNullOrWhiteSpace(page.english) || string.IsNullOrWhiteSpace(page.italian))
                    throw new InvalidOperationException("Una pagina del ricordo e incompleta.");
            if (pastIllustration == null) pastIllustration = Resources.Load<Texture2D>("ForestMemory/MemoryPast");
            if (presentIllustration == null) presentIllustration = Resources.Load<Texture2D>("ForestMemory/MemoryPresent");
        }
        catch (Exception error)
        {
            Debug.LogError("Ricordo della nonna: " + error.Message, this);
            enabled = false;
            return;
        }
        initialPosition = player.transform.position;
        readyAt = Time.unscaledTime + 0.4f;
        initialized = true;
        HoldNpcTriggers();
        Debug.Log("Ricordo nonna V3 attivo: immagini continue fino a Comincia.", this);
    }

    private void Update()
    {
        if (!initialized || IsCompleted || player == null) return;
        if (exiting) return;
        if (!IsPlaying)
        {
            if (Time.unscaledTime < readyAt) return;
            Vector3 here = player.transform.position;
            float distance = Vector2.Distance(new Vector2(here.x, here.z), new Vector2(transform.position.x, transform.position.z));
            float travelled = Vector2.Distance(new Vector2(here.x, here.z), new Vector2(initialPosition.x, initialPosition.z));
            bool nearStone = distance <= approachRadius && Mathf.Abs(here.y - transform.position.y) < 4f;
            bool nearHerbalist = holdHerbalistUntilFinished && herbalist != null
                && Vector2.Distance(new Vector2(here.x, here.z),
                    new Vector2(herbalist.transform.position.x, herbalist.transform.position.z)) <= 4f
                && Mathf.Abs(here.y - herbalist.transform.position.y) < 4f;
            if (playImmediately || nearStone || nearHerbalist
                || (startAfterTravel > 0f && travelled >= startAfterTravel)) BeginMemory();
            return;
        }
        if (playerinputcontroller.ConsumeEscape()) { FinishMemory(); return; }
        Keyboard keys = Keyboard.current;
        if (keys != null && (keys.spaceKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame
            || keys.numpadEnterKey.wasPressedThisFrame)) Advance();
    }

    public void BeginMemory()
    {
        if (!initialized || IsPlaying || IsCompleted || player == null || !isActiveAndEnabled) return;
        if (herbalist != null && herbalist.IsOpen) herbalist.CloseDialogue();
        try
        {
            if (uiRoot == null) BuildUI();
            pageIndex = 0;
            IsPlaying = true;
            ownsInput = true;
            player.SetUiMode(this, true);
            uiRoot.SetActive(true);
            group.alpha = 1f;
            memoryBegan = Time.unscaledTime;
            ApplyPage();
            SetTextAlpha(0f);
            if (backgroundMusic != null)
            {
                musicSource.clip = backgroundMusic;
                musicSource.volume = musicVolume;
                musicSource.Play();
            }
            StartCoroutine(FadeIn());
        }
        catch (Exception error)
        {
            Debug.LogException(error, this);
            FinishMemory();
        }
    }

    private void LateUpdate()
    {
        if (!IsPlaying || uiRoot == null || !uiRoot.activeSelf) return;
        if (!exiting)
        {
            // Keep an opaque curtain behind both pictures for the entire memory.
            // Only the explicit closing animation may reveal the live game.
            group.alpha = 1f;
            background.color = new Color(0.025f, 0.035f, 0.03f, 1f);
        }
        float elapsed = Mathf.Max(0f, Time.unscaledTime - memoryBegan);
        float amount = gentleImageMotion ? Mathf.Clamp(imageZoom, 0f, 0.08f) : 0f;
        float zoom = 1f + amount * (0.65f + 0.35f * Mathf.Sin(elapsed * 0.055f));
        float span = 1f / zoom;
        float room = 1f - span;
        float x = room * (0.5f + 0.32f * Mathf.Sin(elapsed * 0.065f));
        float y = room * (0.5f + 0.24f * Mathf.Sin(elapsed * 0.05f + 0.7f));
        Rect view = new Rect(x, y, span, span);
        illustration.uvRect = view;
        incomingIllustration.uvRect = view;
    }

    private void BuildUI()
    {
        RectTransform root = MushroomUI.Canvas("ForestMemory_UI");
        uiRoot = root.gameObject;
        root.GetComponent<Canvas>().sortingOrder = 1300;
        group = root.gameObject.AddComponent<CanvasGroup>();
        background = MushroomUI.Image("Dim", root, new Color(0.025f, 0.035f, 0.03f, 1f));
        MushroomUI.Fill(background.rectTransform);
        illustration = MushroomUI.Rect("Memory", root).gameObject.AddComponent<RawImage>();
        MushroomUI.Fill(illustration.rectTransform);
        illustration.raycastTarget = false;
        pictureAspect = illustration.gameObject.AddComponent<AspectRatioFitter>();
        pictureAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        // This picture is drawn over the previous one. The previous picture stays
        // opaque until its replacement completely covers it, so the game cannot flash through.
        incomingIllustration = MushroomUI.Rect("IncomingMemory", root).gameObject.AddComponent<RawImage>();
        MushroomUI.Fill(incomingIllustration.rectTransform);
        incomingIllustration.raycastTarget = false;
        incomingAspect = incomingIllustration.gameObject.AddComponent<AspectRatioFitter>();
        incomingAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        incomingIllustration.gameObject.SetActive(false);

        Image top = MushroomUI.Image("TopShade", root, new Color(0.025f, 0.035f, 0.03f, 0.64f));
        top.rectTransform.anchorMin = new Vector2(0f, 1f);
        top.rectTransform.anchorMax = Vector2.one;
        top.rectTransform.pivot = new Vector2(0.5f, 1f);
        top.rectTransform.sizeDelta = new Vector2(0f, 76f);
        section = MushroomUI.Text("Section", top.transform, "", 22f, MushroomUI.Paper);
        MushroomUI.Fill(section.rectTransform, 22f);
        section.alignment = TextAlignmentOptions.MidlineLeft;
        sectionGroup = section.gameObject.AddComponent<CanvasGroup>();

        Image panel = MushroomUI.Image("NarrationPanel", root, new Color(0.025f, 0.065f, 0.045f, 0.95f));
        panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        panel.rectTransform.pivot = new Vector2(0.5f, 0f);
        panel.rectTransform.anchoredPosition = new Vector2(0f, 12f);
        panel.rectTransform.sizeDelta = new Vector2(1180f, 208f);
        narration = MushroomUI.Text("Narration", panel.transform, "", 28f, MushroomUI.Paper);
        Anchors(narration.rectTransform, new Vector2(0.035f, 0.23f), new Vector2(0.965f, 0.93f));
        narration.alignment = TextAlignmentOptions.MidlineLeft;
        narration.enableAutoSizing = true;
        narration.fontSizeMin = 20f;
        narration.fontSizeMax = 28f;
        narration.overflowMode = TextOverflowModes.Overflow;
        narrationGroup = narration.gameObject.AddComponent<CanvasGroup>();
        footer = MushroomUI.Text("Controls", panel.transform, "", 15f, new Color(0.75f, 0.80f, 0.71f));
        Anchors(footer.rectTransform, new Vector2(0.035f, 0.045f), new Vector2(0.60f, 0.19f));
        footer.alignment = TextAlignmentOptions.MidlineLeft;
        Button next = MushroomUI.Button("Next", panel.transform, "", MushroomUI.Green, out nextCaption);
        Anchors((RectTransform)next.transform, new Vector2(0.74f, 0.035f), new Vector2(0.965f, 0.20f));
        nextCaption.fontSize = 21f;
        next.onClick.AddListener(Advance);
        Button skip = MushroomUI.Button("Skip", top.transform, "", new Color(0.12f, 0.17f, 0.13f, 0.8f), out skipCaption);
        Anchors((RectTransform)skip.transform, new Vector2(0.80f, 0.21f), new Vector2(0.975f, 0.79f));
        skipCaption.fontSize = 17f;
        skip.onClick.AddListener(FinishMemory);
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.spatialBlend = 0f;
        uiRoot.SetActive(false);
    }

    private static void Anchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private IEnumerator FadeIn()
    {
        transitioning = true;
        float began = Time.unscaledTime;
        while (Time.unscaledTime - began < 0.55f)
        {
            SetTextAlpha(Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - began) / 0.55f));
            yield return null;
        }
        SetTextAlpha(1f);
        transitioning = false;
        pageShownAt = Time.unscaledTime;
    }

    private void ApplyPage()
    {
        Page page = story.pages[pageIndex];
        SetPagePicture(page);
        ApplyPageText(page);
    }

    private Texture2D PagePicture(Page page)
    {
        // Older story files use "scene" for their last page. V3 deliberately
        // keeps the present-day still there, without requiring any JSON edits.
        Texture2D chosen = page.image == "past" ? pastIllustration : presentIllustration;
        if (chosen != null) return chosen;
        return pastIllustration != null ? pastIllustration : presentIllustration;
    }

    private void SetPagePicture(Page page)
    {
        Texture2D chosen = PagePicture(page);
        illustration.gameObject.SetActive(chosen != null);
        illustration.texture = chosen;
        illustration.color = Color.white;
        if (chosen != null) pictureAspect.aspectRatio = (float)chosen.width / chosen.height;
        background.color = new Color(0.025f, 0.035f, 0.03f, 1f);
    }

    private void ApplyPageText(Page page)
    {
        section.text = (italianDialogue ? "UN RICORDO  /  " : "A MEMORY  /  ")
            + (italianDialogue ? page.titleItalian : page.titleEnglish);
        narration.text = italianDialogue ? page.italian : page.english;
        footer.text = (pageIndex + 1) + " / " + story.pages.Length
            + (italianDialogue ? "     Spazio / Invio: continua" : "     Space / Enter: continue");
        nextCaption.text = pageIndex == story.pages.Length - 1
            ? (italianDialogue ? "Comincia" : "Begin") : (italianDialogue ? "Continua" : "Continue");
        skipCaption.text = italianDialogue ? "Salta [Esc]" : "Skip [Esc]";
        pageShownAt = Time.unscaledTime;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        voiceSource.Stop();
        if (voicePerPage != null && pageIndex < voicePerPage.Length && voicePerPage[pageIndex] != null)
        {
            voiceSource.clip = voicePerPage[pageIndex];
            voiceSource.Play();
        }
    }

    public void Advance()
    {
        if (!IsPlaying || transitioning || lastAdvanceFrame == Time.frameCount
            || Time.unscaledTime - pageShownAt < 0.3f) return;
        lastAdvanceFrame = Time.frameCount;
        if (pageIndex + 1 >= story.pages.Length) { FinishMemory(); return; }
        StartCoroutine(ChangePage());
    }

    private IEnumerator ChangePage()
    {
        transitioning = true;
        Page nextPage = story.pages[pageIndex + 1];
        Texture2D nextPicture = PagePicture(nextPage);
        bool pictureChanges = illustration.texture != nextPicture;

        // Only the words fade between paragraphs. Background, image and controls remain visible.
        float began = Time.unscaledTime;
        while (Time.unscaledTime - began < 0.12f)
        {
            SetTextAlpha(1f - Mathf.Clamp01((Time.unscaledTime - began) / 0.12f));
            yield return null;
        }
        SetTextAlpha(0f);
        pageIndex++;
        ApplyPageText(nextPage);

        if (pictureChanges && nextPicture != null)
        {
            incomingIllustration.texture = nextPicture;
            incomingAspect.aspectRatio = (float)nextPicture.width / nextPicture.height;
            incomingIllustration.color = new Color(1f, 1f, 1f, 0f);
            incomingIllustration.uvRect = illustration.uvRect;
            incomingIllustration.gameObject.SetActive(true);
        }
        float duration = pictureChanges ? Mathf.Clamp(illustrationBlendSeconds, 0.25f, 2f) : 0.20f;
        began = Time.unscaledTime;
        while (Time.unscaledTime - began < duration)
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - began) / duration));
            SetTextAlpha(t);
            if (pictureChanges && nextPicture != null)
                incomingIllustration.color = new Color(1f, 1f, 1f, t);
            yield return null;
        }
        // Commit the picture before removing the fully opaque overlay in the same frame.
        if (pictureChanges)
        {
            SetPagePicture(nextPage);
            incomingIllustration.gameObject.SetActive(false);
            incomingIllustration.texture = null;
        }
        SetTextAlpha(1f);
        pageShownAt = Time.unscaledTime;
        transitioning = false;
    }

    private void SetTextAlpha(float value)
    {
        narrationGroup.alpha = value;
        sectionGroup.alpha = value;
    }

    public void FinishMemory()
    {
        if (!initialized || IsCompleted || exiting) return;
        StopAllCoroutines();
        if (IsPlaying && uiRoot != null && group != null && isActiveAndEnabled)
        {
            exiting = true;
            transitioning = true;
            StartCoroutine(CloseMemory());
        }
        else CompleteMemory();
    }

    private IEnumerator CloseMemory()
    {
        // This is the only place where the opaque memory may fade back to the game.
        float began = Time.unscaledTime;
        while (Time.unscaledTime - began < 0.65f)
        {
            group.alpha = 1f - Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - began) / 0.65f);
            yield return null;
        }
        CompleteMemory();
    }

    private void CompleteMemory()
    {
        IsPlaying = false;
        IsCompleted = true;
        transitioning = false;
        exiting = false;
        ReleaseRuntimeState();
        onFinished.Invoke();
    }

    private void HoldNpcTriggers()
    {
        if (!holdHerbalistUntilFinished || herbalist == null) return;
        if (herbalist.IsOpen) herbalist.CloseDialogue();
        foreach (Collider trigger in herbalist.GetComponentsInChildren<Collider>(true))
            if (trigger.isTrigger && trigger.enabled)
            {
                pausedNpcTriggers.Add(trigger);
                trigger.enabled = false;
            }
    }

    private void ReleaseRuntimeState()
    {
        if (uiRoot != null) uiRoot.SetActive(false);
        if (musicSource != null) musicSource.Stop();
        if (voiceSource != null) voiceSource.Stop();
        if (ownsInput && player != null) player.SetUiMode(this, false);
        ownsInput = false;
        foreach (Collider trigger in pausedNpcTriggers) if (trigger != null) trigger.enabled = true;
        pausedNpcTriggers.Clear();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (initialized) IsCompleted = true;
        IsPlaying = false;
        transitioning = false;
        exiting = false;
        ReleaseRuntimeState();
    }

    private void OnDestroy()
    {
        ReleaseRuntimeState();
        if (uiRoot != null) Destroy(uiRoot);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.85f, 0.71f, 0.37f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.6f, approachRadius);
    }

    [Serializable] private class Story { public Page[] pages; }
    [Serializable] private class Page
    {
        public string image, titleEnglish, titleItalian, english, italian;
    }
}
