using System.Collections.Generic;
using Festival.Core;
using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>TWISTVIS-1: stand-ins for each festival's twists, from primitives and existing props, until ART-1 and ART-2
    /// replace them. Palm Mirage: the influencers' phone frames, the VIP ropes and the Ferris wheel. Ember Playa: the art
    /// cars and the effigy, which burns for Night 2's last three minutes; its dust storms are FestivalNightLighting's fog.
    /// Everything follows the round state with no network calls, and nothing collides, so sight lines, steps and paths stay
    /// exactly the rules'.</summary>
    public sealed class FestivalTwistVisuals
    {
        public const string PoloRootName="Palm Mirage twists",PlayaRootName="Ember Playa twists",FramePrefix="Influencer frame ";
        // Frames and cars glide to each snapshot the way FestivalSession's actors do (snapping past 5 m), so a rider's body
        // stays on its car's deck and an influencer's frame stays at their feet.
        private const float Glide=15,SnapDistance=5;
        // The phone is held up in the right hand; its light leans down to the middle of the frame. Edges lie on the ground.
        private static readonly Vector3 Phone=new Vector3(.28f,1.45f,.32f);
        private const float EdgeWidth=.07f,PhoneLightIntensity=4;
        private const int ArcSegments=4;
        // VIP ropes: posts no more than this far apart, the rope at waist height.
        private const float PostSpacing=2.2f,RopeHeight=.88f;
        // The wheel turns once a ride (Festivals.WheelRideSeconds), its gondolas hanging level below the rim.
        private const float AxleHeight=7.5f,WheelRadius=6,GondolaDrop=.75f;
        private const int Gondolas=8,RimSegments=16;
        // A car tows a party deck: the deck's centre is the car's point in the rules, so a rider stands on it with the van
        // leading, and it turns toward where its loop takes it this far ahead.
        private const double TurnLookahead=1.5;
        private const float VanAhead=4.4f,DeckSize=2.8f;
        // The fire's flames stretch and shrink out of step with each other.
        private const float FlickerHz=9,FlickerDepth=.3f,FireGlow=6;

        private readonly Transform grounds,polo,playa,rotor,fire;
        private readonly Light fireLight;
        private readonly Transform[] gondolas=new Transform[Gondolas],cars=new Transform[Festivals.ArtCars];
        private readonly Vector3[] anchors=new Vector3[Gondolas];
        private readonly List<(Transform Flame,Vector3 Size)> flames=new List<(Transform,Vector3)>();
        private readonly Dictionary<string,Transform> frames=new Dictionary<string,Transform>();
        private readonly List<string> gone=new List<string>();
        private float turn,clock;

        public FestivalTwistVisuals(Transform festival)
        {
            grounds=festival;
            polo=Group(festival,PoloRootName,Vector3.zero);playa=Group(festival,PlayaRootName,Vector3.zero);
            for(int i=0;i<Festivals.VipZones.Length;i++)Ropes(Group(polo,"VIP rope "+i,Vector3.zero),Festivals.VipZones[i]);
            rotor=Wheel(Group(polo,"Ferris wheel",new Vector3(Festivals.WheelX,0,Festivals.WheelZ)));
            for(int k=0;k<cars.Length;k++)cars[k]=ArtCar(Group(playa,"Art car "+k,Vector3.zero),k);
            fire=Effigy(Group(playa,"Effigy",new Vector3(Festivals.EffigyX,0,Festivals.EffigyZ)));fireLight=fire.GetComponentInChildren<Light>(true);
        }

        public void Apply(RoundState state,float deltaTime)
        {
            polo.gameObject.SetActive(state.FestivalIndex==Festivals.PoloFestival);
            playa.gameObject.SetActive(state.FestivalIndex==Festivals.PlayaFestival);
            clock+=deltaTime;
            Film(state,deltaTime);
            turn=Mathf.Repeat(turn+deltaTime*360/(float)Festivals.WheelRideSeconds,360);Turn();
            for(int k=0;k<cars.Length;k++)
            {
                var at=Festivals.ArtCarAt(k,state.ElapsedSeconds);var ahead=Festivals.ArtCarAt(k,state.ElapsedSeconds+TurnLookahead);
                cars[k].localPosition=Glided(cars[k].localPosition,new Vector3(at.X,0,at.Z),deltaTime);
                var heading=new Vector3(ahead.X-at.X,0,ahead.Z-at.Z);if(heading.sqrMagnitude>1e-6f)cars[k].localRotation=Quaternion.LookRotation(heading);
            }
            fire.gameObject.SetActive(FestivalSimulation.Burning(state));
            for(int i=0;i<flames.Count;i++)flames[i].Flame.localScale=Vector3.Scale(flames[i].Size,new Vector3(1,1+FlickerDepth*Mathf.Sin(clock*FlickerHz+i*1.7f),1));
            fireLight.intensity=FireGlow*(1+.15f*Mathf.Sin(clock*13));
        }

        // Rules deal influencers only at Palm Mirage; a frame goes wherever the view says someone films, as the rules film.
        private void Film(RoundState state,float deltaTime)
        {
            gone.Clear();foreach(var id in frames.Keys)if(!state.Npcs.Exists(n=>n.Id==id&&n.Twist==FestivalSimulation.Influencer))gone.Add(id);
            foreach(var id in gone){Dispose(frames[id].gameObject);frames.Remove(id);}
            foreach(var n in state.Npcs)
            {
                if(n.Twist!=FestivalSimulation.Influencer)continue;
                var at=new Vector3(n.X,0,n.Z);
                if(!frames.TryGetValue(n.Id,out var frame))frames[n.Id]=frame=Frame(n.Id,at);
                frame.localPosition=Glided(frame.localPosition,at,deltaTime);frame.localRotation=Quaternion.Euler(0,n.Yaw,0);
            }
        }
        private static Vector3 Glided(Vector3 from,Vector3 to,float deltaTime)=>Vector3.Distance(from,to)>SnapDistance?to:Vector3.Lerp(from,to,1-Mathf.Exp(-Glide*deltaTime));

        // The phone's view on the ground: two edges FilmRange long, FilmConeDegrees apart, closed by an arc, lit from the phone.
        private Transform Frame(string npcId,Vector3 at)
        {
            var frame=Group(grounds,FramePrefix+npcId,at);float half=Festivals.FilmConeDegrees/2;
            Vector3 Reach(float degrees)=>Quaternion.Euler(0,degrees,0)*Vector3.forward*Festivals.FilmRange+Vector3.up*EdgeWidth/2;
            Beam(frame,"Frame edge left",Vector3.up*EdgeWidth/2,Reach(-half),EdgeWidth,"StageGlowRose");
            Beam(frame,"Frame edge right",Vector3.up*EdgeWidth/2,Reach(half),EdgeWidth,"StageGlowRose");
            for(int s=0;s<ArcSegments;s++)Beam(frame,"Frame arc",Reach(-half+2*half*s/ArcSegments),Reach(-half+2*half*(s+1)/ArcSegments),EdgeWidth,"StageGlowRose");
            Block(frame,"Phone",PrimitiveType.Cube,Phone,new Vector3(.08f,.15f,.02f),"White");
            var beam=new GameObject("Phone light");beam.transform.SetParent(frame,false);beam.transform.localPosition=Phone;
            beam.transform.localRotation=Quaternion.Euler(Mathf.Atan2(Phone.y,Festivals.FilmRange/2)*Mathf.Rad2Deg,0,0);
            var light=beam.AddComponent<Light>();light.type=LightType.Spot;light.spotAngle=Festivals.FilmConeDegrees;light.range=Festivals.FilmRange+1;
            light.intensity=PhoneLightIntensity;light.color=new Color(1f,.95f,.9f);light.shadows=LightShadows.None;
            return frame;
        }

        // Brass posts round the zone's edge, a velvet rope between them; the knobs glow so the ropes read at night.
        private static void Ropes(Transform ropes,Festivals.Area zone)
        {
            var corners=new[]{new Vector3(zone.MinX,0,zone.MinZ),new Vector3(zone.MaxX,0,zone.MinZ),new Vector3(zone.MaxX,0,zone.MaxZ),new Vector3(zone.MinX,0,zone.MaxZ)};
            for(int c=0;c<corners.Length;c++)
            {
                Vector3 a=corners[c],b=corners[(c+1)%corners.Length];int posts=Mathf.CeilToInt(Vector3.Distance(a,b)/PostSpacing);
                for(int p=0;p<posts;p++)
                {
                    Vector3 from=Vector3.Lerp(a,b,(float)p/posts),to=Vector3.Lerp(a,b,(float)(p+1)/posts);
                    Block(ropes,"Rope post",PrimitiveType.Cylinder,from+Vector3.up*.5f,new Vector3(.1f,.5f,.1f),"Gold");
                    Block(ropes,"Rope knob",PrimitiveType.Sphere,from+Vector3.up*1.02f,Vector3.one*.16f,"StageGlowGold");
                    Beam(ropes,"Velvet rope",from+Vector3.up*RopeHeight,to+Vector3.up*RopeHeight,.06f,"PaintRose");
                }
            }
        }

        // Two A-frames hold the axle over a boarding platform at the rules' base; the rotor carries a glowing rim and its spokes.
        private Transform Wheel(Transform wheel)
        {
            Block(wheel,"Boarding platform",PrimitiveType.Cube,new Vector3(0,.125f,0),new Vector3(3.2f,.25f,2.4f),"Wood");
            foreach(float x in new[]{-1.1f,1.1f})foreach(float z in new[]{-3.2f,3.2f})Beam(wheel,"Wheel leg",new Vector3(x,0,z),new Vector3(x,AxleHeight,0),.22f,"Metal");
            var hub=Group(wheel,"Wheel rotor",Vector3.up*AxleHeight);
            Block(hub,"Axle",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.35f,1.3f,.35f),"Metal").localRotation=Quaternion.Euler(0,0,90);
            Vector3 Rim(float degrees)=>new Vector3(0,WheelRadius*Mathf.Sin(degrees*Mathf.Deg2Rad),WheelRadius*Mathf.Cos(degrees*Mathf.Deg2Rad));
            for(int s=0;s<RimSegments;s++)Beam(hub,"Wheel rim",Rim(360f*s/RimSegments),Rim(360f*(s+1)/RimSegments),.18f,s%2==0?"StageGlowMint":"StageGlowRose");
            for(int k=0;k<Gondolas;k++)
            {
                anchors[k]=Rim(360f*k/Gondolas);Beam(hub,"Wheel spoke",Vector3.zero,anchors[k],.1f,"Metal");
                gondolas[k]=Block(wheel,"Gondola",PrimitiveType.Cube,Vector3.zero,new Vector3(1.2f,.9f,1f),k%3==0?"Rose":k%3==1?"Mint":"Gold");
            }
            return hub;
        }
        private void Turn()
        {
            rotor.localRotation=Quaternion.Euler(turn,0,0);
            for(int k=0;k<Gondolas;k++)gondolas[k].localPosition=rotor.localPosition+rotor.localRotation*anchors[k]-Vector3.up*GondolaDrop;
        }

        // The camp van (existing vehicle art, its front at the model's -z) tows a neon-railed party deck.
        private static Transform ArtCar(Transform car,int k)
        {
            string glow=k%2==0?"StageGlowMint":"StageGlowRose";float edge=DeckSize/2;
            Block(car,"Party deck",PrimitiveType.Cube,new Vector3(0,.04f,0),new Vector3(DeckSize,.08f,DeckSize),"Dark");
            foreach(float x in new[]{-edge,edge})
            {
                Beam(car,"Deck rail",new Vector3(x,.9f,-edge),new Vector3(x,.9f,edge),.07f,glow);
                foreach(float z in new[]{-edge,edge})Block(car,"Deck post",PrimitiveType.Cube,new Vector3(x,.45f,z),new Vector3(.07f,.9f,.07f),"Metal");
            }
            Beam(car,"Tow bar",new Vector3(0,.35f,edge),new Vector3(0,.35f,VanAhead-2.6f),.1f,"Metal");
            var van=FestivalArtView.Create(car,"FestivalCampVan");
            if(van!=null){van.transform.localPosition=new Vector3(0,0,VanAhead);van.transform.localRotation=Quaternion.Euler(0,180,0);}
            var at=Festivals.ArtCarAt(k,0);car.localPosition=new Vector3(at.X,0,at.Z);
            return car;
        }

        // A wooden figure with raised arms, standing astride the main path so walkers pass between its legs. Its fire waits
        // for the burn.
        private Transform Effigy(Transform effigy)
        {
            foreach(float side in new[]{-1f,1f})
            {
                Beam(effigy,"Effigy leg",new Vector3(side*1.6f,0,0),new Vector3(side*.55f,4.6f,0),.45f,"Wood");
                Beam(effigy,"Effigy arm",new Vector3(side*.6f,7.1f,0),new Vector3(side*2.4f,9.3f,0),.32f,"Wood");
            }
            Block(effigy,"Effigy hips",PrimitiveType.Cube,new Vector3(0,4.7f,0),new Vector3(1.6f,.55f,.6f),"Wood");
            Block(effigy,"Effigy chest",PrimitiveType.Cube,new Vector3(0,6.1f,0),new Vector3(1.3f,2.4f,.7f),"Wood");
            Block(effigy,"Effigy heart",PrimitiveType.Cube,new Vector3(0,6.4f,-.37f),new Vector3(.4f,.4f,.05f),"StageGlowGold");
            Block(effigy,"Effigy head",PrimitiveType.Cube,new Vector3(0,7.95f,0),new Vector3(.9f,1f,.9f),"Wood");
            var burn=Group(effigy,"Effigy fire",Vector3.zero);
            var spots=new[]{new Vector3(-1.45f,.6f,0),new Vector3(1.45f,.6f,0),new Vector3(-1.05f,2.4f,0),new Vector3(1.05f,2.4f,0),new Vector3(0,4.9f,0),
                new Vector3(0,6.3f,0),new Vector3(-1.6f,8.3f,0),new Vector3(1.6f,8.3f,0),new Vector3(0,8.6f,0)};
            for(int i=0;i<spots.Length;i++)
            {
                var size=new Vector3(.8f,1.5f,.8f)*(i<4?.9f:1.1f);
                var flame=Block(burn,"Flame",PrimitiveType.Cube,spots[i],size,i%3==2?"StageGlowRose":"StageGlowGold");flame.localRotation=Quaternion.Euler(0,45,0);
                flames.Add((flame,size));
            }
            var glow=new GameObject("Fire light");glow.transform.SetParent(burn,false);glow.transform.localPosition=new Vector3(0,5,-1);
            var light=glow.AddComponent<Light>();light.type=LightType.Point;light.range=26;light.intensity=FireGlow;light.color=new Color(1f,.55f,.2f);light.shadows=LightShadows.None;
            burn.gameObject.SetActive(false);
            return burn;
        }

        private static Transform Group(Transform parent,string name,Vector3 at){var group=new GameObject(name).transform;group.SetParent(parent,false);group.localPosition=at;return group;}
        private static Transform Block(Transform parent,string name,PrimitiveType shape,Vector3 at,Vector3 size,string look)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=FestivalArtView.MaterialFor(look);
            // Stand-ins never collide: only the rules decide who may pass, see or walk where.
            var collider=go.GetComponent<Collider>();collider.enabled=false;Dispose(collider);
            return go.transform;
        }
        // A cube stretched from one point to another along its forward axis.
        private static Transform Beam(Transform parent,string name,Vector3 from,Vector3 to,float thickness,string look)
        {
            var beam=Block(parent,name,PrimitiveType.Cube,(from+to)*.5f,new Vector3(thickness,thickness,Vector3.Distance(from,to)),look);
            var along=to-from;beam.localRotation=Quaternion.LookRotation(along,Mathf.Abs(along.normalized.y)>.99f?Vector3.forward:Vector3.up);
            return beam;
        }
        private static void Dispose(Object value){if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
    }
}
