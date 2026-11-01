using System;
using UnityEngine;

public class Hand : MonoBehaviour
{
    [Header("Visuel")]
    public Transform handVisual;          // Arm_L ou Arm_R
    public float moveSpeed = 12f;

    [Header("Endurance")]
    public float maxStamina = 500f;
    public float drainRate = 20f;         // 100 / 20 = 5 s sur une prise

    public Hold CurrentHold { get; private set; }   // null = main libre
    public bool IsHolding => CurrentHold != null;
    public float Stamina { get; private set; }

    public event Action<Hand> OnStaminaEmpty;
    public event Action<Hand> OnReleased;   // main devenue libre (lâchée ou fatiguée)

    Vector3 restLocalPosition;

    void Awake()
    {
        if (handVisual == null)
        {
            Debug.LogError($"Hand '{name}' : handVisual non assigné.", this);
            enabled = false;
            return;
        }
        restLocalPosition = handVisual.localPosition;
        Stamina = maxStamina;
    }

    public void Grab(Hold hold)
    {
        if (hold == null || hold.IsTaken) return;   // prise déjà prise
        if (IsHolding) Release(false);              // lâche l'ancienne sans notifier
        CurrentHold = hold;
        hold.SetTaken(this);
        Stamina = maxStamina;                        // barre pleine à chaque prise
    }

    public void Release(bool notify = true)
    {
        if (!IsHolding) return;
        CurrentHold.SetTaken(null);
        CurrentHold = null;
        if (notify) OnReleased?.Invoke(this);
    }

    void Update()
    {
        if (!IsHolding) return;                      // main libre : pas de fatigue
        Stamina -= drainRate * Time.deltaTime;
        if (Stamina <= 0f)
        {
            Stamina = 0f;
            Release();
            OnStaminaEmpty?.Invoke(this);
        }
    }

    void LateUpdate()
    {
        if (IsHolding)   // accrochée : reste sur la prise (position monde)
            handVisual.position = Vector3.Lerp(handVisual.position,
                CurrentHold.transform.position, moveSpeed * Time.deltaTime);
        else             // libre : revient à sa place sur le corps
            handVisual.localPosition = Vector3.Lerp(handVisual.localPosition,
                restLocalPosition, moveSpeed * Time.deltaTime);
    }
}