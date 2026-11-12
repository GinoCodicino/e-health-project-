using UnityEngine;
using UnityEngine.EventSystems;

namespace Level02.BridgeMission
{
    // Unico nuovo componente: gestisce il trascinamento di un'asse della UI.
    public sealed class BridgePlankDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        BridgeQuestController owner;
        int id,homeSibling;
        RectTransform rect,parent;
        Vector2 home,pointerOffset;
        bool dragging,locked;
        public void Initialize(BridgeQuestController controller,int plank)
        {
            owner=controller; id=plank;
            rect=(RectTransform)transform; parent=(RectTransform)rect.parent;
            home=rect.anchoredPosition; homeSibling=rect.GetSiblingIndex();
        }
        public void OnBeginDrag(PointerEventData data)
        {
            if(locked || data.button!=PointerEventData.InputButton.Left) return;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,data.position,data.pressEventCamera,out Vector2 local)) return;
            home=rect.anchoredPosition; homeSibling=rect.GetSiblingIndex();
            pointerOffset=(Vector2)rect.localPosition-local;
            dragging=true; rect.SetAsLastSibling();
        }
        public void OnDrag(PointerEventData data)
        {
            if(!dragging || locked) return;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,data.position,data.pressEventCamera,out Vector2 local))
                rect.localPosition=new Vector3(local.x+pointerOffset.x,local.y+pointerOffset.y,0);
        }
        public void OnEndDrag(PointerEventData data)
        {
            if(!dragging || locked) return;
            dragging=false;
            // Il controller verifica destinazione, lunghezza e avanzamento della quest.
            if(owner==null || !owner.DropPlank(id,data.position,data.pressEventCamera)) Restore();
        }
        public void LockTo(RectTransform slot)
        {
            locked=true; dragging=false;
            rect.position=slot.position; rect.localRotation=Quaternion.identity;
            rect.SetAsLastSibling();
            var image=GetComponent<UnityEngine.UI.Image>();
            if(image!=null) image.raycastTarget=false;
        }
        void Restore()
        {
            if(rect==null || locked) return;
            rect.anchoredPosition=home; rect.SetSiblingIndex(homeSibling);
        }
        void OnDisable() { if(dragging) { dragging=false; Restore(); } }
    }
}
