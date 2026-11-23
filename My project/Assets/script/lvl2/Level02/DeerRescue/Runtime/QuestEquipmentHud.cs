using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Level02.DeerRescue
{
    // Un solo inventario. Le missioni forniscono i dati; nessun'altra box disegna gli oggetti.
    public sealed class QuestEquipmentHud : MonoBehaviour
    {
        GameObject root;
        RectTransform panel;
        TMP_Text heading,items;
        public void Show(bool axe,bool kit,bool net,int fish,int planks,bool visible)
        {
            var rows=new List<string>();
            if(axe)rows.Add("• Axe");if(kit)rows.Add("• Aid kit");if(net)rows.Add("• Net");
            if(fish>0)rows.Add("• Fish: "+fish);if(planks>0)rows.Add("• Planks: "+planks);
            if(!visible || rows.Count==0){Hide();return;}
            if(root==null)
            {
                var canvas=MushroomUI.Canvas("Quest_Equipment_List");root=canvas.gameObject;root.GetComponent<Canvas>().sortingOrder=700;
                var bg=MushroomUI.Image("Equipment card",canvas,new Color(.055f,.13f,.12f,.92f));bg.raycastTarget=false;bg.sprite=RescueGamePanel.RoundedPanelSprite();bg.type=Image.Type.Sliced;panel=bg.rectTransform;
                panel.anchorMin=panel.anchorMax=new Vector2(1,1);panel.pivot=new Vector2(1,1);panel.anchoredPosition=new Vector2(-24,-24);
                heading=MushroomUI.Text("Heading",panel,"EQUIPMENT",17,new Color(.94f,.80f,.48f));
                heading.alignment=TextAlignmentOptions.TopLeft;heading.raycastTarget=false;
                heading.rectTransform.anchorMin=heading.rectTransform.anchorMax=new Vector2(0,1);heading.rectTransform.pivot=new Vector2(0,1);
                heading.rectTransform.anchoredPosition=new Vector2(18,-13);heading.rectTransform.sizeDelta=new Vector2(184,25);
                items=MushroomUI.Text("Items",panel,"",20,new Color(.96f,.97f,.89f));items.alignment=TextAlignmentOptions.TopLeft;items.raycastTarget=false;
                items.rectTransform.anchorMin=items.rectTransform.anchorMax=new Vector2(0,1);items.rectTransform.pivot=new Vector2(0,1);
                items.rectTransform.anchoredPosition=new Vector2(18,-45);items.lineSpacing=8;
            }
            panel.sizeDelta=new Vector2(220,60+rows.Count*32);
            items.rectTransform.sizeDelta=new Vector2(184,rows.Count*32+5);items.text=string.Join("\n",rows);root.SetActive(true);
        }
        public void Hide(){if(root!=null)root.SetActive(false);}
        void OnDisable(){Hide();}
        void OnDestroy(){if(root!=null)Destroy(root);}
    }
}
