using System.Collections;
using UnityEngine;

public class NpcStorytellerAnimation : MonoBehaviour
{
    [Header("Articolazioni (Assegna da Hierarchy)")]
    [Tooltip("Osso o oggetto della testa (se lasciato vuoto non muove la testa)")]
    public Transform headBone;
    [Tooltip("Osso o oggetto della mano/braccio sinistro")]
    public Transform leftArmBone;
    [Tooltip("Osso o oggetto della mano/braccio destro")]
    public Transform rightArmBone;

    [Header("Gesticolazione Mani / Braccia")]
    public float gestureSpeed = 3f;
    public float gestureIntensity = 8f;

    [Header("Sguardo e Indicazione Funghi (Leggero)")]
    public float lookDownAngle = 7f;
    public float lookSideAngle = 16f;
    public float smoothSpeed = 2.5f;

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
        // Il corpo resta fisso nella sua posizione e rotazione iniziale
        transform.localPosition = initialPos;
        transform.localRotation = initialRot;

        // 1. Rotazione morbida e leggera della testa
        if (headBone != null)
        {
            headBone.localRotation = Quaternion.Slerp(
                headBone.localRotation,
                targetHeadRot,
                Time.deltaTime * smoothSpeed
            );
        }

        // 2. Movimento e gesticolazione delle mani / braccia
        float handFlutterLeft = Mathf.Sin(Time.time * gestureSpeed) * gestureIntensity;
        float handFlutterRight = Mathf.Cos(Time.time * gestureSpeed * 0.9f) * gestureIntensity;

        if (leftArmBone != null)
        {
            Quaternion flutter = Quaternion.Euler(handFlutterLeft, 0f, handFlutterLeft * 0.5f);
            Quaternion finalTarget = initialLeftArmRot * targetLeftArmOffset * flutter;
            leftArmBone.localRotation = Quaternion.Slerp(
                leftArmBone.localRotation,
                finalTarget,
                Time.deltaTime * smoothSpeed
            );
        }

        if (rightArmBone != null)
        {
            Quaternion flutter = Quaternion.Euler(handFlutterRight, 0f, -handFlutterRight * 0.5f);
            Quaternion finalTarget = initialRightArmRot * targetRightArmOffset * flutter;
            rightArmBone.localRotation = Quaternion.Slerp(
                rightArmBone.localRotation,
                finalTarget,
                Time.deltaTime * smoothSpeed
            );
        }
    }

    IEnumerator StorytellerRoutine()
    {
        while (true)
        {
            // PARLA AL GIOCATORE: gesticola leggermente con entrambe le mani
            if (headBone != null)
                targetHeadRot = initialHeadRot;
            targetLeftArmOffset = Quaternion.Euler(-10f, 8f, 6f);
            targetRightArmOffset = Quaternion.Euler(-10f, -8f, -6f);
            yield return new WaitForSeconds(Random.Range(3f, 4.5f));

            // INDICA A SINISTRA: leggero movimento di testa e mano sinistra
            if (headBone != null)
                targetHeadRot = initialHeadRot * Quaternion.Euler(lookDownAngle, -lookSideAngle, 0f);
            targetLeftArmOffset = Quaternion.Euler(14f, -10f, 18f);
            targetRightArmOffset = Quaternion.identity;
            yield return new WaitForSeconds(Random.Range(2.5f, 3.5f));

            // TORNA A PARLARE AL GIOCATORE
            if (headBone != null)
                targetHeadRot = initialHeadRot;
            targetLeftArmOffset = Quaternion.Euler(-8f, 10f, 4f);
            targetRightArmOffset = Quaternion.Euler(-8f, -10f, -4f);
            yield return new WaitForSeconds(Random.Range(2.5f, 4f));

            // INDICA A DESTRA: leggero movimento di testa e mano destra
            if (headBone != null)
                targetHeadRot = initialHeadRot * Quaternion.Euler(lookDownAngle, lookSideAngle, 0f);
            targetLeftArmOffset = Quaternion.identity;
            targetRightArmOffset = Quaternion.Euler(14f, 10f, -18f);
            yield return new WaitForSeconds(Random.Range(2.5f, 3.5f));
        }
    }
}