using System;

namespace Festival.Core
{
    /// <summary>TRIP-4: anyone at the festival can lie on their back and watch the clouds (LieDown, an interaction that lasts the
    /// level like an art car ride), and get up again (Cancel). They can't walk while lying there (MayStep), their body keeps facing
    /// where they lay down (HoldsFacing), and lying moves nobody's suspicion. Each day level has one clue cloud, up for
    /// CloudShapes.ClueSeconds from a seeded moment. The tripper reads it by lying down while it is up and staying down
    /// ClueReadSeconds: it pictures one of the festival's landmarks, and a SecretStashCash stash waits at that landmark's stash
    /// spot until anyone walks up to it, as with a vision's stash. It has no marker: the tripper has to say where. The clue is
    /// always true, at any dose, and a dust storm, which hides the sky, stops a read while it blows.</summary>
    public sealed partial class FestivalSimulation
    {
        public const string LieDownKind="LieDown";
        public const double ClueReadSeconds=3;

        /// <summary>Whether playerId is lying down. Reads a client's view too, since a view carries its own player's interactions;
        /// everyone else sees a lying player's VisualPose "LieDown".</summary>
        public static bool LyingDown(RoundState s,string playerId)=>s.Interactions.Exists(i=>i.PlayerId==playerId&&i.Kind==LieDownKind&&i.Status=="Active");
        /// <summary>Whether p may lie down: alive at the festival, out of any camp space, doing nothing else, and holding no friend or
        /// body. The rules refuse anyone else, and the HUD offers it by this.</summary>
        public static bool CanLieDown(RoundState s,PlayerState p)=>s.Phase=="Playing"&&p.Life=="Alive"&&p.CampVisitId==""&&p.InteractionId==""&&p.DragTargetId==""&&p.CarryBodyId=="";
        /// <summary>Whether the level's clue cloud is up. It reads only public state, so every client draws the same cloud.</summary>
        public static bool CloudClueUp(RoundState s)=>s.Phase=="Playing"&&s.CloudClueStart>=0&&s.ElapsedSeconds>=s.CloudClueStart&&s.ElapsedSeconds<s.CloudClueStart+CloudShapes.ClueSeconds;
        /// <summary>Where the read cloud's stash waits, or null: unread, found, or a view that doesn't hold the landmark.</summary>
        public static WorldPoint CloudStashAt(RoundState s)=>s.CloudClueReadAt>=0&&!s.CloudStashFound&&s.CloudClueLandmark>=0&&s.CloudClueLandmark<CloudShapes.Landmarks.Length?CloudShapes.Landmarks[s.CloudClueLandmark].Stash:null;
        /// <summary>The landmark viewer is shown in the clue cloud (an index into CloudShapes.Landmarks), or -1. Only the tripper is,
        /// and only once they have read it; FestivalSession.ViewFor sends nobody else anything.</summary>
        public static int VisibleCloudLandmark(RoundState s,string viewer)=>s.TripperId!=""&&viewer==s.TripperId&&s.CloudClueReadAt>=0?s.CloudClueLandmark:-1;

        // As the crew leaves camp, after the spinners: a day's clue cloud, and the landmark it pictures, from its own stream. It
        // warms up with one draw, as GiggleTanks.cs does, so neither reads a fresh stream's barely stirred first value.
        void PlaceCloudClue()
        {
            var random=new ContentRandom(unchecked(State.SpinSeed*29+17));random.Next(100);
            bool day=!Festivals.For(State).Night;State.CloudClueReadAt=-1;State.CloudStashFound=false;
            State.CloudClueStart=day?CloudShapes.ClueEarliestSeconds+random.Next(CloudShapes.ClueLatestSeconds-CloudShapes.ClueEarliestSeconds+1):-1;
            State.CloudClueLandmark=day?random.Next(CloudShapes.LandmarkCount(State.FestivalIndex)):-1;
        }
        // Apply lets only a living, free player at the festival this far; CanLieDown turns away one holding a friend or a body.
        CommandResult LieDown(PlayerState p)
        {
            if(!CanLieDown(State,p))return Reject("Let go of your friend before you lie down");
            NewInteraction(p,LieDownKind,"sky",State.DurationSeconds);return Ok("You lie back and watch the clouds");
        }
        // Every Playing step, beside FindStashes. The tripper's ClueReadSeconds count from when they lay down or the cloud came up,
        // whichever was later, each on its own clock; then whoever reaches the stash banks it.
        void WatchClouds()
        {
            var lying=State.CloudClueReadAt<0&&CloudClueUp(State)&&!DustStorm(State)?State.Interactions.Find(i=>i.PlayerId==State.TripperId&&i.Kind==LieDownKind&&i.Status=="Active"):null;
            if(lying!=null&&Math.Min(State.SimulationSeconds-lying.StartSeconds,State.ElapsedSeconds-State.CloudClueStart)>=ClueReadSeconds)State.CloudClueReadAt=State.SimulationSeconds;
            var stash=CloudStashAt(State);
            if(stash!=null&&State.Players.Exists(p=>p.Connected&&p.Life=="Alive"&&Near(p,stash.X,stash.Z))){State.CloudStashFound=true;State.StashCash+=SecretStashCash;}
        }
    }
}
