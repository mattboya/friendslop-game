using System;
using System.Collections.Generic;

namespace Festival.Core
{
    /// <summary>The campfire debrief after every level: vote a friend per award, reveal together, shots and badges.</summary>
    public sealed partial class FestivalSimulation
    {
        // DEBRIEF-1: each debrief draws two worst awards and one best award by seed. Every connected player votes one
        // friend per award; nothing is shown until every vote is in, then all the winners are revealed together.
        // Worst winners take a shot as the next level starts, and every winner wears the award until that level's Results.
        const double ShotSeconds=90;
        // Solo practice draws no awards, so its (empty) verdict counts as revealed and the host can move straight on.
        public static bool ReviewRevealed(RoundState s)=>s.ReviewWinners.Count==s.ReviewAwards.Count;
        // Before the reveal a viewer sees their own picks and who else has voted, never whom anyone else picked.
        public static List<CampReviewVote> VisibleReviewVotes(RoundState s,string viewer)=>ReviewRevealed(s)?s.ReviewVotes:s.ReviewVotes.ConvertAll(v=>v.PlayerId==viewer?v:new CampReviewVote{PlayerId=v.PlayerId,Award=v.Award});
        // Whether a player has picked a friend for every award. It reads only who voted on what, which every view carries.
        public static bool VotedOnEveryAward(RoundState s,string playerId){for(int slot=0;slot<s.ReviewAwards.Count;slot++)if(!s.ReviewVotes.Exists(v=>v.PlayerId==playerId&&v.Award==slot))return false;return true;}
        // Winning a worst award pours its winner a shot as the next level starts.
        public static bool TakesShot(string award)=>Array.IndexOf(CampFeatures.WorstAwards,award)>=0;
        void DrawReviewAwards()
        {
            if(State.Players.FindAll(p=>p.Connected).Count<2)return;
            var worst=CampFeatures.WorstAwards;var random=new ContentRandom(State.Seed);int first=random.Next(worst.Length);
            State.ReviewAwards.Add(worst[first]);State.ReviewAwards.Add(worst[(first+1+random.Next(worst.Length-1))%worst.Length]);
            State.ReviewAwards.Add(CampFeatures.BestAwards[random.Next(CampFeatures.BestAwards.Length)]);
        }
        CommandResult VoteForReview(PlayerState p,GameCommand c)
        {
            if(State.Phase!="CampReview"||ReviewRevealed(State))return Reject("No debrief vote is open");
            var target=Player(c.TargetId);
            if(c.Amount<0||c.Amount>=State.ReviewAwards.Count||target==null||!target.Connected)return Reject("Pick a friend at camp for one of the awards");
            var vote=State.ReviewVotes.Find(v=>v.PlayerId==p.Id&&v.Award==c.Amount);
            if(vote==null)State.ReviewVotes.Add(new CampReviewVote{PlayerId=p.Id,TargetId=target.Id,Award=c.Amount});else vote.TargetId=target.Id;
            RevealIfAllVoted();
            return Ok("Vote in for "+State.ReviewAwards[c.Amount]);
        }
        // Runs after every vote and disconnect. Each award goes to the most-voted player; a tie goes to one of the
        // leaders, picked by the round seed.
        void RevealIfAllVoted()
        {
            if(State.Phase!="CampReview"||ReviewRevealed(State))return;
            foreach(var voter in State.Players)if(voter.Connected&&!VotedOnEveryAward(State,voter.Id))return;
            var random=new ContentRandom(State.Seed);
            for(int slot=0;slot<State.ReviewAwards.Count;slot++)
            {
                var tally=new int[State.Players.Count];
                foreach(var v in State.ReviewVotes)if(v.Award==slot){int i=State.Players.FindIndex(x=>x.Id==v.TargetId);if(i>=0)tally[i]++;}
                int most=0;foreach(var n in tally)most=Math.Max(most,n);
                var leaders=new List<PlayerState>();for(int i=0;i<tally.Length;i++)if(tally[i]==most)leaders.Add(State.Players[i]);
                var winner=leaders[random.Next(leaders.Count)];string award=State.ReviewAwards[slot];
                State.ReviewWinners.Add(winner.Id);winner.Badge=winner.Badge==""?award:winner.Badge+", "+award;
            }
        }
        CommandResult FinishCampReview(PlayerState p)
        {
            if(State.Phase!="CampReview"||p.Id!=State.HostPlayerId)return Reject("Only the host can close the camp review");
            if(!ReviewRevealed(State))return Reject("Wait for every connected player to vote on every award");
            State.Phase="Shopping";return Ok("Review closed. Camp supplies are open for the next round");
        }
        // Called once, as the next level starts: one shot per worst award won.
        void PourShots()
        {
            for(int slot=0;slot<State.ReviewWinners.Count;slot++)
            {
                var winner=Player(State.ReviewWinners[slot]);
                if(winner!=null&&TakesShot(State.ReviewAwards[slot]))winner.Effects.Add(new ActiveEffect{Id="shot",InstanceId=Id("effect"),SourceCommandId="debrief",StartSeconds=State.SimulationSeconds,RemainingSeconds=ShotSeconds});
            }
        }
    }
}
