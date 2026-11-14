using UnityEngine;
using TMPro;

namespace Huisheng.PlantMusic
{
    public class PlantMusicGarden : MonoBehaviour
    {
        [Range(0,1)] public float volume=.65f;
        public AudioClip[] plantSounds=new AudioClip[4];
        public int totalNotes;
        public PlantProximitySound[] Plants {get;private set;}
        public PlantDemoPlayer Player {get;private set;}
        TMP_FontAsset font;
        TextMeshProUGUI status;
        Transform[] art=new Transform[4];
        SpriteRenderer[] zones=new SpriteRenderer[4];
        Sprite disc,ring;
        readonly string[] names={"花朵","竹子","蘑菇","大树"};
        readonly string[] instruments={"钢琴","笛子","木琴","低音弦乐"};
        readonly Color ink=new Color(.23f,.28f,.23f);
        readonly Color accent=new Color(.39f,.44f,.35f);
        RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
        {
            var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);
            var r=g.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;return r;
        }
        TextMeshProUGUI Label(string text,Transform parent,Vector2 pos,Vector2 size,float fs,Color color)
        {
            var r=Rect(text,parent,pos,size);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=text;t.fontSize=fs;t.color=color;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;return t;
        }
        Sprite Circle(bool outline)
        {
            var t=new Texture2D(128,128,TextureFormat.RGBA32,false);
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(64,64));
                float alpha=outline?Mathf.Clamp01(62-d)*Mathf.Clamp01(d-59):Mathf.Clamp01(62-d);
                t.SetPixel(x,y,new Color(1,1,1,alpha));
            }
            t.Apply();return Sprite.Create(t,new Rect(0,0,128,128),new Vector2(.5f,.5f),128);
        }
        SpriteRenderer Visual(string name,Transform parent,Vector3 pos,Sprite sprite,Color color,Vector3 scale,int order)
        {
            var g=new GameObject(name,typeof(SpriteRenderer));g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=scale;
            var sr=g.GetComponent<SpriteRenderer>();sr.sprite=sprite;sr.color=color;sr.sortingOrder=order;return sr;
        }
        void Awake()
        {
            font=TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("PlantMusic/ChineseFont"));
            disc=Circle(false);ring=Circle(true);
            var camera=Camera.main;camera.orthographic=true;camera.orthographicSize=5.4f;
            camera.backgroundColor=new Color(.86f,.88f,.82f);
            var atlas=Resources.Load<Texture2D>("PlantMusic/BotanicalAtlas");
            Plants=new PlantProximitySound[4];
            for(int i=0;i<4;i++)
            {
                int index=i;float x=(i-1.5f)*4;
                var g=new GameObject("Plant_"+i);g.transform.SetParent(transform,false);g.transform.localPosition=new Vector3(x,-.65f,0);
                var zone=g.AddComponent<CircleCollider2D>();zone.isTrigger=true;zone.radius=1.3f;
                var sound=g.AddComponent<PlantProximitySound>();sound.sound=plantSounds[i]?plantSounds[i]:Resources.Load<AudioClip>($"PlantMusic/voice{i}_0");sound.volume=volume*.5f;
                sound.triggered.AddListener(()=>OnPlantTriggered(index));Plants[i]=sound;
                zones[i]=Visual("感应范围",g.transform,Vector3.zero,ring,new Color(.63f,.56f,.34f,0),Vector3.one*2.6f,-2);
                var texture=atlas ? atlas : Resources.Load<Texture2D>($"PlantMusic/plant{i}");
                var region=atlas ? new Rect((i%2)*texture.width/2f,(1-i/2)*texture.height/2f,texture.width/2f,texture.height/2f) : new Rect(0,0,texture.width,texture.height);
                float[] basePivots={.085f,.078f,.139f,.078f};
                var sprite=Sprite.Create(texture,region,new Vector2(.5f,atlas?basePivots[i]:.1f),region.height/3.7f);
                Visual("Ground shadow",g.transform,new Vector3(0,-.22f,0),disc,new Color(.20f,.27f,.16f,.10f),new Vector3(2.3f,.45f,1),-3);
                art[i]=Visual("Botanical specimen",g.transform,new Vector3(0,-.22f,0),sprite,Color.white,Vector3.one*1.15f,0).transform;
            }
            var p=new GameObject("Player",typeof(Rigidbody2D),typeof(CircleCollider2D));p.transform.SetParent(transform,false);p.transform.position=new Vector3(0,-3.3f,0);Player=p.AddComponent<PlantDemoPlayer>();
            Visual("Player halo",p.transform,Vector3.zero,disc,new Color(.86f,.8f,.55f,.15f),Vector3.one*.8f,2);
            Visual("Player body",p.transform,Vector3.zero,disc,new Color(.42f,.43f,.31f),Vector3.one*.42f,3);
            var canvas=new GameObject("Instructions",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));canvas.transform.SetParent(transform,false);canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1440,900);scaler.matchWidthOrHeight=1;
            var heading=Label("洄生  /  植物声景",canvas.transform,new Vector2(-445,365),new Vector2(430,50),27,ink);
            heading.alignment=TextAlignmentOptions.Left;
            var sub=Label("B O T A N I C A L   S O U N D S C A P E",canvas.transform,new Vector2(-445,323),new Vector2(430,35),13,accent);
            sub.alignment=TextAlignmentOptions.Left;
            for(int i=0;i<4;i++)
            {
                var obj=new GameObject(names[i],typeof(TextMeshPro));obj.transform.SetParent(Plants[i].transform,false);obj.transform.localPosition=new Vector3(0,-1.6f,0);
                var text=obj.GetComponent<TextMeshPro>();text.font=font;text.text=names[i]+"  /  "+instruments[i];text.fontSize=2.1f;text.color=ink;text.alignment=TextAlignmentOptions.Center;text.rectTransform.sizeDelta=new Vector2(3.8f,.6f);text.GetComponent<MeshRenderer>().sortingOrder=4;
            }
            Label("WASD / 方向键移动     ·     靠近植物，聆听一次回应",canvas.transform,new Vector2(0,-389),new Vector2(1320,40),18,accent);
            status=Label("",canvas.transform,new Vector2(0,-340),new Vector2(1320,40),19,ink);
        }
        void OnPlantTriggered(int index){totalNotes++;status.text=$"{names[index]}   ·   {instruments[index]}";}
        void Update()
        {
            if(Plants==null)return;
            for(int i=0;i<4;i++)
            {
                art[i].localScale=Vector3.one*1.15f*(1+.06f*Plants[i].Pulse);
                zones[i].color=Plants[i].PlayerInside?new Color(.63f,.56f,.34f,.25f):new Color(.63f,.56f,.34f,0);
            }
        }
        void OnDestroy(){if(font){Destroy(font.material);Destroy(font);}}
    }
}
