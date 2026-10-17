using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Festival.Presentation
{
    /// <summary>VISION-4: the pool something casts on the ground right under it, because the festival sun is only 14 degrees up and a
    /// real shadow would land about 10 m away. A truth's pool is round and wider than a person; a fake's is subtly wrong, never
    /// missing: too small, or a goblin's, squat with a pointed ear to either side. Vision markers and the Light Show's extras and
    /// lights (MINI-3) share it, so the tell learnt in one is the tell in the other. Flat, with no collider, casting and receiving no
    /// shadow; it turns with the view, so the ears are always left and right.</summary>
    public sealed class FestivalShadowTellView
    {
        // Truths: a pool wider than the festivalgoer, so it shows around their feet; just clear of the footpaths (their tops sit
        // under .03 m). Night ground is about 15 of 255 at one dose, so the night pool glows instead of darkening.
        private const float Width=1.4f,Thickness=.01f,Lift=.05f;
        // Small: the pool at SmallShadow of a truth's width. Goblin: a squat pool GoblinWide by GoblinDeep with a pointed ear (a
        // square EarSize on a side, turned 45 degrees) EarOut to either side and EarBack toward the far edge.
        private const float SmallShadow=.6f,GoblinWide=1.3f,GoblinDeep=.75f,EarSize=.3f,EarOut=.62f,EarBack=.2f;
        private static readonly Vector3 TruthPool=new Vector3(Width,Thickness/2,Width),SmallPool=new Vector3(Width*SmallShadow,Thickness/2,Width*SmallShadow),GoblinPool=new Vector3(GoblinWide,Thickness/2,GoblinDeep);
        private static readonly Color NightGlow=new Color(.03f,.03f,.04f);

        public readonly Transform Root,Pool;
        public readonly GameObject Ears;
        private FestivalShadowTellView(Transform root,Transform pool,GameObject ears){Root=root;Pool=pool;Ears=ears;}

        /// <summary>A shadow named Shadow under parent: a Pool and two Ears, every piece drawn in material.</summary>
        public static FestivalShadowTellView Build(Transform parent,Material material)
        {
            var shadow=new GameObject("Shadow").transform;shadow.SetParent(parent,false);
            var pool=Flat(PrimitiveType.Cylinder,"Pool",shadow,material);pool.localScale=TruthPool;
            var ears=new GameObject("Ears");ears.transform.SetParent(shadow,false);ears.SetActive(false);
            for(int side=-1;side<=1;side+=2)
            {
                var ear=Flat(PrimitiveType.Cube,"Ear",ears.transform,material);
                ear.localPosition=new Vector3(side*EarOut,0,EarBack);ear.localRotation=Quaternion.Euler(0,45,0);ear.localScale=new Vector3(EarSize,Thickness,EarSize);
            }
            return new FestivalShadowTellView(shadow,pool,ears);
        }

        /// <summary>The normal pool, or, for a fake still showing its tells, its wrong one.</summary>
        public void Show(bool wrong,FestivalVisionMarkers.FakeShadow shape)
        {
            bool goblin=wrong&&shape==FestivalVisionMarkers.FakeShadow.Goblin;
            Pool.localScale=!wrong?TruthPool:goblin?GoblinPool:SmallPool;
            if(Ears.activeSelf!=goblin)Ears.SetActive(goblin);
        }

        /// <summary>On the ground at ground, turned with view.</summary>
        public void Place(Vector3 ground,Transform view)
        {
            Root.position=ground+Vector3.up*Lift;
            if(view!=null)Root.rotation=Quaternion.Euler(0,view.eulerAngles.y,0);
        }

        /// <summary>The pool's material, which its caller owns: dark by day; by night, when the ground is too dark for any shadow to
        /// show, a faint pale glow.</summary>
        public static Material Material(bool night)
        {
            var pool=new Material(Resources.Load<Material>("FestivalLit")){color=Color.black};
            if(pool.HasProperty("_Smoothness"))pool.SetFloat("_Smoothness",0);
            if(night&&pool.HasProperty("_EmissionColor")){pool.EnableKeyword("_EMISSION");pool.SetColor("_EmissionColor",NightGlow);}
            return pool;
        }

        private static Transform Flat(PrimitiveType shape,string name,Transform parent,Material material)
        {
            var piece=GameObject.CreatePrimitive(shape);piece.name=name;piece.transform.SetParent(parent,false);
            var collider=piece.GetComponent<Collider>();collider.enabled=false;
            if(Application.isPlaying)Object.Destroy(collider);else Object.DestroyImmediate(collider);
            var renderer=piece.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return piece.transform;
        }
    }
}
