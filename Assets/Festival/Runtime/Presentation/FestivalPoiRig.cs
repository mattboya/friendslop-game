using UnityEngine;
using UnityEngine.Rendering;

namespace Festival.Presentation
{
    /// <summary>Cosmetic handle, rope and weighted head. The grip is the attachment point.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class FestivalPoiRig : MonoBehaviour
    {
        const float RopeLength=.62f;
        const float FirstPersonRopeLength=.34f;
        Vector3 ball,velocity,previousGrip;
        LineRenderer rope;
        Transform head;
        bool initialized;
        bool firstPerson;
        int side;
        Transform performer;
        float motionPhase;
        public bool Spinning;
        /// <summary>Walking spin while poi are equipped: each head wheels beside its hand.</summary>
        public bool Carrying;
        /// <summary>Butterfly speed in radians per second; a dance sets its own beat.</summary>
        public float SpinRate=9f;
        float carryAngle,spinUp;

        public static FestivalPoiRig Create(Transform parent,Vector3 grip,int side,bool led,bool firstPerson=false)
        {
            var root=new GameObject("Poi grip and tether");root.transform.SetParent(parent,false);root.layer=parent.gameObject.layer;
            // Blender hand bones carry an imported scale. Cancel it so the
            // handle and its offset stay hand sized in both camera and world.
            var inherited=parent.lossyScale;
            var invX=1/Mathf.Max(.01f,Mathf.Abs(inherited.x));
            var invY=1/Mathf.Max(.01f,Mathf.Abs(inherited.y));
            var invZ=1/Mathf.Max(.01f,Mathf.Abs(inherited.z));
            root.transform.localPosition=new Vector3(grip.x*invX,grip.y*invY,grip.z*invZ);
            root.transform.localScale=new Vector3(invX,invY,invZ);
            var rig=root.AddComponent<FestivalPoiRig>();rig.side=side;rig.firstPerson=firstPerson;
            var actor=parent.GetComponentInParent<FestivalCharacter>();
            // Camera hands have no FestivalCharacter parent. Their own camera
            // attachment frame still drives the same opposed orbit in first person.
            rig.performer=actor!=null?actor.transform:firstPerson?parent:null;
            rig.motionPhase=actor!=null?actor.MotionPhase:0;
            // Handle and heads are Blender models from scripts/generate_poi_parts.py.
            var handle=FestivalArtView.Create(root.transform,"FestivalPoiHandle");
            if(handle!=null)handle.name="Poi handle held in palm";
            var headObject=new GameObject(led?"LED weighted poi head":"Weighted practice poi head");headObject.layer=root.layer;
            FestivalArtView.Create(headObject.transform,led?"FestivalPoiHeadLED":"FestivalPoiHeadPractice");
            // First-person props are drawn smaller so a head passing near the camera stays readable.
            if(firstPerson)headObject.transform.localScale=Vector3.one*.7f;
            rig.head=headObject.transform;
            rig.rope=root.AddComponent<LineRenderer>();
            rig.rope.useWorldSpace=true;rig.rope.positionCount=2;
            // A 16 mm line reads as a rigid stick at character scale. Real
            // poi cord stays taut in a fast orbit but has a thin silhouette.
            rig.rope.startWidth=.006f;rig.rope.endWidth=.005f;
            rig.rope.sharedMaterial=FestivalArtView.MaterialFor("Cream");
            rig.rope.shadowCastingMode=ShadowCastingMode.Off;
            return rig;
        }
        void LateUpdate()
        {
            // Anchor the cord at the palm end of the handle. The weighted head
            // hangs below the grip and lags behind movement like a short flail.
            Vector3 grip=transform.position+transform.up*(firstPerson ? .085f : -.085f);
            float length=firstPerson?FirstPersonRopeLength:RopeLength;
            Vector3 rest=Vector3.down*length;
            if(!initialized||Vector3.Distance(previousGrip,grip)>3)
            {
                ball=grip+rest;velocity=Vector3.zero;initialized=true;spinUp=0;
            }
            float dt=Mathf.Min(Time.deltaTime,.033f);
            if(dt>0)
            {
                if(Spinning&&performer!=null)
                {
                    // Paired butterfly circles: the hands lead the two heads
                    // through the same high/low beat, while their lateral
                    // travel mirrors across the performer's center line.
                    // Smooth following supplies lag without stretching the cord.
                    float angle=Time.time*SpinRate+motionPhase;spinUp=0;
                    float mirroredSide=side==0?-1f:1f;
                    // A small outward bias keeps the two weighted heads from
                    // occupying the same point at the top of the circle.
                    Vector3 orbit=performer.right*(mirroredSide*(Mathf.Sin(angle)-.2f))
                        +performer.up*Mathf.Cos(angle)
                        // As in a real butterfly, the heads pass in separate
                        // front/back planes instead of striking one another.
                        +performer.forward*(mirroredSide*.3f+.10f*Mathf.Sin(angle+.55f*mirroredSide));
                    Vector3 offset=ball-grip;
                    if(offset.sqrMagnitude<.0001f)offset=Vector3.down;
                    Vector3 previousBall=ball;
                    ball=grip+Vector3.Slerp(offset.normalized,orbit.normalized,
                        1-Mathf.Exp(-23f*dt))*length;
                    velocity=(ball-previousBall)/dt;
                }
                else if(Carrying&&performer!=null)
                {
                    // Forward wheels in the vertical plane beside each hand, the two
                    // hands half a turn apart like an arm swing. Gravity shows in the
                    // pace: the head slows over the top and whips through the bottom.
                    Vector3 current=(ball-grip).sqrMagnitude>.0001f?(ball-grip).normalized:Vector3.down;
                    Vector3 forward=Vector3.ProjectOnPlane(performer.forward,Vector3.up);
                    forward=forward.sqrMagnitude>.0001f?forward.normalized:Vector3.forward;
                    // Pick the wheel up from wherever the head hangs and swing it up to speed.
                    if(spinUp==0)carryAngle=Mathf.Atan2(Vector3.Dot(current,forward),Vector3.Dot(current,Vector3.up));
                    spinUp=Mathf.MoveTowards(spinUp,1,2.5f*dt);
                    carryAngle+=7.5f*spinUp*(1-.35f*current.y)*dt;
                    Vector3 outward=Vector3.Cross(Vector3.up,forward)*(side==0?-1f:1f);
                    // In first person the wheel leans outward and down so heads sweep beside
                    // and below the hands, topping out just under the eyeline.
                    Vector3 orbit=Vector3.up*Mathf.Cos(carryAngle)+forward*Mathf.Sin(carryAngle)+
                        (firstPerson?outward*1.1f-Vector3.up*.35f:outward*.28f);
                    Vector3 previousBall=ball;
                    ball=grip+Vector3.Slerp(current,orbit.normalized,1-Mathf.Exp(-30f*dt))*length;
                    velocity=(ball-previousBall)/dt;
                }
                else
                {
                    // An idle head is a pendulum under full gravity, never sprung toward the hand.
                    // The damping stands in for the hand stilling the cord after a stop.
                    spinUp=0;
                    Vector3 acceleration=Physics.gravity*1.35f-velocity*1.7f;
                    velocity+=acceleration*dt;
                    velocity*=Mathf.Exp(-.8f*dt);
                    Vector3 previousBall=ball;
                    ball+=velocity*dt;
                    Vector3 offset=ball-grip;
                    if(offset.sqrMagnitude<.0001f)offset=Vector3.down;
                    ball=grip+offset.normalized*length;
                    velocity=(ball-previousBall)/dt;
                }
            }
            // The head lives outside the bone hierarchy so its world-space
            // tether can swing freely. Follow layer changes on local-player spawn.
            if(head.gameObject.layer!=gameObject.layer)
                foreach(var part in head.GetComponentsInChildren<Transform>(true))part.gameObject.layer=gameObject.layer;
            head.position=ball;
            rope.SetPosition(0,grip);rope.SetPosition(1,ball);
            previousGrip=grip;
        }
        void OnDisable(){initialized=false;if(head!=null)head.gameObject.SetActive(false);if(rope!=null)rope.enabled=false;}
        void OnEnable(){if(head!=null)head.gameObject.SetActive(true);if(rope!=null)rope.enabled=true;}
        void OnDestroy(){if(head!=null)Destroy(head.gameObject);}
    }
}
