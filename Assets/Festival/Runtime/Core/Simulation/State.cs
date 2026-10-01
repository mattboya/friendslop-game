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
        public float X,Z,Yaw,CampInteriorX,CampInteriorZ; public int Cash=20,Health=100,RevivalCount;
        public double DownedRemaining, RecoveryUntil, ChimeUntil, SprintUntil, HelpUntil;
        public int Performances, CampAntics; public double LastRhythmScore=-1;
        public int EscapeProgress; public string VisualPose="Idle",NpcSpeech="",NpcSpeaker=""; public double NpcSpeechUntil;
        public int VisualDanceStepSequence,VisualDanceStepDirection=-1;
        public string VisualOfferItem="",VisualOfferTarget="",VisualReceivedItem="";
        public int VisualReceiptSequence;
        public double VisualReceiptAt;
        public List<ItemStack> Inventory=new List<ItemStack>(); public List<ActiveEffect> Effects=new List<ActiveEffect>();
        public List<string> Wristbands=new List<string>(); public DialogueHistory Dialogue=new DialogueHistory();
    }
    [Serializable] public sealed class ObserverState { public string PlayerId="",BystanderDanceId=""; public double Suspicion, LastSeenSeconds, AccusationSeconds=-1; }
    [Serializable] public sealed class EvidenceState { public string PlayerId="", Kind=""; public double DetainAt; }
    [Serializable] public sealed class NpcState {
        public string Id="",Kind="Wook",Mode="Blending",TargetId="",IdlePose="Idle"; public float X,Z,Yaw;
        // TRIP-2: Buyer, Narc, Regular or (at night) ClueHolder, dealt as the crew leaves camp; never sent to clients. "" is not
        // dealt yet (a snapshot from before roles), and buys like every festivalgoer did then.
        public string Role="";
        // POLO-1: a festival twist this festivalgoer plays (FestivalTwists.cs), public so every client can show it: Influencer, VipGuard or "".
        public string Twist="";
        // VISION-3: view-only, never set on the host: FestivalSession.ViewFor sets it when FestivalSimulation.Engaged says the host
        // would refuse a check on them, which the view's masked Mode and filtered interactions can't tell the HUD.
        public bool Busy;
        public bool HighlyIntoxicated,RedEyes,CanTalk;
        public double Suspicion, DistractedUntil, AttackAt, AttackCooldownUntil, LastTalkSeconds=-100; public int Sales,TalkCount;
        public List<ObserverState> Observers=new List<ObserverState>(); public List<EvidenceState> Evidence=new List<EvidenceState>();
    }
    [Serializable] public sealed class RhythmInput { public int Direction; public double TimeSeconds; }
    [Serializable] public sealed class InteractionState {
        public string Id="",PlayerId="",TargetId="",Kind="",Status="Active",ReservedItemId="",DialogueId="",DialogueText="",PartnerId="";
        public int ChartSeed,NoteCount=8,Phrase,ReservedCash; public double StartSeconds,DurationSeconds,GoodWindowSeconds=.15,Score,BeatSeconds=.5;
        // DANCE-5: how busy a rhythm challenge's chart is (RhythmChart.For), the level it began on; 0 in older snapshots.
        public int ChartDifficulty;
        public List<RhythmInput> Inputs=new List<RhythmInput>(); public List<string> WitnessIds=new List<string>();
        // TRIP-3: a ConfirmChat's conversation (DialogueGrammar). A view carries only its own player's interactions, so only the tripper reads it.
        public Conversation Chat=new Conversation();
    }
    [Serializable] public sealed class DropState { public string Id="", ItemId="",OwnerId=""; public int Count; public float X,Z; }
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
        // Sale cash paid out this level, after any payout multiplier; a day's quota counts it (DayQuota.cs).
        public int LevelSales;
        // GAS-2: this level's Giggle Tank (GiggleTanks.cs), public like the spin: its place in Festivals.GiggleTankSpots, or -1 for
        // none (as in a snapshot from before tanks); found once someone has grabbed it.
        public int GiggleTankSpot=-1; public bool GiggleTankFound;
        // TRIP-4: a day's clue cloud (CloudClue.cs), up for CloudShapes.ClueSeconds from CloudClueStart on the level clock, or -1 for
        // none (a night, or a snapshot from before it); every view has it, as everyone sees the cloud. It pictures landmark
        // CloudClueLandmark (CloudShapes.Landmarks), which leaves the host only for the tripper once they read it at CloudClueReadAt
        // (SimulationSeconds, -1 unread). Then a cash stash waits by the landmark until someone finds it (CloudStashFound).
        public double CloudClueStart=-1,CloudClueReadAt=-1; public int CloudClueLandmark=-1; public bool CloudStashFound;
        public string ReviewResult=""; public int ReviewSales,ReviewSurvivors,ReviewAntics;
        public List<CampReviewVote> ReviewVotes=new List<CampReviewVote>();
        // The debrief's awards (two worst, then one best) and, once every vote is in, each award's winner in the same order.
        public List<string> ReviewAwards=new List<string>(), ReviewWinners=new List<string>();
        public WorldPoint FriendPosition=new WorldPoint(); public string FriendLeaderId="";
        // CROWD-2: a crew of FestivalSimulation.TwoFriendCrew or more at the start of a night also looks for this friend (SplitObjective.cs).
        public LostFriendState SecondFriend=new LostFriendState();
        // Night 2 dead, in death order (Bodies.cs): the first one moves with one carrier, later ones need two.
        public List<BodyState> Bodies=new List<BodyState>();
        // TRIP-2: what the tripper sees (Visions.cs), and the night's clue holders in trail order; CluesRead is the next link.
        public List<VisionState> Visions=new List<VisionState>(); public List<string> ClueChain=new List<string>();
        public List<PlayerState> Players=new List<PlayerState>(); public List<NpcState> Npcs=new List<NpcState>();
        public List<string> VendorOffers=new List<string>(); public List<ShopStockState> ShopStock=new List<ShopStockState>(); public List<InteractionState> Interactions=new List<InteractionState>();
        public List<CommittedCommand> Commands=new List<CommittedCommand>(); public List<DropState> Drops=new List<DropState>();
        public List<TransferOffer> Transfers=new List<TransferOffer>(); public List<StashState> Stashes=new List<StashState>();
    }
}
