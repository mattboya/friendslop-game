using System;
using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Festival.Presentation
{
    /// <summary>TRIP-5: what the local player's dose looks and feels like, by the substance the third wheel picked, on their own screen
    /// only and stronger with every dose. Substances also twist rhythm travel and, for Tongue Stamps, Fun Guys and Couch Lock, the
    /// host's walking; visions and other rules depend on the dose alone. Look and For are pure, and their values
    /// reach the screen through the single writers: FestivalHud.ApplyEffects adds Fov to the view's field of view,
    /// FestivalSession.UpdateCamera tilts the view by Roll and trails the mouse by Lag (Follow), and FestivalNightLighting adds
    /// Saturation and Brightness to the colour grade. This component draws the rest: its own overlay canvas under the HUD (tint,
    /// haze, vignette, and sleepy eyes that now and then blink, black above and below), pooled colour trails and glows around other
    /// people, TRIP-11's pattern on the scenery (FestivalTripSurfaces) in the view's camera alone, and a low-pass on the view's listener.
    /// GAS-1: a Giggle Balloon's gas at camp (Gas) goes the same way, and adds a tremolo and (GAS-5) a reverb: FestivalTrip is the
    /// one writer of AudioListener.volume and of the listener's filters.</summary>
    public sealed class FestivalTrip : MonoBehaviour
    {
        /// <summary>One moment of a trip. Zero everywhere is a sober view.</summary>
        public struct Perception
        {
            // Over the whole view, alpha included, and a milky veil's alpha.
            public Color Tint;
            public float Haze;
            // How dark the edges close in (0-1).
            public float Vignette;
            // Degrees added to the field of view and to the view's roll right now, and the seconds the camera trails the mouse by.
            public float Fov,Roll,Lag;
            // Added to the colour grade's saturation and post exposure.
            public float Saturation,Brightness;
            // How strongly people leave colour trails and glow (0-1), and how muffled the sound is (0-1).
            public float Trails,Glow,Muffle;
            // How far the volume of everything heard dips right now (0-1): GAS-1's tremolo. How much everything heard echoes (0-1): GAS-5.
            public float Tremolo,Reverb;
            // TRIP-11: the pattern on the scenery's surfaces. How strongly Tongue Stamps' crisp lattice and Fun Guys' curved cells show
            // (0-1, colour), and the pattern's motion right now: how far it drifts along the surfaces (metres) and how far they swell
            // (a signed share of the pattern's size).
            public float Lattice,Cells,Drift,Breathe;
            // TRIP-16: how far sleepy eyes' lids rest closed (0-1, EyeRest): the view is an eye's opening, and they blink now and then.
            public float Eye;
        }

        /// <summary>Reduced motion keeps every colour, the surface patterns included, but cuts camera motion, the swell, the roll and the
        /// lag, and the patterns' drift and breathing (TRIP-11), to this share.</summary>
        public const float ReducedMotionShare=.25f;
        /// <summary>LIGHT-1 took a level-long murk off the neon night, so at night a substance's tint and haze stay this faint.</summary>
        public const float NightTint=.03f;
        public const int MaxTrails=48,MaxGlows=12;

        /// <summary>A dose of substance (a catalog effect id) at time t. An id off the wheel (or none) looks plain. stall is how deep
        /// the walk is in Pony Dust's stall (Intoxication.Stall, 0-1), which wobbles a Pony Dust view.</summary>
        public static Perception Look(string substance,int dose,bool reducedMotion,float time,float stall=0)
        {
            var look=new Perception();float s=(float)EffectPresentation.Strength(dose),motion=reducedMotion?ReducedMotionShare:1;
            if(s<=0)return look;
            switch(substance)
            {
                case "lsd": // Tongue Stamps: a slowly cycling rainbow tint, colour trails behind moving people, a wobble and richer colour.
                    var hue=Color.HSVToRGB(Mathf.Repeat(time*.05f,1),.75f,1);look.Tint=new Color(hue.r,hue.g,hue.b,.07f*s);look.Trails=s;
                    look.Roll=Sway(substance,time)*s*motion;look.Saturation=DoseSaturation*s;
                    // TRIP-11: a crisp lattice on the scenery, subtle at one dose, that slowly drifts to and fro from three.
                    look.Lattice=s;look.Drift=Mathf.Sin(time*.23f)*PatternDrift*From(s,.6f)*motion;break;
                case "mushrooms": // Fun Guys: the world breathes (the field of view swells) and sways under a faint violet, in richer colour.
                    look.Fov=Mathf.Sin(time*.8f)*4*s*motion;look.Tint=new Color(.6f,.45f,1,.035f*s);
                    look.Roll=Sway(substance,time)*s*motion;look.Saturation=DoseSaturation*s;
                    // TRIP-11: the scenery breathes, slow and irregular (two waves out of step), and grows curved cells from two doses.
                    look.Cells=From(s,.4f);look.Breathe=(Mathf.Sin(time*.37f)+.6f*Mathf.Sin(time*.61f+2))/1.6f*PatternBreathe*s*motion;break;
                case "ecstasy": // Rolly Pollies: warm, bright, saturated colours, and a soft glow around other people.
                    look.Tint=new Color(1,.6f,.35f,.05f*s);look.Saturation=30*s;look.Brightness=.15f*s;look.Glow=s;break;
                case "ketamine": // Pony Dust: tunnel vision through sleepy eyes, a camera that lags behind the mouse, and muffled sound.
                    look.Tint=new Color(.55f,.65f,.8f,.03f*s);look.Vignette=.75f*s;look.Eye=EyeRest(dose);look.Lag=.25f*s*motion;look.Muffle=s;
                    // TRIP-16: a slow sway, by the dose. TRIP-17: while the walk stalls, a faster wobble, alike at every dose.
                    look.Roll=(PonySway(time)*s+StallWobble(time)*stall)*motion;break;
                case "weed": // Couch Lock: a soft haze with a green cast, through sleepy eyes.
                    look.Tint=new Color(.35f,.8f,.35f,.05f*s);look.Haze=.12f*s;look.Eye=EyeRest(dose);break;
            }
            return look;
        }

        // Tongue Stamps' and Fun Guys' grade saturation at full strength: below Rolly Pollies' 30, with no brightness of their own.
        private const float DoseSaturation=20;
        // TRIP-11 at full strength: how far Tongue Stamps' lattice drifts each way (metres), and how far Fun Guys' surfaces swell (a share
        // of the pattern's size). Starting points for the captures.
        private const float PatternDrift=.6f,PatternBreathe=.08f;
        // A strength's share above from (0 at or below it, 1 at full strength), so a part of a look starts at a later dose.
        private static float From(float strength,float from)=>Mathf.Clamp01((strength-from)/(1-from));

        /// <summary>The view's roll in degrees at time t that a shop-bought Tongue Stamp (a wobble) or Fun Guy (a sway) gives at full
        /// strength; a dose of either rolls by this times its strength. Anything else rolls nothing.</summary>
        public static float Sway(string id,float time)=>id=="lsd"?Mathf.Sin(time*2.1f)*2.2f:id=="mushrooms"?Mathf.Sin(time*1.3f+1.1f)*1.5f:0;

        /// <summary>TRIP-16: Pony Dust's roll in degrees at time t at full strength: two slow waves out of step, so it sways unevenly,
        /// never past 1.5 degrees. A starting point for the captures.</summary>
        public static float PonySway(float time)=>.75f*(Mathf.Sin(time*.7f)+Mathf.Sin(time*1.1f+1.3f));
        // TRIP-17: the roll in degrees at the floor of Pony Dust's stall, fast enough to wobble through its second. A starting point.
        private static float StallWobble(float time)=>Mathf.Sin(time*4.5f)*1f;

        // At full strength: the vignette's floor and its pulse on top, the field-of-view kick in degrees, the colour drained from the
        // grade, the haze, the low-pass's floor (it pulses up to 1) and how far the tremolo dips the volume. GAS-5 set the vignette,
        // its pulse and the kick (from .3, .3 and a 1.2 degree throb): starting points for PLAYTEST-1.
        private const float GasVignette=.45f,GasVignettePulse=.4f,GasThrob=4,GasDrain=55,GasHaze=.1f,GasMuffle=.4f,GasTremolo=.4f;
        // GAS-4: two hits on at once (strength 2) kick, drain and dip this far; 95 takes camp's -5 grade to URP's full grey.
        private const float GasStrongThrob=8,GasStrongDrain=95,GasStrongTremolo=.7f;
        // GAS-5's reverb at full strength: its wet level in millibels (6 dB under the dry sound, so the beat stays clear) and a big
        // hall's decay in seconds.
        private const float GasReverbRoom=-600,GasReverbDecay=2.5f;

        /// <summary>GAS-1: Giggle Gas at strength 0-2 (FestivalSimulation.GiggleGasStrength of the player, on the simulation clock).
        /// GAS-5: a wah-wah pulse on every beat (beat: the phase, 0 on the kick, rising to 1 at the next), sharp on the kick and
        /// easing off to nothing by the next, runs through the sound (a low-pass closing in, and a tremolo) and the view (a deep
        /// vignette closing in, and a strong field-of-view kick); colours drain, a big reverb and a pale haze put the world at a
        /// distance. Reduced motion drops the kick and halves the vignette's pulse; the sound stays. GAS-4: a second hit (strength
        /// 1-2) ramps the kick, the drain and the tremolo on to their strong values; the rest stays a hit's.</summary>
        public static Perception Gas(float strength,bool reducedMotion,float beat)
        {
            var look=new Perception();if(strength<=0)return look;
            float pulse=Mathf.Exp(-4*beat)*(1-beat),hit=Mathf.Min(strength,1);
            look.Vignette=hit*(GasVignette+GasVignettePulse*(reducedMotion?.5f:1)*pulse);
            look.Fov=reducedMotion?0:Stacked(strength,GasThrob,GasStrongThrob)*pulse;
            look.Saturation=-Stacked(strength,GasDrain,GasStrongDrain);look.Haze=GasHaze*hit;
            look.Muffle=hit*(GasMuffle+(1-GasMuffle)*pulse);look.Tremolo=Stacked(strength,GasTremolo,GasStrongTremolo)*pulse;look.Reverb=hit;
            return look;
        }
        // A hit's value times strength up to 1, then on to strong at 2.
        private static float Stacked(float strength,float hit,float strong)=>strength<=1?hit*strength:Mathf.Lerp(hit,strong,strength-1);

        /// <summary>The local player's trip in state, never for a spirit. A dose shows only while the festival is shown (Playing or
        /// Results), only for their own dose of 1 or more (cross-cutting call 3), and at night with its tint and haze held to NightTint.
        /// Giggle Gas shows by the summed strength of the player's hits (GAS-4: up to two); it lives only at camp (the host clears it
        /// as the crew leaves), so never with a dose. It pulses on beat, the camp music's phase (FestivalCampAudio.Beat); with no music
        /// (null) it keeps a 120 BPM clock, so it never stops. Pony Dust's stall runs on simulationSeconds, the state's own clock if
        /// none is given.</summary>
        public static Perception For(RoundState state,string localPlayerId,bool reducedMotion,float time,float? beat=null,double? simulationSeconds=null)
        {
            if(state==null)return default;
            foreach(var p in state.Players)
            {
                if(p.Id!=localPlayerId)continue;
                if(p.Life=="Spirit")return default;
                foreach(var e in p.Effects)
                {
                    if(e.Id==FestivalSimulation.GiggleGasEffect)return Gas((float)FestivalSimulation.GiggleGasStrength(p,state.SimulationSeconds),reducedMotion,beat??Mathf.Repeat(time*2,1));
                    if(e.Id!=FestivalSimulation.DoseEffect||e.Intensity<1||!FestivalWorld.ShowsFestival(state.Phase))continue;
                    var look=Look(e.Substance,e.Intensity,reducedMotion,time,Intoxication.Stall(p,simulationSeconds??state.SimulationSeconds));
                    if(FestivalNightLighting.IsNight(state)){look.Tint.a=Mathf.Min(look.Tint.a,NightTint);look.Haze=Mathf.Min(look.Haze,NightTint);}
                    return look;
                }
                return default;
            }
            return default;
        }

        // TRIP-16: Pony Dust and Couch Lock look out through sleepy eyes. EyeSoft is the lids' soft edge, as a share of the opening.
        private const float EyeSoft=.35f,BlinkClose=.4f,BlinkShut=.1f;
        /// <summary>A blink, close to reopen: .4 s easing shut, .1 s shut and .5 s easing open.</summary>
        public const float BlinkSeconds=1;

        /// <summary>TRIP-16: how dark the view is at (x, y), each -1 to 1 from the screen's centre to its edges, looking out through
        /// lids open to aperture (1 wide, 0 shut): each lid curves from a tapered corner at the screen's side to aperture's height at
        /// its middle, with a soft edge. Past the lids (|y| over aperture) it is shut, so plain bars can fill above and below.</summary>
        public static float EyeShade(float x,float y,float aperture)
        {
            if(aperture<=0||Mathf.Abs(y)>=aperture)return 1;
            return Mathf.SmoothStep(0,1,(Mathf.Abs(y)/aperture-(1-x*x)+EyeSoft)/EyeSoft);
        }

        /// <summary>TRIP-16: how far a dose's eyes rest closed (0 open), more with each dose; 1 minus it is the resting aperture.</summary>
        public static float EyeRest(int dose){float s=(float)EffectPresentation.Strength(dose);return s<=0?0:.1f+.3f*s;}

        /// <summary>TRIP-16: how shut a blink is t seconds in (1 shut), easing at both ends of each move; 0 outside it.</summary>
        public static float Blink(float t)
        {
            if(t<=0||t>=BlinkSeconds)return 0;
            if(t<BlinkClose)return Mathf.SmoothStep(0,1,t/BlinkClose);
            if(t<=BlinkClose+BlinkShut)return 1;
            return Mathf.SmoothStep(1,0,(t-BlinkClose-BlinkShut)/(BlinkSeconds-BlinkClose-BlinkShut));
        }

        /// <summary>TRIP-16: the lids' aperture resting rest closed and blink shut: reduced motion moves them a quarter of the way.</summary>
        public static float Aperture(float rest,float blink,bool reducedMotion)=>(1-rest)*(1-blink*(reducedMotion?ReducedMotionShare:1));

        /// <summary>TRIP-16: when this player's eyes blink, on their own screen alone: a wait drawn once from next (8-18 s) after each
        /// reopening, then a blink. Held (a menu, a talk, a rhythm, a security stop), it starts none, eases a blink still closing back
        /// open from where it is, and draws a fresh wait when let go. Long frames step through in order, so a blink never sticks.</summary>
        public sealed class Blinker
        {
            public const float MinWait=8,MaxWait=18;
            private readonly Func<float> next;
            private float wait,into=-1;
            private bool held;
            public Blinker(Func<float> next){this.next=next;wait=next();}
            /// <summary>A Blinker of its own, never Core's ContentRandom, so friends never blink together.</summary>
            public static Blinker Seeded(int seed){var random=new System.Random(seed);return new Blinker(()=>MinWait+(MaxWait-MinWait)*(float)random.NextDouble());}
            /// <summary>How shut the eyes are deltaTime seconds on (Blink), held or not.</summary>
            public float Advance(float deltaTime,bool hold)
            {
                // Held while closing: jump to the reopening's moment that is as shut (smoothstep's inverse), so nothing pops.
                if(hold&&into>=0&&into<=BlinkClose+BlinkShut)into=BlinkClose+BlinkShut+(.5f-Mathf.Sin(Mathf.Asin(2*Blink(into)-1)/3))*(BlinkSeconds-BlinkClose-BlinkShut);
                if(hold)held=true;
                else if(held){held=false;if(into<0)wait=next();}
                while(deltaTime>0)
                {
                    if(into>=0)
                    {
                        float step=Mathf.Min(deltaTime,BlinkSeconds-into);into+=step;deltaTime-=step;
                        if(into>=BlinkSeconds){into=-1;if(!held)wait=next();}
                    }
                    else if(held)break;
                    else if(wait>deltaTime){wait-=deltaTime;break;}
                    else{deltaTime-=wait;wait=0;into=0;}
                }
                return into<0?0:Blink(into);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            /// <summary>Seconds until the next blink begins; 0 through a blink.</summary>
            public float Wait=>into<0?wait:0;
#endif
        }

        /// <summary>The listener's low-pass cutoff for a muffle of 0-1: mild, so the rhythm's beat stays audible.</summary>
        public static float LowPassHz(float muffle)=>muffle<=0?22000:Mathf.Lerp(5000,2200,muffle);

        /// <summary>GAS-5: the listener reverb's wet level (its room, in millibels) for a reverb of 0-1: off at 0, as loud as the reverb
        /// is strong, and a capped big hall at 1.</summary>
        public static float ReverbRoom(float reverb)=>reverb<=0?-10000:Mathf.Max(-10000,2000*Mathf.Log10(reverb)+GasReverbRoom);

        /// <summary>One frame of a camera angle trailing its target by lag seconds, the short way round; no lag snaps to it.</summary>
        public static float Follow(float current,float target,float lag,float deltaTime)=>lag<=0?target:current+Mathf.DeltaAngle(current,target)*(1-Mathf.Exp(-deltaTime/lag));

        /// <summary>The effect whose look the rhythm arrows take (EffectPresentation.Path, Catalog's LeadSeconds): the dose's substance
        /// first, else the newest catalog effect taken (HUD-4). An unnamed legacy dose and the debrief's shot have no catalog look.</summary>
        public static string LaneEffect(PlayerState player)
        {
            var effect=SelectedLaneEffect(player);
            return effect==null?"":effect.Id==FestivalSimulation.DoseEffect?effect.Substance:effect.Id;
        }

        /// <summary>The selected dose's intensity, otherwise zero: shop and unnamed legacy effects keep their original flight.</summary>
        public static int LaneDose(PlayerState player)
        {
            var effect=SelectedLaneEffect(player);
            return effect!=null&&effect.Id==FestivalSimulation.DoseEffect?Math.Max(0,effect.Intensity):0;
        }

        private static ActiveEffect SelectedLaneEffect(PlayerState player)
        {
            foreach(var e in player.Effects)if(e.Id==FestivalSimulation.DoseEffect&&!string.IsNullOrEmpty(e.Substance))return e;
            for(int i=player.Effects.Count-1;i>=0;i--)if(Catalog.FindEffect(player.Effects[i].Id)!=null)return player.Effects[i];
            return null;
        }

        // Under the HUD (100) and the spinner (110), so the HUD stays readable over the bars and the spin covers it all.
        private const int SortingOrder=90;
        // A trail ghost drops every TrailGap seconds from each person within TrailRange metres who moved since their last one, and fades
        // over TrailLife. Glows go round the first MaxGlows people in front of the view within GlowRange, GlowBehind metres beyond them.
        private const float TrailGap=.1f,TrailLife=.7f,TrailAlpha=.45f,TrailRange=25,MinStride=.05f,GlowRange=20,GlowBehind=.35f,GlowAlpha=.35f;
        private static readonly Color GlowColor=new Color(1,.55f,.75f);
        private static readonly Color HazeColor=new Color(.9f,.97f,.88f);

        /// <summary>TRIP-16: the seconds each wait between blinks lasts, drawn once per reopening; null draws them from a Blinker seeded
        /// for this screen alone, so friends never blink together.</summary>
        public Func<float> BlinkWaits;
        private FestivalSession session;
        private FestivalCampAudio campAudio;
        private Func<string,Transform> actorFor;
        private GameObject overlay;
        private Image tint,haze,top,bottom;
        private RawImage vignette,eye;
        private Texture2D vignetteTexture,blobTexture,eyeTexture;
        // TRIP-16: this screen's blinks, for the substance they began on (a new one starts afresh), and the HUD's chat card, if any.
        private Blinker blinker;
        private string blinkingOn="";
        private FestivalChatWindow chat;
        private Sprite blob;
        private Material sprites;
        private AudioLowPassFilter lowPass;
        private AudioReverbFilter reverb;
        private FestivalTripSurfaces surfaces;
        private readonly SpriteRenderer[] trails=new SpriteRenderer[MaxTrails];
        private readonly float[] trailAge=new float[MaxTrails];
        private readonly SpriteRenderer[] glows=new SpriteRenderer[MaxGlows];
        private readonly Dictionary<string,Vector3> lastDrop=new Dictionary<string,Vector3>();
        private int nextTrail;
        private float sinceDrop;
        private ProfileData audio;
        private bool focused=true;
        private Perception listening;

        // After FestivalSession.Update has moved the first-person camera this frame.
        private void LateUpdate()
        {
            if(session==null&&!TryGetComponent(out session))return;
            if(campAudio==null)TryGetComponent(out campAudio);
            if(actorFor==null)actorFor=id=>{var body=session.WorldCharacter(id);return body!=null?body.transform:null;};
            Apply(session.State,session.LocalPlayerId,session.ViewCamera!=null?session.ViewCamera.transform:null,actorFor,session.Profile.Data.ReducedMotion,Time.unscaledTime,Time.unscaledDeltaTime,campAudio!=null?campAudio.Beat:null);
        }
        // Turned off, only trip processing goes; the player's master and focus choice still apply.
        private void OnDisable(){if(overlay!=null)Show(default,null,null,null,null,false,0);else Listen(null,default);}
        private void OnApplicationFocus(bool hasFocus)
        {
            focused=hasFocus;Listen(null,listening);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[Settings] Audio focus="+hasFocus+" listener="+AudioListener.volume);
#endif
        }

        /// <summary>Draws localPlayerId's trip in state over view; actorFor finds a person's drawn body (null when not drawn). The gas
        /// pulses on beat (For).</summary>
        public void Apply(RoundState state,string localPlayerId,Transform view,Func<string,Transform> actorFor,bool reducedMotion,float time,float deltaTime,float? beat=null)
        {
            if(overlay==null)Build();
            Show(For(state,localPlayerId,reducedMotion,time,beat),state,localPlayerId,view,actorFor,reducedMotion,deltaTime);
        }

        private void Show(Perception look,RoundState state,string localPlayerId,Transform view,Func<string,Transform> actorFor,bool reducedMotion,float deltaTime)
        {
            bool shown=look.Tint.a>0||look.Haze>0||look.Vignette>0||look.Eye>0;
            if(overlay.activeSelf!=shown)overlay.SetActive(shown);
            tint.color=look.Tint;haze.color=new Color(HazeColor.r,HazeColor.g,HazeColor.b,look.Haze);vignette.color=new Color(0,0,0,look.Vignette);
            Lids(look,state,localPlayerId,reducedMotion,deltaTime);
            Listen(view,look);
            Trail(state,localPlayerId,view,actorFor,look.Trails,deltaTime);
            Glow(state,localPlayerId,view,actorFor,look.Glow);
            surfaces.Show(view!=null&&view.TryGetComponent(out Camera viewCamera)?viewCamera:null,look);
        }

        // TRIP-16: the eye's opening across the middle of the screen, as tall as the lids are open (Aperture), with the black bars flush
        // above and below it. With no eye, the blink goes, so a shut eye never outlives its dose, and the bars fold away.
        private void Lids(Perception look,RoundState state,string localPlayerId,bool reducedMotion,float deltaTime)
        {
            float rest=look.Eye,closure=0,aperture=1;bool drawn=rest>0,held=false;
            if(eye.enabled!=drawn)eye.enabled=drawn;
            if(!drawn)
            {
                blinker=null;blinkingOn="";
                top.rectTransform.anchorMin=new Vector2(0,1);bottom.rectTransform.anchorMax=new Vector2(1,0);
            }
            else
            {
                var player=Local(state,localPlayerId);string substance=LaneEffect(player);
                if(blinker==null||substance!=blinkingOn)
                {
                    blinker=BlinkWaits!=null?new Blinker(BlinkWaits):Blinker.Seeded(Environment.TickCount^localPlayerId.GetHashCode());blinkingOn=substance;
                    if(chat==null)chat=GetComponentInChildren<FestivalChatWindow>(true);
                }
                held=Held(state,player);closure=blinker.Advance(deltaTime,held);aperture=Aperture(rest,closure,reducedMotion);
                float half=aperture/2;
                eye.rectTransform.anchorMin=new Vector2(0,.5f-half);eye.rectTransform.anchorMax=new Vector2(1,.5f+half);
                top.rectTransform.anchorMin=new Vector2(0,.5f+half);bottom.rectTransform.anchorMax=new Vector2(1,.5f-half);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(DevelopmentDiagnostics.Visible)TraceEye(rest,reducedMotion,closure,held,aperture);
#endif
        }
        private static PlayerState Local(RoundState state,string id){foreach(var p in state.Players)if(p.Id==id)return p;return null;}
        // TRIP-16: no blink starts, and one closing eases open, while the player is busy with something timed or protected: anything but
        // open play (a minigame, the results), the menu, a rhythm or a talk, a chat card still up, or security stopping them.
        private bool Held(RoundState state,PlayerState player)
        {
            if(state.Phase!="Playing"||session!=null&&session.PointerFree||chat!=null&&chat.Open)return true;
            foreach(var i in state.Interactions)if(i.Id==player.InteractionId&&i.Status=="Active")return true;
            foreach(var n in state.Npcs)if(n.Kind=="Cop"&&n.TargetId==player.Id&&(n.Mode=="Stop"||n.Mode=="ArrestWarning"))return true;
            return false;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private (bool,string,float,bool,bool,bool) tracedEye=(false,"",0,false,false,false);
        // Opt-in: the eye this screen draws, each time it shows or goes, its substance or resting opening changes, a blink starts or
        // ends, a protected moment holds it open or lets go, or reduced motion turns on or off.
        private void TraceEye(float rest,bool reducedMotion,float closure,bool held,float aperture)
        {
            var now=(rest>0,blinkingOn,rest,closure>0,held,rest>0&&reducedMotion);
            if(now==tracedEye)return;
            tracedEye=now;
            DevelopmentDiagnostics.GraphicsEvent("Trip","eye","shown="+now.Item1+" substance="+now.Item2+" rest="+rest.ToString("F2")+" reduced="+reducedMotion
                +" blinking="+now.Item4+" held="+held+" aperture="+aperture.ToString("F2")+" next_blink="+(blinker!=null?blinker.Wait:0).ToString("F1"));
        }
#endif

        private void Listen(Transform view,Perception look)
        {
            if(session==null)TryGetComponent(out session);
            if(session?.Profile!=null)audio=session.Profile.Data;
            var settings=audio;
            if(session!=null)FestivalSettings.ConfigureVoice(session.Voice,settings);
            listening=look;
            // ponytail: a frame-rate tremolo on the global listener volume; an OnAudioFilterRead tremolo if it ever sounds stepped.
            float master=settings==null?1:settings.MasterVolume;
            float volume=(!focused&&settings!=null&&settings.MuteWhenUnfocused?0:master)*(1-look.Tremolo);
            if(AudioListener.volume!=volume)AudioListener.volume=volume;
            // The filters need the view's AudioListener beside them; a view without one (an EditMode test) is left alone.
            if(lowPass==null&&look.Muffle>0&&Hears(view))lowPass=view.gameObject.AddComponent<AudioLowPassFilter>();
            if(lowPass!=null){lowPass.enabled=look.Muffle>0;lowPass.cutoffFrequency=LowPassHz(look.Muffle);}
            if(reverb==null&&look.Reverb>0&&Hears(view)){reverb=view.gameObject.AddComponent<AudioReverbFilter>();reverb.reverbPreset=AudioReverbPreset.User;reverb.decayTime=GasReverbDecay;}
            if(reverb!=null){reverb.enabled=look.Reverb>0;reverb.room=ReverbRoom(look.Reverb);}
        }
        private static bool Hears(Transform view)=>view!=null&&view.GetComponent<AudioListener>()!=null;

        private void Trail(RoundState state,string localPlayerId,Transform view,Func<string,Transform> actorFor,float strength,float deltaTime)
        {
            sinceDrop+=deltaTime;
            if(strength>0&&view!=null&&actorFor!=null&&sinceDrop>=TrailGap)
            {
                sinceDrop=0;
                foreach(var p in state.Players)if(p.Id!=localPlayerId)Drop(p.Id,view,actorFor);
                foreach(var n in state.Npcs)Drop(n.Id,view,actorFor);
            }
            if(strength<=0)lastDrop.Clear();
            for(int i=0;i<MaxTrails;i++)
            {
                var ghost=trails[i];if(ghost==null||!ghost.enabled)continue;
                trailAge[i]+=deltaTime;float left=1-trailAge[i]/TrailLife;
                if(left<=0||strength<=0){ghost.enabled=false;continue;}
                var color=ghost.color;color.a=TrailAlpha*strength*left;ghost.color=color;
                if(view!=null)ghost.transform.rotation=view.rotation;
            }
        }
        // A ghost where id stands now, if they moved since their last one; the oldest ghost makes way when all are out.
        private void Drop(string id,Transform view,Func<string,Transform> actorFor)
        {
            var body=actorFor(id);if(body==null||!body.gameObject.activeInHierarchy)return;
            var at=body.position;
            if((at-view.position).sqrMagnitude>TrailRange*TrailRange)return;
            bool seen=lastDrop.TryGetValue(id,out var last);lastDrop[id]=at;
            if(!seen||(at-last).sqrMagnitude<MinStride*MinStride)return;
            if(trails[nextTrail]==null)trails[nextTrail]=Billboard("Trip trail",.7f,1.8f);
            var ghost=trails[nextTrail];trailAge[nextTrail]=0;nextTrail=(nextTrail+1)%MaxTrails;
            var hue=Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime*.4f+(id.GetHashCode()&255)/255f,1),.8f,1);
            ghost.color=new Color(hue.r,hue.g,hue.b,0);ghost.transform.SetPositionAndRotation(at+Vector3.up*.9f,view.rotation);ghost.enabled=true;
        }

        private void Glow(RoundState state,string localPlayerId,Transform view,Func<string,Transform> actorFor,float strength)
        {
            int used=0;
            if(strength>0&&view!=null&&actorFor!=null)
            {
                foreach(var p in state.Players)if(used<MaxGlows&&p.Id!=localPlayerId&&Halo(p.Id,used,view,actorFor,strength))used++;
                foreach(var n in state.Npcs)if(used<MaxGlows&&Halo(n.Id,used,view,actorFor,strength))used++;
            }
            for(int i=used;i<MaxGlows;i++)if(glows[i]!=null&&glows[i].enabled)glows[i].enabled=false;
        }
        // A soft glow just beyond id from the view, so their body covers its middle and it shows round them.
        private bool Halo(string id,int slot,Transform view,Func<string,Transform> actorFor,float strength)
        {
            var body=actorFor(id);if(body==null||!body.gameObject.activeInHierarchy)return false;
            var at=body.position+Vector3.up;var away=at-view.position;away.y=0;
            if(away.sqrMagnitude>GlowRange*GlowRange||Vector3.Dot(away,view.forward)<=0)return false;
            if(glows[slot]==null)glows[slot]=Billboard("Trip glow",1.6f,2.3f);
            var glow=glows[slot];
            glow.transform.SetPositionAndRotation(at+away.normalized*GlowBehind,view.rotation);
            glow.color=new Color(GlowColor.r,GlowColor.g,GlowColor.b,GlowAlpha*strength);glow.enabled=true;return true;
        }

        // ponytail: Sprites/Default quads with a soft blob, unlit and fog-free; a real afterimage shader would need a new asset.
        private SpriteRenderer Billboard(string name,float width,float height)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.localScale=new Vector3(width,height,1);
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=blob;renderer.sharedMaterial=sprites;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.enabled=false;return renderer;
        }

        private void Build()
        {
            overlay=new GameObject("Festival trip");overlay.transform.SetParent(transform,false);
            var canvas=overlay.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=SortingOrder;
            vignetteTexture=Radial(64,r=>Mathf.SmoothStep(0,1,(r-.45f)/.55f),"Festival trip vignette");
            vignette=Layer<RawImage>("Trip vignette",Vector2.zero,Vector2.one);vignette.texture=vignetteTexture;
            haze=Layer<Image>("Trip haze",Vector2.zero,Vector2.one);
            tint=Layer<Image>("Trip tint",Vector2.zero,Vector2.one);
            // TRIP-16: one almond at its widest, scaled to each frame's opening, which draws exactly EyeShade's lids at that aperture.
            // Over the tint and haze, so the lids are the same black as the bars they meet.
            eyeTexture=Mask(128,64,(x,y)=>EyeShade(x,y,1),"Festival trip eye");
            eye=Layer<RawImage>("Trip eye",new Vector2(0,.5f),new Vector2(1,.5f));eye.texture=eyeTexture;eye.enabled=false;
            top=Layer<Image>("Trip letterbox top",new Vector2(0,1),Vector2.one);
            bottom=Layer<Image>("Trip letterbox bottom",Vector2.zero,new Vector2(1,0));
            top.color=bottom.color=eye.color=Color.black;
            overlay.SetActive(false);
            blobTexture=Radial(32,r=>{float a=Mathf.Clamp01(1-r);return a*a;},"Festival trip blob");
            blob=Sprite.Create(blobTexture,new Rect(0,0,32,32),new Vector2(.5f,.5f),32);blob.name="Festival trip blob";
            sprites=new Material(Shader.Find("Sprites/Default")){name="Festival trip sprites"};
            surfaces=new FestivalTripSurfaces();
        }
        private T Layer<T>(string name,Vector2 min,Vector2 max) where T:Graphic
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(T));go.transform.SetParent(overlay.transform,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var graphic=go.GetComponent<T>();graphic.raycastTarget=false;graphic.color=Color.clear;return graphic;
        }
        // A white texture whose alpha is alpha(r), r running 0 at the centre to 1 at the middle of each edge.
        private static Texture2D Radial(int size,Func<float,float> alpha,string name)=>Mask(size,size,(x,y)=>alpha(Mathf.Sqrt(x*x+y*y)),name);
        // A white texture whose alpha is alpha(x, y), each running -1 to 1 from edge to edge through the centre.
        private static Texture2D Mask(int width,int height,Func<float,float,float> alpha,string name)
        {
            var pixels=new Color[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)pixels[y*width+x]=new Color(1,1,1,alpha((x+.5f)*2/width-1,(y+.5f)*2/height-1));
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name=name,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            texture.SetPixels(pixels);texture.Apply();return texture;
        }

        private void OnDestroy()
        {
            // The listener's volume is global and outlives this component and its scene.
            Listen(null,default);
            if(lowPass!=null)Dispose(lowPass);
            if(reverb!=null)Dispose(reverb);
            surfaces?.Dispose();
            foreach(var thing in new Object[]{blob,blobTexture,vignetteTexture,eyeTexture,sprites})if(thing!=null)Dispose(thing);
        }
        private static void Dispose(Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
