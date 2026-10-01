namespace Festival.Core
{
    /// <summary>TRIP-3: the tripper checks a vision in person. A ConfirmChat takes ConfirmChatSeconds and is safe; the festivalgoer's
    /// answers (DialogueGrammar) carry their role's tells. A ConfirmDance is a four-note phrase whose verdict lands sooner, but like
    /// any dance a score under .6 draws the watchers' suspicion. Either way every vision about them is confirmed, so its truth
    /// reaches the tripper (VisibleVisions), and a true clue holder moves the night's trail on (ConfirmVisionsOf).</summary>
    public sealed partial class FestivalSimulation
    {
        // A chat check takes this long; the tripper stands within ConfirmReach of the festivalgoer to start either check.
        public const double ConfirmChatSeconds=5,ConfirmReach=2.5;
        // Four notes half a second apart: 2 s of steps after the chart's lead-in, with no countdown, so the verdict lands in
        // 4.75 s (the chart's 4 s plus the .75 s every rhythm verdict waits for late input), just ahead of a chat.
        const int ConfirmDanceNotes=4;const double ConfirmDanceBeat=.5;

        /// <summary>Whether p may check n: only the tripper, and only someone they still have an unchecked vision about. POLO-1's one
        /// exception: anyone may chat with Palm Mirage's VIP guard to be talked past the ropes (FestivalTwists.cs). It reads only
        /// what a client's view holds, so the HUD offers the checks and the guard's chat by it.</summary>
        public static bool MayConfirm(RoundState s,PlayerState p,NpcState n,string kind)=>CanCheckVision(s,p,n)||kind=="ConfirmChat"&&TalksPastTheRope(p,n);
        /// <summary>Whether p has a vision about n still to check. A client's view carries visions only to its tripper, so the HUD
        /// asks it of the view to offer the checks.</summary>
        public static bool CanCheckVision(RoundState s,PlayerState p,NpcState n)=>p.Id==s.TripperId&&s.Visions.Exists(v=>v.NpcId==n.Id&&!v.Confirmed);
        /// <summary>VISION-3: whether n is too taken up for anyone to check: worked up (swarming or accusing anyone) or the target of an
        /// interaction under way. A client's view hides both and carries the host's answer in Busy (FestivalSession.ViewFor), so the
        /// HUD asks it of the view and says they're busy instead of offering a check the host would refuse.</summary>
        public static bool Engaged(RoundState s,NpcState n)=>n.Busy||WorkedUp(n)||s.Interactions.Exists(x=>x.TargetId==n.Id&&x.Status=="Active");
        static bool WorkedUp(NpcState n)=>n.Mode=="Swarming"||n.Mode=="Accusing";

        CommandResult BeginConfirm(PlayerState p,GameCommand c)
        {
            var npc=State.Npcs.Find(n=>n.Id==c.TargetId);
            if(npc==null||!Near(p,npc.X,npc.Z,ConfirmReach))return Reject("Stand next to the festivalgoer you want to check");
            if(!MayConfirm(State,p,npc,c.Kind))return Reject(npc.Twist==VipGuard&&c.Kind=="ConfirmChat"?RopeRefusal(p):p.Id==State.TripperId?"You have no vision about them to check":"Only the tripper can check a vision");
            if(Engaged(State,npc))return Reject(WorkedUp(npc)?"They're too worked up to stop for you":"NPC is busy");
            if(c.Kind=="ConfirmChat")
            {
                var chat=NewInteraction(p,c.Kind,npc.Id,ConfirmChatSeconds);
                chat.Chat=DialogueGrammar.Build(npc.Role,DialogueGrammar.PersonaFor(npc.Id),State.FestivalIndex,chat.ChartSeed);
                return Ok();
            }
            GiveDanceRoom(p,npc,c.Kind);
            var dance=NewInteraction(p,c.Kind,npc.Id,0);dance.NoteCount=ConfirmDanceNotes;dance.BeatSeconds=ConfirmDanceBeat;
            dance.DurationSeconds=RhythmChart.Create(dance.ChartSeed,dance.NoteCount,dance.BeatSeconds).DurationSeconds;
            foreach(var n in State.Npcs)if(n.Kind=="Wook"&&Sees(n,p))dance.WitnessIds.Add(n.Id);
            return Ok();
        }
        // Every finished interaction passes through here; a finished check confirms whatever the dance scored. Only the tripper's
        // checks confirm: anyone else chatting (with the VIP guard) is only talking their way in.
        void FinishConfirm(InteractionState i)
        {
            var npc=i.Kind=="ConfirmChat"||i.Kind=="ConfirmDance"?State.Npcs.Find(n=>n.Id==i.TargetId):null;
            if(npc==null)return;
            if(i.PlayerId==State.TripperId)ConfirmVisionsOf(npc);
            PassTheRope(i,npc);
        }
    }
}
