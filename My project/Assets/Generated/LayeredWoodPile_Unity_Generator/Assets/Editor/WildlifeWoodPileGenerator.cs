using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class WildlifeWoodPileGenerator {
 const string Dir="Assets/WildlifeWoodPile";
 static Material bark,cut,dark;
 static System.Random random;
 static float R(float a,float b){return Mathf.Lerp(a,b,(float)random.NextDouble());}
 [MenuItem("Tools/Low Poly Wood/Generate Layered Wood Pile")]
 public static void Generate(){
  if(!AssetDatabase.IsValidFolder(Dir))AssetDatabase.CreateFolder("Assets","WildlifeWoodPile");
  random=new System.Random(124);
  bark=MaterialAsset("Bark",new Color(.32f,.18f,.095f));
  cut=MaterialAsset("CutWood",new Color(.73f,.53f,.28f));
  dark=MaterialAsset("DarkBark",new Color(.22f,.12f,.065f));
  GameObject root=new GameObject("LayeredWoodPile");
  // Front is negative Z. Two rows leave a central ground-level recess.
  for(int side=-1;side<=1;side+=2)for(int n=0;n<3;n++)
   Log(root,"BaseLog_"+side+"_"+n,new Vector3(side*.33f,.105f+n*.12f,.02f),new Vector3(R(-7,7),R(-12,12),90+R(-8,8)),.9f-n*.05f,.095f,0);
  for(int n=0;n<4;n++)
   Log(root,"CrossLog_"+n,new Vector3(R(-.11f,.11f),.31f+n*.055f,R(-.16f,.18f)),new Vector3(90,R(-40,40),R(-8,8)),.90f,.067f,0);
  for(int layer=0;layer<2;layer++)for(int n=0;n<5;n++){
   var g=Plate(root,"BarkSlab_"+layer+"_"+n,.24f+R(0,.10f),.58f+R(0,.15f),.022f);
   g.transform.localPosition=new Vector3((n-2)*.165f,.51f+layer*.055f,R(-.03f,.03f));
   g.transform.localRotation=Quaternion.Euler(R(-9,9),R(-16,16),R(-9,9));
  }
  for(int layer=0;layer<4;layer++)for(int n=0;n<6;n++)
   Log(root,"Branch_"+layer+"_"+n,new Vector3((n-2.5f)*.13f,.65f+layer*.055f,R(-.06f,.06f)),new Vector3(90,layer%2==0?R(-23,23):R(55,100),R(-8,8)),R(.74f,1.02f),R(.013f,.025f),0);
  // Hollow tubes run front-to-back, with real open ends and inner walls.
  for(int row=0;row<3;row++)for(int n=0;n<8-row;n++){
   float radius=R(.029f,.045f);
   Log(root,"HollowStem_"+row+"_"+n,new Vector3((n-(7-row)*.5f)*.085f, .91f+row*.075f,R(-.035f,.035f)),new Vector3(90+R(-5,5),R(-5,5),0),R(.45f,.61f),radius,radius*.69f);
  }
  for(int side=-1;side<=1;side+=2){
   Log(root,"UpperPost_"+side,new Vector3(side*.43f,1.15f,.10f),new Vector3(0,0,side*-5),.47f,.034f,0);
   for(int n=0;n<3;n++) Log(root,"TopRail_"+side+"_"+n,new Vector3(0,1.11f+n*.085f,side*.29f),new Vector3(0,0,90+R(-4,4)),1.04f,R(.022f,.032f),0);
  }
  for(int n=0;n<5;n++) Log(root,"LooseTopBranch_"+n,new Vector3(R(-.22f,.22f),1.22f+n*.022f,R(-.12f,.12f)),new Vector3(90,R(25,145),R(-6,6)),R(.66f,.93f),R(.012f,.021f),0);
  PrefabUtility.SaveAsPrefabAsset(root,Dir+"/LayeredWoodPile.prefab");Object.DestroyImmediate(root);
  AssetDatabase.SaveAssets();AssetDatabase.Refresh();
  EditorUtility.DisplayDialog("Wood pile generated","Prefab ready in "+Dir,"OK");
 }
 static Material MaterialAsset(string name,Color c){
  string path=Dir+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(m==null){var sh=Shader.Find("Universal Render Pipeline/Lit");if(sh==null)sh=Shader.Find("Standard");m=new Material(sh);AssetDatabase.CreateAsset(m,path);}m.color=c;return m;
 }
 static void Tri(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c){int n=v.Count;v.Add(a);v.Add(b);v.Add(c);t.Add(n);t.Add(n+1);t.Add(n+2);}
 static void Quad(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c,Vector3 d){Tri(v,t,a,b,c);Tri(v,t,a,c,d);}
 static Mesh Store(string name,List<Vector3> v,List<int> a,List<int> b){
  string path=Dir+"/"+name+".asset";var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(m==null){m=new Mesh();AssetDatabase.CreateAsset(m,path);}m.Clear();m.name=name;m.SetVertices(v);m.subMeshCount=2;m.SetTriangles(a,0);m.SetTriangles(b,1);m.RecalculateNormals();m.RecalculateBounds();return m;
 }
 static GameObject Render(GameObject root,string name,Mesh m){
  var g=new GameObject(name);g.transform.SetParent(root.transform,false);g.AddComponent<MeshFilter>().sharedMesh=m;
  g.AddComponent<MeshRenderer>().sharedMaterials=new Material[]{bark,cut};return g;
 }
 static Vector3 Ring(float angle,float radius,float y){return new Vector3(Mathf.Cos(angle)*radius,y,Mathf.Sin(angle)*radius);}
 static void Log(GameObject root,string name,Vector3 pos,Vector3 rot,float length,float radius,float inner){
  var v=new List<Vector3>();var t=new List<int>();var caps=new List<int>();int sides=7;
  float[] rr=new float[sides];for(int k=0;k<sides;k++)rr[k]=radius*R(.88f,1.1f);
  for(int k=0;k<sides;k++){
   int j=(k+1)%sides;float a=k*Mathf.PI*2/sides,b=j*Mathf.PI*2/sides;
   Vector3 A=Ring(a,rr[k],-length/2),B=Ring(b,rr[j],-length/2),C=Ring(b,rr[j],length/2),D=Ring(a,rr[k],length/2);
   Quad(v,t,A,D,C,B);
   if(inner<=0){Tri(v,caps,Vector3.down*length/2,A,B);Tri(v,caps,Vector3.up*length/2,C,D);}
   else{
    Vector3 E=Ring(a,inner,-length/2),F=Ring(b,inner,-length/2),G=Ring(b,inner,length/2),H=Ring(a,inner,length/2);
    Quad(v,t,E,F,G,H);Quad(v,caps,A,B,F,E);Quad(v,caps,D,H,G,C);
   }
  }
  var g=Render(root,name,Store(name,v,t,caps));g.transform.localPosition=pos;g.transform.localRotation=Quaternion.Euler(rot);
  if(inner<=0){var mc=g.AddComponent<MeshCollider>();mc.sharedMesh=g.GetComponent<MeshFilter>().sharedMesh;mc.convex=true;}
  else for(int k=0;k<sides;k++){
   float a=(k+.5f)*Mathf.PI*2/sides;
   var h=new GameObject("TubeWallHitbox_"+k);h.transform.SetParent(g.transform,false);
   h.transform.localPosition=Ring(a,(radius+inner)/2,0);h.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);
   var c=h.AddComponent<BoxCollider>();c.size=new Vector3(radius-inner,length,2*radius*Mathf.Tan(Mathf.PI/sides));
  }
 }
 static GameObject Plate(GameObject root,string name,float width,float length,float height){
  var v=new List<Vector3>();var t=new List<int>();var caps=new List<int>();
  Vector3[] top=new Vector3[8],bottom=new Vector3[8];
  for(int i=0;i<8;i++){float a=i*Mathf.PI/4;top[i]=new Vector3(Mathf.Cos(a)*width*.5f*R(.86f,1.12f),height*.5f+R(0,.012f),Mathf.Sin(a)*length*.5f*R(.9f,1.08f));bottom[i]=top[i]-Vector3.up*height;}
  for(int i=0;i<8;i++){int j=(i+1)%8;Tri(v,t,Vector3.up*height,top[j],top[i]);Tri(v,t,Vector3.down*height,bottom[i],bottom[j]);Quad(v,caps,top[i],top[j],bottom[j],bottom[i]);}
  var g=Render(root,name,Store(name,v,t,caps));g.GetComponent<MeshRenderer>().sharedMaterials=new Material[]{dark,cut};
  var c=g.AddComponent<BoxCollider>();c.size=new Vector3(width,height+.015f,length);return g;
 }
}
