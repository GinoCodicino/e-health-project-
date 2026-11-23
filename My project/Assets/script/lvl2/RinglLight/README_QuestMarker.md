# Quest Marker – Ring of Light for Unity Minigames

A small, drop-in component that shows the player **where to go**:

- a glowing **ring** on the ground,
- a vertical **beam of light** (visible from far away),
- a floating **label** with the title and the **distance in metres**.

Everything is generated **at runtime**. No prefabs, no colliders, no assets to import, and nothing is added to the scene while you edit it.

You can use it in any minigame: find the objects, reach a place, bring an item to an NPC, and so on.

---

## 1. Requirements

- Unity with **TextMeshPro** (Window → TextMeshPro → Import TMP Essential Resources, the first time).
- The player object must have the **`Player` tag** (or you assign it by hand).
- Works best with **URP** (it uses the `Universal Render Pipeline/Unlit` shader). If URP is not found, it falls back to `Sprites/Default`.
- The main camera must have the **`MainCamera` tag** (the label rotates to face it).

---

## 2. Quick setup (3 steps)

1. Copy `QuestMarker.cs` into your `Assets/Scripts` folder.
2. Create an empty GameObject where the player has to go (for example in the middle of the area) and name it `QuestTarget`.
3. Add the `QuestMarker` component to any GameObject (for example your minigame manager) and set:
   - **Target** → `QuestTarget`
   - **Radius** → how close the player must get (see section 4)
   - **Title** → text shown on the label

Press Play: the ring, the beam and the distance label appear on the target.

---

## 3. Using it from code

Most minigames have several objectives one after the other. Move the marker from a script:

```csharp
public QuestMarker marker;
public Transform bushArea;
public Transform house;

void StartSearchPhase()
{
    marker.SetTarget(bushArea, 8f, "FIND THE ITEMS");
}

void Update()
{
    // Player entered the ring
    if (marker.PlayerInside)
    {
        // show a hint, enable interaction, etc.
    }
}

void OnAllItemsFound()
{
    marker.SetTarget(house, 2f, "RETURN TO THE HOUSE");
}

void OnQuestComplete()
{
    marker.Hide();
}
```

Useful members:

| Member | Description |
|---|---|
| `SetTarget(Transform, radius, title)` | Moves the marker. `radius` and `title` are optional. |
| `Hide()` | Hides the marker. |
| `PlayerInside` | `true` when the player is inside the ring. |
| `Distance` | Distance in metres between player and target. |

---

## 4. How the distance works

The distance is the straight-line (3D) distance from the player to the target:

```csharp
Vector3.Distance(player.position, target.position)
```

- The label shows it rounded **up** to whole metres, for example `18 m`.
- The **ring radius is also the "arrival" distance**. When the distance is smaller than or equal to `radius`, `PlayerInside` becomes `true` and the label changes to `insideText` ("You are here").
- A bigger radius means a bigger area and an easier quest. For a precise point (a door, an NPC) use 1.5 – 2. For a search area (a group of bushes, a clearing) use 6 – 12.

Tip: select the object with the `QuestMarker` component in the editor. An orange circle (Gizmo) shows the area, so you can adjust the radius without pressing Play.

---

## 5. Parameters

| Parameter | Meaning | Suggested |
|---|---|---|
| `target` | Where the marker is placed. | – |
| `player` | Player transform. Empty = object tagged `Player`. | empty |
| `radius` | Ring radius and arrival distance. | 3 |
| `height` | Height of the beam. | 6 |
| `color` | Colour of ring, beam and label. | orange |
| `lineWidth` | Thickness of ring and beam. | 0.08 |
| `ringSegments` | Smoothness of the ring. | 48 |
| `ringHeight` | Lifts the ring slightly above the ground. | 0.10 |
| `showLabel` | Shows or hides the label. | true |
| `title` | First line of the label. | OBJECTIVE |
| `insideText` | Second line when the player is inside. | You are here |
| `fontSize` | Size of the label text. | 3 |

---

## 6. How it works (short version)

1. The first time there is a target, `Build()` creates a `Quest_Marker_Runtime` object with two `LineRenderer`s (ring and beam) and a `TextMeshPro` label.
2. Every frame `Update()` moves the marker to the target, computes the distance, redraws the ring with the current radius, and updates the label.
3. `LateUpdate()` rotates the label to face the camera.
4. `OnDestroy()` removes the object and the material, so nothing is left behind.

The `LineRenderer`s use `useWorldSpace = false`, so the points are relative to the marker. That is why the marker only needs to be moved to the target.

---

## 7. Tips

- **Hide it during dialogues or menus**: call `marker.Hide()` or set `marker.enabled = false`, and restore it afterwards with `SetTarget()`.
- **Uneven terrain**: the ring is flat. If the ground is sloped, increase `ringHeight` a little or place the target on the highest point.
- **Several markers**: add one `QuestMarker` component per objective on different GameObjects.
- **Distance on screen as well**: if you want a UI text in a corner, read `marker.Distance` and write it to your own HUD.
- **Builds**: `Shader.Find` can fail in a build if the shader is stripped. Add `Universal Render Pipeline/Unlit` to *Project Settings → Graphics → Always Included Shaders*.

---

## 8. Troubleshooting

| Problem | Fix |
|---|---|
| Nothing appears | `target` is empty, the component is disabled, or the object is inactive. |
| The ring is pink | The shader was not found. Use URP, or change the shader name in `Build()`. |
| Distance is always `0 m` | The player is not found. Check the `Player` tag or assign `player`. |
| The label is missing or shows squares | Import the TMP Essential Resources. |
| The label does not face the camera | Check that the camera has the `MainCamera` tag. |
| The ring is half underground | Increase `ringHeight` or place the target higher. |
| Ring too big or too small | Change `radius`. |
