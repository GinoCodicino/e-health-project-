using UnityEngine;

namespace Level02.BridgeMission
{
    // Il controller deve stare fuori dai due oggetti che attiva/disattiva.
    public sealed class BridgeCondition : MonoBehaviour
    {
        public GameObject intactBridge;
        public GameObject brokenBridge;
        public GameObject closedSign;
        public bool IsRepaired { get; private set; }
        public bool IsConfigured { get { return intactBridge != null && brokenBridge != null
            && intactBridge != brokenBridge && !transform.IsChildOf(intactBridge.transform)
            && !transform.IsChildOf(brokenBridge.transform); } }
        void Awake() { SetRepaired(false); }
        public bool SetRepaired(bool repaired)
        {
            if (!IsConfigured)
            {
                Debug.LogError("Bridge quest: assign two separate bridge objects. Keep the controller outside both.",this);
                return false;
            }
            // SetActive spegne anche tutti i collider del ponte nascosto.
            intactBridge.SetActive(repaired);
            brokenBridge.SetActive(!repaired);
            if (closedSign != null) closedSign.SetActive(!repaired);
            IsRepaired = repaired;
            return true;
        }
    }
}
