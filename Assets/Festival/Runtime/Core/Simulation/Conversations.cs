using System;
namespace Festival.Core
{
    /// <summary>DIALOG-3: selling (Sale), buying (Buy, ECON-2), chatting (Conversation) and talking to security (Police) are talks,
    /// not dances. A talk is ConversationTurns turns: they say a line (their reaction to your last reply first), and you pick one
    /// of three replies with the Reply command (Amount the reply 0-2, Direction the turn). DIALOG-9 waits for each pick;
    /// only Police turns count silence after ReplySeconds. One reply is smooth, one so-so and one awkward by the person's taste
    /// (ConversationLines),
    /// and a dose garbles one of them a turn, two from dose 3. Smooth counts 1, so-so .5, awkward or nothing 0, and the average
    /// is the talk's Score, which settles it as the dance used to: a sale's pay, a buy, security waving off a stop, and every
    /// partner's and watcher's RhythmDelta. A festivalgoer wary of you (TrustDanceSuspicion), or one of the ravers and burners who always are,
    /// first asks for a dance (TrustAsk): eight beats on the talk's own chart, danced and watched like any dance. TrustScore or
    /// better wins their trust and the talk follows; less and they walk off, the talk over at 0. A chat or a buy reads the
    /// person: a reply that lands draws out a line with their role's tell, more often the smoother it was (TellOdds), and the
    /// first tell shown sets the Hunch the talk leaves, as its last turn begins. A narc asked to sell talks too, its tells in its
    /// lines; finishing the buy busts you (StockBuys.cs).</summary>
    public sealed partial class FestivalSimulation
    {
        public const int ConversationTurns=3;
        public const double ReplySeconds=4,TrustDanceSuspicion=40,TrustScore=.6;
        public const string TrustAsk="Dance with me first, then we talk.";
        public static bool Talks(string kind)=>kind=="Sale"||kind=="Buy"||kind=="Conversation"||kind=="Police";
        /// <summary>Whether talk i is still in the dance a wary festivalgoer asked for, before its first turn.</summary>
        public static bool TrustDancing(InteractionState i)=>Talks(i.Kind)&&i.TrustDance&&i.Turns.Count==0;
        /// <summary>Whether i shows its player dancing with a partner: a dance, a check dance or a talk's trust dance.</summary>
        public static bool Dancing(InteractionState i)=>DancesVisibly(i.Kind)||TrustDancing(i);

