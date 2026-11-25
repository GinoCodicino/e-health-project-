using System.Collections;
using UnityEngine;

namespace Level02.DeerRescue
{
    public sealed class DeerRescueView : MonoBehaviour
    {
        public Transform animalVisual;
        public GameObject fallenBranches;
        public GameObject bandageVisual;
        public Vector3 restingEuler=new Vector3(90,0,0);
        Quaternion standing;
        Vector3 standingPosition;
        void Awake()
        {
            if(bandageVisual!=null) bandageVisual.SetActive(false);
            if(animalVisual!=null)
            {
                standing=animalVisual.localRotation;standingPosition=animalVisual.localPosition;
                animalVisual.localRotation=standing*Quaternion.Euler(restingEuler);
                float lowest=float.PositiveInfinity;
                foreach(Renderer renderer in animalVisual.GetComponentsInChildren<Renderer>()) lowest=Mathf.Min(lowest,renderer.bounds.min.y);
                if(!float.IsInfinity(lowest)) animalVisual.position+=Vector3.up*(transform.position.y+.02f-lowest);
            }
        }
        public void SetClearedBranches(int count)
        {
            if(fallenBranches==null)return;
            for(int i=0;i<fallenBranches.transform.childCount;i++) fallenBranches.transform.GetChild(i).gameObject.SetActive(i>=count);
        }
        public void Free() { if(fallenBranches!=null) fallenBranches.SetActive(false); }
        public void Heal()
        {
            Free(); if(bandageVisual!=null) bandageVisual.SetActive(true);
            if(animalVisual!=null) StartCoroutine(Stand());
        }
        IEnumerator Stand()
        {
            Quaternion from=animalVisual.localRotation;Vector3 fromPosition=animalVisual.localPosition;
            for(float t=0;t<1;t+=Time.deltaTime/.9f)
            {
                float step=Mathf.SmoothStep(0,1,t);
                animalVisual.localRotation=Quaternion.Slerp(from,standing,step);
                animalVisual.localPosition=Vector3.Lerp(fromPosition,standingPosition,step);yield return null;
            }
            animalVisual.localRotation=standing;animalVisual.localPosition=standingPosition;
        }
    }
}
