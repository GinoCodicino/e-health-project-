// Editor-only palette conversion. No pipeline package dependency is compiled into this utility.
using System;
using System.Linq;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace WoodlandVillageFree {
public static class PipelineMaterials {
 const string Root="Assets/WoodlandVillageFree";
 const string Menu="Tools/Woodland Village/Free/";
 [MenuItem(Menu+"Match palette to current render pipeline")]
 public static void MatchCurrentPipeline() {
  var pipeline=QualitySettings.renderPipeline??GraphicsSettings.defaultRenderPipeline;
  string kind=pipeline==null?"BuiltIn":pipeline.GetType().Name.Contains("Universal")?"URP":pipeline.GetType().Name.Contains("HDRenderPipeline")?"HDRP":null;
  if(kind==null)throw new InvalidOperationException("This render pipeline is not supported by the Woodland palette presets.");
  Apply(kind);
 }
 public static void Apply(string kind) {
  string shaderName=kind=="URP"?"Universal Render Pipeline/Lit":kind=="HDRP"?"HDRP/Lit":kind=="BuiltIn"?"Standard":null;
  if(shaderName==null)throw new ArgumentException("Unknown palette preset "+kind);
  var shader=Shader.Find(shaderName);
  if(shader==null)throw new InvalidOperationException("Install and configure the corresponding Unity render pipeline before applying "+kind+".");
  var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/ForestPalette.png");
  var active=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/ForestPalette.mat");
  if(texture==null||active==null)throw new InvalidOperationException("Keep the shipped shared palette material and atlas together with the models.");
  Undo.RecordObject(active,"Match Woodland palette to "+kind);
  Configure(active,shader,texture,kind);
  // Store an editable matching material variant without changing prefab or scene references.
  string variantPath=Root+"/Materials/ForestPalette_"+kind+".mat";
  var variant=AssetDatabase.LoadAssetAtPath<Material>(variantPath);
  if(variant==null&&AssetDatabase.LoadMainAssetAtPath(variantPath)!=null)throw new InvalidOperationException("Another asset occupies the generated palette variant path.");
  if(variant==null){variant=new Material(shader){name="ForestPalette_"+kind};AssetDatabase.CreateAsset(variant,variantPath);}
  Configure(variant,shader,texture,kind);EditorUtility.SetDirty(active);EditorUtility.SetDirty(variant);AssetDatabase.SaveAssetIfDirty(active);AssetDatabase.SaveAssetIfDirty(variant);
  Debug.Log("WOODLAND_PALETTE_APPLIED "+Root+" "+kind+" atlas="+AssetDatabase.GetAssetPath(texture));
 }
 [MenuItem(Menu+"Prepare current package demo scene")]
 public static void PrepareCurrentDemo() {
  var scene=SceneManager.GetActiveScene();
  if(!scene.path.StartsWith(Root+"/Scenes/",StringComparison.Ordinal))throw new InvalidOperationException("Open a demo scene from "+Root+"/Scenes first. Other scenes are never edited by this command.");
  MatchCurrentPipeline();var pipeline=QualitySettings.renderPipeline??GraphicsSettings.defaultRenderPipeline;
  bool hdrp=pipeline!=null&&pipeline.GetType().Name.Contains("HDRenderPipeline");
  var lights=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Light>(true));
  foreach(var light in lights)if(light.type==LightType.Directional){
   Undo.RecordObject(light,"Prepare Woodland demo sunlight");
   if(hdrp){var type=FindType("UnityEngine.Rendering.HighDefinition.HDAdditionalLightData");if(light.GetComponent(type)==null)Undo.AddComponent(light.gameObject,type);}
   light.intensity=hdrp?100000:1.2f;light.shadows=LightShadows.Soft;
  }
  if(hdrp){
   var volumeType=FindType("UnityEngine.Rendering.Volume");var profileType=FindType("UnityEngine.Rendering.VolumeProfile");
   var go=scene.GetRootGameObjects().FirstOrDefault(x=>x.name=="Woodland HDRP Demo Lighting");
   if(go==null){go=new GameObject("Woodland HDRP Demo Lighting");Undo.RegisterCreatedObjectUndo(go,"Create Woodland HDRP demo lighting");}
   var volume=go.GetComponent(volumeType)??Undo.AddComponent(go,volumeType);Set(volume,"isGlobal",true);Set(volume,"priority",20f);Set(volume,"weight",1f);
   string folder=Root+"/Materials/SceneSettings";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder(Root+"/Materials","SceneSettings");
   string path=folder+"/"+scene.name+"_HDRP.asset";var profile=AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
   if(profile==null&&AssetDatabase.LoadMainAssetAtPath(path)!=null)throw new InvalidOperationException("Another asset occupies the generated demo profile path.");
   if(profile!=null&&!profileType.IsInstanceOfType(profile))throw new InvalidOperationException("The existing demo settings asset has another type.");
   if(profile==null){profile=ScriptableObject.CreateInstance(profileType);AssetDatabase.CreateAsset(profile,path);}
   var exposure=Component(profile,"UnityEngine.Rendering.HighDefinition.Exposure");Override(exposure,"mode","Fixed");Override(exposure,"fixedExposure",13f);
   var environment=Component(profile,"UnityEngine.Rendering.HighDefinition.VisualEnvironment");Override(environment,"skyType",3);Override(environment,"skyAmbientMode","Dynamic");
   var sky=Component(profile,"UnityEngine.Rendering.HighDefinition.GradientSky");Override(sky,"top",new Color(.3f,.45f,.65f));Override(sky,"middle",new Color(.5f,.6f,.65f));Override(sky,"bottom",new Color(.18f,.15f,.1f));Override(sky,"skyIntensityMode","Multiplier");Override(sky,"multiplier",3000f);
   foreach(UnityEngine.Object component in (IEnumerable)profileType.GetField("components").GetValue(profile)){if(!AssetDatabase.Contains(component))AssetDatabase.AddObjectToAsset(component,profile);EditorUtility.SetDirty(component);}
   Set(volume,"sharedProfile",profile);EditorUtility.SetDirty(profile);EditorUtility.SetDirty(volume);AssetDatabase.SaveAssetIfDirty(profile);
   var cameraType=FindType("UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData");
   foreach(var camera in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Camera>(true))){var data=camera.GetComponent(cameraType)??Undo.AddComponent(camera.gameObject,cameraType);Set(data,"clearColorMode","Sky");EditorUtility.SetDirty(data);}
  }else{
   var previous=scene.GetRootGameObjects().FirstOrDefault(x=>x.name=="Woodland HDRP Demo Lighting");if(previous!=null)Undo.DestroyObjectImmediate(previous);
   RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.32f,.38f,.45f);RenderSettings.ambientEquatorColor=new Color(.22f,.25f,.2f);RenderSettings.ambientGroundColor=new Color(.12f,.1f,.08f);
  }
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Debug.Log("WOODLAND_DEMO_PREPARED "+scene.path+" hdrp="+hdrp);
 }
 static Type FindType(string name){var type=AppDomain.CurrentDomain.GetAssemblies().Select(x=>x.GetType(name)).FirstOrDefault(x=>x!=null);if(type==null)throw new InvalidOperationException("Install the corresponding render pipeline first: "+name);return type;}
 static object ConvertValue(object value,Type target){if(target.IsInstanceOfType(value))return value;return target.IsEnum?Enum.Parse(target,(string)value):Convert.ChangeType(value,target);}
 static void Set(object target,string name,object value){var type=target.GetType();var property=type.GetProperty(name,BindingFlags.Instance|BindingFlags.Public);if(property!=null){property.SetValue(target,ConvertValue(value,property.PropertyType));return;}var field=type.GetField(name,BindingFlags.Instance|BindingFlags.Public);if(field==null)throw new MissingMemberException(type.Name,name);field.SetValue(target,ConvertValue(value,field.FieldType));}
 static object Component(ScriptableObject profile,string name){var type=FindType(name);foreach(var existing in (IEnumerable)profile.GetType().GetField("components").GetValue(profile))if(existing.GetType()==type)return existing;return profile.GetType().GetMethod("Add",new[]{typeof(Type),typeof(bool)}).Invoke(profile,new object[]{type,true});}
 static void Override(object component,string name,object value){var parameter=component.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public).GetValue(component);Set(parameter,"value",value);Set(parameter,"overrideState",true);}
 static void Configure(Material material,Shader shader,Texture2D texture,string kind) {
  material.shader=shader;material.renderQueue=-1;material.enableInstancing=true;
  string textureSlot=kind=="URP"?"_BaseMap":kind=="HDRP"?"_BaseColorMap":"_MainTex";
  string colorSlot=kind=="BuiltIn"?"_Color":"_BaseColor";
  material.SetTexture(textureSlot,texture);material.SetColor(colorSlot,Color.white);
  if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.1f);
  if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",.1f);
  if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",0);
 }
}
}
