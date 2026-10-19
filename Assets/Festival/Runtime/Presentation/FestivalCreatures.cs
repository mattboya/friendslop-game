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
    /// shimmer while the camera moves and are slightly off in hue, like a fake vision (FestivalVisionMarkers), but they cast
    /// ordinary sun shadows: nobody mistakes them for real (VISION-4).
    /// ART-1: each is a Blender model, its parts painted from the palette, in one of 8 looks its arrival rolls (VISION-6: palette
    /// swaps and a size). VISION-6: now and then one points. It reads the view but sends nothing: when the view holds a vision
    /// the host hinted (only the tripper's does, and only true ones are hinted), the next arrival is that job's kind and points at
    /// it, and any dosed player's jackalope now and then points at an ungrabbed Giggle Tank. A pointer faces its target, beckons
    /// (a nod and a hop toward it, sparkles on the ground, one squeak) and, when you come near, scurries on toward it.</summary>
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
        // VISION-6: a pointer's kind is its job (Looks' order): a gnome a stash or a double buyer, a pixie a buyer, a dragon a narc,
        // a mushroom sprite a clue, a jackalope the Giggle Tank.
        private const int Gnome=0,Pixie=1,Dragon=2,MushroomSprite=3,Jackalope=4;
        // The tank is public, so each client times its own jackalope: the first no sooner than TankFirst s of level clock, then at
        // most one every TankEvery s.
        private const double TankFirst=60,TankEvery=120;
        // With the eye within Startle m, a pointer scurries up to ScurryMetres on toward its target over ScurrySeconds, stopping
        // StopShort m short (not for less than MinScurry m); it goes once the player is within Reached m of the target.
        private const float Startle=5,ScurryMetres=6,ScurrySeconds=1,StopShort=3,MinScurry=.5f,Reached=4;
        // A scurry weaves Wiggles times per ScurryMetres, MinWeave-MaxWeave m to either side at most (both in proportion
        // for a shorter one), faded out at both ends so it still arrives where a straight one would.
        private const float Wiggles=2,MinWeave=.7f,MaxWeave=1;
        // The beckon, NodHz times a second: a lean of up to Lean degrees about its feet and a hop PointHop up and HopAhead toward
        // its target. Sparkles flattened glints drift along the ground ahead, SparkleGap m apart, Drift gaps a second, twinkling.
        private const float Lean=18,NodHz=2,PointHop=.08f,HopAhead=.06f,SparkleGap=.6f,SparkleSize=.14f,SparkleFlat=.15f,SparkleLift=.02f,Drift=1.2f,TwinkleHz=1.7f,SparkleGlow=1.5f;
        private const int Sparkles=5;
        // One squeak as it appears: SqueakSeconds rising from SqueakFrom to SqueakTo Hz.
        private const float SqueakSeconds=.15f,SqueakFrom=1400,SqueakTo=2600;

        // The fixed palette, every colour saturated enough for the fake hue shift to show.
        private static readonly Color[] Paints={
            new Color(.22f,.42f,.9f),new Color(.9f,.18f,.16f),new Color(.95f,.68f,.52f),new Color(.98f,.9f,.62f),   // coat, red, skin, cream
            new Color(1f,.45f,.8f),new Color(.45f,.95f,1f),new Color(.3f,.8f,.3f),new Color(.15f,.55f,.35f),         // pink, wing, green, dark green
            new Color(.72f,.52f,.32f),new Color(.85f,.72f,.5f),new Color(.3f,.12f,.4f),                                // tan, antler, eyes
            new Color(.62f,.3f,.9f),new Color(1f,.55f,.15f),new Color(.95f,.85f,.2f),new Color(.1f,.7f,.65f)};         // VISION-6: purple, orange, yellow, teal
        public static IReadOnlyList<Color> Palette=>Paints;
        private static AudioClip squeak;
        /// <summary>VISION-6: the chirp a pointer gives as it appears, made once.</summary>
        public static AudioClip Squeak{get{if(squeak==null)squeak=CreateSqueak();return squeak;}}
        // ART-1: each kind is a model whose parts end __P<n>, painted from Paints[n] (VISION-6: through its look's swaps).
        private static readonly (string Name,string Model)[] Looks={("Gnome","FestivalCreatureGnome"),("Pixie","FestivalCreaturePixie"),
            ("Dragon","FestivalCreatureDragon"),("Mushroom sprite","FestivalCreatureMushroomSprite"),("Jackalope","FestivalCreatureJackalope")};
        // VISION-6: each kind's 8 looks, in Looks' order; look 0 is the model as painted. A look swaps palette colours (from>to) and
        // sizes the model. The gnome is about .48 m tall, so it never grows: every creature stays .2-.5 m tall.
        private const int LooksPerKind=8;
        private static readonly (string Swaps,float Size)[] Variations={
            ("",1),("0>6",1),("0>1 1>0",.9f),("0>11 1>13",1),("0>12",.9f),("0>14 1>4",1),("1>6 3>8",.9f),("0>7 1>12",1),            // gnome: coat 0, hat 1, beard 3
            ("",1),("4>11",1.1f),("4>13 5>4",.9f),("5>6",1),("4>12",1.1f),("4>5 5>4",.9f),("4>14 5>13",1),("4>1",1.1f),            // pixie: dress and hair 4, wings 5
            ("",1),("6>1 7>12",1.1f),("6>0 7>5",.9f),("6>11 7>4",1),("6>13 7>12",1.1f),("6>7 7>6",.9f),("6>14 7>0",1),("6>8 7>1",1.1f), // dragon: body 6, wings and legs 7
            ("",1),("1>12",1.1f),("1>13",.9f),("1>11",1),("1>4",1.1f),("1>0",.9f),("1>6 3>13",1),("1>14",1.1f),                    // mushroom sprite: cap 1, stem 3
            ("",1),("8>3 3>8",1.1f),("8>2",.9f),("9>13",1),("8>12",1.1f),("8>3 9>11",.9f),("9>4",1),("8>0 9>3",1.1f)};             // jackalope: fur 8, antlers 9
        private static readonly int[][] Remaps=Remapped();
        private static int[][] Remapped()
        {
            var remaps=new int[Variations.Length][];
            for(int i=0;i<remaps.Length;i++)
            {
                var remap=remaps[i]=new int[Paints.Length];for(int p=0;p<remap.Length;p++)remap[p]=p;
                foreach(var swap in Variations[i].Swaps.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries)){var ends=swap.Split('>');remap[int.Parse(ends[0])]=int.Parse(ends[1]);}
            }
            return remaps;
        }
        /// <summary>VISION-6: the palette colour a kind's look paints a part marked __P(paint) with.</summary>
        public static int PaintOf(int kind,int look,int paint)=>Remaps[kind*LooksPerKind+look][paint];
        /// <summary>VISION-6: how much a kind's look scales its model.</summary>
        public static float SizeOf(int kind,int look)=>Variations[kind*LooksPerKind+look].Size;

        /// <summary>When creatures arrive and what each one is: a pure function of the seed and the clock it is stepped with.</summary>
        public sealed class Schedule
        {
            public struct Arrival { public double At; public bool Persists; public int Look,Variation; public float Life,Bearing,Distance; }
            // VISION-6: each arrival's look comes from a stream of its own, so the first draws everything it always drew.
            private readonly ContentRandom random,looks;
            private double next=-1;
            public Schedule(int seed){random=new ContentRandom(seed);looks=new ContentRandom(unchecked(seed*59+23));}
            // The first dosed step arms it one gap ahead, so joining mid-level brings no burst, and a sober step disarms it. Every
            // arrival draws the same values, and the next gap counts from the last arrival, dropped or not.
            public bool Due(double clock,int dose,out Arrival arrival)
            {
                arrival=default;
                if(dose<1){next=-1;return false;}
                if(next<0){next=clock+Gap(dose);return false;}
                if(clock<next)return false;
                arrival=new Arrival{At=next,Persists=random.Next(PersistOneIn)==0,Look=random.Next(Looks.Length),Life=Mathf.Lerp(MinLife,MaxLife,Unit()),Bearing=Unit()*2-1,Distance=Mathf.Lerp(Near,Far,Unit()),Variation=looks.Next(LooksPerKind)};
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

        // A pointer's target is a hinted vision's Id (Hint) or the tank; Goal is where that stands, Scurry the seconds into a scurry
        // from From to To (-1 none). Scurries counts its scurries; each one's Weave (m) and Phase shape its path, and
        // Heading is the way it runs. Its sparkles and voice are made the first time it points and kept in the pool.
        private sealed class Creature
        {
            public Transform Root; public int Look,Variation,Index; public bool Persists,InView,Seen; public float Age,Life,Fading; public Vector3 Spot;
            public string Hint=""; public bool Tank; public float Scurry=-1,Weave,Phase; public int Scurries; public Vector3 Goal,From,To,Heading; public Transform Trail; public Transform[] Glints; public AudioSource Voice;
            public bool Points=>Tank||Hint!="";
        }
        private readonly List<Creature> live=new List<Creature>();
        // One shelf per (kind, look), so a creature taken back is never repainted.
        private readonly List<Creature>[] pool=new List<Creature>[Looks.Length*LooksPerKind];
        private readonly Material[] paints=new Material[Paints.Length];
        private Material glint;
        // The hints this client has pointed at this level (vision ids restart every round), and when a tank pointer may next come.
        private readonly List<string> pointed=new List<string>();
        private double nextTank=TankFirst;
        private float volume;
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
            if(session==null)TryGetComponent(out session);
            volume=session?.Profile==null?0:FestivalSettings.EffectsGain(session.Profile.Data);
            if(state==null||view==null||!FestivalWorld.ShowsFestival(state.Phase)){Clear();return;}
            int key=SeedFor(state,localPlayerId);if(schedule==null||key!=seed){Clear();if(key!=seed){pointed.Clear();nextTank=TankFirst;}seed=key;schedule=new Schedule(key);}
            var me=Find(state,localPlayerId);int dose=DoseOf(me),cap=dose<1?0:Caps[Math.Min(dose,Caps.Length)-1];
            bool paused=state.Phase==FestivalSimulation.MinigamePhase;
            var eye=view.transform.position;float shimmer=motion.Step(view.transform,paused?0:deltaTime);
            for(int i=live.Count-1;i>=0;i--)
            {
                var c=live[i];if(paused)continue;c.Age+=deltaTime;
                if(c.Fading>=0)c.Fading+=deltaTime;else if(dose<1||!c.Persists&&c.Age>=FadeSeconds+c.Life||c.Points&&!Aim(state,me,c))c.Fading=0;
                if(c.Points)Scurry(c,eye,deltaTime);
                var off=eye-c.Spot;off.y=0;
                if(c.Fading>=FadeSeconds||c.Persists&&off.sqrMagnitude>=LeaveDistance*LeaveDistance)Release(i);
            }
            // Those already on screen keep their places; one coming into view takes a free place or stays hidden, so turning back
            // to the ones left behind never shows more than the cap.
            GeometryUtility.CalculateFrustumPlanes(view,frustum);OnScreen=0;
            foreach(var c in live){c.InView=InView(c.Spot);if(!c.InView)c.Seen=false;else if(c.Seen)OnScreen++;}
            foreach(var c in live)if(c.InView&&!c.Seen&&OnScreen<cap){c.Seen=true;OnScreen++;}
            // Arrivals only while the level clock runs. One over the cap is dropped (only those in view count) and the schedule
            // keeps ticking. A hint not pointed at yet, or else a due tank, makes the arrival its pointer (only its kind, bearing
            // and staying change, so the schedule draws what it always drew); dropped, the hint waits for the next.
            if(state.Phase=="Playing")
                while(schedule.Due(state.ElapsedSeconds,dose,out var arrival))
                {
                    var hint=Unpointed(state,me,out var goal);bool tank=hint==null&&arrival.At>=nextTank&&Target(state,me,"",true,out goal);
                    // It keeps its drawn distance but stands on its target's side, so it never faces back at you.
                    if(hint!=null||tank){arrival.Look=hint!=null?JobOf(hint.Kind):Jackalope;arrival.Persists=true;arrival.Bearing=Mathf.Clamp(Mathf.DeltaAngle(view.transform.eulerAngles.y,Mathf.Atan2(goal.x-eye.x,goal.z-eye.z)*Mathf.Rad2Deg)/HalfWidth(view),-1,1);}
                    if(!(OnScreen<cap&&Place(view,arrival,out var spot)))continue;
                    var c=Spawn(arrival,spot);c.InView=InView(spot);if(c.InView){c.Seen=true;OnScreen++;}
                    if(hint!=null){pointed.Add(hint.Id);c.Hint=hint.Id;}else if(tank){nextTank=arrival.At+TankEvery;c.Tank=true;}
                    if(c.Points){Aim(state,me,c);Beckon(c);}
                }
            foreach(var c in live)
            {
                if(c.Voice!=null)c.Voice.volume=volume;
                bool drawn=!c.InView||c.Seen;if(c.Root.gameObject.activeSelf!=drawn)c.Root.gameObject.SetActive(drawn);
                if(!drawn||paused)continue;
                var ahead=c.Goal-c.Spot;ahead.y=0;
                if(c.Points&&ahead.sqrMagnitude>1e-4f)
                {
                    // It faces its target, not you (the way it runs while it scurries), leaning and hopping that way.
                    ahead.Normalize();var facing=c.Scurry>=0?c.Heading:ahead;float nod=Mathf.Abs(Mathf.Sin(c.Age*NodHz*Mathf.PI));
                    c.Root.SetPositionAndRotation(c.Spot+Vector3.up*(PointHop*nod)+facing*(HopAhead*nod),Quaternion.LookRotation(facing)*Quaternion.Euler(Lean*nod,0,0));
                }
                else
                {
                    c.Root.position=c.Spot+Vector3.up*(Hop*Mathf.Abs(Mathf.Sin((c.Age*HopHz+c.Index*.37f)*Mathf.PI)));
                    var toEye=eye-c.Spot;toEye.y=0;if(toEye.sqrMagnitude>1e-4f)c.Root.rotation=Quaternion.LookRotation(toEye);
                }
                float grown=Mathf.SmoothStep(0,1,c.Age/FadeSeconds)*(1-Mathf.SmoothStep(0,1,Mathf.Max(0,c.Fading)/FadeSeconds));
                float scale=grown*FestivalVisionMarkers.ShimmerScale(shimmer,time,c.Index);c.Root.localScale=Vector3.one*scale;
                // Placed after the body is scaled (they grow and fade with it); at no size at all there is nothing to place.
                if(c.Points&&ahead.sqrMagnitude>1e-4f&&scale>1e-3f)Twinkle(c,ahead,time);
            }
        }

        private static PlayerState Find(RoundState state,string playerId){foreach(var p in state.Players)if(p.Id==playerId)return p;return null;}
        // Only the local player's own dose counts, never another effect; a spirit sees none.
        private static int DoseOf(PlayerState p)
        {
            if(p==null||p.Life=="Spirit")return 0;
            foreach(var e in p.Effects)if(e.Id==FestivalSimulation.DoseEffect)return e.Intensity;
            return 0;
        }
        // VISION-6: which kind points at a vision of this kind, or -1 for none.
        private static int JobOf(string kind)=>kind=="Clue"?MushroomSprite:kind=="Buyer"?Pixie:kind=="Narc"?Dragon:kind=="Stash"||kind=="DoubleBuyer"?Gnome:-1;
        // The first hinted vision in the view this client has not pointed at yet and whose target is there to point at, or null.
        private VisionState Unpointed(RoundState state,PlayerState me,out Vector3 at)
        {
            at=default;
            for(int i=0;i<state.Visions.Count;i++){var v=state.Visions[i];if(v.Hinted&&JobOf(v.Kind)>=0&&!pointed.Contains(v.Id)&&Target(state,me,v.Id,false,out at))return v;}
            return null;
        }
        // Where a pointer's target stands now (the vision's festivalgoer, its spot, or the tank), false once there is nothing left
        // to point at: the vision left the view or was checked, its festivalgoer left the view, the tank was grabbed, or the player
        // is within Reached m of it.
        private static bool Target(RoundState state,PlayerState me,string hint,bool tank,out Vector3 at)
        {
            at=default;
            if(tank){var spot=FestivalSimulation.GiggleTankAt(state);if(spot==null)return false;at=new Vector3(spot.X,0,spot.Z);}
            else
            {
                VisionState v=null;for(int i=0;i<state.Visions.Count&&v==null;i++)if(state.Visions[i].Id==hint&&!state.Visions[i].Confirmed)v=state.Visions[i];
                if(v==null)return false;
                if(v.NpcId=="")at=new Vector3(v.X,0,v.Z);
                else{NpcState n=null;for(int i=0;i<state.Npcs.Count&&n==null;i++)if(state.Npcs[i].Id==v.NpcId)n=state.Npcs[i];if(n==null)return false;at=new Vector3(n.X,0,n.Z);}
            }
            return me==null||(me.X-at.x)*(me.X-at.x)+(me.Z-at.z)*(me.Z-at.z)>=Reached*Reached;
        }
        // A pointer's Goal follows its target while there is one; once there is none it keeps the last, so it fades out still
        // facing where its target stood (not the world's origin).
        private static bool Aim(RoundState state,PlayerState me,Creature c)
        {
            if(!Target(state,me,c.Hint,c.Tank,out var at))return false;
            c.Goal=at;return true;
        }
        // With the eye close, it scurries on toward its target over the ground, the way Place stands it, and stops short of it. A
        // spot inside something keeps it where it is. It weaves on the way, a sine across the line faded in and out,
        // each scurry its own from the creature's Index and its count.
        // ponytail: only To is probed; probe the weave's widest points too if one is seen running through a wall.
        private static void Scurry(Creature c,Vector3 eye,float deltaTime)
        {
            if(c.Scurry<0)
            {
                var ahead=c.Goal-c.Spot;ahead.y=0;float room=ahead.magnitude-StopShort;var off=eye-c.Spot;off.y=0;
                if(c.Fading>=0||room<MinScurry||off.sqrMagnitude>=Startle*Startle)return;
                var to=c.Spot+ahead/ahead.magnitude*Mathf.Min(ScurryMetres,room);
                if(!Ground(to.x,to.z,eye.y,out to))return;
                c.From=c.Spot;c.To=to;c.Scurry=0;c.Scurries++;
                c.Weave=Mathf.Lerp(MinWeave,MaxWeave,Mathf.Repeat(c.Index*.618034f+c.Scurries*.381966f,1));c.Phase=2*Mathf.PI*Mathf.Repeat(c.Index*.754878f+c.Scurries*.56984f,1);
            }
            c.Scurry+=deltaTime;float u=Mathf.SmoothStep(0,1,c.Scurry/ScurrySeconds);
            var line=c.To-c.From;line.y=0;float length=line.magnitude;var across=new Vector3(line.z,0,-line.x)/length;
            float weave=c.Weave*length/ScurryMetres,turns=2*Mathf.PI*Wiggles*length/ScurryMetres,wave=turns*u+c.Phase,fade=Mathf.Sin(Mathf.PI*u);
            c.Spot=Vector3.Lerp(c.From,c.To,u)+across*(weave*Mathf.Sin(wave)*fade*fade);
            c.Heading=(line+across*(weave*(turns*Mathf.Cos(wave)*fade*fade+Mathf.PI*Mathf.Sin(wave)*Mathf.Sin(2*Mathf.PI*u)))).normalized;
            if(c.Scurry>=ScurrySeconds){c.Spot=c.To;c.Scurry=-1;}
        }
        // Its sparkles drift along the ground toward the target, swelling and shrinking as they go and twinkling; none past it.
        private static void Twinkle(Creature c,Vector3 ahead,float time)
        {
            var off=c.Goal-c.Spot;off.y=0;float room=off.magnitude;
            for(int i=0;i<c.Glints.Length;i++)
            {
                float along=Mathf.Repeat(i+time*Drift,Sparkles),at=SparkleGap*(.5f+along);
                float size=at>room?0:SparkleSize*Mathf.Sin(along/Sparkles*Mathf.PI)*(.4f+.6f*Mathf.Abs(Mathf.Sin((time*TwinkleHz+i*.37f)*Mathf.PI)));
                c.Glints[i].SetPositionAndRotation(c.Spot+ahead*at+Vector3.up*SparkleLift,Quaternion.identity);c.Glints[i].localScale=new Vector3(size,size*SparkleFlat,size);
            }
        }
        private bool InView(Vector3 spot)=>GeometryUtility.TestPlanesAABB(frustum,new Bounds(spot+Vector3.up*(Box/2),Vector3.one*Box));
        // ponytail: one spot per arrival, dropped when it is inside something; retry nearer if crowded places feel too empty.
        private static bool Place(Camera view,Schedule.Arrival arrival,out Vector3 spot)
        {
            var eye=view.transform.position;var along=Quaternion.Euler(0,view.transform.eulerAngles.y+arrival.Bearing*HalfWidth(view),0)*Vector3.forward*arrival.Distance;
            return Ground(eye.x+along.x,eye.z+along.z,eye.y,out spot);
        }
        // Degrees from the view's middle to an arrival's widest bearing: half the view's width and EdgeMargin past it.
        private static float HalfWidth(Camera view)=>Mathf.Atan(Mathf.Tan(view.fieldOfView*.5f*Mathf.Deg2Rad)*view.aspect)*Mathf.Rad2Deg+EdgeMargin;
        // The ground at x, z: whatever a probe down from eye height lands on (y=0 where it finds nothing); false when a creature
        // standing there would be inside something.
        private static bool Ground(float x,float z,float eyeHeight,out Vector3 spot)
        {
            spot=new Vector3(x,0,z);
            if(Physics.Raycast(new Vector3(x,eyeHeight,z),Vector3.down,out var hit,eyeHeight+1,~0,QueryTriggerInteraction.Ignore))spot.y=hit.point.y;
            return !Physics.CheckSphere(spot+Vector3.up*InsideProbe,InsideRadius,~0,QueryTriggerInteraction.Ignore);
        }

        private Creature Spawn(Schedule.Arrival arrival,Vector3 spot)
        {
            var shelf=pool[arrival.Look*LooksPerKind+arrival.Variation];Creature c;
            if(shelf!=null&&shelf.Count>0){c=shelf[shelf.Count-1];shelf.RemoveAt(shelf.Count-1);}else c=Build(arrival.Look,arrival.Variation);
            c.Persists=arrival.Persists;c.Life=arrival.Life;c.Age=0;c.Fading=-1;c.Spot=spot;c.Seen=false;c.Index=++arrivals;
            c.Hint="";c.Tank=false;c.Scurry=-1;c.Scurries=0;if(c.Trail!=null)c.Trail.gameObject.SetActive(false);
            c.Root.SetPositionAndRotation(spot,Quaternion.identity);c.Root.localScale=Vector3.zero;c.Root.gameObject.SetActive(true);
            live.Add(c);return c;
        }
        private void Release(int index)
        {
            var c=live[index];live.RemoveAt(index);c.Root.gameObject.SetActive(false);
            (pool[c.Look*LooksPerKind+c.Variation]??=new List<Creature>()).Add(c);
        }
        // Back to the pool: the festival is no longer shown, the level changed, or the client left.
        private void Clear(){for(int i=live.Count-1;i>=0;i--)Release(i);schedule=null;OnScreen=0;}

        // A kind in one of its looks. The size goes on the model, since Apply rewrites the root's scale every frame.
        private Creature Build(int kind,int look)
        {
            var root=new GameObject("Creature "+Looks[kind].Name).transform;root.SetParent(transform,false);
            var model=FestivalArtView.Create(root,Looks[kind].Model,castShadows:true);
            if(model!=null)
            {
                model.transform.localScale*=SizeOf(kind,look);
                foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    int mark=renderer.name.LastIndexOf("__P",StringComparison.Ordinal);
                    if(mark>=0&&int.TryParse(renderer.name.Substring(mark+3).Split('.')[0],out int paint)&&paint<Paints.Length)renderer.sharedMaterial=Paint(PaintOf(kind,look,paint));
                }
            }
            return new Creature{Root=root,Look=kind,Variation=look};
        }
        // VISION-6: a pointer's sparkles and voice, made the first time it points; it squeaks as it appears, from where it stands.
        private void Beckon(Creature c)
        {
            if(c.Trail==null)
            {
                c.Trail=new GameObject("Sparkles").transform;c.Trail.SetParent(c.Root,false);c.Glints=new Transform[Sparkles];
                for(int i=0;i<Sparkles;i++)
                {
                    var sparkle=GameObject.CreatePrimitive(PrimitiveType.Sphere);sparkle.name="Sparkle";sparkle.transform.SetParent(c.Trail,false);
                    var collider=sparkle.GetComponent<Collider>();collider.enabled=false;Dispose(collider);
                    var renderer=sparkle.GetComponent<MeshRenderer>();renderer.sharedMaterial=Glint();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                    c.Glints[i]=sparkle.transform;
                }
                // 3-D so it says which way to look, but heard in full anywhere a pointer appears (out to Far); no Doppler, like the stage.
                c.Voice=c.Root.gameObject.AddComponent<AudioSource>();c.Voice.playOnAwake=false;c.Voice.spatialBlend=1;c.Voice.minDistance=Far;c.Voice.dopplerLevel=0;
            }
            c.Trail.gameObject.SetActive(true);c.Voice.volume=volume;c.Voice.PlayOneShot(Squeak);
        }
        // The sparkles' one material: cream, hue-shifted like everything the dose invents, and glowing.
        private Material Glint()
        {
            if(glint!=null)return glint;
            var color=FestivalVisionMarkers.FakeTint(Paints[3]);glint=new Material(Resources.Load<Material>("FestivalLit")){color=color};
            if(glint.HasProperty("_EmissionColor")){glint.EnableKeyword("_EMISSION");glint.SetColor("_EmissionColor",color*SparkleGlow);}
            return glint;
        }
        private static AudioClip CreateSqueak()
        {
            const int rate=22050;var samples=new float[(int)(rate*SqueakSeconds)];double phase=0;
            for(int n=0;n<samples.Length;n++)
            {
                float t=n/(float)samples.Length;phase+=2*Math.PI*Mathf.Lerp(SqueakFrom,SqueakTo,t)/rate;
                samples[n]=(float)Math.Sin(phase)*Mathf.Sin(t*Mathf.PI)*.25f;
            }
            var clip=AudioClip.Create("Creature pointer squeak",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
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

        private void OnDestroy(){foreach(var material in paints)if(material!=null)Dispose(material);if(glint!=null)Dispose(glint);}
        private static void Dispose(Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
