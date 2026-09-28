using System;
using System.Collections.Generic;
namespace Festival.Core
{
    /// <summary>Single-writer authoritative rules. Transport binds player identity and authenticates reconnects.</summary>
    public sealed partial class FestivalSimulation
    {
        public RoundState State { get; private set; }
        // Supply host geometry raycast. The pure domain defaults to unobstructed test geometry.
        public Func<float,float,float,float,bool> HasLineOfSight;
        public FestivalSimulation(int seed=1) { State=CreateRound(seed); }
        static RoundState CreateRound(int seed) {
            var s=new RoundState {Seed=seed,RoundId=Guid.NewGuid().ToString("N"),VendorOffers=Catalog.VendorOffers(seed)};
            foreach(var item in s.VendorOffers)s.ShopStock.Add(new ShopStockState{ItemId=item,CampAvailable=Catalog.ShopCopies(item,2),MarketAvailable=Catalog.ShopCopies(item,2)});
            var positions=new[]{new WorldPoint(-24,25),new WorldPoint(25,24),new WorldPoint(18,5)};
            s.FriendPosition=positions[(seed&int.MaxValue)%3];
            for(int i=0;i<FestivalCrowdLayout.Count;i++) { var point=FestivalCrowdLayout.Get(i,seed);bool wide=(i+seed%7)%7==0; s.Npcs.Add(new NpcState {Id="wook_"+i,X=point.X,Z=point.Z,Yaw=point.Yaw,IdlePose=point.Pose,HighlyIntoxicated=wide,RedEyes=(i+seed%5)%5==0||wide&&i<7,CanTalk=(i+seed%3)%4==0}); }
            s.Npcs.Add(new NpcState{Id="cop_0",Kind="Cop",X=20,Z=-4,Yaw=180}); s.Npcs.Add(new NpcState{Id="cop_1",Kind="Cop",X=-12,Z=-12,Yaw=180});
            s.Stashes.Add(new StashState{Id="stash",X=-25,Z=-8}); return s;
        }
        public PlayerState AddPlayer(string id,string name) {
            if(string.IsNullOrWhiteSpace(id)||id.Length>128) throw new ArgumentException("Invalid identity");
            var existing=Player(id); if(existing!=null) {existing.Connected=true;return existing;}
            if(State.Players.Count>=8) throw new InvalidOperationException("Festival is full (8 players including host).");
            if(State.Phase!="Lobby"&&State.Phase!="Shopping") throw new InvalidOperationException("Round already started; reconnect with your session token.");
            int campIndex=State.Players.Count;
            var p=new PlayerState{Id=id,Name=string.IsNullOrWhiteSpace(name)?"Friend":name.Substring(0,Math.Min(24,name.Length)),X=campIndex%2==0?-1:1,Z=-9-2*(campIndex/2)};
            State.Players.Add(p); if(State.HostPlayerId=="") State.HostPlayerId=id;
            foreach(var stock in State.ShopStock)
            {
                if(Catalog.RareShopItem(stock.ItemId))continue;
                int before=Catalog.ShopCopies(stock.ItemId,State.Players.Count-1),after=Catalog.ShopCopies(stock.ItemId,State.Players.Count);
                stock.CampAvailable+=after-before;stock.MarketAvailable+=after-before;
            }
            return p;
        }
        public void Disconnect(string id) {var p=Player(id);if(p==null)return; Cancel(p,"Disconnected");ReturnHeldOffer(p);p.Connected=false;p.Ready=false;p.DragTargetId="";State.LaunchAtSeconds=0;if(State.FriendLeaderId==id)State.FriendLeaderId=""; ReturnOffers(id);}
        public PlayerState Player(string id) {return State.Players.Find(p=>p.Id==id);}
        public InteractionState Interaction(string id) {return State.Interactions.Find(i=>i.Id==id);}
        public void Restore(RoundState state) {
            if(state==null||state.SchemaVersion!=1||state.Players==null||state.Players.Count>8||!Finite(state.SimulationSeconds)||!Finite(state.DurationSeconds)||state.DurationSeconds<=0)throw new ArgumentException("Unsupported or invalid snapshot");
            var ids=new HashSet<string>();foreach(var p in state.Players)if(p==null||!ids.Add(p.Id)||p.Cash<0||p.Inventory==null||p.Effects==null||!Finite(p.X)||!Finite(p.Z))throw new ArgumentException("Invalid snapshot player");
            if(state.Npcs==null||state.Interactions==null||state.Commands==null||state.Drops==null||state.Transfers==null||state.Stashes==null||state.VendorOffers==null||state.ShopStock==null||state.FriendPosition==null||state.ReviewVotes==null||state.StashCash<0||state.GrossSales<0)throw new ArgumentException("Incomplete snapshot");
            State=state;
        }
        public CommandResult Execute(string playerId,GameCommand command) {
            if(command==null||string.IsNullOrEmpty(command.Id)||command.Id.Length>128||command.Kind==null||command.Kind.Length>32||command.TargetId==null||command.TargetId.Length>128||command.ItemId==null||command.ItemId.Length>64||!Finite(command.TimeSeconds))return Reject("Invalid command envelope");
            var prior=State.Commands.Find(c=>c.PlayerId==playerId&&c.Id==command.Id);if(prior!=null)return prior.Result;
            var p=Player(playerId); if(p==null||!p.Connected)return Reject("Unknown or disconnected player");
            // Refuse new commands rather than evict dedupe evidence and make old financial requests replayable.
            if(State.Commands.Count>=50000)return Reject("Round command limit reached");
            var result=Apply(p,command);result.Id=command.Id;result.Sequence=++State.TransactionSequence;
            State.Commands.Add(new CommittedCommand{PlayerId=playerId,Id=command.Id,Result=result});return result;
        }
        CommandResult Apply(PlayerState p,GameCommand c) {
            if(c.Kind=="Ready") {if(State.Phase!="Shopping"&&State.Phase!="Lobby")return Reject("Not shopping");if(!Near(p,0,19,3.2))return Reject("Ready at the lit trailhead gate");p.Ready=!p.Ready;State.LaunchAtSeconds=0;return Ok(p.Ready?"Ready at the trailhead":"Not ready");}
            if(c.Kind=="Start") {
                if(p.Id!=State.HostPlayerId)return Reject("Only host starts rounds");
                if(State.Phase!="Shopping"&&State.Phase!="Lobby")return Reject("Round already started");
                int required=1;
                var connected=State.Players.FindAll(x=>x.Connected);if(connected.Count<required||connected.Exists(x=>!x.Ready))return Reject("Every connected player must be ready at the trailhead");
                StartRound(connected);return Ok();
            }
            if(c.Kind=="MapReady") {if(State.Phase!="Loading")return Reject("Map is not loading");p.MapReady=true;if(!State.Players.Exists(x=>x.Connected&&!x.MapReady)){for(int index=0;index<State.Players.Count;index++){var teammate=State.Players[index];teammate.X=-7+2*index;teammate.Z=-29;}State.Phase="Playing";}return Ok();}
            if(c.Kind=="Reset") {if(p.Id!=State.HostPlayerId||State.Phase!="Results")return Reject("Host can bring the crew back after results");BeginCampReview();return Ok("Back at camp: review the round before shopping");}
            if(c.Kind=="ReviewVote")return VoteForReview(p,c);
            if(c.Kind=="FinishReview")return FinishCampReview(p);
            if(c.Kind=="EnterCamp"||c.Kind=="ExitCamp"||c.Kind=="CampAntic"||c.Kind=="ChooseCampTrack")return CampAction(p,c);
            if(c.Kind=="DialogueAck") {var current=Interaction(p.InteractionId);if(current==null||current.DialogueId!=c.TargetId)return Reject("Dialogue is not active");p.Dialogue.Acknowledge(c.TargetId,current.Id);return Ok();}
            if(c.Kind=="Cancel") {Cancel(p,"Cancelled");return Ok();}
            if(c.Kind=="Rhythm")return Submit(p,c);
            if(c.Kind=="Chime") {if(p.Life!="Spirit"||!Near(p,24,-20)||State.SimulationSeconds<p.ChimeUntil)return Reject("Chime requires medical memorial and cooldown");p.ChimeUntil=State.SimulationSeconds+10;return Ok("Memorial chime");}
            if(c.Kind=="HelpSelf")return HelpSelf(p);
            if(c.Kind=="BeginRevival"&&p.Life=="Spirit")return Revive(p,c);
            if(p.Life!="Alive")return Reject("Requires a living, free player");
            if(State.Phase!="Playing"&&State.Phase!="Shopping")return Reject("Round is not interactive");
            if(p.CampVisitId!="")return Reject("Leave the camp interior first");
            if(c.Kind=="HoldOffer")return HoldOffer(p,c);
            if(c.Kind=="ReturnOffer")return ReturnHeldOffer(p);
            if(c.Kind=="Buy")return Buy(p,c);
            if(c.Kind=="Equip")return Equip(p,c);
            if(State.Phase!="Playing")return Reject("Start the round first");
            if(p.InteractionId!="")return Reject("Finish or cancel the current interaction");
            switch(c.Kind) {
                case "ReadClue":return ReadClue(p);
                case "ClueSupply":if(!Near(p,-18,-22)||State.GateOpened||p.Effects.Count>0)return Reject("Visit the night market with no active effect for a free clue tasting");p.Effects.Add(new ActiveEffect{Id="mushrooms",InstanceId=Id("effect"),SourceCommandId=c.Id,StartSeconds=State.SimulationSeconds,RemainingSeconds=90});return Ok("You can read the totems for 90 seconds. Bring a sober friend.");
                case "Consume": return Consume(p,c);
                case "Use": return Use(p,c);
                case "Drop": return Drop(p,c);
                case "Pickup": return Pickup(p,c);
                case "Transfer":return Transfer(p,c);
                case "AcceptTransfer":return AcceptTransfer(p,c);
                case "Deposit":case "Withdraw":return Stash(p,c);
                case "StartSale":return BeginChallenge(p,c,"Sale");
                case "Dance":return BeginChallenge(p,c,"Dance");
                case "Conversation":return BeginChallenge(p,c,"Conversation");
                case "Talk":return Talk(p,c);
                case "Police":return BeginChallenge(p,c,"Police");
                case "Poi":return BeginPoi(p,c);
                case "Dj":return BeginDj(p,c);
                case "FindFriend":if(!State.GateOpened)return Reject("Interpret both totems and complete a dance to locate your friend");if(!Near(p,State.FriendPosition.X,State.FriendPosition.Z))return Reject("Move closer to the missing friend");return BeginTask(p,"FindFriend","friend",2);
                case "Extract":if(!State.FriendFound||Distance(State.FriendPosition.X,State.FriendPosition.Z,0,-32)>3||!Near(p,0,-32))return Reject("Bring the friend and a living survivor to the shuttle");return BeginTask(p,"Extract","shuttle",3);
                case "LostProperty":if(!Near(p,-28,16))return Reject("Find lost property marker");return BeginTask(p,"LostProperty",State.LostPropertyTask.ToString(),5);
                case "Drag":return Drag(p,c);
                case "Rescue":return Rescue(p,c);
                case "BeginRelease":return Release(p,c);
                case "BeginRevival":return Revive(p,c);
                default:return Reject("Unknown action");
            }
        }
        public bool TryMove(string playerId,float x,float z,float yaw,double deltaSeconds) {
            var p=Player(playerId);if(p==null||!p.Connected||(State.Phase!="Shopping"&&State.Phase!="CampReview"&&State.Phase!="Playing")||!Finite(x)||!Finite(z)||!Finite(yaw)||!Finite(deltaSeconds)||deltaSeconds<=0||deltaSeconds>.5)return false;
            if(p.CampVisitId!="")
            {
                var site=CampFeatures.Find(p.CampVisitId);if(site==null)return false;
                float roomZ=CampFeatures.InteriorSlotZ(site);
                float roomX=CampFeatures.InteriorSlotX(site);
                if(Math.Abs(x-roomX)>2.3f||z-roomZ>2.3f||z-roomZ< -2.7f)return false;
                if(Distance(p.CampInteriorX,p.CampInteriorZ,x,z)>4.2*deltaSeconds+.03)return false;
                // The front wall has a visible doorway. Crossing its center
                // returns the player to the same outdoor door they entered.
                if(z-roomZ< -2.35f&&Math.Abs(x-roomX)<.9f)
                {
                    LeaveCamp(p,site);p.Yaw=yaw%360;
                    return true;
                }
                p.CampInteriorX=x;p.CampInteriorZ=z;p.Yaw=yaw%360;return true;
            }
            if(Math.Abs(x)>39||Math.Abs(z)>39)return false;
            if(State.Phase=="Shopping"&&p.Ready&&Distance(p.X,p.Z,x,z)>.001)return false;
            double speed=6*Intoxication.MovementMultiplier(p);if(p.InteractionId!="")speed=1;if(p.DragTargetId!="")speed=2;
            if(p.Life=="Downed")speed=.8;
            double distance=Distance(p.X,p.Z,x,z);if(distance>speed*deltaSeconds+.03)return false;
            if(p.Life=="Detained"&&Distance(x,z,27,5)>3)return false;
            if(distance/deltaSeconds>4.2)p.SprintUntil=State.SimulationSeconds+.3;
            p.X=x;p.Z=z;p.Yaw=yaw%360;var target=Player(p.DragTargetId);if(target!=null&&target.Life=="Downed"){target.X=x-1;target.Z=z;}return true;
        }
        static bool Finite(double d){return !double.IsNaN(d)&&!double.IsInfinity(d);}
        static double Distance(float x,float z,float xx,float zz){double a=x-xx,b=z-zz;return Math.Sqrt(a*a+b*b);}
        static bool Near(PlayerState p,float x,float z,double range=2.5){return Distance(p.X,p.Z,x,z)<=range;}
        static CommandResult Ok(string reason="Accepted"){return new CommandResult{Accepted=true,Reason=reason};}
        static CommandResult Reject(string reason){return new CommandResult{Reason=reason};}
        string Id(string prefix){return prefix+"_"+(++State.EntitySequence);}
        static int Count(PlayerState p,string item){return p.Inventory.Find(i=>i.ItemId==item)?.Count??0;}
        static bool Stock(string id){return id=="stock_lsd"||id=="stock_mushrooms";}
        static bool CanAdd(PlayerState p,string item,int count) {var def=Catalog.FindItem(item);if(def==null||count<=0)return false;var stack=p.Inventory.Find(i=>i.ItemId==item);if(stack!=null)return stack.Count+count<=def.StackLimit;if(count>def.StackLimit)return false;if(item=="little_spoon")return true;int handSlots=0;foreach(var owned in p.Inventory)if(owned.ItemId!="little_spoon")handSlots++;return handSlots<3;}
        static void Add(PlayerState p,string item,int count){var s=p.Inventory.Find(i=>i.ItemId==item);if(s==null)p.Inventory.Add(new ItemStack{ItemId=item,Count=count});else s.Count+=count;}
        static void Take(PlayerState p,string item,int count){var s=p.Inventory.Find(i=>i.ItemId==item);s.Count-=count;if(s.Count==0){p.Inventory.Remove(s);if(p.EquippedItemId==item)p.EquippedItemId=p.Inventory.Find(i=>i.ItemId!="little_spoon")?.ItemId??"";}}
        void StartRound(List<PlayerState> connected)
        {
            foreach(var player in connected){ReturnHeldOffer(player);player.MapReady=false;player.Ready=false;}
            State.LaunchAtSeconds=0;State.Phase="Loading";
        }
        void BeginCampReview()
        {
            var old=State;
            var next=CreateRound(old.Seed+1);
            next.ReviewResult=old.Result;next.ReviewSales=old.GrossSales;
            next.ReviewSurvivors=old.Survivors;
            foreach(var player in old.Players)next.ReviewAntics+=player.CampAntics;
            next.CampMusicTrack=old.CampMusicTrack;
            State=next;
            foreach(var player in old.Players)
            {
                var fresh=AddPlayer(player.Id,player.Name);fresh.Connected=player.Connected;
                fresh.HasCosmetic=player.HasCosmetic;fresh.Dialogue=player.Dialogue;
            }
            State.HostPlayerId=old.HostPlayerId;
            State.Phase="CampReview";
        }
        CommandResult VoteForReview(PlayerState p,GameCommand c)
        {
            if(State.Phase!="CampReview"||c.Amount<0||c.Amount>=CampFeatures.ReviewAwards.Length)return Reject("Choose a camp review award");
            var vote=State.ReviewVotes.Find(v=>v.PlayerId==p.Id);
            if(vote==null)State.ReviewVotes.Add(new CampReviewVote{PlayerId=p.Id,Award=c.Amount});
            else vote.Award=c.Amount;
            return Ok("Review vote: "+CampFeatures.ReviewAwards[c.Amount]);
        }
        CommandResult FinishCampReview(PlayerState p)
        {
            if(State.Phase!="CampReview"||p.Id!=State.HostPlayerId)return Reject("Only the host can close the camp review");
            if(State.Players.Exists(player=>player.Connected&&!State.ReviewVotes.Exists(v=>v.PlayerId==player.Id)))return Reject("Wait for every connected player to review the round");
            State.Phase="Shopping";return Ok("Review closed. Camp supplies are open for the next round");
        }
        CommandResult CampAction(PlayerState p,GameCommand c)
        {
            if(State.Phase!="Shopping"&&State.Phase!="CampReview")return Reject("Camp activities happen between rounds");
            if(c.Kind=="ChooseCampTrack")
            {
                if(p.CampVisitId!=""||!Near(p,CampFeatures.DjX,CampFeatures.DjZ,3)||c.Amount<0||c.Amount>=CampFeatures.Tracks.Length)return Reject("Choose a track at the camp DJ table");
                State.CampMusicTrack=c.Amount;return Ok("Camp DJ: "+CampFeatures.Tracks[c.Amount]);
            }
            if(c.Kind=="EnterCamp")
            {
                var site=CampFeatures.Find(c.TargetId);
                if(p.Ready||p.CampVisitId!=""||site==null||!Near(p,site.X,site.Z,3.5))return Reject("Stand beside a camp door to enter");
                p.CampVisitId=site.Id;p.CampGag="";p.X=site.X;p.Z=site.Z;
                p.CampInteriorX=CampFeatures.InteriorSlotX(site);p.CampInteriorZ=CampFeatures.InteriorSlotZ(site)-1.5f;
                return Ok("Inside the "+site.Kind.ToLowerInvariant());
            }
            if(p.CampVisitId=="")return Reject("Enter a camp space first");
            if(c.Kind=="ExitCamp")
            {
                LeaveCamp(p,CampFeatures.Find(p.CampVisitId));
                return Ok("Back outside");
            }
            if(c.Kind=="CampAntic")
            {
                var site=CampFeatures.Find(p.CampVisitId);if(site==null)return Reject("Unknown camp space");
                p.CampGag=CampFeatures.Activity(site.Kind,++p.CampAntics);
                return Ok(p.CampGag);
            }
            return Reject("Unknown camp action");
        }
        static void LeaveCamp(PlayerState p,CampFeatures.Site site)
        {
            p.CampVisitId="";p.CampGag="";
            if(site!=null){p.X=site.X;p.Z=site.Z-3.2f;}
        }
    }
}
