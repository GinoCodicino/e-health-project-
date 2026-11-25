using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
public static class CalfPrefabBuilder {
 const string Root="Assets/LowPolyCalf";
 [MenuItem("Tools/Low Poly Wood/Generate Calf Prefab")]
 public static void Generate(){
  string path=Root+"/Calf.obj";
  var importer=AssetImporter.GetAtPath(path) as ModelImporter;
  if(importer==null){EditorUtility.DisplayDialog("Missing model","Copy the full Assets folder from the ZIP first.","OK");return;}
  importer.globalScale=1;importer.importNormals=ModelImporterNormals.Calculate;importer.normalSmoothingAngle=0;importer.SaveAndReimport();
  if(!AssetDatabase.IsValidFolder(Root+"/Materials"))AssetDatabase.CreateFolder(Root,"Materials");
  string[] names={"Brown","Cream","Pink","Hoof","Black","Nose","Leather","Gold"};
  Color[] colors={new Color(.38f,.17f,.075f),new Color(.86f,.81f,.69f),new Color(.70f,.43f,.34f),new Color(.19f,.105f,.07f),new Color(.035f,.024f,.018f),new Color(.30f,.16f,.13f),new Color(.16f,.065f,.035f),new Color(.79f,.56f,.12f)};
  Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");
  var materials=new Dictionary<string,Material>();
  for(int i=0;i<names.Length;i++){
   string mp=Root+"/Materials/"+names[i]+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(mp);
   if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,mp);}m.color=colors[i];materials[names[i]]=m;
  }
  var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
  GameObject root=new GameObject("LowPolyCalf");var visual=Object.Instantiate(model,root.transform);visual.name="Visual";
  foreach(var renderer in visual.GetComponentsInChildren<Renderer>()){
   var mats=renderer.sharedMaterials;for(int i=0;i<mats.Length;i++)if(mats[i]!=null){string key=mats[i].name.Replace(" (Instance)","");if(materials.ContainsKey(key))mats[i]=materials[key];}renderer.sharedMaterials=mats;
  }
  Hit(root,"BodyHitbox",new Vector3(0,.76f,-.03f),new Vector3(.55f,.54f,.99f));
  Hit(root,"HeadHitbox",new Vector3(0,1.02f,.56f),new Vector3(.37f,.43f,.48f));
  for(int side=-1;side<=1;side+=2)foreach(float z in new float[]{-.36f,.29f})Hit(root,"LegHitbox",new Vector3(side*.19f,.31f,z+.02f),new Vector3(.12f,.60f,.13f));
  PrefabUtility.SaveAsPrefabAsset(root,Root+"/LowPolyCalf.prefab");Object.DestroyImmediate(root);AssetDatabase.SaveAssets();AssetDatabase.Refresh();
  EditorUtility.DisplayDialog("Calf ready","Prefab saved in "+Root,"OK");
 }
 static void Hit(GameObject parent,string name,Vector3 center,Vector3 size){var g=new GameObject(name);g.transform.SetParent(parent.transform,false);var c=g.AddComponent<BoxCollider>();c.center=center;c.size=size;}
}
