namespace Festival.Core
{
    public sealed partial class FestivalSimulation
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Kind/status and transient round/time only; no transcript, history, identity or vision truth.
        public System.Action<string,string,string,double> ChatDiagnostic;
#endif
        /// <summary>DIALOG-9: reply-based festivalgoer talks and chat checks wait for their owner. Police and trust dances keep time.</summary>
        public static bool UntimedChat(InteractionState i)=>i!=null&&(i.Kind=="ConfirmChat"||Talks(i.Kind)&&i.Kind!="Police"&&!TrustDancing(i));

        CommandResult EndChat(PlayerState p,GameCommand c)
        {
            var i=Interaction(c.TargetId);
            if(i==null||i.Id!=p.InteractionId||i.PlayerId!=p.Id||i.Status!="Active"||i.Kind!="ConfirmChat"&&(!Talks(i.Kind)||TrustDancing(i)))return Reject("No active chat to end");
            if(State.Phase!="Playing"||p.Life!="Alive")return Reject("Chat is not interactive");
            if(!TaskStillValid(i,p)){Cancel(p,"Moved away or target changed");return Reject("Chat is out of reach");}
            if(i.Kind=="ConfirmChat")FinishInteraction(i,p);else Cancel(p,"Cancelled");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ChatDiagnostic?.Invoke("ChatEnd",i.Kind+" "+i.Status,State.RoundId,State.SimulationSeconds);
#endif
            return Ok();
        }

        void RestoreChats()
        {
            foreach(var i in State.Interactions)
            {
                if(i.Status!="Active")continue;
                var p=Player(i.PlayerId);if(p==null)continue;
                if(Talks(i.Kind)&&!TrustDancing(i)&&i.Turns.Count==0){i.DurationSeconds=State.SimulationSeconds-i.StartSeconds;AddTurn(i,p);}
                if(UntimedChat(i))i.DurationSeconds=0;
            }
        }
    }
}
