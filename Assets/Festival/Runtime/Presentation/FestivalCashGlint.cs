using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Festival.Presentation
{
    /// <summary>ECON-8: a four-point glint over each random cash find within Range of the eye, so a small pile catches the eye from
    /// about 15 m off. It twinkles, or holds still with reduced motion. The glints are a pool, reused frame to frame and hidden
    /// whenever the view has no find to show (another phase, a spirit's view, a session left); nothing in them collides.</summary>
    public sealed class FestivalCashGlint : MonoBehaviour
    {
        public const string GlintName="Cash glint";
        public const float Range=15;
        // Over the pile (drops stand at 0.2 m), twinkling between SmallSize and BigSize Twinkles times a second, a quarter turn a second.
        const float Height=.6f,SmallSize=.35f,BigSize=.7f,SteadySize=.55f,Twinkles=1.6f,TurnSpeed=90;

        FestivalSession session;
        Transform pool;
        readonly List<Transform> glints=new List<Transform>();

        void Awake(){session=GetComponent<FestivalSession>();}
        void LateUpdate()
        {
            if(session==null)return;
            var eye=session.ViewCamera!=null?session.ViewCamera.transform.position:Vector3.zero;
            Follow(session.State,eye,Time.unscaledTime,session.Profile.Data.ReducedMotion);
        }

        /// <summary>A glint over each of the view's random finds within Range of eye, facing it. Returns how many show.</summary>
        public int Follow(RoundState state,Vector3 eye,float time,bool reducedMotion)
        {
            int shown=0;
            if(state!=null&&state.Phase=="Playing")foreach(var drop in state.Drops)
            {
                var at=new Vector3(drop.X,Height,drop.Z);
                if(!drop.CashFind||(at-eye).sqrMagnitude>Range*Range)continue;
                if(shown==glints.Count)glints.Add(MakeGlint());
                var glint=glints[shown++];glint.gameObject.SetActive(true);glint.position=at;
                // Each find twinkles out of step with the other, from its spot.
                float phase=drop.X*.37f+drop.Z*.61f,size=reducedMotion?SteadySize:Mathf.Lerp(SmallSize,BigSize,Mathf.Abs(Mathf.Sin((time*Twinkles+phase)*Mathf.PI)));
                var facing=at-eye;if(facing.sqrMagnitude<1e-6f)facing=Vector3.forward;
                glint.rotation=Quaternion.LookRotation(facing)*Quaternion.Euler(0,0,reducedMotion?45:time*TurnSpeed+phase*57.3f);
                glint.localScale=Vector3.one*size;
            }
            for(int i=shown;i<glints.Count;i++)if(glints[i].gameObject.activeSelf)glints[i].gameObject.SetActive(false);
            return shown;
        }

        // Two thin crossed quads in the stage's gold glow, which face the eye as one.
        Transform MakeGlint()
        {
            if(pool==null){pool=new GameObject("Cash glints").transform;pool.SetParent(transform,false);}
            var glint=new GameObject(GlintName).transform;glint.SetParent(pool,false);
            foreach(float angle in new[]{0f,90f})
            {
                var ray=GameObject.CreatePrimitive(PrimitiveType.Quad);ray.name="Glint ray";ray.transform.SetParent(glint,false);
                ray.transform.localRotation=Quaternion.Euler(0,0,angle);ray.transform.localScale=new Vector3(1,.14f,1);
                var renderer=ray.GetComponent<Renderer>();renderer.sharedMaterial=FestivalArtView.MaterialFor("StageGlowGold");
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                var collider=ray.GetComponent<Collider>();collider.enabled=false;Dispose(collider);
            }
            return glint;
        }
        void OnDestroy(){if(pool!=null)Dispose(pool.gameObject);glints.Clear();}
        static void Dispose(Object owned){if(Application.isPlaying)Destroy(owned);else DestroyImmediate(owned);}
    }
}
