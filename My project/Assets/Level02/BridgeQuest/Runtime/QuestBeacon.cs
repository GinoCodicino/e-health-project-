using UnityEngine;
namespace Level02.BridgeMission
{
    // Solo indicazione visiva. Nessuna modifica a fiume, terreno, collider o NavMesh.
    public sealed class QuestBeacon : MonoBehaviour
    {
        public Color tint=new Color(1,.78f,.24f,.8f);
        [Tooltip("Facoltativo: percorso ordinato gia controllato, dal giocatore alla destinazione. Non collega automaticamente le sponde.")]
        public Transform[] routeWaypoints;
        GameObject visual;
        LineRenderer ring,beam,route;
        Material material;
        Transform destination;
        public void Show(Transform target)
        {
            destination=target;
            if(target==null){Hide();return;}
            if(visual==null)
            {
                visual=new GameObject("Quest beacon visual");
                Shader shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if(shader==null)shader=Shader.Find("Sprites/Default");
                if(shader==null){Destroy(visual);visual=null;return;}
                material=new Material(shader);material.color=tint;
                if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",tint);
                ring=Line("Ground ring",.09f);ring.loop=true;ring.positionCount=48;
                beam=Line("Beacon",.07f);beam.positionCount=2;
                route=Line("Optional checked route",.07f);
            }
            visual.SetActive(true);
        }
        LineRenderer Line(string name,float width)
        {
            var go=new GameObject(name);go.transform.SetParent(visual.transform,false);
            var l=go.AddComponent<LineRenderer>();l.sharedMaterial=material;l.useWorldSpace=true;l.widthMultiplier=width;
            l.startColor=l.endColor=tint;l.numCapVertices=4;l.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;l.receiveShadows=false;return l;
        }
        void LateUpdate()
        {
            if(visual==null || !visual.activeSelf)return;
            if(destination==null){Hide();return;}
            Vector3 p=destination.position+Vector3.up*.12f;float r=1.1f+Mathf.Sin(Time.unscaledTime*2)*.12f;
            for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;ring.SetPosition(i,p+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r));}
            beam.SetPosition(0,p);beam.SetPosition(1,p+Vector3.up*(3.7f+Mathf.Sin(Time.unscaledTime)*.25f));
            int count=routeWaypoints==null?0:routeWaypoints.Length;
            if(count>0 && (routeWaypoints[count-1]==null || Vector3.Distance(routeWaypoints[count-1].position,destination.position)>4))count=0;
            route.positionCount=count;
            for(int i=0;i<count;i++)route.SetPosition(i,routeWaypoints[i]==null?p:routeWaypoints[i].position+Vector3.up*.15f);
        }
        public void Hide(){if(visual!=null)visual.SetActive(false);}
        void OnDisable(){Hide();}
        void OnDestroy(){if(visual!=null)Destroy(visual);if(material!=null)Destroy(material);}
    }
}
