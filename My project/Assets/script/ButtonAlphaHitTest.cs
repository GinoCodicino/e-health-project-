using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ButtonAlphaHitTest : MonoBehaviour
{
    [SerializeField, Range(0.01f, 1f)]
    private float alphaThreshold = 0.1f;

    private void Start()
    {
        GetComponent<Image>().alphaHitTestMinimumThreshold = alphaThreshold;
    }
}
