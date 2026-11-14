using System.IO;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine.Rendering.Universal;

public static class GrasslandBuilder
{
 const string Root="Assets/Huisheng";
 static Color C(string hex) { ColorUtility.TryParseHtmlString(hex,out var c); return c; }
 static Sprite MakeSprite(string name, int mask, bool path=false, bool flower=false)
 {
  string p=Root+"/Art/"+name+".png";
  var tex=new Texture2D(32,32,TextureFormat.RGBA32,false);
  var rng=new System.Random(317);
  for(int y=0;y<32;y++) for(int x=0;x<32;x++) {
   var c=path?C("#c4aa76"):C("#71934b");
   if(flower) { c=Color.clear; if((x-16)*(x-16)+(y-18)*(y-18)<17)c=C("#e6c775"); if(x==16&&y>6&&y<17)c=C("#3d653b"); }
   else {
    bool edge=((mask&1)==0&&y>28)||((mask&2)==0&&x>28)||((mask&4)==0&&y<3)||((mask&8)==0&&x<3);
    if(edge)c=path?C("#9c895e"):C("#405e3e");
    else if(rng.NextDouble()<0.09)c=path?C("#b69c6d"):C("#80a356");
   }
   tex.SetPixel(x,y,c);
  }
  tex.Apply();File.WriteAllBytes(p,tex.EncodeToPNG());Object.DestroyImmediate(tex);
  AssetDatabase.ImportAsset(p);
  var imp=(TextureImporter)AssetImporter.GetAtPath(p);imp.textureType=TextureImporterType.Sprite;imp.spritePixelsPerUnit=32;imp.filterMode=FilterMode.Point;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.mipmapEnabled=false;imp.SaveAndReimport();
  return AssetDatabase.LoadAssetAtPath<Sprite>(p);
 }
 static RuleTile Rules(string name,bool path) {
  var tile=ScriptableObject.CreateInstance<RuleTile>();tile.m_DefaultColliderType=Tile.ColliderType.None;
  var dirs=new[]{Vector3Int.up,Vector3Int.right,Vector3Int.down,Vector3Int.left};
  for(int mask=0;mask<16;mask++){
   var s=MakeSprite(name+"_"+mask,mask,path);if(mask==15)tile.m_DefaultSprite=s;
   var r=new RuleTile.TilingRule();r.m_NeighborPositions.Clear();r.m_Neighbors.Clear();r.m_Sprites=new[]{s};r.m_ColliderType=Tile.ColliderType.None;
   for(int i=0;i<4;i++){r.m_NeighborPositions.Add(dirs[i]);r.m_Neighbors.Add((mask&(1<<i))!=0?1:2);}
   tile.m_TilingRules.Add(r);
  }
  AssetDatabase.CreateAsset(tile,Root+"/Tiles/"+name+".asset");return tile;
 }
 static Tilemap Layer(Transform grid,string name,int order){var go=new GameObject(name,typeof(Tilemap),typeof(TilemapRenderer));go.transform.SetParent(grid);go.GetComponent<TilemapRenderer>().sortingOrder=order;return go.GetComponent<Tilemap>();}
 [MenuItem("洄生/创建第一张草地地图")]
 public static void Build(){
  if(AssetDatabase.LoadAssetAtPath<SceneAsset>(Root+"/Scenes/Grassland_01.unity")!=null) throw new System.Exception("Grassland_01 already exists; existing map preserved.");
  foreach(var d in new[]{"Art","Tiles","Scenes","Palettes"})Directory.CreateDirectory(Root+"/"+d);AssetDatabase.Refresh();
  var grass=Rules("GrassRule",false);var path=Rules("PathRule",true);
  var flower=ScriptableObject.CreateInstance<Tile>();flower.sprite=MakeSprite("Wildflower",15,false,true);flower.colliderType=Tile.ColliderType.None;AssetDatabase.CreateAsset(flower,Root+"/Tiles/Wildflower.asset");
  AssetDatabase.SaveAssets();
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  grass=AssetDatabase.LoadAssetAtPath<RuleTile>(Root+"/Tiles/GrassRule.asset");path=AssetDatabase.LoadAssetAtPath<RuleTile>(Root+"/Tiles/PathRule.asset");flower=AssetDatabase.LoadAssetAtPath<Tile>(Root+"/Tiles/Wildflower.asset");
  var grid=new GameObject("Grassland Grid",typeof(Grid));var ground=Layer(grid.transform,"Ground - Grass Rule",0);var trail=Layer(grid.transform,"Path - Dirt Rule",1);var details=Layer(grid.transform,"Details - Wildflowers",2);
  int count=0;for(int y=-10;y<=10;y++)for(int x=-17;x<=17;x++) {
   if(x*x/310f+y*y/116f>1f+0.06f*Mathf.Sin(x*1.7f+y))continue;
   var pos=new Vector3Int(x,y,0);ground.SetTile(pos,grass);count++;
   float line=2.6f*Mathf.Sin(x*0.19f);if(Mathf.Abs(y-line)<1.15f)trail.SetTile(pos,path);
   else if((x*37+y*71+4000)%47==0)details.SetTile(pos,flower);
  }
  new GameObject("Player Spawn").transform.position=new Vector3(-12,-2,0);
  var cam=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));cam.tag="MainCamera";cam.transform.position=new Vector3(0,0,-10);var camera=cam.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=12.5f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=C("#233c38");
  cam.AddComponent<UniversalAdditionalCameraData>();
  var light=new GameObject("Global Light 2D",typeof(Light2D)).GetComponent<Light2D>();light.lightType=Light2D.LightType.Global;light.intensity=1;
  PlayerSettings.productName="洄生";EditorSettings.defaultBehaviorMode=EditorBehaviorMode.Mode2D;
  EditorSceneManager.SaveScene(scene,Root+"/Scenes/Grassland_01.unity");
  var palette=GridPaletteUtility.CreateNewPalette(Root+"/Palettes","Grassland",GridLayout.CellLayout.Rectangle,GridPalette.CellSizing.Automatic,Vector3.one,GridLayout.CellSwizzle.XYZ,TransparencySortMode.Default,Vector3.forward);
  var pal=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(palette));grass=AssetDatabase.LoadAssetAtPath<RuleTile>(Root+"/Tiles/GrassRule.asset");path=AssetDatabase.LoadAssetAtPath<RuleTile>(Root+"/Tiles/PathRule.asset");flower=AssetDatabase.LoadAssetAtPath<Tile>(Root+"/Tiles/Wildflower.asset");var map=pal.GetComponentInChildren<Tilemap>();map.SetTile(Vector3Int.zero,grass);map.SetTile(Vector3Int.right*2,path);map.SetTile(Vector3Int.right*4,flower);PrefabUtility.SaveAsPrefabAsset(pal,AssetDatabase.GetAssetPath(palette));PrefabUtility.UnloadPrefabContents(pal);
  EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"/Scenes/Grassland_01.unity",true)};
  AssetDatabase.SaveAssets();Selection.activeGameObject=grid;SceneView.lastActiveSceneView?.FrameSelected();
  Debug.Log("HUISHENG_MAP_READY grass="+count+" rules=32");
 }
}
