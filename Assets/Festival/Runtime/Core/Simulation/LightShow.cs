using System;
using System.Collections.Generic;

namespace Festival.Core
{
    public enum LightShowEventKind { Passer, Sweep, Sign, Glover }
    [Serializable] public sealed class LightShowEvent
    {
        public LightShowEventKind Kind; public float StartsAt,EndsAt; public string Colour="",Shape="",Prop=""; public int Side,Look;
    }
    public enum LightShowShadowTell { Small, Goblin }
    [Serializable] public sealed class LightShowFake
    {
        public LightShowEvent Event=new LightShowEvent(); public LightShowShadowTell Shadow; public bool TintTell=true,MovementShimmer=true;
    }
    [Serializable] public sealed class LightShowQuestion
    {
        public string Category="",Prompt="",Unsure=LightShowQuestions.Unsure; public List<string> Choices=new List<string>(); public int CorrectIndex=-1;
    }
    [Serializable] public sealed class LightShowPlayer
    {
        public string PlayerId=""; public int Dose; public List<LightShowFake> Fakes=new List<LightShowFake>();
        public List<LightShowQuestion> Questions=new List<LightShowQuestion>();
    }
    /// <summary>Persisted records on the advancing game clock, never world entities or simulation deadlines.</summary>
    [Serializable] public sealed class LightShowState
    {
        public List<LightShowEvent> Events=new List<LightShowEvent>();
        public List<LightShowPlayer> Players=new List<LightShowPlayer>();
        internal LightShowState PublicCopy()=>new LightShowState{Events=Events.ConvertAll(e=>new LightShowEvent{Kind=e.Kind,StartsAt=e.StartsAt,EndsAt=e.EndsAt,Colour=e.Colour,Shape=e.Shape,Prop=e.Prop,Side=e.Side,Look=e.Look})};
    }
    public static class LightShow
    {
        public const float ShowSeconds=20;
        public static LightShowState Create(int seed)
        {
            var show=new LightShowState();var random=new ContentRandom(seed);random.Next(100);
            int passers=2+random.Next(5),carrier=random.Next(passers);string prop=LightShowQuestions.Props[random.Next(LightShowQuestions.Props.Count)];
            for(int k=0;k<passers;k++)show.Events.Add(new LightShowEvent{Kind=LightShowEventKind.Passer,StartsAt=1+k*2,EndsAt=5+k*2,Side=random.Next(2)==0?-1:1,Look=random.Next(8),Prop=k==carrier?prop:""});
            for(int k=0;k<3;k++)show.Events.Add(new LightShowEvent{Kind=LightShowEventKind.Sweep,StartsAt=3+k*5,EndsAt=5+k*5,Colour=LightShowQuestions.Colours[random.Next(LightShowQuestions.Colours.Count)],Side=random.Next(2)==0?-1:1});
            for(int k=0;k<3;k++)show.Events.Add(new LightShowEvent{Kind=LightShowEventKind.Sign,StartsAt=2+k*6,EndsAt=4+k*6,Shape=LightShowQuestions.Shapes[random.Next(LightShowQuestions.Shapes.Count)]});
            for(int k=0;k<4;k++)show.Events.Add(new LightShowEvent{Kind=LightShowEventKind.Glover,StartsAt=k*5,EndsAt=(k+1)*5,Colour=LightShowQuestions.Colours[random.Next(LightShowQuestions.Colours.Count)]});
            show.Events.Sort((a,b)=>{int time=a.StartsAt.CompareTo(b.StartsAt);return time!=0?time:a.Kind.CompareTo(b.Kind);});return show;
        }
        // Each viewer owns a separate stream; roster order cannot change their private draw.
        public static LightShowPlayer Capture(int seed,PlayerState player,IReadOnlyList<LightShowEvent> realEvents,double now)
        {
            int dose=0;foreach(var e in player.Effects)if(e.Id==FestivalSimulation.DoseEffect&&e.RemainingSeconds>0&&e.StartSeconds<=now)dose=Math.Max(dose,Math.Max(0,Math.Min(4,e.Intensity)));
            int privateSeed=ViewerSeed(seed,player.Id);var record=new LightShowPlayer{PlayerId=player.Id,Dose=dose,Questions=LightShowQuestions.Draw(realEvents,unchecked(privateSeed*79+47))};var random=new ContentRandom(privateSeed);random.Next(100);
            for(int k=0;k<dose;k++)
            {
                float at=1+random.Next(14);var kind=random.Next(2)==0?LightShowEventKind.Passer:LightShowEventKind.Sweep;
                record.Fakes.Add(new LightShowFake{Shadow=(LightShowShadowTell)random.Next(2),Event=new LightShowEvent{Kind=kind,StartsAt=at,EndsAt=at+4,Side=random.Next(2)==0?-1:1,Look=random.Next(8),Colour=kind==LightShowEventKind.Sweep?LightShowQuestions.Colours[random.Next(LightShowQuestions.Colours.Count)]:""}});
            }
            return record;
        }
        static int ViewerSeed(int seed,string id){uint hash=2166136261;foreach(char c in id)hash=unchecked((hash^c)*16777619);return unchecked(seed^(int)hash);}
    }
    public sealed partial class FestivalSimulation
    {
        public const string LightShowGame="LightShow";
        void StartLightShow()
        {
            int seed=unchecked(State.SpinSeed*73+43)^State.DealSeed;var show=LightShow.Create(seed);
            foreach(string id in State.Booth.PlayerIds){var p=Player(id);if(p!=null)show.Players.Add(LightShow.Capture(seed,p,show.Events,State.SimulationSeconds));}State.Booth.LightShow=show;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RhythmDiagnostic?.Invoke("[Festival.LightShow] start participants="+State.Booth.PlayerIds.Count);
#endif
        }
    }
}
