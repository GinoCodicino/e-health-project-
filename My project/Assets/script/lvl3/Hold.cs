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
        if (highlight)
        {
            mat.SetColor("_EmissionColor", Color.yellow);
        }
        else
        {
            mat.SetColor("_EmissionColor", Color.black);
        }
    }
}
