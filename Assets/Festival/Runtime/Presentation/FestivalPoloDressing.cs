using System.Collections.Generic;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>ART-1: Palm Mirage's polo-field look.
    /// - A palm stands at each of the grounds' tree spots, while the trees hide.
    /// - The main stage gets screens, an arch and its sign; the petal canopy reaches over the stage front.
    /// - A rainbow tower and a giant astronaut stand outside the walls.
    /// - A lawn tint covers the grounds, and desert ridges ring the view.
    /// All of it shows only at Palm Mirage (Show) and none of it collides, so sight lines, steps and paths stay exactly the rules'.</summary>
    public sealed class FestivalPoloDressing
    {
        public const string RootName="Palm Mirage dressing",HorizonName="Desert horizon",LawnName="Polo lawn";
        public static readonly string[] Models={"FestivalPalmTall","FestivalPalmLean","FestivalStageMirage","FestivalPetalCanopy","FestivalRainbowTower",
            "FestivalAstronaut","FestivalDesertRidge1","FestivalDesertRidge2","FestivalDesertRidge3","FestivalWheelBase","FestivalWheelRotor",
            "FestivalWheelGondola","FestivalGiggleTank","FestivalGiggleBalloon","FestivalVipBoard"};
        // ponytail: calibration knobs, tuned against Task 14's screenshots.
        // - Landmarks stand outside the ±40 m walls, and no palm grows within Clear metres of one.
        // - The ridges ring the view at HorizonRadius: the camera clips at 130 m and the fog ends at 90 m, so they read as hazy silhouettes.
        private static readonly (string Model,Vector3 At,float Yaw,float Clear)[] Landmarks={
            ("FestivalRainbowTower",new Vector3(-50,0,48),20,5),("FestivalAstronaut",new Vector3(54,0,8),-90,11)};
        private static readonly Vector3 StageAt=new Vector3(0,0,32);
        private const float HorizonRadius=115,LawnHeight=.0015f,BackRowZ=37.5f;
        private const int Ridges=12;
        private static readonly Color LawnTint=new Color(.55f,.78f,.42f);

        private readonly Transform root,horizon;
        private readonly List<GameObject> trees;
        private bool? shown;

        public FestivalPoloDressing(Transform grounds,List<GameObject> trees,Material earth)
        {
            this.trees=trees;
            root=new GameObject(RootName).transform;root.SetParent(grounds,false);root.gameObject.SetActive(false);
            for(int i=0;i<trees.Count;i++)
            {
                var spot=trees[i].transform;if(InLandmark(spot.localPosition))continue;
                Place(Mathf.Abs(spot.localPosition.z)>=BackRowZ?"FestivalPalmTall":"FestivalPalmLean",spot.localPosition,i*137%360,spot.localScale.x);
            }
            Place("FestivalStageMirage",StageAt,0,1);Place("FestivalPetalCanopy",StageAt,0,1);
            foreach(var l in Landmarks)Place(l.Model,l.At,l.Yaw,1);
            horizon=new GameObject(HorizonName).transform;horizon.SetParent(root,false);
            for(int i=0;i<Ridges;i++)
            {
                var ridge=FestivalArtView.Create(horizon,"FestivalDesertRidge"+(i%3+1));if(ridge==null)continue;
                var turn=Quaternion.Euler(0,i*360f/Ridges,0);ridge.transform.localRotation=turn;ridge.transform.localPosition=turn*Vector3.forward*HorizonRadius;
                ridge.transform.localScale=new Vector3(1,.8f+.15f*(i%4),1);
            }
            var lawn=GameObject.CreatePrimitive(PrimitiveType.Quad);lawn.name=LawnName;lawn.transform.SetParent(root,false);
            lawn.transform.localPosition=new Vector3(0,LawnHeight,0);lawn.transform.localRotation=Quaternion.Euler(90,0,0);lawn.transform.localScale=new Vector3(80,80,1);
            var collider=lawn.GetComponent<Collider>();collider.enabled=false;Dispose(collider);
            var renderer=lawn.GetComponent<Renderer>();renderer.sharedMaterial=new Material(earth){name="Polo lawn",color=LawnTint};
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Every frame: Palm Mirage's dressing on and the trees off, or the other way round, with the ridges kept round the view.
        /// It allocates nothing.</summary>
        public void Show(bool polo,Vector3 view)
        {
            if(shown!=polo)
            {
                shown=polo;root.gameObject.SetActive(polo);
                foreach(var tree in trees)if(tree!=null)tree.SetActive(!polo);
            }
            if(polo)horizon.position=new Vector3(view.x,root.position.y,view.z);
        }

        public static bool InLandmark(Vector3 at)
        {
            foreach(var l in Landmarks)if(new Vector2(at.x-l.At.x,at.z-l.At.z).sqrMagnitude<l.Clear*l.Clear)return true;
            return false;
        }

        private void Place(string model,Vector3 at,float yaw,float scale)
        {
            var go=FestivalArtView.Create(root,model);if(go==null)return;
            go.transform.localPosition=at;go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;
        }
        private static void Dispose(Object value){if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
    }
}
