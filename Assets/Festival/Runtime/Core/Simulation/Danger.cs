using System;
using System.Collections.Generic;
namespace Festival.Core {
public sealed partial class FestivalSimulation {
    public Func<float,float,float,float,double,WorldPoint> Navigate;
    public void Tick(double deltaSeconds){if(!Finite(deltaSeconds)||deltaSeconds<0||deltaSeconds>60)throw new ArgumentOutOfRangeException(nameof(deltaSeconds));while(deltaSeconds>0){double dt=Math.Min(.1,deltaSeconds);Step(dt);deltaSeconds-=dt;}}
    void Step(double dt){State.SimulationSeconds+=dt;State.Tick++;CampEggTick();
        // MINI-1: a minigame stops the level, handoff expiry included (Minigames.cs).
        if(State.Phase==MinigamePhase){MinigameTick(dt);return;}
        foreach(var offer in State.Transfers.ToArray())if(offer.ExpiresAt<=State.SimulationSeconds)ReturnOffer(offer);
        if(State.Phase=="Shopping")
        {
            GiggleGasTick(dt);
            var connected=State.Players.FindAll(p=>p.Connected);
            if(connected.Count<1||connected.Exists(p=>!p.Ready))State.LaunchAtSeconds=0;
            else if(State.LaunchAtSeconds<=0)State.LaunchAtSeconds=State.SimulationSeconds+5;
            else if(State.SimulationSeconds>=State.LaunchAtSeconds)StartRound(connected);
            return;
        }
        if(State.Phase=="Spinning"&&State.SimulationSeconds>=State.SpinEndsAt)State.Phase="Loading";
        if(State.Phase!="Playing")return;State.ElapsedSeconds+=dt;if(EndIfLevelOver())return;KeepTripper();
        foreach(var p in State.Players){foreach(var e in p.Effects.ToArray()){e.RemainingSeconds-=dt;if(e.RemainingSeconds<=0)p.Effects.Remove(e);}if(p.Life=="Downed"){p.DownedRemaining-=dt;if(p.DownedRemaining<=0)Die(p);}if(p.DragTargetId!=""){var target=Player(p.DragTargetId);if(p.Life!="Alive"||target==null||target.Life!="Downed")p.DragTargetId="";}}
        // Evidence/arrests precede all financial settlement within a simulation tick.
        RideArtCars();foreach(var n in State.Npcs)if(n.Kind=="Cop")PoliceTick(n,dt);else WookTick(n,dt);Film(dt);GatherAtBurn(dt);
        foreach(var i in State.Interactions.ToArray())if(i.Status=="Active"){var p=Player(i.PlayerId);if(p==null||!p.Connected||(p.Life!="Alive"&&!(i.Kind=="Revival"&&p.Life=="Spirit"&&i.TargetId==p.Id))){if(p!=null)Cancel(p,"Interrupted");continue;}if(!TaskStillValid(i,p)){Cancel(p,"Moved away or target changed");continue;}if(!UntimedChat(i)&&State.SimulationSeconds>=i.StartSeconds+i.DurationSeconds+(IsRhythm(i)?.75:0)){
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(Talks(i.Kind))ChatDiagnostic?.Invoke("ChatTimeout",i.Kind,State.RoundId,State.SimulationSeconds);
#endif
            FinishInteraction(i,p);}}
        Escort(State.FriendFound,State.FriendPosition,ref State.FriendLeaderId,dt);Escort(State.SecondFriend.Found,State.SecondFriend.Position,ref State.SecondFriend.LeaderId,dt);
        FindStashes();WatchClouds();DropFoundClues();HintVisions();BodiesTick();if(State.Phase=="Playing"&&!EndIfLevelOver())WatchBooth(dt);
    }
    // PLAYA-1: a dust storm cuts everyone's sight (PLAYA-3: cops' too), and festivalgoers never see an art car's rider
    // (FestivalTwists.cs). Suspicion of someone in the burn's crush cools faster (Calm), and an accusation made there carries
    // only a few metres (ShoutRange).
    bool Sees(NpcState n,PlayerState p){double dist=Distance(n.X,n.Z,p.X,p.Z);if(dist>SightRange(n)||p.Life!="Alive"||Hidden(n,p))return false;if(dist>.1){double angle=n.Yaw*Math.PI/180;double dot=(Math.Sin(angle)*(p.X-n.X)+Math.Cos(angle)*(p.Z-n.Z))/dist;if(dot<.5)return false;}return HasLineOfSight==null||HasLineOfSight(n.X,n.Z,p.X,p.Z);}
    ObserverState Observe(NpcState n,PlayerState p){var o=n.Observers.Find(x=>x.PlayerId==p.Id);if(o==null){o=new ObserverState{PlayerId=p.Id};n.Observers.Add(o);}return o;}
    // Little Spoon has no active use. Its modest social benefit stays in host
    // rules and is intentionally absent from the public item description.
    static double WookTrustFactor(PlayerState p)=>Count(p,"little_spoon")>0 ? .9 : 1;
    // CROWD-1's pack factor scales a positive rhythm result; a gain that is neither that nor passive (a refused sale) passes packed: false.
    void Adjust(NpcState n,PlayerState p,double delta,bool packed=true){var o=Observe(n,p);o.Suspicion=Math.Max(0,Math.Min(100,o.Suspicion+(delta>0?delta*WookTrustFactor(p)*(packed?PackFactor(p):1):delta)));o.LastSeenSeconds=State.SimulationSeconds;}
    // ESC-1: passive gains (sprinting in view, standing backstage) scale by the level's suspicion multiplier; cooling off does not.
    // CROWD-1: they also scale by the seen player's pack, the two together capped at MaxCrowdHeat.
    // POLO-1: an influencer's frame is a passive gain too (FestivalTwists.cs).
    double PassiveGain(PlayerState p,double dt,double heat)=>dt*WookTrustFactor(p)*Math.Min(MaxCrowdHeat,heat*PackFactor(p));
    void WookTick(NpcState n,double dt){if(Scatter(n,dt))return;if(n.DistractedUntil>State.SimulationSeconds){n.Mode="Distracted";n.AttackAt=0;return;}double heat=Festivals.For(State).SuspicionMultiplier;ObserverState highest=null;foreach(var p in State.Players){if(!p.Connected||(p.Life!="Alive"&&p.Life!="Downed"))continue;var o=Observe(n,p);bool seen=Sees(n,p);if(seen){o.LastSeenSeconds=State.SimulationSeconds;double gain=PassiveGain(p,dt,heat);if(p.SprintUntil>State.SimulationSeconds)o.Suspicion=Math.Min(100,o.Suspicion+4*gain);if(p.Z>30)o.Suspicion=Math.Min(100,o.Suspicion+8*gain);}else if(State.SimulationSeconds-o.LastSeenSeconds>5&&o.Suspicion<70)o.Suspicion=Math.Max(0,o.Suspicion-2*dt*Calm(p));if(highest==null||o.Suspicion>highest.Suspicion)highest=o;}
        if(highest==null){n.Mode="Blending";n.TargetId="";n.Suspicion=0;return;}n.Suspicion=highest.Suspicion;n.TargetId=highest.PlayerId;var target=Player(n.TargetId);if(highest.Suspicion<70)highest.AccusationSeconds=-1;
        if(highest.Suspicion>=70&&highest.AccusationSeconds<0){highest.AccusationSeconds=State.SimulationSeconds;foreach(var other in State.Npcs)if(other!=n&&other.Kind=="Wook"&&Distance(n.X,n.Z,other.X,other.Z)<=ShoutRange(n)){var heard=Observe(other,target);heard.Suspicion=Math.Max(heard.Suspicion,70);if(heard.AccusationSeconds<0)heard.AccusationSeconds=State.SimulationSeconds;}}
        bool swarm=highest.Suspicion>=70&&((highest.Suspicion>=90&&State.SimulationSeconds-highest.AccusationSeconds>=4)||State.SimulationSeconds-highest.AccusationSeconds>=8);
        n.Mode=swarm?"Swarming":highest.Suspicion>=70?"Accusing":highest.Suspicion>=45?"Questioning":highest.Suspicion>=25?"Watching":"Blending";
        // PLAYA-1: an art-car rider is out of every festivalgoer's sight (Hidden), and none can strike them. PLAYA-4: one already
        // after them trails the car for ArtCarTrailSeconds after the boarding (Trailed): it walks after it, holds off ArtCarReach
        // from the deck, keeps facing the rider and doesn't cool. Then it loses them: it stays put and cools off as if left far behind.
        if((swarm||n.Mode=="Questioning"||n.Mode=="Accusing")&&target!=null){bool lost=Hidden(n,target),trail=lost&&Trailed(target);double dist=Distance(n.X,n.Z,target.X,target.Z);if(!lost||trail){if(dist>(lost?Festivals.ArtCarReach:1.4)){var point=MovePursuer(n,target.X,target.Z,lost?Math.Min(1.7*dt,dist-Festivals.ArtCarReach):(swarm?3.6:1.7)*dt);n.X=point.X;n.Z=point.Z;}n.Yaw=(float)(Math.Atan2(target.X-n.X,target.Z-n.Z)*180/Math.PI);}
            if(swarm&&!lost&&target.Life=="Alive"&&target.RecoveryUntil<=State.SimulationSeconds&&dist<=1.5&&ActiveAttacker(n,target)){if(n.AttackAt==0&&State.SimulationSeconds>=n.AttackCooldownUntil)n.AttackAt=State.SimulationSeconds+.75;if(n.AttackAt>0&&State.SimulationSeconds>=n.AttackAt){if(HasLineOfSight==null||HasLineOfSight(n.X,n.Z,target.X,target.Z)){target.Health=Math.Max(0,target.Health-20);if(target.Health==0){Cancel(target,"Downed");ReturnOffers(target.Id);target.Life="Downed";target.DownedRemaining=35;}}n.AttackAt=0;n.AttackCooldownUntil=State.SimulationSeconds+2;}}else n.AttackAt=0;
            if(dist>16||(lost&&!trail)){highest.Suspicion=Math.Max(0,highest.Suspicion-8*dt*Calm(target));if(highest.Suspicion<70)highest.AccusationSeconds=-1;}
        }
    }
    bool ActiveAttacker(NpcState n,PlayerState target){int rank=0;foreach(var other in State.Npcs){if(other.Kind=="Wook"&&other.TargetId==target.Id&&other.Mode=="Swarming"&&other.DistractedUntil<=State.SimulationSeconds&&Distance(other.X,other.Z,target.X,target.Z)<=1.5){if(other==n)return rank<6;rank++;}}return false;}
    WorldPoint Move(float x,float z,float tx,float tz,double step){if(Navigate!=null){var result=Navigate(x,z,tx,tz,step);if(result!=null&&Finite(result.X)&&Finite(result.Z)&&Distance(x,z,result.X,result.Z)<=step+.05)return result;return new WorldPoint(x,z);}double d=Distance(x,z,tx,tz);if(d<=step)return new WorldPoint(tx,tz);return new WorldPoint(x+(float)((tx-x)*step/d),z+(float)((tz-z)*step/d));}
    void RecordDeal(NpcState cop,PlayerState p){var e=cop.Evidence.Find(x=>x.PlayerId==p.Id);if(e==null){e=new EvidenceState{PlayerId=p.Id};cop.Evidence.Add(e);}e.Kind="WitnessedDeal";e.DetainAt=State.SimulationSeconds+4;}
    // ESC-2 (SecurityStops.cs): being downed, detained or gone ends a stop; an arrest warning stands. A stopping cop follows
    // only while the player is within its sight range (12 m, 5 m in a dust storm); one with an arrest to make, all the way.
    void PoliceTick(NpcState cop,double dt){foreach(var p in State.Players){var e=cop.Evidence.Find(x=>x.PlayerId==p.Id);if(!p.Connected||p.Life!="Alive"){if(e!=null&&!Arrests(e))cop.Evidence.Remove(e);continue;}if(e==null&&StockVisible(p)&&Sees(cop,p))e=StartStop(cop,p);if(e==null||!Pursue(cop,p,e,dt))continue;cop.TargetId=p.Id;cop.Mode=Arrests(e)?"ArrestWarning":"Stop";if(Arrests(e)&&State.SimulationSeconds>=e.DetainAt){Detain(p);cop.Mode="Patrol";return;}double dist=Distance(cop.X,cop.Z,p.X,p.Z);if(dist>2&&(Arrests(e)||dist<=SightRange(cop))){var point=Move(cop.X,cop.Z,p.X,p.Z,3*dt);cop.X=point.X;cop.Z=point.Z;}return;}if(!Confiscate(cop,dt))Patrol(cop,dt);}
    void Detain(PlayerState p){if(p.Life!="Alive")return;var i=Interaction(p.InteractionId);if(i!=null&&i.Kind=="Sale")i.ReservedItemId="";Cancel(p,"Police bust");ReturnOffers(p.Id);p.Inventory.RemoveAll(x=>Stock(x.ItemId));if(!p.Inventory.Exists(x=>x.ItemId==p.EquippedItemId))p.EquippedItemId="";p.Cash-=Math.Min(p.Cash,10);p.Life="Detained";p.EscapeProgress=0;p.X=27;p.Z=5;p.DragTargetId="";}
    void ClearEvidence(string id){foreach(var n in State.Npcs)n.Evidence.RemoveAll(e=>e.PlayerId==id);}
    public const double DeathWheelSeconds=2.5;
    // The independent death stream freezes only after cancellation/refunds and returned handoffs.
    void FreezeDeathCash(PlayerState p)
    {
        if(p.Cash<=0)return;
        p.DeathSpinAt=State.SimulationSeconds;p.DeathSpinSeed=unchecked(State.SpinSeed*67+(int)State.Tick*8+p.Ordinal);
        p.DeathSpinAmount=p.Cash;p.DeathCashDropped=new ContentRandom(p.DeathSpinSeed).Next(4)==0?p.Cash:0;p.DeathSpinSequence++;
        p.DeathSpinRoundId=State.RoundId;p.DeathSpinId=State.RoundId+":death:"+p.Id+":"+p.DeathSpinSequence;p.DeathSpinName=p.Name;
        if(p.DeathCashDropped>0){p.Cash=0;State.Drops.Add(new DropState{Id=Id("cash"),ItemId="cash",OwnerId=p.Id,Count=p.DeathCashDropped,X=p.X,Z=p.Z,LandsAt=p.DeathSpinAt+DeathWheelSeconds});}
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ChatDiagnostic?.Invoke("DeathCash","Frozen",State.RoundId,State.SimulationSeconds);
#endif
    }
    // A death drops gear and wristbands where the player fell; a quarter of positive-cash deaths also drops their cash.
    // Additive snapshot fields must not invent an event, overflow a sequence, or duplicate a financial claim.
    static void ValidateDeathState(RoundState state)
    {
        foreach(var p in state.Players)
        {
            bool spin=p.DeathSpinSequence>0;
            if(!Finite(p.DeathSpinAt)||p.DeathSpinSequence<0||p.DeathSpinSequence>3||p.DeathSpinAmount<0||p.DeathCashDropped<0||p.DeathCashDropped!=0&&p.DeathCashDropped!=p.DeathSpinAmount
                ||p.DeathSpinId==null||p.DeathSpinRoundId==null||p.DeathSpinName==null||p.DeathSpinName.Length>24
                ||spin&&(p.DeathSpinAt<0||p.DeathSpinAmount==0||p.DeathSpinRoundId!=state.RoundId||p.DeathSpinId!=state.RoundId+":death:"+p.Id+":"+p.DeathSpinSequence)
                ||!spin&&(p.DeathSpinAt!=-1||p.DeathSpinAmount!=0||p.DeathCashDropped!=0||p.DeathSpinSeed!=0||p.DeathSpinId!=""||p.DeathSpinRoundId!=""||p.DeathSpinName!="")
                ||p.GearLossSequence<0||p.GearLossSequence>3||p.GearLossKind!=(p.GearLossSequence>0?"Death":"")||p.GearLossNotice!=(p.GearLossSequence>0?DeathGearLossNotice:""))throw new ArgumentException("Invalid snapshot death event");
        }
        var cashIds=new HashSet<string>();
        foreach(var drop in state.Drops)
        {
            if(drop==null||!Finite(drop.LandsAt)||drop.LandsAt<0)throw new ArgumentException("Invalid snapshot drop landing");
            if(drop.ItemId!="cash")continue;
            if(string.IsNullOrEmpty(drop.Id)||drop.Count<=0||!Finite(drop.X)||!Finite(drop.Z)||!cashIds.Add(drop.Id)||state.Drops.Exists(other=>other!=drop&&other.Id==drop.Id))throw new ArgumentException("Invalid snapshot cash pile");
        }
    }
    public const string DeathGearLossNotice="Your gear dropped where you fell: a friend can grab it";
    void Die(PlayerState p)
    {
        if(p.Life!="Downed")return;
        var active=Interaction(p.InteractionId);
        bool dropsGear=p.Inventory.Exists(stack=>stack.Count>0)||(active!=null&&active.Status=="Active"&&active.Kind=="Sale"&&active.ReservedItemId!="")||State.Transfers.Exists(t=>t.FromId==p.Id&&t.Amount>0);
        Cancel(p,"Death");ReturnOffers(p.Id);FreezeDeathCash(p);
        foreach(var stack in p.Inventory)if(stack.Count>0)State.Drops.Add(new DropState{Id=Id("drop"),ItemId=stack.ItemId,Count=stack.Count,X=p.X,Z=p.Z});
        p.Inventory.Clear();p.EquippedItemId="";
        if(dropsGear)
        {
            p.GearLossSequence++;p.GearLossKind="Death";p.GearLossNotice=DeathGearLossNotice;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ChatDiagnostic?.Invoke("GearLoss","Death",State.RoundId,State.SimulationSeconds);
#endif
        }
        foreach(var band in p.Wristbands)State.Drops.Add(new DropState{Id=Id("band"),ItemId="wristband",OwnerId=band,Count=1,X=p.X,Z=p.Z});
        p.Wristbands.Clear();p.Effects.Clear();p.Life="Spirit";LeaveBody(p);ClearEvidence(p.Id);
        if(!State.Drops.Exists(d=>d.ItemId=="wristband"&&d.OwnerId==p.Id)&&!State.Players.Exists(x=>x.Wristbands.Contains(p.Id)))State.Drops.Add(new DropState{Id=Id("band"),ItemId="wristband",OwnerId=p.Id,Count=1,X=p.X,Z=p.Z});
    }
    bool AttackerNear(PlayerState p,double range){return State.Npcs.Exists(n=>n.Kind=="Wook"&&n.Mode=="Swarming"&&n.DistractedUntil<=State.SimulationSeconds&&Distance(n.X,n.Z,p.X,p.Z)<=range);}
    CommandResult Drag(PlayerState p,GameCommand c){var t=Player(c.TargetId);if(t==null||t.Life!="Downed"||p.CarryBodyId!=""||!Near(p,t.X,t.Z)||State.Players.Exists(x=>x!=p&&x.DragTargetId==t.Id))return Reject("Choose nearby unclaimed downed friend");p.DragTargetId=p.DragTargetId==t.Id?"":t.Id;return Ok();}
    CommandResult Rescue(PlayerState p,GameCommand c){var t=Player(c.TargetId);if(t==null||t.Life!="Downed"||!Near(p,t.X,t.Z)||AttackerNear(t,3)||RecoveryClaimed(t.Id))return Reject("Distract attackers and reach downed friend");return BeginTask(p,"Rescue",t.Id,3);}
    bool RecoveryClaimed(string id){return State.Interactions.Exists(i=>i.Status=="Active"&&i.TargetId==id&&(i.Kind=="Rescue"||i.Kind=="Revival"||i.Kind=="Release"));}
    CommandResult Release(PlayerState p,GameCommand c){var t=Player(c.TargetId);bool paid=c.Amount==1;if(t==null||t.Life!="Detained"||RecoveryClaimed(t.Id)||!Near(p,paid?27:-28,paid?5:16)||(paid&&p.Cash<10))return Reject("Release: $10 at holding desk or free task at lost property");var i=NewInteraction(p,"Release",t.Id,paid?3:10);if(paid){p.Cash-=10;i.ReservedCash=10;}return Ok();}
    CommandResult Revive(PlayerState p,GameCommand c){var t=Player(c.TargetId);bool paid=c.Amount==1;bool self=t==p&&State.Players.FindAll(x=>x.Connected).Count==1;if(State.Phase!="Playing"||t==null||t.Life!="Spirit"||t.RevivalCount>=2||(!self&&!p.Wristbands.Contains(t.Id))||!Near(p,24,-20)||RecoveryClaimed(t.Id)||(paid&&p.Cash<10)||p.Life!=(self?"Spirit":"Alive"))return Reject("Bring a wristband to medical; a lone spirit can complete a longer self-revival task");var i=NewInteraction(p,"Revival",t.Id,self?15:paid?3:10);if(paid){p.Cash-=10;i.ReservedCash=10;}return Ok();}
    void End(string result){if(State.Phase=="Results")return;State.Phase="Results";State.Result=result;if(result=="Success"&&!State.RewardCommitted){State.RewardCommitted=true;State.Survivors=State.Players.FindAll(p=>p.Connected&&p.Life=="Alive"&&Near(p,Festivals.CampGateX,Festivals.CampGateZ,Festivals.CampGateRadius)).Count;State.SurvivorBonus=Math.Max(0,State.Survivors-1)*10;State.StashCash+=State.SurvivorBonus;foreach(var p in State.Players)p.HasCosmetic=true;UnlockNextFestival(State);}foreach(var p in State.Players){Cancel(p,"Round ended");p.Badge="";}foreach(var t in State.Transfers.ToArray())ReturnOffer(t);foreach(var n in State.Npcs)if(n.Kind=="Cop"){n.Evidence.Clear();n.Mode="Patrol";n.TargetId="";}}
}
}
