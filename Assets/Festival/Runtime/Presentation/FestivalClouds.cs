using Festival.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Festival.Presentation
{
    /// <summary>LIGHT-2: soft clouds drift across day skies at the festival, and now and then the one nearest the zenith turns into
    /// something funny. CloudShapes works the sky out from the round's spin seed and clock, so every player sees the same duck at
    /// the same moment. Nights, dust storms and camp have none. Each cloud is one mesh of puff quads in the built-in Sprites/Default
    /// shader (unlit, fog-free, always in player builds) with a puff texture made here, as the art is frozen. Driven by
    /// FestivalWorld.SetLighting with no network calls; nothing collides or casts a shadow.</summary>
    public sealed class FestivalClouds
    {
        public const string RootName="Festival clouds";
        // A touch see-through, so the dusk sky shows through the puffs' soft edges.
        public const float Opacity=.85f;
        // Cream tops warming to peach-orange undersides, the colour of the low festival sun (FestivalWorld's twilight sun).
        private static readonly Color Top=new Color(1f,.95f,.86f),Underside=new Color(1f,.67f,.48f);
        private const int PuffPixels=64;
        private static readonly int ColorId=Shader.PropertyToID("_Color");

        private readonly Transform root;
        private readonly Texture2D puff;
        private readonly Material material;
        private readonly Transform[] clouds=new Transform[CloudShapes.MaxClouds];
        private readonly Mesh[] meshes=new Mesh[CloudShapes.MaxClouds];
        private readonly MeshRenderer[] renderers=new MeshRenderer[CloudShapes.MaxClouds];
        // What each cloud shows now: its fade, and how far into which shape it is, so a mesh is redrawn only when that changes.
        private readonly float[] alphas=new float[CloudShapes.MaxClouds],morphs=new float[CloudShapes.MaxClouds];
        private readonly int[] shapes=new int[CloudShapes.MaxClouds];
        private readonly Vector3[] corners=new Vector3[4*CloudShapes.MaxPuffs];
        private readonly Color[] tints=new Color[4*CloudShapes.MaxPuffs];
        private readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        private CloudShapes.Sky sky;
        private int seed;
        // Whether the sky shows, and the phase, festival, level, encore tier, spin and whole second of clock it was worked out for.
        private bool shown;
        private (string,int,int,int,int,double) shownFor=(null,-1,-1,-1,0,0);

        // The pool: every cloud a level can have, each with a quad for every puff a cloud can have. Hidden until a day sky.
        public FestivalClouds(Transform festival)
        {
            root=new GameObject(RootName).transform;root.SetParent(festival,false);root.gameObject.SetActive(false);
            puff=Puff();material=new Material(Shader.Find("Sprites/Default")){name="Festival cloud puffs",mainTexture=puff};
            var uv=new Vector2[corners.Length];var triangles=new int[6*CloudShapes.MaxPuffs];
            for(int i=0;i<CloudShapes.MaxPuffs;i++)
            {
                uv[4*i]=new Vector2(0,0);uv[4*i+1]=new Vector2(0,1);uv[4*i+2]=new Vector2(1,1);uv[4*i+3]=new Vector2(1,0);
                triangles[6*i]=4*i;triangles[6*i+1]=4*i+1;triangles[6*i+2]=4*i+2;triangles[6*i+3]=4*i;triangles[6*i+4]=4*i+2;triangles[6*i+5]=4*i+3;
            }
            for(int c=0;c<clouds.Length;c++)
            {
                var cloud=new GameObject("Cloud "+c);cloud.transform.SetParent(root,false);clouds[c]=cloud.transform;
                var mesh=new Mesh{name="Cloud "+c};mesh.MarkDynamic();mesh.vertices=corners;mesh.uv=uv;mesh.colors=tints;mesh.triangles=triangles;meshes[c]=mesh;
                // Unity culls by these bounds and leaves them alone when Draw moves the puffs, so they are set once to the whole
                // footprint every puff stays inside, ordinary, shaped or halfway between. Bounds of the empty pool would be a point,
                // and the whole cloud would vanish the moment its middle left the view.
                mesh.bounds=new Bounds(Vector3.zero,new Vector3(2*CloudShapes.HalfWidth,2*CloudShapes.HalfHeight,1));
                cloud.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=cloud.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderers[c]=renderer;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
        }

        /// <summary>Clouds fill day skies at the festival, but not while a dust storm blows; nights and camp have none. They pop
        /// off and on, like the storm's fog.</summary>
        public static bool Shows(RoundState state)=>FestivalWorld.ShowsFestival(state.Phase)&&!FestivalNightLighting.IsNight(state)&&!FestivalSimulation.DustStorm(state);

        /// <summary>Shows the round's sky at its clock, kept over `viewer` (a world position) the way a real sky is: far enough off
        /// that walking the grounds never brings a cloud nearer, and never past the camera's 130 m far clip.</summary>
        public void Apply(RoundState state,Vector3 viewer)
        {
            // Shows makes a little garbage (a level's tuning, a storm's random), so it is rechecked only when what it reads changes.
            // Storms start and stop on whole seconds, so checking once a second of clock misses none of them.
            var key=(state.Phase,state.FestivalIndex,state.LevelIndex,state.EncoreTier,state.SpinSeed,System.Math.Floor(state.ElapsedSeconds));
            if(key!=shownFor){shownFor=key;shown=Shows(state);}
            if(root.gameObject.activeSelf!=shown)root.gameObject.SetActive(shown);
            if(!shown)return;
            if(sky==null||seed!=state.SpinSeed){seed=state.SpinSeed;sky=CloudShapes.For(seed);Rebuild();}
            var ground=root.parent.InverseTransformPoint(viewer);root.localPosition=new Vector3(ground.x,0,ground.z);
            double now=state.ElapsedSeconds;int show=sky.ShowAt(now);
            for(int c=0;c<sky.Clouds.Length;c++)
            {
                var at=sky.At(c,now);var place=new Vector3(at.X,at.Y,at.Z);
                // Facing along the viewer's line of sight puts the shape's x to their right and its y up.
                clouds[c].localPosition=place;clouds[c].localRotation=Quaternion.LookRotation(place);
                if(at.Alpha!=alphas[c]){alphas[c]=at.Alpha;block.SetColor(ColorId,new Color(1,1,1,Opacity*at.Alpha));renderers[c].SetPropertyBlock(block);}
                int shape=show>=0&&sky.Shows[show].Cloud==c?sky.Shows[show].Shape:-1;
                float amount=shape<0?0:Mathf.SmoothStep(0,1,sky.Shows[show].Amount(now));
                if(amount!=morphs[c]||amount>0&&shape!=shapes[c])Draw(c,shape,amount);
            }
        }

        // A new level's sky: its clouds in their own shapes, the rest of the pool put away.
        private void Rebuild()
        {
            for(int c=0;c<clouds.Length;c++)
            {
                clouds[c].gameObject.SetActive(c<sky.Clouds.Length);alphas[c]=-1;
                if(c<sky.Clouds.Length)Draw(c,-1,0);
            }
        }
        // Lays cloud c's puffs out `amount` of the way into `shape` (-1 none), shaded from warm undersides to cream tops.
        private void Draw(int c,int shape,float amount)
        {
            var own=sky.Clouds[c].Puffs;var into=shape<0?own:CloudShapes.Shapes[shape].Puffs;
            for(int i=0;i<CloudShapes.MaxPuffs;i++)
            {
                var p=CloudShapes.Morph(own,into,i,amount);float left=p.X-p.Radius,right=p.X+p.Radius,low=p.Y-p.Radius,high=p.Y+p.Radius;
                corners[4*i]=new Vector3(left,low,0);corners[4*i+1]=new Vector3(left,high,0);corners[4*i+2]=new Vector3(right,high,0);corners[4*i+3]=new Vector3(right,low,0);
                tints[4*i]=tints[4*i+3]=Shade(low);tints[4*i+1]=tints[4*i+2]=Shade(high);
            }
            meshes[c].vertices=corners;meshes[c].colors=tints;morphs[c]=amount;shapes[c]=shape;
        }
        private static Color Shade(float y)=>Color.Lerp(Underside,Top,Mathf.InverseLerp(-CloudShapes.HalfHeight,CloudShapes.HalfHeight,y));
        // A soft white disc, solid in the middle and fading to nothing well inside the square, so no puff shows an edge.
        private static Texture2D Puff()
        {
            var texture=new Texture2D(PuffPixels,PuffPixels,TextureFormat.RGBA32,true){name="Festival cloud puff",wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[PuffPixels*PuffPixels];
            for(int y=0;y<PuffPixels;y++)for(int x=0;x<PuffPixels;x++)
            {
                float dx=(x+.5f)/PuffPixels*2-1,dy=(y+.5f)/PuffPixels*2-1,fall=Mathf.Clamp01(1-dx*dx-dy*dy);
                pixels[y*PuffPixels+x]=new Color32(255,255,255,(byte)Mathf.RoundToInt(255*fall*fall));
            }
            texture.SetPixels32(pixels);texture.Apply(true);
            return texture;
        }

        /// <summary>Destroys what the clouds made: their material, puff texture and meshes. FestivalWorld calls it as it goes.</summary>
        public void Dispose()
        {
            Dispose(material);Dispose(puff);
            foreach(var mesh in meshes)Dispose(mesh);
        }
        private static void Dispose(Object value){if(value==null)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
    }
}
