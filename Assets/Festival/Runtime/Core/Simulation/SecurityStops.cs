namespace Festival.Core
{
    /// <summary>ESC-2: a security stop is a real moment. A cop who sees stock out without a merch bag (PoliceTick) calls the
    /// player out and stops them. Stowing or concealing the stock ends visibility evidence; a talk scoring .75 waves them on,
    /// ditching every unit in sight lets the cop claim it, and keeping out of sight for BoltSeconds loses the stop (Sees: 12 m,
    /// 5 m in a dust storm, and walls). Stronger witnessed-deal and ignored-stop evidence survives concealment. Ignored
    /// for StopArrestSeconds in its sight with the stock, it becomes an arrest warning, warned at StopWarnSeconds. The cop's accepted barks
    /// are public speech from the actual cop; case/evidence state remains private.</summary>
    public sealed partial class FestivalSimulation
    {
        /// <summary>How long a stop may be ignored in the cop's sight before it is an arrest warning (the HUD's stop notice says so).</summary>
        public const double StopArrestSeconds=20;
        const double StopWarnSeconds=12,BoltSeconds=6,ConfiscateSeconds=10,BarkSeconds=5;
        const string StopBark="HEY! What's in the bag?",WarnBark="Last chance. Empty your pockets.",DitchBark="Fine. Move along.";
        static bool CarriesStock(PlayerState p)=>p.Inventory.Exists(i=>i.Count>0&&Stock(i.ItemId));
        // An arrest warning: a deal the cop saw (RecordDeal), or a stop ignored too long. Either detains at DetainAt.
        static bool Arrests(EvidenceState e)=>e.Kind=="WitnessedDeal"||e.Kind=="IgnoredStop";
        bool Stopped(PlayerState p)=>State.Npcs.Exists(n=>n.Kind=="Cop"&&n.Evidence.Exists(e=>e.PlayerId==p.Id&&e.Kind=="VisibleStock"));
        bool TalkingTo(PlayerState p,NpcState cop){var i=Interaction(p.InteractionId);return i!=null&&i.Status=="Active"&&i.Kind=="Police"&&i.TargetId==cop.Id;}
        void Bark(NpcState cop,PlayerState p,string line){RetractSpeech(p,"",true);PublishSpeech(p,cop.Id,"SECURITY",line,BarkSeconds);}
        // K2: a stop over without a word (waved on, bolted, ditched unseen) takes back what the guard said, so the HUD's card drops a
        // call-out or a warning it still holds unread (FestivalTalkLine).
        void Hush(PlayerState p){RetractSpeech(p,"",true);var talk=Interaction(p.InteractionId);if(talk!=null&&talk.Kind=="Police")RetractSpeech(p,talk.Id);if(p.NpcSpeaker=="SECURITY")p.NpcSpeech="";}
        EvidenceState StartStop(NpcState cop,PlayerState p){var e=new EvidenceState{PlayerId=p.Id,Kind="VisibleStock",StartSeconds=State.SimulationSeconds,SeenSeconds=State.SimulationSeconds};cop.Evidence.Add(e);Bark(cop,p,StopBark);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ChatDiagnostic?.Invoke("StockVisibility","stop:"+cop.Id+":"+p.Id,State.RoundId,State.SimulationSeconds);
#endif
            return e;}
        // A talk scoring .75 (Interactions.cs) waves the stopped player on: the cop lets them be until it has lost sight of them.
        void WaveOn(string copId,PlayerState p){var e=StopBy(copId,p);if(e!=null){e.Kind="WavedOn";Hush(p);}}
        // K2: a talk that doesn't wave p on leaves the stop as it was, and the warning Pursue held back for it comes as it ends, if
        // the stop is past StopWarnSeconds and not yet an arrest warning.
        void HeardOut(string copId,PlayerState p){var e=StopBy(copId,p);if(e==null)return;double held=State.SimulationSeconds-e.StartSeconds;if(held>=StopWarnSeconds&&held<StopArrestSeconds)Bark(State.Npcs.Find(n=>n.Id==copId),p,WarnBark);}
        EvidenceState StopBy(string copId,PlayerState p)=>State.Npcs.Find(n=>n.Id==copId)?.Evidence.Find(x=>x.PlayerId==p.Id&&x.Kind=="VisibleStock");

        // Whether cop goes after p this tick on evidence e, turned to face them. Concealment retracts the call-out; ditching all
        // stock in the cop's sight says so and claims stock lying in view. A stop also ends once p has kept out
        // of its sight for BoltSeconds. Ignored, it escalates only in the cop's sight, and never mid-talk: the cop hears a talk
        // out. Ceiling: a cop works one case at a time (PoliceTick), so a second player's stop waits, its clocks running.
        bool Pursue(NpcState cop,PlayerState p,EvidenceState e,double dt)
        {
            double now=State.SimulationSeconds;
            if(e.Kind=="WavedOn"){if(!Sees(cop,p))cop.Evidence.Remove(e);return false;}
            cop.Yaw=Bearing(cop.X,cop.Z,p.X,p.Z);
            if(e.Kind!="VisibleStock")return true;
            // A stop saved before ESC-2 has no start: it is timed from the load, the first tick after it.
            if(e.StartSeconds<0)e.StartSeconds=e.SeenSeconds=now;
            bool seen=Sees(cop,p);if(seen)e.SeenSeconds=now;
            if(!StockVisible(p)){if(seen&&!CarriesStock(p)&&!ReservedStockOut(p)){Bark(cop,p,DitchBark);ClaimDitchedStock(cop);}else Hush(p);cop.Evidence.Remove(e);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                ChatDiagnostic?.Invoke("StockVisibility","cleared:"+cop.Id+":"+p.Id,State.RoundId,now);
#endif
                return false;}
            // ESC-3: up on the Ferris wheel nobody can talk, ditch or walk away (MayStep, Apply), so the stop waits for the turn: its
            // clocks stand still, and the guard waits at the base. Stowing the stock up there still ends it, above.
            if(OnWheel(State,p.Id)){e.StartSeconds=System.Math.Min(now,e.StartSeconds+dt);e.SeenSeconds=now;return true;}
            if(now-e.SeenSeconds>=BoltSeconds){Hush(p);cop.Evidence.Remove(e);return false;}
            double held=now-e.StartSeconds;
            // K2: mid-talk the warning waits for the talk's end (HeardOut), as the arrest warning does.
            if(held>=StopWarnSeconds&&held-dt<StopWarnSeconds&&!TalkingTo(p,cop))Bark(cop,p,WarnBark);
            // The usual 4 s from an arrest warning to detention, as for a witnessed deal (RecordDeal).
            if(held>=StopArrestSeconds&&seen&&!TalkingTo(p,cop)){e.Kind="IgnoredStop";e.DetainAt=now+4;}
            return true;
        }
        void ClaimDitchedStock(NpcState cop){foreach(var d in State.Drops)if(Stock(d.ItemId)&&d.ClaimedBy==""&&Distance(cop.X,cop.Z,d.X,d.Z)<=SightRange(cop)&&(HasLineOfSight==null||HasLineOfSight(cop.X,cop.Z,d.X,d.Z))){d.ClaimedBy=cop.Id;d.ClaimAt=State.SimulationSeconds+ConfiscateSeconds;}}
        // Ditched stock lies there for anyone for ConfiscateSeconds, the cop standing by; then it walks over at its 3 m/s and takes
        // what is still there, from a player's reach (Near's 2.5 m). False with nothing to take, so the cop patrols.
        bool Confiscate(NpcState cop,double dt)
        {
            var d=State.Drops.Find(x=>x.ClaimedBy==cop.Id);if(d==null)return false;
            cop.Mode="Patrol";cop.TargetId="";double dist=Distance(cop.X,cop.Z,d.X,d.Z);if(dist>.1)cop.Yaw=Bearing(cop.X,cop.Z,d.X,d.Z);
            if(State.SimulationSeconds<d.ClaimAt)return true;
            if(dist<=2.5){State.Drops.Remove(d);CrewNews(null,"Items","SECURITY TOOK the ditched "+Catalog.FindItem(d.ItemId)?.Name);return true;}
            var point=Move(cop.X,cop.Z,d.X,d.Z,3*dt);cop.X=point.X;cop.Z=point.Z;return true;
        }
    }
}
