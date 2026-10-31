using UnityEngine;

/// <summary>
/// Regard à la souris pour le mode escalade (remplace celui de playercontroller).
/// Lit lookInputVector de playerinputcontroller, qui reste activé.
/// Yaw limité autour de l'orientation d'entrée en escalade, pitch limité.
/// </summary>
[RequireComponent(typeof(playerinputcontroller))]
public class ClimbLook : MonoBehaviour
{
    [SerializeField] private Transform pitchPivot;
    [Tooltip("Mettre la même valeur que mouseSensitivity de playercontroller (0.5 dans Level3).")]
    [SerializeField] private float mouseSensitivity = 0.5f;
    [SerializeField] private float maxYawAngle = 70f;
    [SerializeField] private float maxPitchAngle = 80f;

    private playerinputcontroller inputController;
    private float baseYaw;        // orientation du joueur à l'entrée en escalade
    private float entryPitch;     // pitch de la caméra à l'entrée en escalade
    private float yawOffset;      // écart cumulé par rapport à baseYaw
    private float pitch;

    private void Awake()
    {
        inputController = GetComponent<playerinputcontroller>();
    }

    private void OnEnable()
    {
        if (inputController == null) inputController = GetComponent<playerinputcontroller>();

        baseYaw = transform.eulerAngles.y;
        yawOffset = 0f;

        entryPitch = pitchPivot != null
            ? Mathf.DeltaAngle(0f, pitchPivot.localEulerAngles.x)
            : 0f;
        pitch = entryPitch;
    }

    private void OnDisable()
    {
        // playercontroller garde son propre cameraPitch privé (celui d'avant l'escalade)
        // et va réécrire pitchPivot dès sa première frame. On remet donc le pitch d'entrée
        // pour éviter un saut de caméra au retour en marche.
        if (pitchPivot != null)
            pitchPivot.localRotation = Quaternion.Euler(entryPitch, 0f, 0f);
    }

    private void Update()
    {
        Vector2 look = inputController.lookInputVector;

        yawOffset = Mathf.Clamp(yawOffset + look.x * mouseSensitivity, -maxYawAngle, maxYawAngle);
        transform.rotation = Quaternion.Euler(0f, baseYaw + yawOffset, 0f);

        pitch = Mathf.Clamp(pitch - look.y * mouseSensitivity, -maxPitchAngle, maxPitchAngle);
        if (pitchPivot != null)
            pitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
