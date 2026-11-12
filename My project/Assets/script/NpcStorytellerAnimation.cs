using System.Collections;
using UnityEngine;

public class NpcStorytellerAnimation : MonoBehaviour
{
    [Header("Articolazioni (Assegna da Hierarchy)")]
    [Tooltip("Osso della testa o del collo")]
    public Transform headBone;
    [Tooltip("Osso del braccio sinistro (es. LeftUpperArm o LeftArm)")]
    public Transform leftArmBone;
    [Tooltip("Osso del braccio destro (es. RightUpperArm o RightArm)")]
    public Transform rightArmBone;

    [Header("Respiro e Tronco")]
    public float breathingSpeed = 2f;
    public float breathingAmount = 0.03f;
    public float bodySwaySpeed = 3f;
    public float bodySwayAngle = 2.5f;

    [Header("Gesticolazione Mani / Braccia")]
    public float gestureSpeed = 3.5f;
    public float gestureIntensity = 12f;

    [Header("Sguardo e Indicazione Funghi")]
    public float lookDownAngle = 12f;
    public float lookSideAngle = 35f;
    public float smoothSpeed = 2f;

    private Vector3 initialPos;
    private Quaternion initialRot;
    private Quaternion initialHeadRot;
    private Quaternion initialLeftArmRot;
    private Quaternion initialRightArmRot;

    private Quaternion targetHeadRot;
    private Quaternion targetLeftArmOffset = Quaternion.identity;
    private Quaternion targetRightArmOffset = Quaternion.identity;

    void Start()
    {
        initialPos = transform.localPosition;
        initialRot = transform.localRotation;

        if (headBone != null)
        {
            initialHeadRot = headBone.localRotation;
            targetHeadRot = initialHeadRot;
        }

        if (leftArmBone != null)
            initialLeftArmRot = leftArmBone.localRotation;

        if (rightArmBone != null)
            initialRightArmRot = rightArmBone.localRotation;

        StartCoroutine(StorytellerRoutine());
    }

    void LateUpdate()
    {
        // 1. Respiro continuo
        float newY = initialPos.y + Mathf.Sin(Time.time * breathingSpeed) * breathingAmount;
        transform.localPosition = new Vector3(initialPos.x, newY, initialPos.z);

        // 2. Piccola oscillazione del busto
        float bodyAngleZ = Mathf.Sin(Time.time * bodySwaySpeed) * bodySwayAngle;
        float bodyAngleY = Mathf.Cos(Time.time * (bodySwaySpeed * 0.7f)) * (bodySwayAngle * 1.5f);
        transform.localRotation = initialRot * Quaternion.Euler(0, bodyAngleY, bodyAngleZ);

        // 3. Rotazione morbida della testa
        if (headBone != null)
        {
            headBone.localRotation = Quaternion.Slerp(headBone.localRotation, targetHeadRot, Time.deltaTime * smoothSpeed);
        }

        // 4. Movimento e gesticolazione delle braccia / mani
        float handFlutterLeft = Mathf.Sin(Time.time * gestureSpeed) * gestureIntensity;
        float handFlutterRight = Mathf.Cos(Time.time * gestureSpeed * 0.9f) * gestureIntensity;

        if (leftArmBone != null)
        {
            Quaternion flutter = Quaternion.Euler(handFlutterLeft, 0, handFlutterLeft * 0.5f);
            Quaternion finalTarget = initialLeftArmRot * targetLeftArmOffset * flutter;
            leftArmBone.localRotation = Quaternion.Slerp(leftArmBone.localRotation, finalTarget, Time.deltaTime * smoothSpeed);
        }

        if (rightArmBone != null)
        {
            Quaternion flutter = Quaternion.Euler(handFlutterRight, 0, -handFlutterRight * 0.5f);
            Quaternion finalTarget = initialRightArmRot * targetRightArmOffset * flutter;
            rightArmBone.localRotation = Quaternion.Slerp(rightArmBone.localRotation, finalTarget, Time.deltaTime * smoothSpeed);
        }
    }

    IEnumerator StorytellerRoutine()
    {
        while (true)
        {
            // PARLA AL GIOCATORE: gesticola con entrambe le mani aperte verso il petto/giocatore
            targetHeadRot = headBone != null ? initialHeadRot : Quaternion.identity;
            targetLeftArmOffset = Quaternion.Euler(-15f, 10f, 10f);
            targetRightArmOffset = Quaternion.Euler(-15f, -10f, -10f);
            yield return new WaitForSeconds(Random.Range(3f, 4.5f));

            // MOSTRA I FUNGHI A SINISTRA: volge la testa e allunga la mano sinistra verso il terreno
            if (headBone != null)
                targetHeadRot = initialHeadRot * Quaternion.Euler(lookDownAngle, -lookSideAngle, 0);
            targetLeftArmOffset = Quaternion.Euler(25f, -20f, 35f);  // braccio sinistro disteso verso il basso
            targetRightArmOffset = Quaternion.identity;               // braccio destro a riposo
            yield return new WaitForSeconds(Random.Range(2.5f, 3.5f));

            // TORNA A PARLARE AL GIOCATORE
            if (headBone != null)
                targetHeadRot = initialHeadRot;
            targetLeftArmOffset = Quaternion.Euler(-10f, 15f, 5f);
            targetRightArmOffset = Quaternion.Euler(-10f, -15f, -5f);
            yield return new WaitForSeconds(Random.Range(2.5f, 4f));

            // MOSTRA I FUNGHI A DESTRA: volge la testa e allunga la mano destra verso il terreno
            if (headBone != null)
                targetHeadRot = initialHeadRot * Quaternion.Euler(lookDownAngle, lookSideAngle, 0);
            targetLeftArmOffset = Quaternion.identity;                // braccio sinistro a riposo
            targetRightArmOffset = Quaternion.Euler(25f, 20f, -35f); // braccio destro disteso verso il basso
            yield return new WaitForSeconds(Random.Range(2.5f, 3.5f));
        }
    }
}