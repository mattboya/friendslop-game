using UnityEngine;
using UnityEngine.Rendering;

namespace Festival.Presentation
{
    /// <summary>POLO-6: an influencer's phone, in place of POLO-1's pink frame on the ground. FestivalCharacter.SetFilming puts it in
    /// the HandR hand of every influencer in the view, held up beside its face whatever else it does: screen to itself, camera toward
    /// the crowd, as the rules film along its facing (FestivalSimulation.Films). The red light by the camera blinks once a
    /// second and glows, so it reads at night. While it has the local player on camera (their view's VisualFilmedBy) the light stays
    /// lit and "• REC" floats over its head, on that player's screen alone. Built from primitives, like the old floating phone, and
    /// nothing in it collides.</summary>
    public sealed class FestivalFilmingPhone
    {
        public const string PhoneName="Influencer phone",LightName="REC light",LabelName="REC label";
        // The display font has "•" but no "●".
        const string LabelText="• REC";
        // The phone in metres, chunky enough to read beside the bodies' big hands: a white case with the red light by its camera at
        // the top corner of its back, and a lit screen on its front.
        static readonly Vector3 Size=new Vector3(.09f,.18f,.016f);
        // Where the wrist goes, from the head bone (at the base of the skull) in the body's own frame and size: Aside out to the phone
        // hand's side, Lift up and Ahead out. The arms are short and the heads big, so a phone held in front of the face sits on the
        // nose and reads from afar as a hand to the face; held up beside the face, the raised arm and the phone read as filming. The
        // fist closes Palm on from the wrist along the forearm, round the phone's lower half: the phone stands Grip (metres) above it,
        // at eye height, and Tilt leans its top back and Turn swings its screen toward the eyes.
        const float Aside=.46f,Lift=.18f,Ahead=.43f,Palm=.06f,Grip=.05f,Tilt=12,Turn=12;
        // "• REC" floats LabelRise over the head bone, clear of the tallest hat and under where a name tag would float.
        const float LabelRise=.75f,LabelSize=.028f;
        static readonly Color LabelColor=new Color(1f,.22f,.2f);

        readonly Transform actor,arm,forearm,hand,head,phone,label;
        readonly Renderer light;
        // Which way the phone arm's shoulder sits from the body's middle, along its right: the elbow bends out that way.
        readonly int side;
        bool recording;

        public FestivalFilmingPhone(Transform actor,Transform arm,Transform forearm,Transform hand,Transform head)
        {
            this.actor=actor;this.arm=arm;this.forearm=forearm;this.hand=hand;this.head=head;
            side=Vector3.Dot(arm.position-actor.position,actor.right)<0?-1:1;
            phone=new GameObject(PhoneName).transform;phone.SetParent(hand,false);
            Block("Phone body",PrimitiveType.Cube,Vector3.zero,Size,"White");
            Block("Phone screen",PrimitiveType.Cube,new Vector3(0,0,-Size.z/2-.001f),new Vector3(Size.x*.86f,Size.y*.88f,.002f),"StageGlowMint");
            light=Block(LightName,PrimitiveType.Sphere,new Vector3(Size.x*.25f,Size.y*.38f,Size.z/2+.006f),Vector3.one*.03f,"StageGlowRed").GetComponent<Renderer>();
            label=new GameObject(LabelName).transform;label.SetParent(actor,false);label.gameObject.layer=30;
            label=FestivalWorldText.Add(label.gameObject,LabelText,64*LabelSize,LabelColor).transform;
            Show(false,false);
        }

        public bool Shown=>phone.gameObject.activeSelf;
        /// <summary>Out while filming, put away otherwise (a later level deals other influencers); recording, it has you on camera.</summary>
        public void Show(bool filming,bool recording)
        {
            this.recording=filming&&recording;
            if(phone.gameObject.activeSelf!=filming)phone.gameObject.SetActive(filming);
            if(label.gameObject.activeSelf!=this.recording)label.gameObject.SetActive(this.recording);
            // MINI-5 freezes world rigs during a minigame, so Hold may not run: a recording light can't wait for it.
            if(this.recording)light.enabled=true;
        }

        /// <summary>Each frame once the body is posed (FestivalCharacter.LateUpdate): the phone arm reaches up to hold it beside the
        /// face, the light blinks on the clock at seconds (lit for the first half of each one) or stays lit while recording, and
        /// "• REC" turns to face view.</summary>
        public void Hold(float seconds,Transform view)
        {
            var facing=actor.rotation;float size=actor.lossyScale.y;
            FestivalDjHandContact.Reach(actor,arm,forearm,hand,head.position+facing*new Vector3(side*Aside,Lift,Ahead)*size,side);
            var palm=hand.position+(hand.position-forearm.position).normalized*Palm*size;
            phone.SetPositionAndRotation(palm+facing*Vector3.up*Grip,facing*Quaternion.Euler(-Tilt,side*Turn,0));
            // At its real size whatever the body's: turned with the body, the scale it inherits lies along its own axes.
            phone.localScale=Vector3.one;var inherited=phone.lossyScale;phone.localScale=new Vector3(1/inherited.x,1/inherited.y,1/inherited.z);
            light.enabled=recording||Mathf.Repeat(seconds,1)<.5f;
            if(!recording)return;
            label.position=head.position+Vector3.up*LabelRise*size;
            if(view!=null)label.rotation=view.rotation;
        }

        Transform Block(string name,PrimitiveType shape,Vector3 at,Vector3 scale,string look)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(phone,false);go.transform.localPosition=at;go.transform.localScale=scale;
            var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=FestivalArtView.MaterialFor(look);renderer.shadowCastingMode=ShadowCastingMode.Off;
            // Only the rules decide who may pass or see: the phone blocks nothing, the third-person camera's boom included.
            var collider=go.GetComponent<Collider>();collider.enabled=false;
            if(Application.isPlaying)Object.Destroy(collider);else Object.DestroyImmediate(collider);
            return go.transform;
        }
    }
}
