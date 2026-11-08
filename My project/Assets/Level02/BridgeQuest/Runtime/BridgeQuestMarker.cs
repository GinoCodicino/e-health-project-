using TMPro;
using UnityEngine;

namespace Level02.BridgeMission
{
    // Etichetta di obiettivo sullo schermo; usa la Camera del giocatore anche senza MainCamera.
    public sealed class BridgeQuestMarker : MonoBehaviour
    {
        public Transform target;
        public Camera view;
        public string caption;
        GameObject root;
        RectTransform badge;
        TMP_Text label;
        Canvas canvas;
        QuestBeacon beacon;
        public void Show(Transform destination,string text,Camera camera)
        {
            target = destination; caption = text; view = camera;
            if (root == null)
            {
                RectTransform rt = MushroomUI.Canvas("BridgeQuest_ObjectiveMarker");
                root = rt.gameObject; canvas = root.GetComponent<Canvas>(); canvas.sortingOrder = 850;
                var image = MushroomUI.Image("Marker",rt,new Color(.04f,.16f,.19f,.92f));
                badge = image.rectTransform; badge.pivot = new Vector2(.5f,.5f);
                badge.anchorMin = badge.anchorMax = Vector2.zero;
                badge.sizeDelta = new Vector2(270,80); image.raycastTarget = false;
                label = MushroomUI.Text("Objective",badge,"",20,Color.white);
                label.alignment = TextAlignmentOptions.Center;
                MushroomUI.Fill(label.rectTransform,8);
            }
            root.SetActive(target != null);
            if(beacon==null)beacon=GetComponent<QuestBeacon>();
            if(beacon==null)beacon=gameObject.AddComponent<QuestBeacon>();
            beacon.Show(target);
        }
        public void Hide() { if (root != null) root.SetActive(false); if(beacon!=null)beacon.Hide(); }
        void LateUpdate()
        {
            if (root == null || !root.activeSelf || target == null || view == null) return;
            Vector3 screen = view.WorldToScreenPoint(target.position+Vector3.up*2.3f);
            bool behind = screen.z <= 0;
            Vector2 desired = new Vector2(screen.x,screen.y);
            Vector2 screenCenter = new Vector2(Screen.width*.5f,Screen.height*.5f);
            if (behind) desired = screenCenter-(desired-screenCenter);
            float marginX = 150*canvas.scaleFactor, marginY = 65*canvas.scaleFactor;
            desired.x = Mathf.Clamp(desired.x,marginX,Mathf.Max(marginX,Screen.width-marginX));
            desired.y = Mathf.Clamp(desired.y,marginY,Mathf.Max(marginY,Screen.height-marginY));
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,desired,null,out Vector2 local);
            badge.localPosition = local;
            float distance = Vector3.Distance(view.transform.position,target.position);
            label.text = caption+"\n"+Mathf.CeilToInt(distance)+" m"+(behind ? " - turn around" : "");
        }
        void OnDisable() { Hide(); }
        void OnDestroy() { if (root != null) Destroy(root); if(beacon!=null)Destroy(beacon); }
    }
}
