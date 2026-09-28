using UnityEngine;
using UnityEngine.Rendering;

namespace Festival.Presentation
{
    /// <summary>Cosmetic handle, rope and weighted head. The grip is the attachment point.</summary>
    public sealed class FestivalPoiRig : MonoBehaviour
    {
        const float RopeLength=.43f;
        const float FirstPersonRopeLength=.34f;
        Vector3 ball,velocity,previousGrip;
        LineRenderer rope;
        Transform head;
        bool initialized;
        bool firstPerson;
        int side;
        public bool Spinning;

        public static FestivalPoiRig Create(Transform parent,Vector3 grip,int side,bool led,bool firstPerson=false)
        {
            var root=new GameObject("Poi grip and tether");root.transform.SetParent(parent,false);
            root.transform.localPosition=grip;
            // Blender hand bones carry an imported scale. Cancel it so the
            // handle stays hand sized in both the world rig and camera rig.
            var inherited=parent.lossyScale;
            root.transform.localScale=new Vector3(1/Mathf.Max(.01f,Mathf.Abs(inherited.x)),
                1/Mathf.Max(.01f,Mathf.Abs(inherited.y)),1/Mathf.Max(.01f,Mathf.Abs(inherited.z)));
            var rig=root.AddComponent<FestivalPoiRig>();rig.side=side;rig.firstPerson=firstPerson;
            var handle=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name="Poi handle held in palm";handle.transform.SetParent(root.transform,false);
            handle.transform.localPosition=new Vector3(0,-.10f,0);
            handle.transform.localScale=new Vector3(.045f,.11f,.045f);
            handle.GetComponent<Renderer>().sharedMaterial=FestivalArtView.MaterialFor("Dark");
            Destroy(handle.GetComponent<Collider>());
            var cap=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name="Handle pommel";cap.transform.SetParent(root.transform,false);
            cap.transform.localPosition=new Vector3(0,.015f,0);cap.transform.localScale=Vector3.one*.075f;
            cap.GetComponent<Renderer>().sharedMaterial=FestivalArtView.MaterialFor("Metal");
            Destroy(cap.GetComponent<Collider>());
            var ballObject=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name=led?"LED weighted poi head":"Weighted practice poi head";
            ballObject.transform.localScale=Vector3.one*.20f;
            ballObject.GetComponent<Renderer>().sharedMaterial=FestivalArtView.MaterialFor(led?"Mint":"Gold");
            ballObject.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            Destroy(ballObject.GetComponent<Collider>());
            rig.head=ballObject.transform;
            rig.rope=root.AddComponent<LineRenderer>();
            rig.rope.useWorldSpace=true;rig.rope.positionCount=2;
            rig.rope.startWidth=.018f;rig.rope.endWidth=.016f;
            rig.rope.sharedMaterial=FestivalArtView.MaterialFor("Cream");
            rig.rope.shadowCastingMode=ShadowCastingMode.Off;
            return rig;
        }
        void LateUpdate()
        {
            // Anchor the cord at the palm end of the handle. The weighted head
            // hangs below the grip and lags behind movement like a short flail.
            Vector3 grip=transform.position+transform.up*(firstPerson ? .055f : -.19f);
            float length=firstPerson?FirstPersonRopeLength:RopeLength;
            Vector3 rest=firstPerson
                ? (transform.forward*.18f-transform.up*.04f+transform.right*(side==0?-.32f:.32f)).normalized*length
                : Vector3.down*length;
            if(!initialized||Vector3.Distance(previousGrip,grip)>3)
            {
                ball=grip+rest;velocity=Vector3.zero;initialized=true;
            }
            float dt=Mathf.Min(Time.deltaTime,.033f);
            if(dt>0)
            {
                // A damped rope constraint lets the head lag when the hand moves.
                // Performance adds a sideways impulse; gravity returns it below the grip.
                Vector3 acceleration=firstPerson
                    ? (grip+rest-ball)*18f-velocity*4f+Physics.gravity*.28f
                    : Physics.gravity*1.35f;
                if(Spinning)acceleration+=transform.right*(side==0?-1:1)*Mathf.Sin(Time.time*7f+side)*22f;
                velocity+=acceleration*dt;
                velocity*=Mathf.Exp(-2.6f*dt);
                ball+=velocity*dt;
                Vector3 offset=ball-grip;
                if(offset.sqrMagnitude<.0001f)offset=Vector3.down;
                Vector3 constrained=grip+offset.normalized*length;
                velocity=(constrained-(ball-velocity*dt))/Mathf.Max(dt,.001f);
                ball=constrained;
            }
            head.position=ball;
            rope.SetPosition(0,grip);rope.SetPosition(1,ball);
            previousGrip=grip;
        }
        void OnDisable(){initialized=false;if(head!=null)head.gameObject.SetActive(false);if(rope!=null)rope.enabled=false;}
        void OnEnable(){if(head!=null)head.gameObject.SetActive(true);if(rope!=null)rope.enabled=true;}
        void OnDestroy(){if(head!=null)Destroy(head.gameObject);}
    }
}
