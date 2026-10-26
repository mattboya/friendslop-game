using System;
namespace Festival.Core
{
    /// <summary>DIALOG-3: selling (Sale), buying (Buy, ECON-2), chatting (Conversation) and talking to security (Police) are talks,
    /// not dances. A talk is ConversationTurns turns, one for each of its rounds (ConversationLines.Rounds: the greeting, small
    /// talk, the ask and the close, each with its own replies): they say a line (their reaction to your last reply first), and you
    /// pick one of three replies with the Reply command (Amount the reply 0-2, Direction the turn 0-3). DIALOG-9 waits for each pick;
    /// only Police turns count silence after ReplySeconds. One reply is smooth, one so-so and one awkward by the person's taste
    /// (ConversationLines),
    /// and a dose garbles one of them a turn, two from dose 3. Each reply is worth Points (smooth 2, so-so 1, awkward or nothing
    /// -1), and the total over TalkPoints, never below 0, is the talk's Score, which settles it as the dance used to: a sale's pay,
    /// a buy, security waving off a stop, and every partner's and watcher's RhythmDelta. A festivalgoer wary of you (TrustDanceSuspicion), or one of the ravers and burners who always are,
    /// first asks for a dance (TrustAsk): eight beats on the talk's own chart, danced and watched like any dance. The talk always
    /// follows, and their first line reacts to the dance (ConversationLines.DanceReaction). Under TrustScore the talk starts
    /// FailedDanceStart points down (InteractionState.HeadStart); at NailedScore or better round 1's smooth reply is hinted to
    /// the talker alone (InteractionState.HintedReply); in between it starts level. A chat or a buy reads the
    /// person: a reply that lands draws out a line with their role's tell, more often the smoother it was (TellOdds), and the
    /// first tell shown sets the Hunch the talk leaves, as its last turn begins. A narc asked to sell talks too, its tells in its
    /// lines; finishing the buy busts you (StockBuys.cs).</summary>
    public sealed partial class FestivalSimulation
    {
        public const int ConversationTurns=4;
        public const double ReplySeconds=4,TrustDanceSuspicion=40,TrustScore=.6,NailedScore=.9;
        public const int FailedDanceStart=-2;
        public const string TrustAsk="Dance with me first, then we talk.";
        /// <summary>What a reply of this grade (ConversationLines.Smooth, SoSo, Awkward or Silence) is worth. A talk's Score is its
        /// points over TalkPoints, never below 0, so every line keeps its place: a Fair sale's full price at 6, a buy at 4, security
        /// waving you on at 6.</summary>
        public static int Points(int grade)=>grade==ConversationLines.Smooth?2:grade==ConversationLines.SoSo?1:-1;
        public const int TalkPoints=2*ConversationTurns;
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
            if(kind=="Sale"){i.ReservedEquipped=p.EquippedItemId==c.ItemId;i.ReservedSlot=p.Inventory.FindIndex(s=>s.ItemId==c.ItemId);Take(p,c.ItemId,1);i.ReservedItemId=c.ItemId;i.Ask=c.Amount;}
            if(kind=="Buy")HoldPrice(p,i);
            if(kind=="Sale"||kind=="Buy")foreach(var cop in State.Npcs)if(cop.Kind=="Cop"&&Sees(cop,p))RecordDeal(cop,p);
            if(wary){i.TrustDance=true;ScheduleStageRhythm(i);i.DialogueText=TrustAsk;PublishSpeech(p,npc.Id,"FESTIVALGOER",TrustAsk,i.StartSeconds+i.DurationSeconds-State.SimulationSeconds,i.Id);}else AddTurn(i,p);
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
            turn.Pick=c.Amount;PublishSpeech(p,p.Id,p.Name,turn.Replies[c.Amount],7,i.Id);
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
            PublishSpeech(p,i.TargetId,i.Kind=="Police"?"SECURITY":"FESTIVALGOER",i.Turns[t].Line,7,i.Id);
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
                // K2: the talk that follows roots p, so the dance lifts no one to WookTick's accusing 70 (a watcher's shout would
                // carry it to the partner); the talk's own Score moves them again once p is free.
                foreach(var n in State.Npcs)if(i.WitnessIds.Contains(n.Id)||n.Id==i.TargetId){var o=Observe(n,p);double before=o.Suspicion;Adjust(n,p,delta);if(before<70&&o.Suspicion>=70)o.Suspicion=69;}
                i.HeadStart=danced<TrustScore?FailedDanceStart:0;
                i.DialogueText="";i.DurationSeconds=State.SimulationSeconds-i.StartSeconds;AddTurn(i,p);
                var first=i.Turns[0];var history=new DialogueHistory{Seen=new System.Collections.Generic.List<string>(p.Dialogue.Seen??new System.Collections.Generic.List<string>())};
                first.Reaction=ConversationLines.DanceReaction(danced<TrustScore?0:danced<NailedScore?1:2,State.FestivalIndex,new ContentRandom(unchecked(TurnSeed(i,0)^0x44616e63)),history,out var key);first.GrammarKeys.Add(key);
                if(danced>=NailedScore)i.HintedReply=Array.IndexOf(first.Grades,ConversationLines.Smooth);
                return false;
            }
            if(i.Turns.Count<ConversationTurns){AddTurn(i,p);return false;}
            int total=i.HeadStart;foreach(var turn in i.Turns)total+=Points(Graded(turn));
            i.Score=Math.Max(0,total)/(double)TalkPoints;p.LastRhythmScore=i.Score;p.Performances++;
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
        // A notice given replaces the authored one. A cut talk (Cancel) has no last line of theirs, so it leaves the speech alone.
        void DeliverConversationOutcome(InteractionState i,PlayerState p,string notice="",string result="",bool cut=false)
        {
            if(p.ConversationThreadId=="")p.ConversationThreadId=i.Id;
            if(p.ConversationOutcomes==null)p.ConversationOutcomes=new System.Collections.Generic.List<ConversationOutcome>();
            if(p.ConversationOutcomes.Exists(o=>o.RoundId==State.RoundId&&o.InteractionId==i.Id))return;
            var outcome=new ConversationOutcome{RoundId=State.RoundId,InteractionId=i.Id,DeliveryId=State.RoundId+":"+i.Id,Kind=i.Kind,TargetId=i.TargetId,Speaker=i.Kind=="Police"?"SECURITY":"FESTIVALGOER",Hunch=i.Hunch,Result=result,Score=cut?-1:i.Score,Sequence=++p.ConversationOutcomeSequence};
            if(cut){}
            else
            {
                var npc=State.Npcs.Find(n=>n.Id==i.TargetId);string persona=i.Kind=="Police"?ConversationLines.Security:DialogueGrammar.PersonaFor(i.TargetId,npc?.Twist??"");
                var history=new DialogueHistory{Seen=new System.Collections.Generic.List<string>(p.Dialogue.Seen??new System.Collections.Generic.List<string>())};
                outcome.Reaction=ConversationLines.Reaction(persona,Graded(i.Turns[i.Turns.Count-1]),State.FestivalIndex,new ContentRandom(unchecked(TurnSeed(i,ConversationTurns)^0x4f757463)),history,out var key);outcome.GrammarKeys.Add(key);
                if(i.Status=="Police bust")outcome.Notice=NarcBust;
            }
            if(notice!="")outcome.Notice=notice;
            p.ConversationOutcomes.Add(outcome);while(p.ConversationOutcomes.Count>8)p.ConversationOutcomes.RemoveAt(0);
            if(!cut&&i.Kind!="Police"){p.SpeechDeliveryId="";p.SpeechById="";p.SpeechSequence=0;p.SpeechAtWorldSeconds=p.SpeechUntilWorldSeconds=-1;p.NpcSpeaker=outcome.Speaker;p.NpcSpeech=outcome.Notice==NarcBust?NarcBust:outcome.Reaction;p.NpcSpeechUntil=State.SimulationSeconds+7;}
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ChatDiagnostic?.Invoke("OutcomeDelivered",i.Kind,State.RoundId,State.SimulationSeconds);
#endif
        }
        // What a finished talk settles on its Score: a sale pays by its ask (SalePay), or else its stock comes back and a pushed ask
        // costs suspicion; a buy (StockBuys.cs); and security at .75 waves off its stop, or gives the warning it held back (HeardOut).
        void SettleTalk(InteractionState i,PlayerState p)
        {
            string notice="",result="";
            if(i.Kind=="Sale")
            {
                var npc=State.Npcs.Find(n=>n.Id==i.TargetId);int pay=SalePay(i.Ask,i.Score);
                if(pay>0)
                {
                    int payout=SalePayout(npc,pay*VipPayout(State,p,npc)),full=SalePayout(npc,SalePay(i.Ask,1)*VipPayout(State,p,npc));string name=Catalog.FindItem(i.ReservedItemId)?.Name??"stock";
                    p.Cash+=payout;State.GrossSales+=payout;State.LevelSales+=payout;CrewNews(p,"Sales",MoneyNotice("SALES",payout)+"  •  "+p.Name+" sold "+name);if(npc!=null)npc.Sales++;
                    notice=MoneyNotice("SOLD "+name,payout)+(pay<SalePay(i.Ask,1)?"  •  talked down from $"+full:"");result="SOLD +$"+payout;i.ReservedItemId="";
                }
                else{PushedAskFailed(i,p);if(i.ReservedItemId!=""){notice=HandBack(i,p,"NO SALE");result="NO SALE";}}
            }
            if(i.Kind=="Buy")SettleBuy(i,p);
            if(i.Kind=="Police"){if(i.Score>=.75)WaveOn(i.TargetId,p);else HeardOut(i.TargetId,p);}
            DeliverConversationOutcome(i,p,notice,result);
        }
        // A sale's unit back from a failed or cut talk: in the seller's gear (a last unit in the slot its stack left), in their hand
        // again if it left it, or at their feet when their hands filled meanwhile. Returns head and where the unit went.
        string HandBack(InteractionState i,PlayerState p,string head)
        {
            string item=i.ReservedItemId,name=Catalog.FindItem(item)?.Name??item;i.ReservedItemId="";
            if(!CanAdd(p,item,1)){State.Drops.Add(new DropState{Id=Id("drop"),ItemId=item,Count=1,X=p.X,Z=p.Z});return head+"  •  "+name+" dropped at your feet";}
            if(Count(p,item)==0&&i.ReservedSlot>=0)p.Inventory.Insert(Math.Min(i.ReservedSlot,p.Inventory.Count),new ItemStack{ItemId=item,Count=1});else Add(p,item,1);
            if(i.ReservedEquipped)p.EquippedItemId=item;return head+"  •  "+name+(p.EquippedItemId==item?" back in your hand":" back in your gear");
        }
    }
}
