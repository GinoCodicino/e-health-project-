using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class FindMushroomsGame : MonoBehaviour
{
    [Header("Collegamenti impostati dal comando Tools")]
    public playercontroller player;
    public NpcDialogue dialogue;
    [Tooltip("0 = disposizione diversa a ogni nuova partita; altro valore = disposizione ripetibile.")]
    public int randomSeed;
    public UnityEvent onCompleted = new UnityEvent();

    public bool IsOpen { get; private set; }
    public bool IsCompleted { get { return found == 5; } }
    public int FoundCount { get { return found; } }

    private GameObject uiRoot;
    private RectTransform grid;
    private TMP_Text counter;
    private TMP_Text feedback;
    private TMP_Text instructions;
    private int found;
    private bool boardCreated;
    private Button replay;

    private void EnsureUI()
    {
        if (uiRoot != null) return;
        RectTransform canvas = MushroomUI.Canvas("FindMushrooms_UI");
        uiRoot = canvas.gameObject;
        Image shade = MushroomUI.Image("Shade", canvas, new Color(0.04f, 0.10f, 0.08f, 0.80f));
        MushroomUI.Fill(shade.rectTransform);
        Image panel = MushroomUI.Image("GamePanel", canvas, MushroomUI.Paper);
        MushroomUI.Place(panel.rectTransform, 0f, 0f, 1120f, 650f);

        TMP_Text title = MushroomUI.Text("Title", panel.transform, "FIND MUSHROOMS", 34f, MushroomUI.Ink);
        MushroomUI.Place(title.rectTransform, -210f, 269f, 620f, 48f);
        title.fontStyle = FontStyles.Bold;
        TMP_Text subtitle = MushroomUI.Text("Subtitle", panel.transform,
            "Look carefully. Select the five mushrooms among the forest objects.", 22f, MushroomUI.Ink);
        MushroomUI.Place(subtitle.rectTransform, 0f, 218f, 1040f, 42f);

        counter = MushroomUI.Text("Counter", panel.transform, "0 / 5", 54f, MushroomUI.Green);
        MushroomUI.Place(counter.rectTransform, -365f, 115f, 260f, 85f);
        counter.fontStyle = FontStyles.Bold;
        counter.alignment = TextAlignmentOptions.Center;
        instructions = MushroomUI.Text("Instructions", panel.transform,
            "Mushrooms found\n\nClick each mushroom once.\nThere is no time limit.", 23f, MushroomUI.Ink);
        MushroomUI.Place(instructions.rectTransform, -365f, -10f, 265f, 155f);
        instructions.alignment = TextAlignmentOptions.Center;

        MushroomIcon sample = MushroomUI.Rect("ExampleMushroom", panel.transform).gameObject.AddComponent<MushroomIcon>();
        sample.picture = MushroomIcon.Picture.Mushroom;
        sample.raycastTarget = false;
        MushroomUI.Place(sample.rectTransform, -365f, -151f, 140f, 124f);

        grid = MushroomUI.Rect("Pictures", panel.transform);
        MushroomUI.Place(grid, 150f, -18f, 648f, 420f);
        GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(208f, 132f);
        layout.spacing = new Vector2(12f, 12f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;

        feedback = MushroomUI.Text("Feedback", panel.transform, "", 20f, MushroomUI.Green);
        MushroomUI.Place(feedback.rectTransform, 150f, -248f, 650f, 48f);
        feedback.alignment = TextAlignmentOptions.Center;

        TMP_Text unused;
        Button back = MushroomUI.Button("Back", panel.transform, "Back to herbalist", MushroomUI.Green, out unused);
        MushroomUI.Place((RectTransform)back.transform, -320f, -278f, 330f, 52f);
        back.onClick.AddListener(ReturnToNpc);
        replay = MushroomUI.Button("Replay", panel.transform, "New search", new Color(0.87f, 0.84f, 0.73f), out unused);
        MushroomUI.Place((RectTransform)replay.transform, 385f, 269f, 220f, 46f);
        replay.onClick.AddListener(NewBoard);
        uiRoot.SetActive(false);
    }

    public void StartGame(playercontroller who)
    {
        if (who != null) player = who;
        if (player == null) player = GetPlayer();
        if (player == null)
        {
            Debug.LogError("Find Mushrooms: manca il playercontroller nella scena.", this);
            return;
        }
        EnsureUI();
        player.SetUiMode(this, true);
        IsOpen = true;
        uiRoot.SetActive(true);
        if (!boardCreated || IsCompleted) NewBoard();
    }

    private playercontroller GetPlayer()
    {
        return UnityEngine.Object.FindFirstObjectByType<playercontroller>();
    }

    private void NewBoard()
    {
        // Disattiva prima di Destroy: gli elementi vecchi non partecipano al layout.
        for (int i = grid.childCount - 1; i >= 0; i--)
        {
            grid.GetChild(i).gameObject.SetActive(false);
            Destroy(grid.GetChild(i).gameObject);
        }
        found = 0;
        boardCreated = true;
        counter.text = "0 / 5";
        instructions.text = "Mushrooms found\n\nClick each mushroom once.\nThere is no time limit.";
        feedback.text = "Select the mushrooms to help the herbalist.";
        replay.interactable = false;

        int[] items = { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
        System.Random random = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);
        for (int i = items.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            int temp = items[i]; items[i] = items[j]; items[j] = temp;
        }
        for (int i = 0; i < items.Length; i++) CreateCard(items[i], i + 1);
    }

    private void CreateCard(int item, int number)
    {
        bool target = item < 5;
        TMP_Text label;
        Button card = MushroomUI.Button("Picture_" + number, grid, "", Color.white, out label);
        MushroomUI.Place(label.rectTransform, 0f, -48f, 188f, 22f);
        label.fontSize = 16f;
        label.text = number.ToString("00");

        MushroomIcon icon = MushroomUI.Rect("Illustration", card.transform).gameObject.AddComponent<MushroomIcon>();
        MushroomUI.Place(icon.rectTransform, 0f, 12f, 130f, 103f);
        icon.raycastTarget = false;
        icon.picture = target ? MushroomIcon.Picture.Mushroom
            : item == 5 || item == 8 ? MushroomIcon.Picture.Leaf
            : item == 6 ? MushroomIcon.Picture.Rock : MushroomIcon.Picture.Flower;
        Color[] caps = {
            new Color(0.78f, 0.26f, 0.17f), new Color(0.52f, 0.32f, 0.19f),
            new Color(0.82f, 0.57f, 0.23f), new Color(0.65f, 0.38f, 0.23f),
            new Color(0.70f, 0.25f, 0.23f)
        };
        if (target) { icon.capColor = caps[item]; icon.spots = item == 0 || item == 4; }
        icon.SetVerticesDirty();
        bool selected = false;
        card.onClick.AddListener(() =>
        {
            if (!IsOpen || IsCompleted || selected) return;
            if (!target)
            {
                feedback.text = icon.picture == MushroomIcon.Picture.Rock ? "That is a rock. Try another picture."
                    : icon.picture == MushroomIcon.Picture.Flower ? "That is a flower. Look for a mushroom."
                    : "That is a leaf. Keep looking!";
                return;
            }
            selected = true;
            found++;
            label.text = "FOUND";
            Image background = card.GetComponent<Image>();
            background.color = new Color(0.75f, 0.87f, 0.69f);
            ColorBlock colors = card.colors;
            colors.disabledColor = Color.white;
            card.colors = colors;
            card.interactable = false;
            counter.text = found + " / 5";
            feedback.text = "Well spotted! Keep looking.";
            if (IsCompleted)
            {
                instructions.text = "Search complete!\n\nYou found every mushroom.\nReturn to the herbalist.";
                feedback.text = "All five mushrooms found. Well done!";
                replay.interactable = true;
                onCompleted.Invoke();
            }
        });
    }

    public void ReturnToNpc()
    {
        if (!IsOpen) return;
        // Acquisisce il blocco del dialogo prima di rilasciare quello del gioco.
        if (dialogue != null && dialogue.isActiveAndEnabled) dialogue.OpenAfterGame(player, IsCompleted);
        IsOpen = false;
        if (uiRoot != null) uiRoot.SetActive(false);
        if (player != null) player.SetUiMode(this, false);
    }

    private void Update()
    {
        if (IsOpen && playerinputcontroller.ConsumeEscape()) ReturnToNpc();
    }

    private void OnDisable()
    {
        IsOpen = false;
        if (uiRoot != null) uiRoot.SetActive(false);
        if (player != null) player.SetUiMode(this, false);
    }

    private void OnDestroy() { if (uiRoot != null) Destroy(uiRoot); }
}
