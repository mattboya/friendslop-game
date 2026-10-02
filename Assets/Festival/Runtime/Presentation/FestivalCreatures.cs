using System;
using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Festival.Presentation
{
    /// <summary>VISION-2: a dosed player's own client sometimes shows little mythical creatures, 0.2 to 0.5 m tall (gnomes,
    /// pixies, tiny dragons, mushroom sprites and jackalopes), more often at higher doses. Only the local player's own dose counts,
    /// a spirit sees none, and nothing here touches the simulation or the network. Arrivals run on the view's level clock
    /// (ElapsedSeconds), so they come only while Playing, seeded by the round seed, the level and the player. Each creature's own
    /// life runs on frame time, so it finishes fading while the clock stands still at Results. The dose invents them, so they
    /// carry the fake-vision tells (FestivalVisionMarkers): no shadow, a shimmer while the camera moves, a slightly shifted hue.
    /// ART-1: each is a Blender model, its parts painted from the palette.</summary>
    public sealed class FestivalCreatures : MonoBehaviour
    {
        // Doses 1-4: the mean gap between arrivals (each gap randomised by +-50%) and how many may be on screen at once.
        private static readonly float[] MeanGaps={45,20,10,5};
        private static readonly int[] Caps={1,2,4,6};
        // One in three persists, staying put until you are LeaveDistance away, the dose ends or the festival is left. The rest are
        // fleeting: in over FadeSeconds, about for MinLife-MaxLife seconds, out over FadeSeconds. Opaque lit materials cannot
        // fade, so they grow in and shrink away.
        private const int PersistOneIn=3;
        private const float FadeSeconds=1,MinLife=2,MaxLife=6,LeaveDistance=40;
        // Near-Far metres off, on a bearing across the view's width and up to EdgeMargin degrees past either edge, on whatever a
        // probe down from eye height lands on (y=0 where it finds nothing). A spot inside something drops the arrival.
        private const float Near=4,Far=15,EdgeMargin=5,InsideProbe=.3f,InsideRadius=.2f;
        // A creature is on screen while this box at its feet is inside the camera's frustum.
        private const float Box=.5f;
        // A little hop, and a slight glow so they read at night.
        private const float Hop=.05f,HopHz=1.5f,Glow=.3f;

        // The fixed palette, every colour saturated enough for the fake hue shift to show.
        private static readonly Color[] Paints={
            new Color(.22f,.42f,.9f),new Color(.9f,.18f,.16f),new Color(.95f,.68f,.52f),new Color(.98f,.9f,.62f),   // coat, red, skin, cream
            new Color(1f,.45f,.8f),new Color(.45f,.95f,1f),new Color(.3f,.8f,.3f),new Color(.15f,.55f,.35f),         // pink, wing, green, dark green
            new Color(.72f,.52f,.32f),new Color(.85f,.72f,.5f),new Color(.3f,.12f,.4f)};                               // tan, antler, eyes
        public static IReadOnlyList<Color> Palette=>Paints;
        // ART-1: each look is a model whose parts end __P<n>, painted from Paints[n].
        private static readonly (string Name,string Model)[] Looks={("Gnome","FestivalCreatureGnome"),("Pixie","FestivalCreaturePixie"),
            ("Dragon","FestivalCreatureDragon"),("Mushroom sprite","FestivalCreatureMushroomSprite"),("Jackalope","FestivalCreatureJackalope")};

        /// <summary>When creatures arrive and what each one is: a pure function of the seed and the clock it is stepped with.</summary>
        public sealed class Schedule
        {
            public struct Arrival { public double At; public bool Persists; public int Look; public float Life,Bearing,Distance; }
            private readonly ContentRandom random;
            private double next=-1;
            public Schedule(int seed){random=new ContentRandom(seed);}
            // The first dosed step arms it one gap ahead, so joining mid-level brings no burst, and a sober step disarms it. Every
            // arrival draws the same values, and the next gap counts from the last arrival, dropped or not.
            public bool Due(double clock,int dose,out Arrival arrival)
            {
                arrival=default;
                if(dose<1){next=-1;return false;}
                if(next<0){next=clock+Gap(dose);return false;}
                if(clock<next)return false;
                arrival=new Arrival{At=next,Persists=random.Next(PersistOneIn)==0,Look=random.Next(Looks.Length),Life=Mathf.Lerp(MinLife,MaxLife,Unit()),Bearing=Unit()*2-1,Distance=Mathf.Lerp(Near,Far,Unit())};
                next+=Gap(dose);return true;
            }
            private float Gap(int dose)=>MeanGaps[Math.Min(dose,MeanGaps.Length)-1]*(.5f+Unit());
            private float Unit()=>random.Next(1001)/1000f;
        }

        /// <summary>The schedule's seed: the round seed, the level and the player, so each player's creatures are their own.</summary>
        public static int SeedFor(RoundState state,string playerId)
        {
            uint hash=2166136261;foreach(char c in playerId??"")hash=unchecked((hash^c)*16777619);
            return unchecked(state.Seed*41+state.FestivalIndex*7919+state.LevelIndex*131+(int)hash);
        }

        private sealed class Creature { public Transform Root; public int Look,Index; public bool Persists,InView,Seen; public float Age,Life,Fading; public Vector3 Spot; }
        private readonly List<Creature> live=new List<Creature>();
        private readonly List<Creature>[] pool=new List<Creature>[Looks.Length];
        private readonly Material[] paints=new Material[Paints.Length];
        private readonly Plane[] frustum=new Plane[6];
        private readonly FestivalVisionMarkers.MotionTracker motion=new FestivalVisionMarkers.MotionTracker();
        private Schedule schedule;
        private int seed,arrivals;
        private FestivalSession session;

        /// <summary>Creatures alive now, drawn or not.</summary>
        public int Live=>live.Count;
        /// <summary>Creatures inside the camera's view this frame, never more than the dose's cap.</summary>
        public int OnScreen {get;private set;}

        // After FestivalSession.Update has moved the first-person camera this frame.
        private void LateUpdate()
        {
            if(session==null){session=GetComponent<FestivalSession>();if(session==null)return;}
            Apply(session.State,session.LocalPlayerId,session.ViewCamera,Time.time,Time.unscaledDeltaTime);
        }
        // Turned off (TEST-1's pixel checks do), it shows nothing. Not while the object itself is going inactive or away: its
        // creatures go with it, and they cannot be switched off while their parent is being switched off.
        private void OnDisable(){if(gameObject.activeInHierarchy)Clear();}

        /// <summary>Shows localPlayerId's creatures in state, seen through view; time drives the shimmer, deltaTime each life.</summary>
        public void Apply(RoundState state,string localPlayerId,Camera view,float time,float deltaTime)
        {
            if(state==null||view==null||!FestivalWorld.ShowsFestival(state.Phase)){Clear();return;}
            int key=SeedFor(state,localPlayerId);if(schedule==null||key!=seed){Clear();seed=key;schedule=new Schedule(key);}
            int dose=DoseOf(state,localPlayerId),cap=dose<1?0:Caps[Math.Min(dose,Caps.Length)-1];
            var eye=view.transform.position;float shimmer=motion.Step(view.transform,deltaTime);
            for(int i=live.Count-1;i>=0;i--)
            {
                var c=live[i];c.Age+=deltaTime;
                if(c.Fading>=0)c.Fading+=deltaTime;else if(dose<1||!c.Persists&&c.Age>=FadeSeconds+c.Life)c.Fading=0;
                var off=eye-c.Spot;off.y=0;
                if(c.Fading>=FadeSeconds||c.Persists&&off.sqrMagnitude>=LeaveDistance*LeaveDistance)Release(i);
            }
            // Those already on screen keep their places; one coming into view takes a free place or stays hidden, so turning back
            // to the ones left behind never shows more than the cap.
            GeometryUtility.CalculateFrustumPlanes(view,frustum);OnScreen=0;
            foreach(var c in live){c.InView=InView(c.Spot);if(!c.InView)c.Seen=false;else if(c.Seen)OnScreen++;}
            foreach(var c in live)if(c.InView&&!c.Seen&&OnScreen<cap){c.Seen=true;OnScreen++;}
            // Arrivals only while the level clock runs. One over the cap is dropped (only those in view count) and the schedule
            // keeps ticking.
            if(state.Phase=="Playing")
                while(schedule.Due(state.ElapsedSeconds,dose,out var arrival))
                    if(OnScreen<cap&&Place(view,arrival,out var spot)){var c=Spawn(arrival,spot);c.InView=InView(spot);if(c.InView){c.Seen=true;OnScreen++;}}
            foreach(var c in live)
            {
                bool drawn=!c.InView||c.Seen;if(c.Root.gameObject.activeSelf!=drawn)c.Root.gameObject.SetActive(drawn);
                if(!drawn)continue;
                c.Root.position=c.Spot+Vector3.up*(Hop*Mathf.Abs(Mathf.Sin((c.Age*HopHz+c.Index*.37f)*Mathf.PI)));
                var toEye=eye-c.Spot;toEye.y=0;if(toEye.sqrMagnitude>1e-4f)c.Root.rotation=Quaternion.LookRotation(toEye);
                float grown=Mathf.SmoothStep(0,1,c.Age/FadeSeconds)*(1-Mathf.SmoothStep(0,1,Mathf.Max(0,c.Fading)/FadeSeconds));
                c.Root.localScale=Vector3.one*grown*FestivalVisionMarkers.ShimmerScale(shimmer,time,c.Index);
            }
        }

        // Only the local player's own dose counts, never another effect; a spirit sees none.
        private static int DoseOf(RoundState state,string playerId)
        {
            foreach(var p in state.Players)
                if(p.Id==playerId){if(p.Life=="Spirit")return 0;foreach(var e in p.Effects)if(e.Id==FestivalSimulation.DoseEffect)return e.Intensity;return 0;}
            return 0;
        }
        private bool InView(Vector3 spot)=>GeometryUtility.TestPlanesAABB(frustum,new Bounds(spot+Vector3.up*(Box/2),Vector3.one*Box));
        // ponytail: one spot per arrival, dropped when it is inside something; retry nearer if crowded places feel too empty.
        private static bool Place(Camera view,Schedule.Arrival arrival,out Vector3 spot)
        {
            float half=Mathf.Atan(Mathf.Tan(view.fieldOfView*.5f*Mathf.Deg2Rad)*view.aspect)*Mathf.Rad2Deg+EdgeMargin;
            var eye=view.transform.position;var along=Quaternion.Euler(0,view.transform.eulerAngles.y+arrival.Bearing*half,0)*Vector3.forward*arrival.Distance;
            spot=new Vector3(eye.x+along.x,0,eye.z+along.z);
            if(Physics.Raycast(new Vector3(spot.x,eye.y,spot.z),Vector3.down,out var hit,eye.y+1,~0,QueryTriggerInteraction.Ignore))spot.y=hit.point.y;
            return !Physics.CheckSphere(spot+Vector3.up*InsideProbe,InsideRadius,~0,QueryTriggerInteraction.Ignore);
        }

        private Creature Spawn(Schedule.Arrival arrival,Vector3 spot)
        {
            var shelf=pool[arrival.Look];Creature c;
            if(shelf!=null&&shelf.Count>0){c=shelf[shelf.Count-1];shelf.RemoveAt(shelf.Count-1);}else c=Build(arrival.Look);
            c.Persists=arrival.Persists;c.Life=arrival.Life;c.Age=0;c.Fading=-1;c.Spot=spot;c.Seen=false;c.Index=++arrivals;
            c.Root.SetPositionAndRotation(spot,Quaternion.identity);c.Root.localScale=Vector3.zero;c.Root.gameObject.SetActive(true);
            live.Add(c);return c;
        }
        private void Release(int index)
        {
            var c=live[index];live.RemoveAt(index);c.Root.gameObject.SetActive(false);
            (pool[c.Look]??=new List<Creature>()).Add(c);
        }
        // Back to the pool: the festival is no longer shown, the level changed, or the client left.
        private void Clear(){for(int i=live.Count-1;i>=0;i--)Release(i);schedule=null;OnScreen=0;}

        private Creature Build(int look)
        {
            var root=new GameObject("Creature "+Looks[look].Name).transform;root.SetParent(transform,false);
            var model=FestivalArtView.Create(root,Looks[look].Model);
            if(model!=null)foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                int mark=renderer.name.LastIndexOf("__P",StringComparison.Ordinal);
                if(mark>=0&&int.TryParse(renderer.name.Substring(mark+3).Split('.')[0],out int paint)&&paint<Paints.Length)renderer.sharedMaterial=Paint(paint);
            }
            return new Creature{Root=root,Look=look};
        }
        // One material per palette colour, shared by every creature: the included URP material, hue-shifted like a fake vision.
        private Material Paint(int paint)
        {
            if(paints[paint]!=null)return paints[paint];
            var color=FestivalVisionMarkers.FakeTint(Paints[paint]);
            var material=new Material(Resources.Load<Material>("FestivalLit")){color=color};
            if(material.HasProperty("_EmissionColor")){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*Glow);}
            return paints[paint]=material;
        }

        private void OnDestroy(){foreach(var material in paints)if(material!=null)Dispose(material);}
        private static void Dispose(Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
