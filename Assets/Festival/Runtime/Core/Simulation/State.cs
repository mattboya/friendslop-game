using System;
using System.Collections.Generic;
namespace Festival.Core
{
    [Serializable] public sealed class WorldPoint { public float X, Z; public WorldPoint() {} public WorldPoint(float x,float z) { X=x; Z=z; } }
    [Serializable] public sealed class GameCommand { public string Id="", Kind="", TargetId="", ItemId=""; public int Amount, Direction; public double TimeSeconds; }
    [Serializable] public sealed class CommandResult { public bool Accepted; public string Reason="", Id=""; public long Sequence; }
    [Serializable] public sealed class CommittedCommand { public string PlayerId="", Id=""; public CommandResult Result=new CommandResult(); }
    [Serializable] public sealed class ItemStack { public string ItemId=""; public int Count; }
    // TRIP-5: Substance is what the spinner's dose was (a catalog effect id, FestivalSimulation.Substances); "" on every other effect and in older snapshots.
    [Serializable] public sealed class ActiveEffect { public string Id="", InstanceId="", SourceCommandId="", Substance=""; public double RemainingSeconds, StartSeconds; public int Intensity; }
    [Serializable] public sealed class PlayerState {
        public string Id="", Name="", Life="Alive", InteractionId="", DragTargetId="", CarryBodyId="", HeldOfferId="", EquippedItemId="", CampVisitId="", CampGag="";
        // DEBRIEF-1: the award names this player won at the last debrief, worn until the next level's Results.
        public string Badge="";
        // PLAYA-1: this player's place in the crew's joining order (0-7), kept all weekend; it picks their odd-object money.
        public int Ordinal;
        public bool Ready, Connected=true, MapReady, HasCosmetic, WearingLittleSpoon, VisualWideEyes, VisualRedEyes;
        // GAS-1: view-only, never set on the host: FestivalSession.ViewFor sets it while FestivalSimulation.Giggling, so friends see a giggle.
        public bool VisualGiggling;
        // POI-1: the host sets it each tick while this player holds left click and may spin their poi (FestivalSimulation.CanSpinPoi);
        // every view carries it, so friends see the spin. Only for show; false in older snapshots.
        public bool VisualPoiSpin;
        // POLO-5: view-only, never set on the host: FestivalSession.ViewFor sets it to the SimulationSeconds this player boarded
        // the Ferris wheel, so every client seats them in the same gondola; -1 off the wheel and in older snapshots.
        public double VisualWheelSince=-1;
        // POLO-6: view-only, never set on the host: FestivalSession.ViewFor fills it on the viewer's own copy alone with the
        // influencers filming them (FestivalSimulation.Films), for their ON CAMERA line and those phones' REC; empty otherwise and in
        // older snapshots.
        public List<string> VisualFilmedBy=new List<string>();
        public float X,Z,Yaw,CampInteriorX,CampInteriorZ; public int Cash=20,Health=100,RevivalCount;
        // ECON-6: successful starter payments this camp, shared across both kinds; zero in older snapshots. Owner-only in views.
        public int StarterStockBought;
        // Owner-only latest positive-cash death. -1 means no spin in older snapshots; logical age survives booth pauses.
        public double DeathSpinAt=-1; public int DeathSpinSeed,DeathSpinAmount,DeathCashDropped; public long DeathSpinSequence;
        public string DeathSpinId="",DeathSpinRoundId="",DeathSpinName="";
        // Persistent owner-only semantic loss event; independent of cash and retained through covered screens.
        public long GearLossSequence; public string GearLossKind="",GearLossNotice="";
        public double DownedRemaining, RecoveryUntil, ChimeUntil, SprintUntil, HelpUntil;
        // Latest deck tip receipt is copied only to its owner; no public speech delivery or transcript. Legacy defaults are empty.
        public int DeckTipAmount; public string DeckTipDeliveryId=""; public double DeckTipAtWorldSeconds=-1,DeckTipUntilWorldSeconds=-1;
        public int Performances, CampAntics; public double LastRhythmScore=-1;
        // CAMP-3: shared last poke; the cumulative session discovery mask reaches only this player's own view.
        public string LastCampEggId="",LastCampEggLine=""; public int CampEggSequence,FoundCampEggMask; public double LastCampEggAt=-1;
        public int EscapeProgress; public string VisualPose="Idle",NpcSpeech="",NpcSpeaker="",SpeechById=""; public double NpcSpeechUntil;
        // Public speech identity is independent of text/expiry. -1 world timestamps preserve legacy snapshots.
        public string SpeechDeliveryId=""; public long SpeechSequence;
        public double SpeechAtWorldSeconds=-1,SpeechUntilWorldSeconds=-1;
        public int VisualDanceStepSequence,VisualDanceStepDirection=-1;
        public string VisualOfferItem="",VisualOfferTarget="",VisualReceivedItem="";
        public int VisualReceiptSequence;
        public double VisualReceiptAt;
        public List<ItemStack> Inventory=new List<ItemStack>(); public List<ActiveEffect> Effects=new List<ActiveEffect>();
        public List<string> Wristbands=new List<string>(); public DialogueHistory Dialogue=new DialogueHistory();
        // Owner-only acknowledged history revision and bounded terminal deliveries, additive in schema 2.
        public long DialogueRevision,ConversationOutcomeSequence,ConversationDismissedSequence;
        public string ConversationThreadId="",ConversationDismissedId="";
        public List<ConversationOutcome> ConversationOutcomes=new List<ConversationOutcome>();
    }
    [Serializable] public sealed class PublicSpeechState {
        public string DeliveryId="",SpeakerId="",Speaker="",Text="";
        public long Sequence; public double StartedAtWorldSeconds,ExpiresAtWorldSeconds; public bool Retracted;
        // Host routing only. ViewFor copies the public delivery fields explicitly.
        public string OwnerId="",InteractionId="";
    }
    [Serializable] public sealed class ConversationOutcome {
        public string RoundId="",InteractionId="",DeliveryId="",Kind="",TargetId="",Speaker="",Reaction="",Notice="",Hunch="";
        public long Sequence; public List<string> GrammarKeys=new List<string>();
    }
    [Serializable] public sealed class ObserverState { public string PlayerId="",BystanderDanceId=""; public double Suspicion, LastSeenSeconds, AccusationSeconds=-1; }
    // A cop's case on a player (Danger.cs, SecurityStops.cs): Kind VisibleStock is a security stop, WavedOn one talked down,
    // and WitnessedDeal or IgnoredStop an arrest warning, detaining at DetainAt. ESC-2: a stop runs from StartSeconds, and the
    // cop last saw them at SeenSeconds; -1 in older snapshots, whose stops are timed from the load.
    [Serializable] public sealed class EvidenceState { public string PlayerId="", Kind=""; public double DetainAt, StartSeconds=-1, SeenSeconds=-1; }
    [Serializable] public sealed class NpcState {
        public string Id="",Kind="Wook",Mode="Blending",TargetId="",IdlePose="Idle"; public float X,Z,Yaw;
        // TRIP-2: Buyer, Narc, Regular or (at night) ClueHolder, dealt as the crew leaves camp; never sent to clients. "" is not
        // dealt yet (a snapshot from before roles), and buys like every festivalgoer did then.
        public string Role="";
        // Host-only stable approach reservations; old snapshots have no reservation and acquire one on their next step.
        public string ApproachTargetId=""; public long ApproachEpoch; public int ApproachSlot=-1,CrowdDetourSide;
        // POLO-1: a festival twist this festivalgoer plays (FestivalTwists.cs), public so every client can show it: Influencer, VipGuard or "".
        public string Twist="";
        // VISION-3: view-only, never set on the host: FestivalSession.ViewFor sets it when FestivalSimulation.Engaged says the host
        // would refuse a check on them, which the view's masked Mode and filtered interactions can't tell the HUD.
        public bool Busy;
        public bool HighlyIntoxicated,RedEyes,CanTalk;
        public double Suspicion, DistractedUntil, AttackAt, AttackCooldownUntil, LastTalkSeconds=-100; public int Sales,TalkCount;
        // ECON-2: units this seller has sold the crew this level (StockBuys.cs). Host-only like Role: ViewFor never copies it. 0 in older snapshots.
        public int StockSold;
        // Global once-per-level art-car tip flag. Host snapshots persist it; ViewFor omits it. False in older snapshots.
        public bool Tipped;
        public List<ObserverState> Observers=new List<ObserverState>(); public List<EvidenceState> Evidence=new List<EvidenceState>();
    }
    [Serializable] public sealed class RhythmInput { public int Direction; public double TimeSeconds; public bool Release; }
    [Serializable] public sealed class InteractionState {
        public string Id="",PlayerId="",TargetId="",Kind="",Status="Active",ReservedItemId="",DialogueId="",DialogueText="",PartnerId="";
        public int ChartSeed,NoteCount=8,Phrase,ReservedCash; public double StartSeconds,DurationSeconds,GoodWindowSeconds=.15,Score,BeatSeconds=.5;
        // DANCE-5: how busy a rhythm challenge's chart is (RhythmChart.For), the level it began on; 0 in older snapshots.
        public int ChartDifficulty;
        // The authored stage transport uses world time, independent of StartSeconds' simulation countdown.
        // Track 0 keeps pre-grid snapshots and the takeover's own fixed chart. Delay is relative to StartSeconds, so pauses
        // shift only that simulation start; the world anchor, chart delay and musical stride never move.
        public int StageTrack; public double StageFirstHeadWorldSeconds,StageFirstHeadDelaySeconds,StageBeatStride;
        public List<RhythmInput> Inputs=new List<RhythmInput>(); public List<string> WitnessIds=new List<string>();
        // TRIP-3: a ConfirmChat's conversation (DialogueGrammar). A view carries only its own player's interactions, so only the tripper reads it.
        public Conversation Chat=new Conversation();
        // DIALOG-3: a talk's turns (Conversations.cs), the last one live until StartSeconds+DurationSeconds; TrustDance, a wary
        // festivalgoer asked for a dance first, on this interaction's chart; Hunch, what a chat or a buy left the player feeling,
        // set as its last turn begins. Empty and false in older snapshots.
        public List<ConversationTurn> Turns=new List<ConversationTurn>(); public bool TrustDance; public string Hunch="";
    }
    // ESC-2: ClaimedBy is the cop who saw this stock ditched, taking it at ClaimAt unless someone has first; "" unclaimed (and in
    // older snapshots).
    // Cash LandsAt is a simulation-clock deadline translated across booth pauses; zero keeps legacy drops immediate.
    [Serializable] public sealed class DropState { public string Id="", ItemId="",OwnerId="",ClaimedBy=""; public int Count; public float X,Z; public double ClaimAt,LandsAt; }
    // A frozen positive-cash death, retained for every viewer without publishing the player's current private state.
    // AtWorldSeconds is immutable world time: compare it directly with WorldSeconds/EstimatedWorldSeconds, never ShiftClock it.
    [Serializable] public sealed class DeathSpinState {
        public string Id="",RoundId="",PlayerId="",Name=""; public long Sequence; public int Amount,Seed,CashDropped; public double AtWorldSeconds=-1;
    }
    [Serializable] public sealed class TransferOffer { public string Id="",FromId="",ToId="",ItemId=""; public int Amount; public double ExpiresAt; }
    [Serializable] public sealed class StashState { public string Id=""; public float X,Z; public List<ItemStack> Items=new List<ItemStack>(); }
    [Serializable] public sealed class ShopStockState { public string ItemId=""; public int CampAvailable, MarketAvailable; }
    // One debrief vote: PlayerId picked TargetId for award slot Award (an index into RoundState.ReviewAwards).
    [Serializable] public sealed class CampReviewVote { public string PlayerId="", TargetId=""; public int Award; }
    [Serializable] public sealed class PlayerDose { public string PlayerId="", Substance=""; public int Dose; }
    [Serializable] public sealed class RoundState {
        // Schema 2 is festival weekends. A schema 1 round, saved before them, is still restored (FestivalSimulation.Restore).
        public const int CurrentSchemaVersion=2;
        public int SchemaVersion=CurrentSchemaVersion, Seed; public string RoundId="",Phase="Shopping",Result="",HostPlayerId="",MissionId="rescue_compact";
        // Weekend position: a row of Festivals.cs. Clearing festival k unlocks k+1.
        public int FestivalIndex,LevelIndex,EncoreTier,UnlockedFestivalCount=1;
        // TRIP-1 spin result, public so every client animates the same spin. TripperBag: players still due a turn this weekend.
        public int SpinSeed; public double SpinEndsAt; public string TripperId=""; public List<PlayerDose> Doses=new List<PlayerDose>(); public List<string> TripperBag=new List<string>();
        // TRIP-7: the host's secret for this level's hidden deal (FestivalSimulation.DealSecret, asked for at each spin), mixed into
        // the roles, the clue trails, the lost friends' spots and the cloud's landmark. No view carries it (FestivalSession.ViewFor),
        // so no client can rebuild them from the public seeds. 0, no secret (tests, previews, older snapshots), deals as before.
        public int DealSeed;
        public double ElapsedSeconds,DurationSeconds=600,SimulationSeconds,LaunchAtSeconds; public long Tick,TransactionSequence,EntitySequence;
        public int CluesRead, ObjectiveReward, SurvivorBonus, Survivors, ConnectedCrewCount;
        // The night's clue trail is complete (Visions.cs): the lost friend can be found.
        public bool GateOpened;
        public int GrossSales,StashCash,LostPropertyTask=1,CampMusicTrack=1; public bool FriendFound,RewardCommitted;
        // CAMP-3: persistent public prop state and host-picked points for this camp visit; additive in schema 2.
        public List<CampEggState> CampEggs=new List<CampEggState>();
        // Sale cash paid out this level, after any payout multiplier; a day's quota counts it (DayQuota.cs).
        public int LevelSales;
        // GAS-2: this level's Giggle Tank (GiggleTanks.cs), public like the spin: its place in Festivals.GiggleTankSpots, or -1 for
        // none (as in a snapshot from before tanks); found once someone has grabbed it.
        public int GiggleTankSpot=-1; public bool GiggleTankFound;
        // MINI-1: this level's minigame booth and its game (Minigames.cs), public like the tank; Spot -1, none (camp, and snapshots
        // from before booths). MinigameBag: the games this weekend still has to offer, carried like TripperBag; host-only.
        public MinigameState Booth=new MinigameState(); public List<string> MinigameBag=new List<string>();
        // TRIP-4: a day's clue cloud (CloudClue.cs), up for CloudShapes.ClueSeconds from CloudClueStart on the level clock, or -1 for
        // none (a night, or a snapshot from before it); every view has it, as everyone sees the cloud. It pictures landmark
        // CloudClueLandmark (CloudShapes.Landmarks), which leaves the host only for the tripper once they read it at CloudClueReadAt
        // (SimulationSeconds, -1 unread). Then a cash stash waits by the landmark until someone finds it (CloudStashFound).
        public double CloudClueStart=-1,CloudClueReadAt=-1; public int CloudClueLandmark=-1; public bool CloudStashFound;
        // TRIP-8: who found it, for their notice; every view has it. "" until then, and in older snapshots.
        public string CloudStashFinderId="";
        public string ReviewResult=""; public int ReviewSales,ReviewSurvivors,ReviewAntics;
        // PLAYA-2: the festival the reviewed round was played at, whose money ReviewSales is counted in. The campfire already holds
        // the next round, so clearing a weekend has moved FestivalIndex on. -1 unknown (a snapshot from before it): read FestivalIndex.
        public int ReviewFestivalIndex=-1;
        public List<CampReviewVote> ReviewVotes=new List<CampReviewVote>();
        // The debrief's awards (two worst, then one best) and, once every vote is in, each award's winner in the same order.
        public List<string> ReviewAwards=new List<string>(), ReviewWinners=new List<string>();
        public WorldPoint FriendPosition=new WorldPoint(); public string FriendLeaderId="";
        // CROWD-2: a crew of FestivalSimulation.TwoFriendCrew or more at the start of a night also looks for this friend (SplitObjective.cs).
        public LostFriendState SecondFriend=new LostFriendState();
        // POLO-3: view-only, never set on the host: FestivalSession.ViewFor fills it with FestivalSimulation.WheelFriendAreas, the
        // rough area of each friend still lost, while this view's player rides the Ferris wheel at night. Empty otherwise and in
        // older snapshots.
        public List<WorldPoint> WheelFriendAreas=new List<WorldPoint>();
        // Night 2 dead, in death order (Bodies.cs): the first one moves with one carrier, later ones need two.
        public List<BodyState> Bodies=new List<BodyState>();
        // TRIP-2: what the tripper sees (Visions.cs), and the night's clue holders in trail order; CluesRead is the next link.
        public List<VisionState> Visions=new List<VisionState>(); public List<string> ClueChain=new List<string>();
        // VISION-6: when a creature next hints at a true vision (HintVisions), on the level clock; 0, not armed (older snapshots arm
        // on their first step). Host-only: ViewFor builds the view field by field and leaves it out.
        public double NextVisionHintAt;
        public List<DeathSpinState> DeathSpins=new List<DeathSpinState>();
        public long CrowdArrivalEpoch;
        public long SpeechSequence; public List<PublicSpeechState> PublicSpeech=new List<PublicSpeechState>();
        public List<PlayerState> Players=new List<PlayerState>(); public List<NpcState> Npcs=new List<NpcState>();
        public List<string> VendorOffers=new List<string>(); public List<ShopStockState> ShopStock=new List<ShopStockState>(); public List<InteractionState> Interactions=new List<InteractionState>();
        public List<CommittedCommand> Commands=new List<CommittedCommand>(); public List<DropState> Drops=new List<DropState>();
        public List<TransferOffer> Transfers=new List<TransferOffer>(); public List<StashState> Stashes=new List<StashState>();
    }
}