        // BeginChallenge's talks, once the person has agreed to one: the stock or the price is held, a cop who sees a deal start
        // records it, as for a dance, and the first turn (or the trust dance) begins.
        CommandResult BeginTalk(PlayerState p,GameCommand c,NpcState npc,string kind)
        {
            bool wary=Wary(npc,p);if(wary)GiveDanceRoom(p,npc);
            var i=NewInteraction(p,kind,npc.Id,0);p.ConversationThreadId=i.Id;
            foreach(var n in State.Npcs)if(n.Kind=="Wook"&&Sees(n,p))i.WitnessIds.Add(n.Id);
            if(kind=="Sale"){Take(p,c.ItemId,1);i.ReservedItemId=c.ItemId;}
            if(kind=="Buy")HoldPrice(p,i);
            if(kind=="Sale"||kind=="Buy")foreach(var cop in State.Npcs)if(cop.Kind=="Cop"&&Sees(cop,p))RecordDeal(cop,p);
            if(wary){i.TrustDance=true;ScheduleChallengeLeadIn(i);i.DialogueText=TrustAsk;i.DurationSeconds=RhythmChart.For(i).DurationSeconds;}else AddTurn(i,p);
            return Ok();
        }
        // A festivalgoer this wary of p, or one of the 1 in 4 ravers and burners who always want a dance first (by their id and the
        // round's seed, from bits of the id's FNV-1a hash that don't pick its persona).
        bool Wary(NpcState n,PlayerState p)
        {
            if(n.Kind!="Wook")return false;
            if((n.Observers.Find(o=>o.PlayerId==p.Id)?.Suspicion??0)>=TrustDanceSuspicion)return true;
            string persona=DialogueGrammar.PersonaFor(n.Id,n.Twist);if(persona!="Raver"&&persona!="Burner")return false;
            uint hash=2166136261;foreach(char ch in n.Id)hash=unchecked((hash^ch)*16777619);
            return ((hash>>16)^(uint)State.Seed)%4==0;
        }
        CommandResult Reply(PlayerState p,GameCommand c)
        {
            var i=Interaction(p.InteractionId);
            if(i==null||i.Status!="Active"||!Talks(i.Kind)||i.Turns.Count==0)return Reject("No conversation to reply in");
            var turn=i.Turns[i.Turns.Count-1];
            if(c.Direction!=i.Turns.Count-1||turn.Pick>=0||!UntimedChat(i)&&State.SimulationSeconds>=i.StartSeconds+i.DurationSeconds)return Reject("Too late: that moment has passed");
            if(c.Amount<0||c.Amount>2)return Reject("Pick reply 1, 2 or 3");
            AcknowledgeGrammar(p,turn.GrammarKeys,i.Id+":"+(i.Turns.Count-1));
            turn.Pick=c.Amount;
            if(i.Turns.Count<ConversationTurns)AddTurn(i,p);else FinishInteraction(i,p);
            return Ok(i.Status=="Police bust"?NarcBust:"Accepted");
        }
        // The next turn, drawn from its own stream of the talk's seed. Only Police gives it a ReplySeconds deadline;
        // silent Police turns keep a steady beat whatever the tick. Other talks use finite zero for untimed duration.
        void AddTurn(InteractionState i,PlayerState p)
        {
            if(p.Dialogue==null)p.Dialogue=new DialogueHistory();
            int t=i.Turns.Count;var npc=State.Npcs.Find(n=>n.Id==i.TargetId);string role=npc?.Role??"";
            string persona=i.Kind=="Police"?ConversationLines.Security:DialogueGrammar.PersonaFor(i.TargetId,npc?.Twist??"");
            int dose=DoseOf(p);var random=new ContentRandom(TurnSeed(i,t));bool clear=t>0&&Told(random,i.Turns[t-1]);
            var history=new DialogueHistory{Seen=new System.Collections.Generic.List<string>(p.Dialogue.Seen??new System.Collections.Generic.List<string>())};
            i.Turns.Add(ConversationLines.Turn(ConversationLines.Topic(i.Kind),persona,role,State.FestivalIndex,t,t==0?-1:Graded(i.Turns[t-1]),clear,dose>=3?2:dose>=1?1:0,random,history));
            i.DurationSeconds=UntimedChat(i)?0:Math.Min(State.SimulationSeconds,i.StartSeconds+i.DurationSeconds)+ReplySeconds-i.StartSeconds;
            if(t==ConversationTurns-1&&(i.Kind=="Conversation"||i.Kind=="Buy"))i.Hunch=Hunch(i,role);
        }
        static int TurnSeed(InteractionState i,int t)=>unchecked(i.ChartSeed*31+t);
        static int Graded(ConversationTurn t)=>t.Pick<0?ConversationLines.Silence:t.Grades[t.Pick];
        // How often, in 8, a turn after a smooth, so-so, awkward or silent reply comes out clear, carrying the person's tell. The
        // first draw of the turn's stream, so Hunch can read it again.
        static readonly int[] TellOdds={6,3,0,0};
        static bool Told(ContentRandom random,ConversationTurn heard)=>random.Next(8)<TellOdds[Graded(heard)];
        // What a chat or a buy's lines showed by its last turn: the hunch from its first clear line, or nothing to go on. A sale's
        // buyer is buying and security is security, so neither leaves one.
        string Hunch(InteractionState i,string role)
        {
            var npc=State.Npcs.Find(n=>n.Id==i.TargetId);bool seller=npc!=null&&SellerCategory(npc);
            for(int t=1;t<i.Turns.Count;t++)if(Told(new ContentRandom(TurnSeed(i,t)),i.Turns[t-1]))return ConversationLines.Hunch(role,i.Turns[t].Line,State.FestivalIndex,seller);
            return ConversationLines.Hunch(role,null,State.FestivalIndex);
        }
        // FinishInteraction calls this each time the talk's clock runs out (a trust dance's verdict, or a turn left silent) and
        // after its last reply. True once the talk is over, its Score set and its partner or witnesses moved once each.
        bool TalkEnds(InteractionState i,PlayerState p)
        {
            if(TrustDancing(i))
            {
                double danced=Judge(i);int delta=RhythmDelta(danced);
                foreach(var n in State.Npcs)if(i.WitnessIds.Contains(n.Id)||n.Id==i.TargetId)Adjust(n,p,delta);
                if(danced<TrustScore){i.Score=0;p.LastRhythmScore=0;p.Performances++;p.NpcSpeaker="FESTIVALGOER";p.NpcSpeech="Nah. Not feeling it.";p.NpcSpeechUntil=State.SimulationSeconds+7;return true;}
                i.DialogueText="";i.DurationSeconds=State.SimulationSeconds-i.StartSeconds;AddTurn(i,p);return false;
            }
            if(i.Turns.Count<ConversationTurns){AddTurn(i,p);return false;}
            double total=0;foreach(var turn in i.Turns){int grade=Graded(turn);total+=grade==ConversationLines.Smooth?1:grade==ConversationLines.SoSo?.5:0;}
            i.Score=total/ConversationTurns;p.LastRhythmScore=i.Score;p.Performances++;
            int moved=RhythmDelta(i.Score);foreach(var n in State.Npcs)if(i.WitnessIds.Contains(n.Id)||n.Id==i.TargetId)Adjust(n,p,moved);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ChatDiagnostic?.Invoke("TalkSettled",i.Kind,State.RoundId,State.SimulationSeconds);
#endif
            return true;
        }
        CommandResult AcknowledgeTalk(PlayerState p,GameCommand c)
        {
            if(c.Direction==ConversationTurns)
            {
                var outcome=p.ConversationOutcomes?.Find(o=>o.RoundId==State.RoundId&&o.InteractionId==c.TargetId);
                if(outcome==null)return c.Amount==1&&p.ConversationDismissedId==c.TargetId?Ok():Reject("Dialogue is not active");
                if(c.Amount!=0&&c.Amount!=1)return Reject("Invalid dialogue acknowledgment");
                AcknowledgeGrammar(p,outcome.GrammarKeys,outcome.DeliveryId);
                if(c.Amount==1){p.ConversationDismissedSequence=Math.Max(p.ConversationDismissedSequence,outcome.Sequence);p.ConversationDismissedId=outcome.InteractionId;p.ConversationOutcomes.RemoveAll(o=>o.Sequence<=p.ConversationDismissedSequence);}
                return Ok();
            }
            var i=Interaction(c.TargetId);
            if(i==null||i.PlayerId!=p.Id||!Talks(i.Kind)||c.Direction<0||c.Direction>=i.Turns.Count)return Reject("Dialogue is not active");
            AcknowledgeGrammar(p,i.Turns[c.Direction].GrammarKeys,i.Id+":"+c.Direction);return Ok();
        }
        void AcknowledgeGrammar(PlayerState p,System.Collections.Generic.List<string> keys,string delivery)
        {
            if(keys==null||keys.Count==0)return;
            if(p.Dialogue==null)p.Dialogue=new DialogueHistory();
            bool changed=false;
            foreach(string key in keys)
            {
                string ack=delivery.Length+":"+delivery+key;
                if(p.Dialogue.AcknowledgedDeliveries?.Contains(ack)==true)continue;
                p.Dialogue.Acknowledge(key,delivery);changed=true;
            }
            if(!changed)return;p.DialogueRevision++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ChatDiagnostic?.Invoke("HistoryAcknowledged","Talk",State.RoundId,State.SimulationSeconds);
#endif
        }
        void DeliverConversationOutcome(InteractionState i,PlayerState p)
        {
            if(p.ConversationThreadId=="")p.ConversationThreadId=i.Id;
            if(p.ConversationOutcomes==null)p.ConversationOutcomes=new System.Collections.Generic.List<ConversationOutcome>();
            if(p.ConversationOutcomes.Exists(o=>o.RoundId==State.RoundId&&o.InteractionId==i.Id))return;
            var outcome=new ConversationOutcome{RoundId=State.RoundId,InteractionId=i.Id,DeliveryId=State.RoundId+":"+i.Id,Kind=i.Kind,Speaker=i.Kind=="Police"?"SECURITY":"FESTIVALGOER",Hunch=i.Hunch,Sequence=++p.ConversationOutcomeSequence};
            if(i.TrustDance&&i.Turns.Count==0){outcome.Reaction="Nah. Not feeling it.";outcome.Notice="They walk off.";}
            else
            {
                var npc=State.Npcs.Find(n=>n.Id==i.TargetId);string persona=i.Kind=="Police"?ConversationLines.Security:DialogueGrammar.PersonaFor(i.TargetId,npc?.Twist??"");
                var history=new DialogueHistory{Seen=new System.Collections.Generic.List<string>(p.Dialogue.Seen??new System.Collections.Generic.List<string>())};
                outcome.Reaction=ConversationLines.Reaction(persona,Graded(i.Turns[i.Turns.Count-1]),State.FestivalIndex,new ContentRandom(unchecked(TurnSeed(i,ConversationTurns)^0x4f757463)),history,out var key);outcome.GrammarKeys.Add(key);
                if(i.Status=="Police bust")outcome.Notice=NarcBust;
            }
            p.ConversationOutcomes.Add(outcome);while(p.ConversationOutcomes.Count>8)p.ConversationOutcomes.RemoveAt(0);
            if(i.Kind!="Police"){p.NpcSpeaker=outcome.Speaker;p.NpcSpeech=outcome.Notice==NarcBust?NarcBust:outcome.Reaction;p.NpcSpeechUntil=State.SimulationSeconds+7;}
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ChatDiagnostic?.Invoke("OutcomeDelivered",i.Kind,State.RoundId,State.SimulationSeconds);
#endif
        }
        // What a finished talk settles on its Score: a sale pays in full at .75 and half at .4, and its stock comes back below;
        // a buy (StockBuys.cs); and security at .75 waves off its stop, or gives the warning it held back (HeardOut).
        void SettleTalk(InteractionState i,PlayerState p)
        {
            if(i.Kind=="Sale"){var npc=State.Npcs.Find(n=>n.Id==i.TargetId);if(i.Score>=.4){int payout=SalePayout(npc,(i.Score>=.75?PerfectSalePay:5)*VipPayout(p,npc));p.Cash+=payout;State.GrossSales+=payout;State.LevelSales+=payout;if(npc!=null)npc.Sales++;}else if(i.ReservedItemId!=""){if(CanAdd(p,i.ReservedItemId,1))Add(p,i.ReservedItemId,1);else State.Drops.Add(new DropState{Id=Id("drop"),ItemId=i.ReservedItemId,Count=1,X=p.X,Z=p.Z});}i.ReservedItemId="";}
            if(i.Kind=="Buy")SettleBuy(i,p);
            if(i.Kind=="Police"){if(i.Score>=.75)WaveOn(i.TargetId,p);else HeardOut(i.TargetId,p);}
            DeliverConversationOutcome(i,p);
        }
    }
}
