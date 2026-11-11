using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonHoverScale : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField, Min(1f)]
    private float hoverMultiplier = 1.08f;

    [SerializeField, Min(0.01f)]
    private float animationDuration = 0.12f;

    private Button button;
    private Vector3 originalScale;
    private float currentMultiplier = 1f;
    private bool isHovered;

    private void Awake()
    {
        button = GetComponent<Button>();
        originalScale = transform.localScale;
    }

    private void Update()
    {
        float targetMultiplier =
            isHovered && button.IsInteractable()
            ? hoverMultiplier
            : 1f;

        float speed =
            Mathf.Max(hoverMultiplier - 1f, 0.001f)
            / animationDuration;

        currentMultiplier = Mathf.MoveTowards(
            currentMultiplier,
            targetMultiplier,
            speed * Time.unscaledDeltaTime
        );

        transform.localScale = originalScale * currentMultiplier;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }

    private void OnDisable()
    {
        isHovered = false;
        currentMultiplier = 1f;
        transform.localScale = originalScale;
    }
}
