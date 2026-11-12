using UnityEngine;
using UnityEngine.EventSystems;
namespace Level02.DeerRescue
{
    public sealed class RescueDressingDrag : MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        public DeerRescueController owner;
        RectTransform rect,parent;
        Vector2 home,offset;
        bool dragging;
        public void OnBeginDrag(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left) return;
            rect=(RectTransform)transform; parent=(RectTransform)rect.parent; home=rect.anchoredPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,e.position,e.pressEventCamera,out Vector2 local);
            offset=(Vector2)rect.localPosition-local; dragging=true; rect.SetAsLastSibling();
        }
        public void OnDrag(PointerEventData e)
        {
            if(!dragging) return;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,e.position,e.pressEventCamera,out Vector2 local)) rect.localPosition=local+offset;
        }
        public void OnEndDrag(PointerEventData e)
        {
            if(!dragging) return; dragging=false;
            if(owner==null || !owner.DropDressing(e.position,e.pressEventCamera)) rect.anchoredPosition=home;
        }
        void OnDisable() { if(dragging && rect!=null) rect.anchoredPosition=home; dragging=false; }
    }
}
