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
            s.FriendPosition=FriendSpot(seed,0);
            for(int i=0;i<FestivalCrowdLayout.Count;i++) { var point=FestivalCrowdLayout.Get(i,seed);bool wide=(i+seed%7)%7==0; s.Npcs.Add(new NpcState {Id="wook_"+i,X=point.X,Z=point.Z,Yaw=point.Yaw,IdlePose=point.Pose,HighlyIntoxicated=wide,RedEyes=(i+seed%5)%5==0||wide&&i<7,CanTalk=(i+seed%3)%4==0}); }
            AddCops(s);
            s.Stashes.Add(new StashState{Id="stash",X=-25,Z=-8});s.DurationSeconds=Festivals.For(s).DurationSeconds;return s;
        }
        public PlayerState AddPlayer(string id,string name) {
            if(string.IsNullOrWhiteSpace(id)||id.Length>128) throw new ArgumentException("Invalid identity");
            var existing=Player(id); if(existing!=null) {existing.Connected=true;return existing;}
            if(State.Players.Count>=8) throw new InvalidOperationException("Festival is full (8 players including host).");
            if(State.Phase!="Lobby"&&State.Phase!="Shopping") throw new InvalidOperationException("Round already started; reconnect with your session token.");
            int campIndex=State.Players.Count;
            var p=new PlayerState{Id=id,Name=string.IsNullOrWhiteSpace(name)?"Friend":name.Substring(0,Math.Min(24,name.Length)),Ordinal=campIndex,X=campIndex%2==0?-1:1,Z=-9-2*(campIndex/2)};
            State.Players.Add(p); State.TripperBag.Add(id); if(State.HostPlayerId=="") State.HostPlayerId=id;
            foreach(var stock in State.ShopStock)
            {
                if(Catalog.RareShopItem(stock.ItemId))continue;
                int before=Catalog.ShopCopies(stock.ItemId,State.Players.Count-1),after=Catalog.ShopCopies(stock.ItemId,State.Players.Count);
                stock.CampAvailable+=after-before;stock.MarketAvailable+=after-before;
            }
            return p;
        }
        public void Disconnect(string id) {var p=Player(id);if(p==null)return; Cancel(p,"Disconnected");ReturnHeldOffer(p);p.Connected=false;p.Ready=false;p.DragTargetId="";State.LaunchAtSeconds=0;if(State.FriendLeaderId==id)State.FriendLeaderId=""; ReturnOffers(id);RevealIfAllVoted();}
        public PlayerState Player(string id) {return State.Players.Find(p=>p.Id==id);}
        public InteractionState Interaction(string id) {return State.Interactions.Find(i=>i.Id==id);}
        public void Restore(RoundState state) {
            if(state==null||state.SchemaVersion<1||state.SchemaVersion>RoundState.CurrentSchemaVersion||state.Players==null||state.Players.Count>8||!Finite(state.SimulationSeconds)||!Finite(state.DurationSeconds)||state.DurationSeconds<=0)throw new ArgumentException("Unsupported or invalid snapshot");
            var ids=new HashSet<string>();foreach(var p in state.Players)if(p==null||!ids.Add(p.Id)||p.Cash<0||p.Inventory==null||p.Effects==null||!Finite(p.X)||!Finite(p.Z))throw new ArgumentException("Invalid snapshot player");
            if(state.Npcs==null||state.Interactions==null||state.Commands==null||state.Drops==null||state.Transfers==null||state.Stashes==null||state.VendorOffers==null||state.ShopStock==null||state.FriendPosition==null||state.SecondFriend==null||state.SecondFriend.ClueChain==null||state.SecondFriend.Position==null||state.ReviewVotes==null||state.Doses==null||state.TripperBag==null||state.Visions==null||state.ClueChain==null||state.Bodies==null||state.ReviewAwards==null||state.ReviewWinners==null||state.ReviewWinners.Count!=0&&state.ReviewWinners.Count!=state.ReviewAwards.Count||state.StashCash<0||state.GrossSales<0||state.LevelSales<0)throw new ArgumentException("Incomplete snapshot");
            if(state.UnlockedFestivalCount>Festivals.Count||state.FestivalIndex<0||state.FestivalIndex>=state.UnlockedFestivalCount||state.LevelIndex<0||state.LevelIndex>=Festivals.LevelCount||state.EncoreTier<0)throw new ArgumentException("Invalid weekend position");
            // A round saved before weekends restores at Day 1 of the first festival. One that had left camp was a rescue: it resumes
            // as Night 1, the night it was, with the way to its friend open, as it holds no tripper's clue trail. Either way it is
            // a current round from here on, so saving it again does not re-read a weekend Day 1 as an old rescue.
            if(state.SchemaVersion==1&&state.Phase!="Lobby"&&state.Phase!="Shopping"){state.LevelIndex=1;state.GateOpened=true;}
            state.SchemaVersion=RoundState.CurrentSchemaVersion;
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
        /// <summary>HUD-4: whether players can act in this phase: at camp shopping and at the festival. The host refuses any other
        /// action as "Round is not interactive", and the HUD offers none then (the wheels spinning, the festival loading).</summary>
        public static bool Interactive(string phase)=>phase=="Playing"||phase=="Shopping";
        CommandResult Apply(PlayerState p,GameCommand c) {
            if(c.Kind=="Ready") {if(State.Phase!="Shopping"&&State.Phase!="Lobby")return Reject("Not shopping");if(!Near(p,0,19,3.2))return Reject("Ready at the lit trailhead gate");p.Ready=!p.Ready;State.LaunchAtSeconds=0;return Ok(p.Ready?"Ready at the trailhead":"Not ready");}
            if(c.Kind=="Start") {
                if(p.Id!=State.HostPlayerId)return Reject("Only host starts rounds");
                if(State.Phase!="Shopping"&&State.Phase!="Lobby")return Reject("Round already started");
                int required=1;
                var connected=State.Players.FindAll(x=>x.Connected);if(connected.Count<required||connected.Exists(x=>!x.Ready))return Reject("Every connected player must be ready at the trailhead");
                StartRound(connected);return Ok();
            }
            if(c.Kind=="MapReady") {if(State.Phase!="Loading")return Reject("Map is not loading");p.MapReady=true;if(!State.Players.Exists(x=>x.Connected&&!x.MapReady)){for(int index=0;index<State.Players.Count;index++){var teammate=State.Players[index];teammate.X=-7+2*index;teammate.Z=-29;}State.Phase="Playing";PourShots();}return Ok();}
            if(c.Kind=="Reset") {if(p.Id!=State.HostPlayerId||State.Phase!="Results")return Reject("Host can bring the crew back after results");BeginCampReview();return Ok("Back at camp: review the round before shopping");}
            if(c.Kind=="ReviewVote")return VoteForReview(p,c);
            if(c.Kind=="FinishReview")return FinishCampReview(p);
            if(c.Kind=="ChooseFestival")return ChooseFestival(p,c);
            if(c.Kind=="EnterCamp"||c.Kind=="ExitCamp"||c.Kind=="CampAntic"||c.Kind=="ChooseCampTrack")return CampAction(p,c);
            if(c.Kind=="DialogueAck") {var current=Interaction(p.InteractionId);if(current==null||current.DialogueId!=c.TargetId)return Reject("Dialogue is not active");p.Dialogue.Acknowledge(c.TargetId,current.Id);return Ok();}
            if(c.Kind=="Cancel") {if(OnWheel(State,p.Id))return Reject("You're stuck until the wheel comes round");Cancel(p,"Cancelled");return Ok();}
            if(c.Kind=="Rhythm")return Submit(p,c);
            if(c.Kind=="Chime") {if(p.Life!="Spirit"||!Near(p,24,-20)||State.SimulationSeconds<p.ChimeUntil)return Reject("Chime requires medical memorial and cooldown");p.ChimeUntil=State.SimulationSeconds+10;return Ok("Memorial chime");}
            if(c.Kind=="HelpSelf")return HelpSelf(p);
            if(c.Kind=="BeginRevival"&&p.Life=="Spirit")return Revive(p,c);
            if(p.Life!="Alive")return Reject("Requires a living, free player");
            if(!Interactive(State.Phase))return Reject("Round is not interactive");
            if(c.Kind=="Transfer")return Transfer(p,c);
            if(c.Kind=="AcceptTransfer")return AcceptTransfer(p,c);
            if(c.Kind=="CancelTransfer")return CancelTransfer(p,c);
            if(p.CampVisitId!="")return Reject("Leave the camp interior first");
            if(c.Kind=="HoldOffer")return HoldOffer(p,c);
            if(c.Kind=="ReturnOffer")return ReturnHeldOffer(p);
            if(c.Kind=="Buy")return Buy(p,c);
            if(c.Kind=="Equip")return Equip(p,c);
            if(c.Kind=="Drop"&&State.Phase=="Shopping")return LeaveAtCamp(p,c);
            if(c.Kind==TakeGiggleBalloonKind)return TakeGiggleBalloon(p);
            if(State.Phase!="Playing")return Reject("Start the round first");
            if(p.InteractionId!="")return Reject("Finish or cancel the current interaction");
            switch(c.Kind) {
                case "Consume": return Consume(p,c);
                case "Use": return Use(p,c);
                case "Drop": return Drop(p,c);
                case "Pickup": return Pickup(p,c);
                case "Deposit":case "Withdraw":return Stash(p,c);
                case "StartSale":return BeginChallenge(p,c,"Sale");
                case "Dance":return BeginChallenge(p,c,"Dance");
                case "Conversation":return BeginChallenge(p,c,"Conversation");
                case "Talk":return Talk(p,c);
                case "ConfirmChat":case "ConfirmDance":return BeginConfirm(p,c);
                case RideWheelKind:return RideWheel(p);
                case RideCarKind:return RideCar(p);
                case GrabGiggleTankKind:return GrabGiggleTank(p);
                case LieDownKind:return LieDown(p);
                case "Police":return BeginChallenge(p,c,"Police");
                case "Poi":return BeginPoi(p,c);
                case "Dj":return BeginDj(p,c);
                case "FindFriend":return FindFriend(p);
                case "Extract":if(!CanExtractNow(p))return Reject(DayLevel?DayExtractRefusal():NightExtractRefusal());return BeginTask(p,"Extract","shuttle",3);
                case "LostProperty":if(!Near(p,-28,16))return Reject("Find lost property marker");return BeginTask(p,"LostProperty",State.LostPropertyTask.ToString(),5);
                case "Drag":return Drag(p,c);
                case "CarryBody":return CarryBody(p,c);
                case "DropBody":return DropBody(p);
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
            if(!InBounds(x,z))return false;
            if(State.Phase=="Shopping"&&p.Ready&&Distance(p.X,p.Z,x,z)>.001)return false;
            double speed=6*Intoxication.MovementMultiplier(p);if(p.InteractionId!="")speed=1;if(p.DragTargetId!="")speed=2;
            if(p.Life=="Downed")speed=.8;
            speed=Math.Min(speed,CarrySpeed(p));
            double distance=Distance(p.X,p.Z,x,z);if(distance>speed*deltaSeconds+.03)return false;
            Crush(p,ref x,ref z);
            if(p.Life=="Detained"&&Distance(x,z,27,5)>3)return false;
            if(!MayStep(p,x,z))return false;
            if(!CarryTo(p,x,z))return false;
            if(distance/deltaSeconds>4.2)p.SprintUntil=State.SimulationSeconds+.3;
            p.X=x;p.Z=z;if(!HoldsFacing(p))p.Yaw=yaw%360;var target=Player(p.DragTargetId);if(target!=null&&target.Life=="Downed"){target.X=x-1;target.Z=z;}return true;
        }
        static bool Finite(double d){return !double.IsNaN(d)&&!double.IsInfinity(d);}
        // The festival grounds' walkable square: TryMove refuses steps beyond it, as does a dancer's step back (DanceSpacing.cs).
        static bool InBounds(float x,float z){return Math.Abs(x)<=39&&Math.Abs(z)<=39;}
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
            foreach(var offer in State.Transfers.ToArray())ReturnOffer(offer);
            foreach(var player in connected){ReturnHeldOffer(player);player.MapReady=false;player.Ready=false;}
            // The level runs the table's length whatever the round was saved with (a snapshot from before weekends held 600 s).
            State.LaunchAtSeconds=0;State.DurationSeconds=Festivals.For(State).DurationSeconds;ClearGiggleGas();Spin(connected);DealRoles();DealTwists();RollGiggleTank();PlaceCloudClue();
        }
        void BeginCampReview()
        {
            var old=State;
            var next=CreateRound(old.Seed+1);
            next.ReviewResult=old.Result;next.ReviewSales=old.GrossSales;next.ReviewFestivalIndex=old.FestivalIndex;
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
            AdvanceWeekend(old);
            DrawReviewAwards();
            State.Phase="CampReview";
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
                int occupants=0;foreach(var other in State.Players)if(other!=p&&other.CampVisitId==site.Id)occupants++;
                p.CampVisitId=site.Id;p.CampGag="";p.X=site.X;p.Z=site.Z;
                // Give the next arrival a distinct standing spot so friends
                // do not spawn directly in one another's first-person view.
                float offset=occupants==0?0:(occupants%2==1?1:-1)*(1.35f+.2f*((occupants-1)/2));
                p.CampInteriorX=CampFeatures.InteriorSlotX(site)+offset;p.CampInteriorZ=CampFeatures.InteriorSlotZ(site)-1.5f;
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
