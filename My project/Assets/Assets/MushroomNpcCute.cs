using UnityEngine;

// Metti questo file FUORI da una cartella Editor (es. Assets/Level02/MushroomQuest/Runtime/).
// Viene aggiunto da solo all'NPC dal menu Tools > Level 02 > Make mushroom NPC cuter.
[DisallowMultipleComponent]
public class MushroomNpcCute : MonoBehaviour
{
    [Tooltip("Cappello a fungo: dondola piano.")]
    public Transform hat;
    [Tooltip("Cartellino col nome: guarda sempre la camera e fluttua leggermente.")]
    public Transform nameplate;
    [Tooltip("L'NPC si gira verso il giocatore quando è vicino.")]
    public bool lookAtPlayer = true;
    [Range(2f,20f)] public float lookDistance = 8f;
    [Range(1f,10f)] public float turnSpeed = 3f;
    [Range(0f,10f)] public float hatWobble = 4f;

    Transform player;
    Quaternion startRotation, hatBaseRotation;
    Vector3 plateBasePosition;

    void Start()
    {
        startRotation = transform.rotation;
        if (hat != null) hatBaseRotation = hat.localRotation;
        if (nameplate != null) plateBasePosition = nameplate.localPosition;
        playercontroller p = FindFirstObjectByType<playercontroller>();
        if (p != null) player = p.transform;
    }

    void LateUpdate()
    {
        float t = Time.time;
        if (hat != null)
            hat.localRotation = hatBaseRotation*Quaternion.Euler(Mathf.Sin(t*1.6f)*hatWobble*.75f,0f,Mathf.Sin(t*1.3f+1f)*hatWobble);
        if (nameplate != null)
        {
            nameplate.localPosition = plateBasePosition+Vector3.up*Mathf.Sin(t*2f)*0.03f;
            Camera cam = Camera.main;
            if (cam != null) nameplate.rotation = cam.transform.rotation;
        }
        if (lookAtPlayer && player != null)
        {
            Vector3 d = player.position-transform.position; d.y = 0f;
            bool near = d.sqrMagnitude < lookDistance*lookDistance && d.sqrMagnitude > 0.01f;
            Quaternion target = near ? Quaternion.LookRotation(d) : startRotation;
            transform.rotation = Quaternion.Slerp(transform.rotation,target,1f-Mathf.Exp(-turnSpeed*Time.deltaTime));
        }
    }
}
