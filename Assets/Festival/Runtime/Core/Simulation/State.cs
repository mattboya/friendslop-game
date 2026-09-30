using System;
using System.Collections.Generic;
namespace Festival.Core
{
    [Serializable] public sealed class WorldPoint { public float X, Z; public WorldPoint() {} public WorldPoint(float x,float z) { X=x; Z=z; } }
    [Serializable] public sealed class GameCommand { public string Id="", Kind="", TargetId="", ItemId=""; public int Amount, Direction; public double TimeSeconds; }
    [Serializable] public sealed class CommandResult { public bool Accepted; public string Reason="", Id=""; public long Sequence; }
    [Serializable] public sealed class CommittedCommand { public string PlayerId="", Id=""; public CommandResult Result=new CommandResult(); }
    [Serializable] public sealed class ItemStack { public string ItemId=""; public int Count; }
    [Serializable] public sealed class ActiveEffect { public string Id="", InstanceId="", SourceCommandId=""; public double RemainingSeconds, StartSeconds; }
    [Serializable] public sealed class PlayerState {
        public string Id="", Name="", Life="Alive", InteractionId="", DragTargetId="", CarryBodyId="", HeldOfferId="", EquippedItemId="", CampVisitId="", CampGag="";
        // DEBRIEF-1: the award names this player won at the last debrief, worn until the next level's Results.
        public string Badge="";
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
    [Serializable] public sealed class ObserverState { public string PlayerId=""; public double Suspicion, LastSeenSeconds, AccusationSeconds=-1; }
    [Serializable] public sealed class EvidenceState { public string PlayerId="", Kind=""; public double DetainAt; }
    [Serializable] public sealed class NpcState {
        public string Id="",Kind="Wook",Mode="Blending",TargetId="",IdlePose="Idle"; public float X,Z,Yaw;
        public bool HighlyIntoxicated,RedEyes,CanTalk;
        public double Suspicion, DistractedUntil, AttackAt, AttackCooldownUntil, LastTalkSeconds=-100; public int Sales,TalkCount;
        public List<ObserverState> Observers=new List<ObserverState>(); public List<EvidenceState> Evidence=new List<EvidenceState>();
    }
    [Serializable] public sealed class RhythmInput { public int Direction; public double TimeSeconds; }
    [Serializable] public sealed class InteractionState {
        public string Id="",PlayerId="",TargetId="",Kind="",Status="Active",ReservedItemId="",DialogueId="",DialogueText="",PartnerId="";
        public int ChartSeed,NoteCount=8,Phrase,ReservedCash; public double StartSeconds,DurationSeconds,GoodWindowSeconds=.15,Score,BeatSeconds=.5;
        public List<RhythmInput> Inputs=new List<RhythmInput>(); public List<string> WitnessIds=new List<string>();
    }
    [Serializable] public sealed class DropState { public string Id="", ItemId="",OwnerId=""; public int Count; public float X,Z; }
    [Serializable] public sealed class TransferOffer { public string Id="",FromId="",ToId="",ItemId=""; public int Amount; public double ExpiresAt; }
    [Serializable] public sealed class StashState { public string Id=""; public float X,Z; public List<ItemStack> Items=new List<ItemStack>(); }
    [Serializable] public sealed class ShopStockState { public string ItemId=""; public int CampAvailable, MarketAvailable; }
    // One debrief vote: PlayerId picked TargetId for award slot Award (an index into RoundState.ReviewAwards).
    [Serializable] public sealed class CampReviewVote { public string PlayerId="", TargetId=""; public int Award; }
    [Serializable] public sealed class RoundState {
        public int SchemaVersion=1, Seed; public string RoundId="",Phase="Shopping",Result="",HostPlayerId="",MissionId="rescue_compact";
        // Weekend position: a row of Festivals.cs. Clearing festival k unlocks k+1.
        public int FestivalIndex,LevelIndex,EncoreTier,UnlockedFestivalCount=1;
        public double ElapsedSeconds,DurationSeconds=600,SimulationSeconds,LaunchAtSeconds; public long Tick,TransactionSequence,EntitySequence;
        public int CluesRead, ObjectiveReward, SurvivorBonus, Survivors, ConnectedCrewCount;
        public bool GateOpened;
        public string PrivateClue="";
        public int GrossSales,StashCash,LostPropertyTask=1,CampMusicTrack=1; public bool FriendFound,RewardCommitted;
        // Sale cash paid out this level, after any payout multiplier; a day's quota counts it (DayQuota.cs).
        public int LevelSales;
        public string ReviewResult=""; public int ReviewSales,ReviewSurvivors,ReviewAntics;
        public List<CampReviewVote> ReviewVotes=new List<CampReviewVote>();
        // The debrief's awards (two worst, then one best) and, once every vote is in, each award's winner in the same order.
        public List<string> ReviewAwards=new List<string>(), ReviewWinners=new List<string>();
        public WorldPoint FriendPosition=new WorldPoint(); public string FriendLeaderId="";
        // Night 2 dead, in death order (Bodies.cs): the first one moves with one carrier, later ones need two.
        public List<BodyState> Bodies=new List<BodyState>();
        public List<PlayerState> Players=new List<PlayerState>(); public List<NpcState> Npcs=new List<NpcState>();
        public List<string> VendorOffers=new List<string>(); public List<ShopStockState> ShopStock=new List<ShopStockState>(); public List<InteractionState> Interactions=new List<InteractionState>();
        public List<CommittedCommand> Commands=new List<CommittedCommand>(); public List<DropState> Drops=new List<DropState>();
        public List<TransferOffer> Transfers=new List<TransferOffer>(); public List<StashState> Stashes=new List<StashState>();
    }
}
