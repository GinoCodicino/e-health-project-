using UnityEngine;

public class Hold : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    Material mat;

    void Awake()
    {
        mat = GetComponent<Renderer>().material;
        mat.EnableKeyword("_EMISSION");
        SetHighlight(false);
    }
    public void SetHighlight(bool highlight)
    {
        mat.SetColor("_EmissionColor", highlight ? Color.yellow : Color.black);
    }
}
