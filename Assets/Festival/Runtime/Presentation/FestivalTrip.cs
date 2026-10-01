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
    /// only and stronger with every dose. Substances change no rule; the dose alone does. Look and For are pure, and their values
    /// reach the screen through the single writers: FestivalHud.ApplyEffects adds Fov to the view's field of view,
    /// FestivalSession.UpdateCamera trails the mouse by Lag (Follow), and FestivalNightLighting adds Saturation and Brightness to the
    /// colour grade. This component draws the rest: its own overlay canvas under the HUD (tint, haze, vignette, letterbox), pooled
    /// colour trails and glows around other people, and a low-pass on the view's listener. GAS-1: a Giggle Balloon's gas at camp (Gas)
    /// goes the same way, and adds a tremolo: FestivalTrip is the one writer of AudioListener.volume.</summary>
    public sealed class FestivalTrip : MonoBehaviour
    {
        /// <summary>One moment of a trip. Zero everywhere is a sober view.</summary>
        public struct Perception
        {
            // Over the whole view, alpha included, and a milky veil's alpha.
            public Color Tint;
            public float Haze;
            // How dark the edges close in (0-1), and each letterbox bar's share of the screen's height.
            public float Vignette,Letterbox;
            // Degrees added to the field of view right now, and the seconds the camera trails the mouse by.
            public float Fov,Lag;
            // Added to the colour grade's saturation and post exposure.
            public float Saturation,Brightness;
            // How strongly people leave colour trails and glow (0-1), and how muffled the sound is (0-1).
            public float Trails,Glow,Muffle;
            // How far the volume of everything heard dips right now (0-1): GAS-1's tremolo.
            public float Tremolo;
        }

        /// <summary>Reduced motion (on by default) keeps every colour but cuts camera motion, the swell and the lag, to this share.</summary>
        public const float ReducedMotionShare=.25f;
        /// <summary>LIGHT-1 took a level-long murk off the neon night, so at night a substance's tint and haze stay this faint.</summary>
        public const float NightTint=.03f;
        public const int MaxTrails=48,MaxGlows=12;
        // Doses 1-4 at 40/60/80/100%.
        private static float Strength(int dose)=>dose<1?0:.4f+.2f*(Mathf.Min(dose,4)-1);

        /// <summary>A dose of substance (a catalog effect id) at time t. An id off the wheel (or none) looks plain.</summary>
        public static Perception Look(string substance,int dose,bool reducedMotion,float time)
        {
            var look=new Perception();float s=Strength(dose),motion=reducedMotion?ReducedMotionShare:1;
            if(s<=0)return look;
            switch(substance)
            {
                case "lsd": // Tongue Stamps: a slowly cycling rainbow tint, and colour trails behind moving people.
                    var hue=Color.HSVToRGB(Mathf.Repeat(time*.05f,1),.75f,1);look.Tint=new Color(hue.r,hue.g,hue.b,.07f*s);look.Trails=s;break;
                case "mushrooms": // Fun Guys: the world breathes, a slow swell of the field of view under a faint violet.
                    look.Fov=Mathf.Sin(time*.8f)*4*s*motion;look.Tint=new Color(.6f,.45f,1,.035f*s);break;
                case "ecstasy": // Rolly Pollies: warm, bright, saturated colours, and a soft glow around other people.
                    look.Tint=new Color(1,.6f,.35f,.05f*s);look.Saturation=30*s;look.Brightness=.15f*s;look.Glow=s;break;
                case "ketamine": // Pony Dust: tunnel vision, a camera that lags behind the mouse, and muffled sound.
                    look.Tint=new Color(.55f,.65f,.8f,.03f*s);look.Vignette=.75f*s;look.Lag=.25f*s*motion;look.Muffle=s;break;
                case "weed": // Couch Lock: a soft haze with a green cast, between letterbox bars.
                    look.Tint=new Color(.35f,.8f,.35f,.05f*s);look.Haze=.12f*s;look.Letterbox=.04f+.06f*s;break;
            }
            return look;
        }

        /// <summary>GAS-1: the wah-wah's beat, in pulses a second.</summary>
        public const float GasPulseHz=4;
        // At full strength: the vignette's floor and its pulse on top, the throb in degrees, the colour drained from the grade, the haze,
        // the low-pass's floor (it pulses up to 1) and how far the tremolo dips the volume.
        private const float GasVignette=.3f,GasVignettePulse=.3f,GasThrob=1.2f,GasDrain=55,GasHaze=.1f,GasMuffle=.4f,GasTremolo=.4f;

        /// <summary>GAS-1: Giggle Gas at strength 0-1 (FestivalSimulation.GiggleGasStrength, on the simulation clock) at local time t. A
        /// wah-wah pulse GasPulseHz times a second runs through the sound (a low-pass closing in, and a tremolo) and the view (a pulsing
        /// vignette and a slight field-of-view throb); colours drain and a pale haze puts the world at a distance. Reduced motion drops
        /// the throb and halves the vignette's pulse; the sound stays.</summary>
        public static Perception Gas(float strength,bool reducedMotion,float time)
        {
            var look=new Perception();if(strength<=0)return look;
            float wave=Mathf.Sin(time*GasPulseHz*2*Mathf.PI),pulse=.5f+.5f*wave;
            look.Vignette=strength*(GasVignette+GasVignettePulse*(reducedMotion?.5f:1)*pulse);
            look.Fov=reducedMotion?0:strength*GasThrob*wave;
            look.Saturation=-GasDrain*strength;look.Haze=GasHaze*strength;
            look.Muffle=strength*(GasMuffle+(1-GasMuffle)*pulse);look.Tremolo=strength*GasTremolo*pulse;
            return look;
        }

        /// <summary>The local player's trip in state, never for a spirit. A dose shows only while the festival is shown (Playing or
        /// Results), only for their own dose of 1 or more (cross-cutting call 3), and at night with its tint and haze held to NightTint.
        /// Giggle Gas shows by its own strength; it lives only at camp (the host clears it as the crew leaves), so never with a dose.</summary>
        public static Perception For(RoundState state,string localPlayerId,bool reducedMotion,float time)
        {
            if(state==null)return default;
            foreach(var p in state.Players)
            {
                if(p.Id!=localPlayerId)continue;
                if(p.Life=="Spirit")return default;
                foreach(var e in p.Effects)
                {
                    if(e.Id==FestivalSimulation.GiggleGasEffect)return Gas((float)FestivalSimulation.GiggleGasStrength(e,state.SimulationSeconds),reducedMotion,time);
                    if(e.Id!=FestivalSimulation.DoseEffect||e.Intensity<1||!FestivalWorld.ShowsFestival(state.Phase))continue;
                    var look=Look(e.Substance,e.Intensity,reducedMotion,time);
                    if(FestivalNightLighting.IsNight(state)){look.Tint.a=Mathf.Min(look.Tint.a,NightTint);look.Haze=Mathf.Min(look.Haze,NightTint);}
                    return look;
                }
                return default;
            }
            return default;
        }

        /// <summary>The listener's low-pass cutoff for a muffle of 0-1: mild, so the rhythm's beat stays audible.</summary>
        public static float LowPassHz(float muffle)=>muffle<=0?22000:Mathf.Lerp(5000,2200,muffle);

        /// <summary>One frame of a camera angle trailing its target by lag seconds, the short way round; no lag snaps to it.</summary>
        public static float Follow(float current,float target,float lag,float deltaTime)=>lag<=0?target:current+Mathf.DeltaAngle(current,target)*(1-Mathf.Exp(-deltaTime/lag));

        /// <summary>The effect whose look the rhythm arrows take (EffectPresentation.Path, Catalog's LeadSeconds): the dose's substance
        /// first, else the newest catalog effect taken (HUD-4). The dose and the debrief's shot are not catalog effects: no look.</summary>
        public static string LaneEffect(PlayerState player)
        {
            foreach(var e in player.Effects)if(e.Id==FestivalSimulation.DoseEffect&&!string.IsNullOrEmpty(e.Substance))return e.Substance;
            for(int i=player.Effects.Count-1;i>=0;i--)if(Catalog.FindEffect(player.Effects[i].Id)!=null)return player.Effects[i].Id;
            return "";
        }

        // Under the HUD (100) and the spinner (110), so the HUD stays readable over the bars and the spin covers it all.
        private const int SortingOrder=90;
        // A trail ghost drops every TrailGap seconds from each person within TrailRange metres who moved since their last one, and fades
        // over TrailLife. Glows go round the first MaxGlows people in front of the view within GlowRange, GlowBehind metres beyond them.
        private const float TrailGap=.1f,TrailLife=.7f,TrailAlpha=.45f,TrailRange=25,MinStride=.05f,GlowRange=20,GlowBehind=.35f,GlowAlpha=.35f;
        private static readonly Color GlowColor=new Color(1,.55f,.75f);
        private static readonly Color HazeColor=new Color(.9f,.97f,.88f);

        private FestivalSession session;
        private Func<string,Transform> actorFor;
        private GameObject overlay;
        private Image tint,haze,top,bottom;
        private RawImage vignette;
        private Texture2D vignetteTexture,blobTexture;
        private Sprite blob;
        private Material sprites;
        private AudioLowPassFilter lowPass;
        private readonly SpriteRenderer[] trails=new SpriteRenderer[MaxTrails];
        private readonly float[] trailAge=new float[MaxTrails];
        private readonly SpriteRenderer[] glows=new SpriteRenderer[MaxGlows];
        private readonly Dictionary<string,Vector3> lastDrop=new Dictionary<string,Vector3>();
        private int nextTrail;
        private float sinceDrop;

        // After FestivalSession.Update has moved the first-person camera this frame.
        private void LateUpdate()
        {
            if(session==null){session=GetComponent<FestivalSession>();if(session==null)return;actorFor=id=>{var body=session.WorldCharacter(id);return body!=null?body.transform:null;};}
            Apply(session.State,session.LocalPlayerId,session.ViewCamera!=null?session.ViewCamera.transform:null,actorFor,session.Profile.Data.ReducedMotion,Time.unscaledTime,Time.unscaledDeltaTime);
        }
        // Turned off, it draws nothing and lets the sound go.
        private void OnDisable(){if(overlay!=null)Show(default,null,null,null,null,0);}

        /// <summary>Draws localPlayerId's trip in state over view; actorFor finds a person's drawn body (null when not drawn).</summary>
        public void Apply(RoundState state,string localPlayerId,Transform view,Func<string,Transform> actorFor,bool reducedMotion,float time,float deltaTime)
        {
            if(overlay==null)Build();
            Show(For(state,localPlayerId,reducedMotion,time),state,localPlayerId,view,actorFor,deltaTime);
        }

        private void Show(Perception look,RoundState state,string localPlayerId,Transform view,Func<string,Transform> actorFor,float deltaTime)
        {
            bool shown=look.Tint.a>0||look.Haze>0||look.Vignette>0||look.Letterbox>0;
            if(overlay.activeSelf!=shown)overlay.SetActive(shown);
            tint.color=look.Tint;haze.color=new Color(HazeColor.r,HazeColor.g,HazeColor.b,look.Haze);vignette.color=new Color(0,0,0,look.Vignette);
            top.rectTransform.anchorMin=new Vector2(0,1-look.Letterbox);bottom.rectTransform.anchorMax=new Vector2(1,look.Letterbox);
            Listen(view,look);
            Trail(state,localPlayerId,view,actorFor,look.Trails,deltaTime);
            Glow(state,localPlayerId,view,actorFor,look.Glow);
        }

        private void Listen(Transform view,Perception look)
        {
            // ponytail: a frame-rate tremolo on the global listener volume; an OnAudioFilterRead tremolo if it ever sounds stepped.
            float volume=1-look.Tremolo;if(AudioListener.volume!=volume)AudioListener.volume=volume;
            // The filter needs the view's AudioListener beside it; a view without one (an EditMode test) is left alone.
            if(lowPass==null){if(look.Muffle<=0||view==null||view.GetComponent<AudioListener>()==null)return;lowPass=view.gameObject.AddComponent<AudioLowPassFilter>();}
            lowPass.enabled=look.Muffle>0;lowPass.cutoffFrequency=LowPassHz(look.Muffle);
        }

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
            top=Layer<Image>("Trip letterbox top",new Vector2(0,1),Vector2.one);
            bottom=Layer<Image>("Trip letterbox bottom",Vector2.zero,new Vector2(1,0));
            top.color=bottom.color=Color.black;
            overlay.SetActive(false);
            blobTexture=Radial(32,r=>{float a=Mathf.Clamp01(1-r);return a*a;},"Festival trip blob");
            blob=Sprite.Create(blobTexture,new Rect(0,0,32,32),new Vector2(.5f,.5f),32);blob.name="Festival trip blob";
            sprites=new Material(Shader.Find("Sprites/Default")){name="Festival trip sprites"};
        }
        private T Layer<T>(string name,Vector2 min,Vector2 max) where T:Graphic
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(T));go.transform.SetParent(overlay.transform,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var graphic=go.GetComponent<T>();graphic.raycastTarget=false;graphic.color=Color.clear;return graphic;
        }
        // A white texture whose alpha is alpha(r), r running 0 at the centre to 1 at the middle of each edge.
        private static Texture2D Radial(int size,Func<float,float> alpha,string name)
        {
            var pixels=new Color[size*size];float half=size*.5f;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=(x+.5f-half)/half,dy=(y+.5f-half)/half;
                pixels[y*size+x]=new Color(1,1,1,alpha(Mathf.Sqrt(dx*dx+dy*dy)));
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name=name,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            texture.SetPixels(pixels);texture.Apply();return texture;
        }

        private void OnDestroy()
        {
            // The listener's volume is global and outlives this component and its scene.
            AudioListener.volume=1;
            if(lowPass!=null)Dispose(lowPass);
            foreach(var thing in new Object[]{blob,blobTexture,vignetteTexture,sprites})if(thing!=null)Dispose(thing);
        }
        private static void Dispose(Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
