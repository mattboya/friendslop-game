using System;
using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Festival.Presentation
{
    /// <summary>SPIN-3: a slot machine whose people reel stops on the tripper, dose reel on their dose and TRIP-5's substance reel on
    /// what they took, then the camera cuts to the tripper taking it at camp (SPIN-1). Everything is computed from the public spin
    /// result (SpinSeed, SpinEndsAt, TripperId, Doses), so every client shows the same spin, landing and reaction without any extra
    /// network traffic.</summary>
    public sealed class FestivalSpinner : MonoBehaviour
    {
        // Seconds from the spin's start (SpinEndsAt - SpinSeconds), all from Core's timeline. The reels (0 people, 1 dose, 2 substance)
        // start together as the lever drops and reel i comes to rest at ReelStops(i); after the results rest come the take and the reaction.
        public static float ReelStops(int reel)=>(float)FestivalSimulation.ReelStopSeconds[reel];
        public static float LastReelStops=>ReelStops(FestivalSimulation.ReelStopSeconds.Count-1);
        public static float TakeStarts=>LastReelStops+(float)FestivalSimulation.RestSeconds;
        public static float ReactStarts=>TakeStarts+(float)FestivalSimulation.TakeSeconds;
        public enum Stage{Hidden,Reels,Take,React}
        public struct Beat
        {
            public Stage Stage;
            // Reel scrolls count tiles, so the payline shows tile TileAt(strip length, scroll). Lever is how far the lever is pulled
            // down and Reaction the dose's size, both 0-1.
            public float Elapsed,PeopleScroll,DoseScroll,SubstanceScroll,Lever,Reaction;
            public bool PeopleLanded,DoseLanded,SubstanceLanded;
            public int Dose;
            public string Substance;
        }

        // Connected friends in state order; a tripper who just dropped keeps their tiles so the reel can still land.
        public static List<string> Crew(RoundState state)
        {
            var crew=state.Players.FindAll(p=>p.Connected).ConvertAll(p=>p.Id);
            if(state.TripperId!=""&&!crew.Contains(state.TripperId))crew.Add(state.TripperId);
            return crew;
        }
        // The people reel's strip: tile i shows crew[PeopleStrip(n)[i]]. The crew repeats until the strip is MinTiles long, so even a
        // crew of one fills the window, and every friend has as many tiles.
        const int MinTiles=5;
        static int PeopleTiles(int crew)=>crew*((MinTiles+crew-1)/crew);
        public static int[] PeopleStrip(int crew){var strip=new int[PeopleTiles(crew)];for(int i=0;i<strip.Length;i++)strip[i]=i%crew;return strip;}
        // The dose reel's strip, dealt round-robin from DoseSlices in units of their common factor: TRIP-12's 30/30/30/10 is 1, 2 and
        // 3 three times each and 4 once, so the 4 comes round rarely and what players see is what the spinner rolls.
        // ponytail: exact, so a table whose slices share no factor would deal 100 tiles; round to fewer if the table ever changes so.
        public static readonly IReadOnlyList<int> DoseStrip=DealDoses();
        static int[] DealDoses()
        {
            var slices=FestivalSimulation.DoseSlices;int unit=0,most=0;
            foreach(var slice in slices)unit=Factor(unit,slice);
            foreach(var slice in slices)most=Mathf.Max(most,slice/unit);
            var strip=new List<int>();
            for(int round=0;round<most;round++)for(int dose=0;dose<slices.Count;dose++)if(round<slices[dose]/unit)strip.Add(dose+1);
            return strip.ToArray();
        }
        static int Factor(int a,int b)=>b==0?a:Factor(b,a%b);
        // The substance reel's strip is the five substances, one tile each; an unknown id (a host from before the reel) rests on the first.
        static int SubstanceTile(string substance){for(int i=0;i<FestivalSimulation.Substances.Count;i++)if(FestivalSimulation.Substances[i]==substance)return i;return 0;}
        /// <summary>What a substance id reads as on screen ("FUN GUYS"), or "" for none.</summary>
        public static string SubstanceName(string substance)=>string.IsNullOrEmpty(substance)?"":Catalog.FindEffect(substance)?.Name.ToUpperInvariant()??"";
        // One of the strip's tiles that hold value, picked by seed, so a reel does not always stop on the same one.
        static int TileOf(IReadOnlyList<int> strip,int value,int seed)
        {
            int count=0;for(int i=0;i<strip.Count;i++)if(strip[i]==value)count++;
            int nth=Pick(seed,Mathf.Max(1,count));
            for(int i=0;i<strip.Count;i++)if(strip[i]==value&&nth--==0)return i;
            return 0;
        }
        static float Unit(int seed){uint x=unchecked((uint)seed*2654435761u);x^=x>>15;x=unchecked(x*0x2c1b3c6du);x^=x>>12;return (x>>8)/16777216f;}
        static int Pick(int seed,int count)=>(int)(Unit(seed)*count);
        /// <summary>The strip tile resting nearest the payline at this scroll.</summary>
        public static int TileAt(int count,float scroll){int tile=Mathf.FloorToInt(scroll+.5f)%count;return tile<0?tile+count:tile;}
        /// <summary>How many tiles above the payline the middle of a strip tile sits at this scroll, wrapped round the strip so it runs
        /// on forever: as the scroll grows the tiles move down.</summary>
        public static float TileOffset(int tile,int count,float scroll)=>Mathf.Repeat(tile-scroll+count*.5f,count)-count*.5f;
        // A reel window shows the payline's tile whole and the tiles either side cut off by its edges.
        public const float WindowTiles=2.2f;

        // A reel spins up from rest over SpinUp as the lever drops, runs about Cruise tiles a second, and spends its last EaseSeconds
        // easing into the stop: a back ease-out (Settle) whose opening speed matches the run, so it slows gradually, passes the
        // result by a little and settles back. Back is the death card's pop.
        const float Cruise=20,SpinUp=.3f,EaseSeconds=1.5f,Settle=.8f,SettleSpeed=3*(Settle+1)-2*Settle,Back=1.70158f;
        // Tiles run by elapsed at one tile a second, for a reel that stops at stop.
        static float Run(float elapsed,float stop)
        {
            float t=Mathf.Clamp(elapsed,0,stop),ease=stop-EaseSeconds;
            if(t<SpinUp)return t*t/(2*SpinUp);
            if(t<=ease)return t-SpinUp*.5f;
            float u=(t-ease)/EaseSeconds-1;
            return ease-SpinUp*.5f+EaseSeconds/SettleSpeed*(1+(Settle+1)*u*u*u+Settle*u*u);
        }
        /// <summary>The scroll, in tiles, elapsed seconds after the lever drops, of a reel of count tiles that starts on a seeded tile
        /// and comes to rest at stop with tile centred on the payline. Every client works out the same scroll from the same seed.</summary>
        public static float Scroll(int count,int tile,int seed,float stop,float elapsed)
        {
            int from=Pick(unchecked(seed*7+1),count);float run=Run(stop,stop);
            int distance=Mathf.RoundToInt((Cruise*run-(tile-from))/count)*count+tile-from;
            return elapsed>=stop?from+distance:from+distance*Run(elapsed,stop)/run;
        }
        // The lever drops as the reels start and springs back.
        static float Lever(float t)=>t<.15f?Mathf.Clamp01(t/.15f):t<.35f?1:1-Mathf.SmoothStep(0,1,(t-.35f)/.45f);
        // The cabinet's ring of bulbs, clockwise from the top left. While the reels spin every third one is lit, chasing round the
        // ring ChaseSteps times a second; in the FlashSeconds after each earlier reel stops they light in order round it; from the
        // last stop to the take the whole ring blinks. A multiple of 3, so the chase closes round the ring without a seam.
        public const int Bulbs=42;
        const float BulbInset=34;
        // Where bulb sits on the cabinet: evenly spaced along a rectangle BulbInset in from the cabinet's edges, round the reels.
        static Vector2 BulbAt(int bulb)
        {
            float x=CabinetSize.x*.5f-BulbInset,y=CabinetSize.y*.5f-BulbInset,along=bulb*4*(x+y)/Bulbs;
            if(along<2*x)return new Vector2(-x+along,y);along-=2*x;
            if(along<2*y)return new Vector2(x,y-along);along-=2*y;
            if(along<2*x)return new Vector2(x-along,-y);along-=2*x;
            return new Vector2(-x,-y+along);
        }
        // Reduced motion keeps whole-ring blinks under 3 a second, the usual photosensitivity line, and chases at a third the speed.
        const float ChaseSteps=12,FlashSeconds=.4f,BlinkHertz=8,ReducedBlinkHertz=2;
        public static bool BulbLit(float elapsed,int bulb,bool reducedMotion)
        {
            if(elapsed>=TakeStarts)return true;
            float since=elapsed-LastReelStops;
            if(since>=0)return Mathf.Repeat(since*(reducedMotion?ReducedBlinkHertz:BlinkHertz),1)<.5f;
            for(int reel=0;reel<FestivalSimulation.ReelStopSeconds.Count-1;reel++)
            {
                since=elapsed-ReelStops(reel);
                if(since>=0&&since<FlashSeconds)return bulb*FlashSeconds<=since*Bulbs;
            }
            return (Mathf.FloorToInt(elapsed*(reducedMotion?ChaseSteps/3:ChaseSteps))-bulb)%3==0;
        }
        public static Beat At(RoundState state,List<string> crew,double now)
        {
            var beat=new Beat();
            if(state==null||state.Phase!="Spinning"||crew.Count==0)return beat;
            float t=beat.Elapsed=(float)(now-(state.SpinEndsAt-FestivalSimulation.SpinSeconds));
            PlayerDose took=null;for(int i=0;i<state.Doses.Count&&took==null;i++)if(state.Doses[i].PlayerId==state.TripperId)took=state.Doses[i];
            beat.Dose=Mathf.Clamp(took?.Dose??1,1,FestivalSimulation.DoseSlices.Count);beat.Substance=took?.Substance??"";
            beat.Reaction=beat.Dose/(float)FestivalSimulation.DoseSlices.Count;
            int size=crew.Count,people=PeopleTiles(size),doseSeed=unchecked(state.SpinSeed*31+7),substanceSeed=unchecked(state.SpinSeed*41+3);
            beat.PeopleScroll=Scroll(people,Mathf.Max(0,crew.IndexOf(state.TripperId))+size*Pick(state.SpinSeed,people/size),state.SpinSeed,ReelStops(0),t);
            beat.DoseScroll=Scroll(DoseStrip.Count,TileOf(DoseStrip,beat.Dose,doseSeed),doseSeed,ReelStops(1),t);
            beat.SubstanceScroll=Scroll(FestivalSimulation.Substances.Count,SubstanceTile(beat.Substance),substanceSeed,ReelStops(2),t);
            beat.PeopleLanded=t>=ReelStops(0);beat.DoseLanded=t>=ReelStops(1);beat.SubstanceLanded=t>=ReelStops(2);
            beat.Lever=Lever(t);
            beat.Stage=t<TakeStarts?Stage.Reels:t<ReactStarts?Stage.Take:Stage.React;
            return beat;
        }

        // LOOP-4: every positive-cash death spins its own small one-reel slot in the corner, three KEEP tiles to one DROP, read off the
        // death's frozen public summary on the world clock, so every viewer (the dead player and spirits too) sees the same spin land
        // just as the pile does. It sits on its own canvas under the HUD and takes no clicks, so it never stops anyone doing anything.
        public const string DeathCanvasName="Festival death spinner";
        // The result rests DeathResultSeconds, fading out over its last DeathFadeSeconds; presentation only, no protocol meaning.
        public const float DeathResultSeconds=2.5f,DeathPopSeconds=.3f,DeathFadeSeconds=.3f;
        public const int DeathKeep=0,DeathDrop=1;
        public static readonly IReadOnlyList<int> DeathStrip=new[]{DeathKeep,DeathKeep,DeathKeep,DeathDrop};
        /// <summary>A corner reel at one moment: Neutral is reduced motion's still, blank reel while the result is pending; Pop is the
        /// card's scale and Alpha its opacity, both 0-1.</summary>
        public struct DeathFrame{public bool Shown,Landed,Neutral;public float Elapsed,Scroll,Pop,Alpha;}
        public static DeathFrame DeathBeat(RoundState state,DeathSpinState spin,double worldNow,bool reducedMotion)
        {
            var frame=new DeathFrame();
            if(state==null||spin==null||state.Phase!="Playing"||spin.RoundId!=state.RoundId||spin.AtWorldSeconds<0)return frame;
            float wheel=(float)FestivalSimulation.DeathWheelSeconds,t=frame.Elapsed=(float)(worldNow-spin.AtWorldSeconds);
            if(!(t>=0&&t<wheel+DeathResultSeconds))return frame;
            frame.Shown=true;frame.Landed=t>=wheel;frame.Neutral=reducedMotion&&!frame.Landed;
            frame.Scroll=Scroll(DeathStrip.Count,TileOf(DeathStrip,spin.CashDropped>0?DeathDrop:DeathKeep,spin.Seed),spin.Seed,wheel,frame.Neutral?0:t);
            float u=Mathf.Clamp01(t/DeathPopSeconds)-1;
            frame.Pop=reducedMotion?1:1+(Back+1)*u*u*u+Back*u*u;
            frame.Alpha=Mathf.Clamp01((wheel+DeathResultSeconds-t)/DeathFadeSeconds);
            return frame;
        }
        /// <summary>The deaths whose corner reels show at worldNow, in the order they happened (the host's list order).</summary>
        public static void ShownDeaths(RoundState state,double worldNow,List<DeathSpinState> shown)
        {
            shown.Clear();if(state==null)return;
            foreach(var spin in state.DeathSpins)if(DeathBeat(state,spin,worldNow,false).Shown)shown.Add(spin);
        }

        // FestivalHud's palette, so the spinner reads as part of the same interface.
        static readonly Color Ink=new Color(.035f,.065f,.075f,1),Paper=new Color(.948f,.928f,.852f,1),Orange=new Color(.99f,.465f,.255f,1);
        static readonly Color Gold=new Color(1,.76f,.29f),Unlit=new Color(.3f,.23f,.09f),Pink=new Color(1,.25f,.55f);
        static readonly Color[] CrewColors={new Color(.99f,.465f,.255f),new Color(.385f,.86f,.725f),new Color(1,.38f,.61f),new Color(1,.76f,.29f),
            new Color(.34f,.78f,1),new Color(.46f,.95f,.68f),new Color(.66f,.52f,1),new Color(.96f,.62f,.50f)};
        // The dose tiles heat up from calm mint to the hot pink 4.
        static readonly Color[] DoseColors={new Color(.385f,.86f,.725f),new Color(1,.76f,.29f),new Color(.99f,.465f,.255f),Pink};
        // The substance tiles, in Substances order: stamp pink, mushroom violet, warm gold, cool sky blue, leaf green.
        static readonly Color[] SubstanceColors={new Color(1,.38f,.61f),new Color(.66f,.52f,1),new Color(1,.76f,.29f),new Color(.34f,.78f,1),new Color(.55f,.85f,.4f)};
        static readonly string[] Reactions={"","Feels fine. Probably.","Whoa.","The colors are talking.","Oh no. Oh yes. Oh no."};
        // The cabinet, in reference pixels: three reel windows side by side inside the ring of bulbs, the lever on its right.
        const float TileHeight=128,ReelWidth=420,ReelSpacing=470,ReelY=-30,LeverLength=150;
        static readonly Vector2 CabinetSize=new Vector2(1500,600);
        // The take camera pushes in from TakeFar to TakeNear. It swings to the first of TakeAngles (degrees from the tripper's
        // facing) whose line to their face passes SceneryClearance from the scenery and TakeClearance from every other friend at
        // camp; past 90 it would see the back of the head.
        const float TakeFar=2.5f,TakeNear=1.8f,TakeClearance=.45f,SceneryClearance=.3f;
        static readonly float[] TakeAngles={0,30,-30,60,-60,90,-90};
        // One reel: a masked window over a strip of tiles that wraps round forever. LOOP-4's corner spinner reuses it.
        sealed class Reel{public RectTransform Strip;public Text Title,Result;public readonly List<RectTransform> Tiles=new List<RectTransform>();public readonly List<Image> Faces=new List<Image>();public readonly List<Color> Colors=new List<Color>();}
        FestivalSession session;
        Font font;
        GameObject overlay,backdrop,letterbox;
        Reel people,dose,substance;
        readonly Image[] bulbs=new Image[Bulbs];
        RectTransform leverRod,leverKnob;
        Text caption;
        RawImage shot;
        RenderTexture shotTexture;
        Camera takeCamera;
        FestivalCharacter acting;
        float focusHeight,takeYaw;
        List<string> crew=new List<string>();
        string spinRound="",whoLine="",doseLine="",tookLine="",takeLine="",reactLine="";
        int spinSeed;
        // A pool of corner cards, filled left to right along the bottom, DeathColumns to a row, then up a row.
        sealed class DeathCard{public RectTransform Root;public CanvasGroup Fade;public Reel Reel;public GameObject Pending;public string SpinId="",Stake="",Result="";}
        const int DeathColumns=4,DeathCardLimit=8; // ponytail: room for a full crew of eight at once; a reel past that is not drawn.
        const float DeathScale=.45f;
        static readonly Vector2 DeathFirstCard=new Vector2(185,215),DeathCardPitch=new Vector2(290,260);
        static readonly Color Mint=new Color(.385f,.86f,.725f);
        GameObject deathOverlay;
        readonly List<DeathCard> deathCards=new List<DeathCard>();
        readonly List<DeathSpinState> shownDeaths=new List<DeathSpinState>();

        AudioSource slotAudio;
        AudioClip leverAudio,tickAudio,clunkAudio;
        string audioRound="";
        readonly HashSet<string> retiredAudioRounds=new HashSet<string>();
        int audioSeed;
        long audioTick;
        double audioStart,audioElapsed;
        bool audioObserved,audioHeld,audioFocused=true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        double nextAudioDiagnostic;
#endif

        void CreateAudio()
        {
            slotAudio=gameObject.AddComponent<AudioSource>();slotAudio.playOnAwake=false;slotAudio.loop=false;slotAudio.spatialBlend=0;
            leverAudio=MakeAudio("lever",.16,130,28);tickAudio=MakeAudio("tick",.025,1800,140);clunkAudio=MakeAudio("clunk",.12,90,40);
        }
        static AudioClip MakeAudio(string cue,double seconds,double tone,double decay)
        {
            const int rate=24000;var samples=new float[(int)(seconds*rate)];
            for(int i=0;i<samples.Length;i++)
            {
                double t=i/(double)rate,edge=Math.Sin(Math.PI*i/(samples.Length-1));
                samples[i]=(float)((Math.Sin(2*Math.PI*tone*t)+.3*Math.Sin(2*Math.PI*(tone*3.17)*t))*Math.Exp(-decay*t)*edge*.55);
            }
            var clip=AudioClip.Create("Original slot "+cue,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        /// <summary>Follows the public spin clock. Returns the number of one-shots actually emitted, at most one per view.
        /// Late observations and lifecycle boundaries establish a cursor, never replay a backlog.</summary>
        public int FollowAudio(RoundState state,double now,bool hidden,ProfileData profile,bool focused)
        {
            FestivalSettings.ApplyEffects(slotAudio,profile,.45f);
            bool valid=state!=null&&state.Phase=="Spinning"&&Spun(state);
            bool held=!valid||hidden||!isActiveAndEnabled||FestivalSettings.EffectsGain(profile)<=0||profile!=null&&profile.MasterVolume<=0
                ||!focused&&(profile==null||profile.MuteWhenUnfocused);
            if(!valid){StopAudio();audioHeld=true;return 0;}
            double start=state.SpinEndsAt-FestivalSimulation.SpinSeconds,t=now-start;
            if(double.IsNaN(t)||double.IsInfinity(t)){StopAudio();audioHeld=true;return 0;}
            if(audioObserved&&(retiredAudioRounds.Contains(state.RoundId)&&state.RoundId!=audioRound||state.RoundId==audioRound&&state.Tick<audioTick))
            {
                if(held){StopAudio();audioHeld=true;}return 0;
            }
            bool changed=!audioObserved||audioRound!=state.RoundId||audioSeed!=state.SpinSeed||audioStart!=start;
            if(!changed&&t<audioElapsed){if(held){StopAudio();audioHeld=true;}return 0;}
            double before=audioElapsed;bool resumed=audioHeld;
            if(changed)
            {
                StopAudio();if(audioObserved&&audioRound!=state.RoundId)retiredAudioRounds.Add(audioRound);
                audioRound=state.RoundId;audioSeed=state.SpinSeed;audioStart=start;audioObserved=true;
            }
            audioTick=Math.Max(changed?state.Tick:audioTick,state.Tick);audioElapsed=t;audioHeld=held;
            if(held){StopAudio();return 0;}
            if(changed)return t>=0&&t<=.1?PlayAudio(leverAudio,-1):0;
            if(resumed||t-before>.25||t==before)return 0;
            if(before<0&&t>=0&&t<=.1)return PlayAudio(leverAudio,-1);
            // Prioritize the current stop over a tick; a stalled view emits neither old stops nor a burst of tile crossings.
            for(int reel=FestivalSimulation.ReelStopSeconds.Count-1;reel>=0;reel--)
                if(before<ReelStops(reel)&&t>=ReelStops(reel))return PlayAudio(clunkAudio,reel);
            for(int reel=0;reel<FestivalSimulation.ReelStopSeconds.Count;reel++)
            {
                float stop=ReelStops(reel);
                if(t<stop&&Mathf.FloorToInt(Cruise*Run((float)t,stop))>Mathf.FloorToInt(Cruise*Run((float)before,stop)))return PlayAudio(tickAudio,reel);
            }
            return 0;
        }
        // A loop, not a lambda over state, so a spin frame makes no garbage.
        static bool Spun(RoundState state){foreach(var p in state.Players)if(p.Connected||p.Id==state.TripperId)return true;return false;}
        int PlayAudio(AudioClip clip,int reel)
        {
            slotAudio.Stop();slotAudio.clip=clip;slotAudio.Play();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(DevelopmentDiagnostics.Visible&&Time.unscaledTimeAsDouble>=nextAudioDiagnostic)
            {
                nextAudioDiagnostic=Time.unscaledTimeAsDouble+1;
                DevelopmentDiagnostics.GraphicsEvent("Spinner","audio_cue","round="+audioRound+" seed="+audioSeed+" start="+audioStart.ToString("F3")+" reel="+reel+" cue="+clip.name);
            }
#endif
            return 1;
        }
        void StopAudio(){if(slotAudio!=null)slotAudio.Stop();}
        void OnApplicationFocus(bool focused){audioFocused=focused;if(!focused){StopAudio();audioHeld=true;}}
        void OnDisable(){StopAudio();audioHeld=true;if(overlay!=null)overlay.SetActive(false);if(deathOverlay!=null)deathOverlay.SetActive(false);if(takeCamera!=null)takeCamera.enabled=false;}
        static void DisposeAudio(UnityEngine.Object owned){if(owned==null)return;if(Application.isPlaying)Destroy(owned);else DestroyImmediate(owned);}

        void Awake()
        {
            session=GetComponent<FestivalSession>();CreateAudio();
            font=Resources.Load<Font>("FestivalDisplay");if(font==null)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            overlay=Overlay("Festival spinner",110); // over the HUD (100)
            // No GraphicRaycaster on either canvas: neither takes a click.
            deathOverlay=Overlay(DeathCanvasName,90);deathOverlay.SetActive(false);
            backdrop=Panel(overlay.transform,"Spinner backdrop",new Color(Ink.r,Ink.g,Ink.b,.92f),Vector2.zero,Vector2.one);
            var cabinet=Block(backdrop.transform,"Cabinet",Orange,new Vector2(-40,0),CabinetSize);
            var face=(RectTransform)Panel(cabinet,"Face",Ink,Vector2.zero,Vector2.one).transform;face.offsetMin=new Vector2(16,16);face.offsetMax=new Vector2(-16,-16);
            for(int i=0;i<Bulbs;i++)
            {
                var bulb=Block(cabinet,"Bulb "+i,Unlit,BulbAt(i),new Vector2(24,24));
                bulb.localEulerAngles=new Vector3(0,0,45);bulbs[i]=bulb.GetComponent<Image>();
            }
            people=MakeReel(cabinet,"People reel",-ReelSpacing,"WHO TRIPS?");
            dose=MakeReel(cabinet,"Dose reel",0,"HOW MANY DOSES?");
            substance=MakeReel(cabinet,"Substance reel",ReelSpacing,"WHAT DID THEY TAKE?");
            var doses=new List<string>();var heat=new List<Color>();foreach(var d in DoseStrip){doses.Add(d.ToString());heat.Add(DoseColors[d-1]);}
            Fill(dose,doses,heat,72);
            var names=new List<string>();var shades=new List<Color>();
            for(int i=0;i<FestivalSimulation.Substances.Count;i++){names.Add(SubstanceName(FestivalSimulation.Substances[i]));shades.Add(SubstanceColors[i%SubstanceColors.Length]);}
            Fill(substance,names,shades,38);
            // The payline frames the row the results rest on, right across the three windows, with a diamond at either end.
            float span=2*ReelSpacing+ReelWidth+60;
            var payline=Block(cabinet,"Payline",Color.clear,new Vector2(0,ReelY),new Vector2(span,TileHeight));
            Block(payline,"Top",Pink,new Vector2(0,TileHeight*.5f),new Vector2(span,5));Block(payline,"Bottom",Pink,new Vector2(0,-TileHeight*.5f),new Vector2(span,5));
            foreach(var end in new[]{-1,1})Block(payline,end<0?"Left":"Right",Pink,new Vector2(end*span*.5f,0),new Vector2(28,28)).localEulerAngles=new Vector3(0,0,45);
            // The lever stands on the cabinet's right; pulled, its knob drops past the pivot.
            var lever=Block(cabinet,"Lever",Paper,new Vector2(CabinetSize.x*.5f+18,ReelY),new Vector2(36,110));
            leverRod=Block(lever,"Rod",Paper,Vector2.zero,new Vector2(12,LeverLength));leverRod.pivot=new Vector2(.5f,0);
            leverKnob=Block(lever,"Knob",Pink,new Vector2(0,LeverLength),new Vector2(54,54));
            letterbox=new GameObject("Take letterbox",typeof(RectTransform));letterbox.transform.SetParent(overlay.transform,false);
            Stretch((RectTransform)letterbox.transform);
            // The take renders into this canvas too, so the HUD stays hidden under the cut as it does under the reels.
            shot=new GameObject("Take shot",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage)).GetComponent<RawImage>();
            shot.transform.SetParent(letterbox.transform,false);Stretch(shot.rectTransform);shot.raycastTarget=false;shot.enabled=false;
            Panel(letterbox.transform,"Top bar",Color.black,new Vector2(0,.87f),Vector2.one);
            var bottom=Panel(letterbox.transform,"Bottom bar",Color.black,Vector2.zero,new Vector2(1,.13f));
            caption=Label(bottom.transform,"Take caption",40);Stretch(caption.rectTransform);
            overlay.SetActive(false);
            takeCamera=new GameObject("Spinner take camera").AddComponent<Camera>();takeCamera.transform.SetParent(transform,false);
            takeCamera.enabled=false;takeCamera.fieldOfView=40;takeCamera.nearClipPlane=.05f;takeCamera.farClipPlane=130;
            takeCamera.cullingMask=~(1<<30); // everyone, the local body included, but no floating name tags
            takeCamera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        }
        GameObject Overlay(string name,int order)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var canvas=go.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=order;
            var scaler=go.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            return go;
        }
        void LateUpdate()
        {
            var state=session!=null?session.State:null;bool reducedMotion=session!=null&&session.Profile.Data.ReducedMotion;
            FollowDeaths(state,session!=null?session.LocalPlayer:null,session!=null?session.EstimatedWorldSeconds:0,reducedMotion);
            if(state!=null&&state.Phase=="Spinning"&&(spinRound!=state.RoundId||spinSeed!=state.SpinSeed))Prepare(state);
            var beat=At(state,crew,session!=null?session.EstimatedSimulationSeconds:0);
            bool hidden=beat.Stage==Stage.Hidden||session==null||session.MenuOpen;
            overlay.SetActive(!hidden);
            FollowAudio(state,session!=null?session.EstimatedSimulationSeconds:0,hidden,session?.Profile?.Data,audioFocused);
            FestivalCharacter tripper=null;
            if(beat.Stage!=Stage.Hidden)
            {
                bool reeling=beat.Stage==Stage.Reels;
                backdrop.SetActive(reeling);letterbox.SetActive(!reeling);
                Show(people,beat.PeopleScroll);Show(dose,beat.DoseScroll);Show(substance,beat.SubstanceScroll);
                people.Result.text=beat.PeopleLanded?whoLine:"";dose.Result.text=beat.DoseLanded?doseLine:"";substance.Result.text=beat.SubstanceLanded?tookLine:"";
                for(int i=0;i<Bulbs;i++)bulbs[i].color=BulbLit(beat.Elapsed,i,reducedMotion)?Gold:Unlit;
                leverRod.localScale=new Vector3(1,1-2*beat.Lever,1);leverKnob.anchoredPosition=new Vector2(0,LeverLength*(1-2*beat.Lever));
                caption.text=beat.Stage==Stage.React?reactLine:takeLine;
                if(!reeling)tripper=session.WorldCharacter(state.TripperId);
            }
            Cut(tripper!=null&&tripper.gameObject.activeInHierarchy?tripper:null,beat,state);
        }
        // Freeze the people reel for this spin, so a friend dropping mid-spin cannot reshuffle its tiles, and word the results once.
        void Prepare(RoundState state)
        {
            spinRound=state.RoundId;spinSeed=state.SpinSeed;crew=Crew(state);
            var names=new List<string>();var colors=new List<Color>();
            foreach(var friend in PeopleStrip(crew.Count)){names.Add(Name(state,crew[friend]));colors.Add(CrewColors[friend%CrewColors.Length]);}
            Fill(people,names,colors,44);
            var beat=At(state,crew,0);string name=Name(state,state.TripperId),took=SubstanceName(beat.Substance);
            whoLine=name+" TRIPS";doseLine=beat.Dose+(beat.Dose==1?" DOSE":" DOSES");tookLine=took;
            takeLine=name+" TAKES "+doseLine+(took!=""?" OF "+took:"");reactLine=takeLine+"\n"+Reactions[beat.Dose];
        }
        static string Name(RoundState state,string id)
        {
            string name=(state.Players.Find(p=>p.Id==id)?.Name??"Friend").ToUpperInvariant();
            return name.Length>10?name.Substring(0,10):name;
        }
        /// <summary>LOOP-4: a corner card for each death whose reel shows at worldNow, oldest first, its amounts in viewer's money.
        /// Returns how many show.</summary>
        public int FollowDeaths(RoundState state,PlayerState viewer,double worldNow,bool reducedMotion)
        {
            ShownDeaths(viewer!=null?state:null,worldNow,shownDeaths);
            int shown=Mathf.Min(shownDeaths.Count,DeathCardLimit);
            deathOverlay.SetActive(shown>0);
            for(int i=0;i<Mathf.Max(shown,deathCards.Count);i++)
            {
                if(i>=deathCards.Count)deathCards.Add(MakeDeathCard(i));
                var card=deathCards[i];
                if(i>=shown){card.Root.gameObject.SetActive(false);card.SpinId="";continue;}
                var spin=shownDeaths[i];var frame=DeathBeat(state,spin,worldNow,reducedMotion);
                if(card.SpinId!=spin.Id)
                {
                    card.SpinId=spin.Id;card.Reel.Title.text=FestivalHudText.DeathSpinTitle(spin);
                    card.Stake=FestivalHudText.DeathSpinStake(state,viewer,spin);card.Result=FestivalHudText.DeathSpinResult(state,viewer,spin);
                }
                card.Root.gameObject.SetActive(true);
                card.Root.anchoredPosition=DeathFirstCard+new Vector2(i%DeathColumns*DeathCardPitch.x,i/DeathColumns*DeathCardPitch.y);
                card.Root.localScale=Vector3.one*DeathScale*frame.Pop;card.Fade.alpha=frame.Alpha;
                card.Reel.Strip.gameObject.SetActive(!frame.Neutral);card.Pending.SetActive(frame.Neutral);
                Show(card.Reel,frame.Scroll);
                card.Reel.Result.text=frame.Landed?card.Result:card.Stake;
            }
            return shown;
        }
        DeathCard MakeDeathCard(int index)
        {
            var root=new GameObject("Death card "+index,typeof(RectTransform),typeof(CanvasGroup)).GetComponent<RectTransform>();root.SetParent(deathOverlay.transform,false);
            root.anchorMin=root.anchorMax=Vector2.zero;root.sizeDelta=Vector2.zero;
            var fade=root.GetComponent<CanvasGroup>();fade.blocksRaycasts=false;fade.interactable=false;
            Block(root,"Card",new Color(Ink.r,Ink.g,Ink.b,.85f),new Vector2(0,-65),new Vector2(640,560));
            var reel=MakeReel(root,"Death reel",0,"");reel.Title.fontSize=56;
            // Odd-object money can run the result to two lines.
            reel.Result.fontSize=56;reel.Result.horizontalOverflow=HorizontalWrapMode.Wrap;reel.Result.rectTransform.sizeDelta=new Vector2(600,140);
            reel.Result.rectTransform.anchoredPosition-=new Vector2(0,30);
            Fill(reel,new List<string>{"KEEP","KEEP","KEEP","DROP"},new List<Color>{Mint,Mint,Mint,Pink},56);
            var pending=Label(reel.Strip.parent,"Pending",96);pending.text="?";Stretch(pending.rectTransform);
            return new DeathCard{Root=root,Fade=fade,Reel=reel,Pending=pending.gameObject};
        }
        // The camera cuts to the tripper at camp: a slow push-in while they take the dose (the existing Consume clip)
        // and react (the Panic clip, sized by the dose). A tripper out of view (inside a tent) keeps just the caption.
        void Cut(FestivalCharacter character,Beat beat,RoundState state)
        {
            if(character!=acting)
            {
                if(acting!=null){acting.Beat="";acting.AlwaysHighDetail=false;}
                acting=character;
                if(acting!=null)
                {
                    acting.AlwaysHighDetail=true;focusHeight=1.35f;takeYaw=TakeYaw(state);
                    foreach(var bone in acting.GetComponentsInChildren<Transform>())if(bone.name=="Head"){focusHeight=bone.position.y-acting.transform.position.y;break;}
                }
            }
            takeCamera.enabled=shot.enabled=acting!=null;
            if(acting==null){if(shotTexture!=null){takeCamera.targetTexture=null;shot.texture=null;Destroy(shotTexture);shotTexture=null;}return;}
            if(shotTexture==null||shotTexture.width!=Screen.width||shotTexture.height!=Screen.height)
            {
                if(shotTexture!=null)Destroy(shotTexture);
                shotTexture=new RenderTexture(Screen.width,Screen.height,24){name="Spinner take shot"};
                takeCamera.targetTexture=shotTexture;shot.texture=shotTexture;
            }
            acting.Beat=beat.Stage==Stage.Take?"TakeDose":"DoseReaction";acting.BeatStrength=beat.Reaction;
            var body=acting.transform;var focus=body.position+Vector3.up*focusHeight;
            float push=Mathf.InverseLerp(TakeStarts,(float)FestivalSimulation.SpinSeconds,beat.Elapsed);
            takeCamera.transform.position=focus+Quaternion.Euler(0,takeYaw,0)*Vector3.forward*Mathf.Lerp(TakeFar,TakeNear,push)+Vector3.up*.1f;
            takeCamera.transform.LookAt(focus);
            // ponytail: the first-person camera keeps rendering underneath for these 3 s; disable it if camp frame time matters.
        }
        // Nobody can move while Spinning and every client builds the same static world colliders, so every client picks the
        // same angle from the public positions. Scenery near the line (the trailhead posts stand inside the gate's 3.2 m Ready
        // circle) counts as no clearance at all, so if no angle clears everything the clearest one wins, preferring friends to posts.
        // ponytail: the scenery capsule runs at 1.2 m, so it reaches 0.9-1.5 m up; something hung higher with nothing under it slips past, raise it to the camera's height then.
        static float TakeYaw(RoundState state)
        {
            var tripper=state.Players.Find(p=>p.Id==state.TripperId);if(tripper==null)return 0;
            var face=new Vector2(tripper.X,tripper.Z);float best=tripper.Yaw,widest=-1;
            foreach(var angle in TakeAngles)
            {
                float yaw=tripper.Yaw+angle,clearance=float.MaxValue;
                var camera=face+new Vector2(Mathf.Sin(yaw*Mathf.Deg2Rad),Mathf.Cos(yaw*Mathf.Deg2Rad))*TakeFar;
                foreach(var p in state.Players)if(p!=tripper&&p.Connected&&p.CampVisitId=="")clearance=Mathf.Min(clearance,Distance(new Vector2(p.X,p.Z),camera,face));
                // A capsule, not a line: a post a few centimetres off a zero-width line still covers half the face and the frame. It
                // starts 0.4 m out, so its round end stops 0.1 m in front of the face and a post beside the head does not count; it also
                // catches a camera that would start inside a post. The push-in stays inside it.
                var from=new Vector3(face.x,1.2f,face.y);var to=new Vector3(camera.x,1.2f,camera.y);
                if(Physics.CheckCapsule(Vector3.MoveTowards(from,to,.4f),to,SceneryClearance,~0,QueryTriggerInteraction.Ignore))clearance=0;
                if(clearance>TakeClearance)return yaw;
                if(clearance>widest){best=yaw;widest=clearance;}
            }
            return best;
        }
        static float Distance(Vector2 point,Vector2 from,Vector2 to)
        {
            var line=to-from;return Vector2.Distance(point,from+line*Mathf.Clamp01(Vector2.Dot(point-from,line)/line.sqrMagnitude));
        }
        Reel MakeReel(RectTransform cabinet,string name,float x,string title)
        {
            var holder=Block(cabinet,name,Paper,new Vector2(x,ReelY),new Vector2(ReelWidth,TileHeight*WindowTiles)+Vector2.one*16); // its paper rim
            var window=(RectTransform)Panel(holder,"Window",Ink,Vector2.zero,Vector2.one).transform;window.offsetMin=Vector2.one*8;window.offsetMax=-Vector2.one*8;
            window.gameObject.AddComponent<RectMask2D>();
            var strip=new GameObject("Strip",typeof(RectTransform));strip.transform.SetParent(window,false);Stretch((RectTransform)strip.transform);
            var heading=Label(holder,"Title",36);heading.text=title;heading.rectTransform.anchoredPosition=new Vector2(0,holder.sizeDelta.y*.5f+40);
            var result=Label(holder,"Result",48);result.color=Orange;result.rectTransform.anchoredPosition=new Vector2(0,-holder.sizeDelta.y*.5f-44);
            return new Reel{Strip=(RectTransform)strip.transform,Title=heading,Result=result};
        }
        // Lays a strip's tiles out, each label on its colour; Show moves them.
        void Fill(Reel reel,List<string> labels,List<Color> colors,int size)
        {
            foreach(Transform old in reel.Strip)Destroy(old.gameObject);
            reel.Tiles.Clear();reel.Faces.Clear();reel.Colors.Clear();
            for(int i=0;i<labels.Count;i++)
            {
                var tile=(RectTransform)Panel(reel.Strip,"Tile "+i,colors[i],new Vector2(0,.5f),new Vector2(1,.5f)).transform;tile.sizeDelta=new Vector2(0,TileHeight-8);
                var label=Label(tile,"Label",size);label.color=Ink;label.text=labels[i];Stretch(label.rectTransform);
                reel.Tiles.Add(tile);reel.Faces.Add(tile.GetComponent<Image>());reel.Colors.Add(colors[i]);
            }
        }
        // Each tile sits TileOffset tiles off the payline, and darkens towards the window's edges as the reel curves away.
        static void Show(Reel reel,float scroll)
        {
            for(int i=0;i<reel.Tiles.Count;i++)
            {
                float offset=TileOffset(i,reel.Tiles.Count,scroll);
                reel.Tiles[i].anchoredPosition=new Vector2(0,offset*TileHeight);
                reel.Faces[i].color=Color.Lerp(reel.Colors[i],Ink,Mathf.Clamp01(Mathf.Abs(offset)*.4f));
            }
        }
        static RectTransform Block(Transform parent,string name,Color color,Vector2 at,Vector2 size)
        {
            var rect=(RectTransform)Panel(parent,name,color,new Vector2(.5f,.5f),new Vector2(.5f,.5f)).transform;rect.sizeDelta=size;rect.anchoredPosition=at;return rect;
        }
        static GameObject Panel(Transform parent,string name,Color color,Vector2 min,Vector2 max)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return go;
        }
        Text Label(Transform parent,string name,int size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));go.transform.SetParent(parent,false);
            var text=go.GetComponent<Text>();text.font=font;text.fontSize=size;text.alignment=TextAnchor.MiddleCenter;text.color=Paper;
            text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;text.raycastTarget=false;return text;
        }
        static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        void OnDestroy()
        {
            StopAudio();DisposeAudio(slotAudio);DisposeAudio(leverAudio);DisposeAudio(tickAudio);DisposeAudio(clunkAudio);
            if(shotTexture!=null)Destroy(shotTexture);
        }
    }
}
