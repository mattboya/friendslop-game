using System;
using System.Collections.Generic;

namespace Festival.Core
{
    public enum LightShowEventKind { Passer, Sweep, Sign, Glover }
    [Serializable] public sealed class LightShowEvent
    {
        public LightShowEventKind Kind; public float StartsAt,EndsAt; public string Colour="",Shape="",Prop=""; public int Side,Look;
        internal LightShowEvent Copy()=>(LightShowEvent)MemberwiseClone();
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
        // One per closed or answered question: a choice 0-3, or -1 for Not sure, which a timeout or a disconnect also gives.
        public List<int> Answers=new List<int>();
    }
    /// <summary>Persisted records on the advancing game clock, never world entities or simulation deadlines.</summary>
    [Serializable] public sealed class LightShowState
    {
        public List<LightShowEvent> Events=new List<LightShowEvent>();
        public List<LightShowPlayer> Players=new List<LightShowPlayer>();
        // The open question, the same for everyone, and when it opened; Slot reaches QuestionSlots once every question has closed.
        public int Slot; public float SlotAt=LightShow.ShowSeconds;
        internal LightShowState PublicCopy()=>new LightShowState{Events=Events.ConvertAll(e=>e.Copy()),Slot=Slot,SlotAt=SlotAt};
    }
    public static class LightShow
    {
        public const float ShowSeconds=20,QuestionSeconds=8; public const int QuestionSlots=3;
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
        public const string LightShowGame="LightShow",LightShowAnswer="Answer";
        /// <summary>One answer's points: a correct one earns 1 + the captured dose, a wrong one costs 1 and Not sure (-1) none.</summary>
        public static int LightShowPoints(int dose,int answer,int correct)
        {
            if(dose<0||dose>4||correct<0||correct>3||answer< -1||answer>3)throw new ArgumentOutOfRangeException(nameof(answer));
            return answer<0?0:answer==correct?1+dose:-1;
        }
        /// <summary>The crew's net points as seconds on the level clock: four each, between -40 and +60.</summary>
        public static int LightShowClockChange(int netPoints)=>(int)Math.Max(-40L,Math.Min(60L,(long)netPoints*4));
        // An answer to the open question by a captured player: Amount is the choice (-1 Not sure), Direction the question's slot.
        CommandResult PlayLightShow(PlayerState p,GameCommand c)
        {
            var b=State.Booth;var show=b.LightShow;var mine=show.Players.Find(x=>x.PlayerId==p.Id);
            if(c.ItemId!=LightShowAnswer||mine==null)return Reject("Answer the light show's question");
            if(show.Slot>=LightShow.QuestionSlots||b.Seconds<LightShow.ShowSeconds)return Reject("Watch the show: the questions come after it");
            if(c.Direction!=show.Slot)return Reject("That question isn't open");
            if(mine.Answers.Count!=show.Slot)return Reject("You already answered this one");
            if(show.Slot>=mine.Questions.Count||c.Amount<-1||c.Amount>=mine.Questions[show.Slot].Choices.Count)return Reject("Pick one of the four answers, or Not sure");
            mine.Answers.Add(c.Amount);LightShowTick();return Ok("Answer locked in");
        }
        // Questions close in turn, each once every connected captured player has answered or its eight seconds are up; the
        // missing answers are Not sure. After the third, the crew's net points score the game.
        void LightShowTick()
        {
            var b=State.Booth;var show=b.LightShow;
            // A question after the first opens as the last one closes, so only the show holds the questions back.
            while(show.Slot<LightShow.QuestionSlots&&b.Seconds>=LightShow.ShowSeconds)
            {
                float closes=show.SlotAt+LightShow.QuestionSeconds;bool timedOut=b.Seconds>=closes||b.Seconds>=MinigameMaxSeconds;
                if(!timedOut&&show.Players.Exists(x=>x.Answers.Count<=show.Slot&&Player(x.PlayerId)?.Connected==true))return;
                foreach(var x in show.Players)while(x.Answers.Count<=show.Slot)x.Answers.Add(-1);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                RhythmDiagnostic?.Invoke("[Festival.LightShow] question="+show.Slot+" closed="+(timedOut?"timeout":"answered"));
#endif
                show.Slot++;show.SlotAt=timedOut?closes:(float)b.Seconds;
            }
            if(show.Slot<LightShow.QuestionSlots)return;
            int net=0;foreach(var x in show.Players)for(int k=0;k<x.Answers.Count&&k<x.Questions.Count;k++)net+=LightShowPoints(x.Dose,x.Answers[k],x.Questions[k].CorrectIndex);
            ScoreMinigame(LightShowClockChange(net));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RhythmDiagnostic?.Invoke("[Festival.LightShow] reveal participants="+show.Players.Count+" clock_change="+b.ClockChange.ToString(System.Globalization.CultureInfo.InvariantCulture));
#endif
        }
        /// <summary>The Light Show as viewer's snapshot carries it (FestivalSession.ViewFor): the real script and question timing for
        /// everyone; until the game is scored, the viewer's own record alone, with the questions opened so far and no answer keys; then
        /// every captured player's questions, keys and answers at once. Fakes only ever reach their own viewer.</summary>
        public static LightShowState VisibleLightShow(RoundState s,string viewer)
        {
            var b=s.Booth;var show=b.LightShow??new LightShowState();var view=show.PublicCopy();bool revealed=b.ScoredAt>=0;
            int open=show.Slot+(show.Slot<LightShow.QuestionSlots&&b.Seconds>=LightShow.ShowSeconds?1:0);
            foreach(var x in show.Players)
            {
                bool own=x.PlayerId==viewer;if(!own&&!revealed)continue;
                var copy=new LightShowPlayer{PlayerId=x.PlayerId,Dose=x.Dose,Answers=new List<int>(x.Answers)};
                if(own)copy.Fakes=x.Fakes.ConvertAll(f=>new LightShowFake{Event=f.Event.Copy(),Shadow=f.Shadow,TintTell=f.TintTell,MovementShimmer=f.MovementShimmer});
                for(int k=0;k<x.Questions.Count&&(revealed||k<open);k++)
                {var q=x.Questions[k];copy.Questions.Add(new LightShowQuestion{Category=q.Category,Prompt=q.Prompt,Unsure=q.Unsure,Choices=new List<string>(q.Choices),CorrectIndex=revealed?q.CorrectIndex:-1});}
                view.Players.Add(copy);
            }
            return view;
        }
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
