using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;
// DEBRIEF-1: the campfire debrief after every level. Everyone votes a friend for each of three drawn awards, nothing
// shows until every vote is in, then the worst winners take a shot and every winner wears a badge for one level.
public static class DebriefTests
{
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("Debrief: "+message);}
    static CommandResult Act(FestivalSimulation s,string player,string kind,string target="",int amount=0)=>s.Execute(player,new GameCommand{Id="debrief_"+(++sequence),Kind=kind,TargetId=target,Amount=amount});
    static readonly string[] Crew={"a","b","c","d","e"};
    static FestivalSimulation Level(int seed,int crew){var s=new FestivalSimulation(seed);for(int i=0;i<crew;i++)s.AddPlayer(Crew[i],Crew[i].ToUpperInvariant());return s;}
    // The level just ended with this result; the host brings everyone back to the campfire.
    static FestivalSimulation Campfire(FestivalSimulation s,string result="Success"){s.State.Phase="Results";s.State.Result=result;Check(Act(s,"a","Reset").Accepted&&s.State.Phase=="CampReview","the host opens the debrief after "+result);return s;}
    static FestivalSimulation Campfire(int seed,int crew,string result="Success")=>Campfire(Level(seed,crew),result);
    // picks[slot] is the friend this voter picks for that award.
    static void Vote(FestivalSimulation s,string voter,params string[] picks){for(int slot=0;slot<picks.Length;slot++)Check(Act(s,voter,"ReviewVote",picks[slot],slot).Accepted,voter+" votes "+picks[slot]+" for award "+slot);}
    // Close the debrief and load into the next level. The crowd is cleared so 90 s of play cannot end the level.
    static void NextLevel(FestivalSimulation s)
    {
        Check(Act(s,"a","FinishReview").Accepted&&s.State.Phase=="Shopping","the host closes the debrief");
        s.State.Phase="Loading";s.State.Npcs.Clear();
        foreach(var p in s.State.Players)if(p.Connected)Check(Act(s,p.Id,"MapReady").Accepted,p.Id+" loads in");
        Check(s.State.Phase=="Playing","the next level starts");
    }
    static int Shots(FestivalSimulation s,string id)=>s.Player(id).Effects.FindAll(e=>e.Id=="shot").Count;
    static bool Worst(string award)=>Array.IndexOf(CampFeatures.WorstAwards,award)>=0;

    public static void Run()
    {
        AwardPools();
        AwardsAreDrawnBySeedAfterEveryLevel();
        VotesStayHiddenUntilEveryoneHasVoted();
        OnlyValidVotesCount();
        RevealCrownsThePluralityPick();
        TiesAreSettledBySeed();
        WorstWinnersTakeAShotWhenTheNextLevelStarts();
        BadgesLastOneLevel();
        SoloSkipsTheVote();
        ALeavingHoldoutCompletesTheVote();
        SnapshotsKeepTheDebrief();
    }

    static void AwardPools()
    {
        Check(string.Join("|",CampFeatures.WorstAwards)=="Worst Dancer|Trusted the Narc|Got Lost|Blew Our Cover","the worst-award pool");
        Check(string.Join("|",CampFeatures.BestAwards)=="MVP Tripper|Carried the Team|Best Moves","the best-award pool");
    }

    static void AwardsAreDrawnBySeedAfterEveryLevel()
    {
        var nights=new HashSet<string>();var seen=new HashSet<string>();
        for(int seed=1;seed<=40;seed++)
        {
            var awards=Campfire(seed,2,seed%2==0?"Success":"Time expired").State.ReviewAwards;
            Check(awards.Count==3,"three awards after every level, won or lost (seed "+seed+")");
            Check(Worst(awards[0])&&Worst(awards[1])&&awards[0]!=awards[1],"two different worst awards come first (seed "+seed+")");
            Check(Array.IndexOf(CampFeatures.BestAwards,awards[2])>=0,"then one best award (seed "+seed+")");
            Check(string.Join("|",Campfire(seed,3).State.ReviewAwards)==string.Join("|",awards),"the same night draws the same awards (seed "+seed+")");
            nights.Add(string.Join("|",awards));seen.UnionWith(awards);
        }
        Check(nights.Count>5,"different nights draw different awards");
        Check(seen.Count==CampFeatures.WorstAwards.Length+CampFeatures.BestAwards.Length,"every award in both pools comes up");
    }

    static void VotesStayHiddenUntilEveryoneHasVoted()
    {
        var s=Campfire(20,3);
        Vote(s,"a","b","b","c");Vote(s,"b","b","a","c");Vote(s,"c","c","a");
        Check(s.State.ReviewWinners.Count==0,"no verdict while c still has an award to vote on");
        Check(!Act(s,"a","FinishReview").Accepted,"the host cannot close the debrief before the reveal");
        var seenByC=FestivalSimulation.VisibleReviewVotes(s.State,"c");
        Check(seenByC.Count==8&&seenByC.FindAll(v=>v.PlayerId!="c").TrueForAll(v=>v.TargetId==""),"c sees that a and b voted, never whom they picked");
        Check(seenByC.Exists(v=>v.PlayerId=="c"&&v.Award==1&&v.TargetId=="a"),"c sees their own picks");
        Check(s.State.ReviewVotes.TrueForAll(v=>v.TargetId!=""),"hiding votes from a viewer keeps them on the host");
        Check(Act(s,"c","ReviewVote","a",2).Accepted&&FestivalSimulation.ReviewRevealed(s.State)&&s.State.ReviewWinners.Count==3,"the last vote reveals every award together");
        Check(FestivalSimulation.VisibleReviewVotes(s.State,"c").Exists(v=>v.PlayerId=="a"&&v.Award==0&&v.TargetId=="b"),"after the reveal everyone sees who voted for whom");
        Check(!Act(s,"a","ReviewVote","a",0).Accepted&&s.State.ReviewVotes.Find(v=>v.PlayerId=="a"&&v.Award==0).TargetId=="b","votes are locked once revealed");
        Check(!Act(s,"b","FinishReview").Accepted&&Act(s,"a","FinishReview").Accepted&&s.State.Phase=="Shopping","after the reveal the host opens the shop");
    }

    static void OnlyValidVotesCount()
    {
        var s=Campfire(30,3);s.Disconnect("c");
        Check(!Act(s,"a","ReviewVote","nobody",0).Accepted,"a vote needs a real player");
        Check(!Act(s,"a","ReviewVote","c",0).Accepted,"a friend who left cannot be voted for");
        Check(!Act(s,"a","ReviewVote","b",3).Accepted&&!Act(s,"a","ReviewVote","b",-1).Accepted,"only the three award slots");
        Check(Act(s,"a","ReviewVote","a",0).Accepted,"self-votes are allowed");
        Check(Act(s,"a","ReviewVote","b",0).Accepted&&s.State.ReviewVotes.FindAll(v=>v.PlayerId=="a"&&v.Award==0).Count==1&&s.State.ReviewVotes.Find(v=>v.PlayerId=="a"&&v.Award==0).TargetId=="b","changing a vote before the reveal replaces it");
        Check(!Act(Level(31,2),"a","ReviewVote","b",0).Accepted,"no voting outside the debrief");
    }

    static void RevealCrownsThePluralityPick()
    {
        var s=Campfire(40,4);var awards=s.State.ReviewAwards;
        Vote(s,"a","b","c","d");Vote(s,"b","b","c","a");Vote(s,"c","a","c","d");Vote(s,"d","b","d","d");
        Check(string.Join(",",s.State.ReviewWinners)=="b,c,d","each award goes to the player with the most votes");
        Check(s.Player("b").Badge==awards[0]&&s.Player("c").Badge==awards[1]&&s.Player("d").Badge==awards[2]&&s.Player("a").Badge=="","each winner wears their award as a badge");
    }

    static void TiesAreSettledBySeed()
    {
        var winners=new HashSet<string>();
        for(int seed=50;seed<70;seed++)
        {
            // a and b get two votes each for every award; c gets one.
            string Tie(){var s=Campfire(seed,5);foreach(var (voter,pick) in new[]{("a","a"),("b","a"),("c","b"),("d","b"),("e","c")})Vote(s,voter,pick,pick,pick);return string.Join(",",s.State.ReviewWinners);}
            string verdict=Tie();
            Check(verdict==Tie(),"the same night settles the same tie the same way (seed "+seed+")");
            winners.UnionWith(verdict.Split(','));
        }
        Check(winners.SetEquals(new[]{"a","b"}),"a tie goes to one of the leaders, and not always the same one");
    }

    static void WorstWinnersTakeAShotWhenTheNextLevelStarts()
    {
        var s=Campfire(70,3);var awards=s.State.ReviewAwards;Check(awards.Count==3,"three awards are up for a vote");
        Vote(s,"a","b","c","a");Vote(s,"b","b","c","a");Vote(s,"c","b","c","a");
        Check(Shots(s,"b")==0&&Shots(s,"c")==0,"nobody drinks at the campfire; the shot waits for the next level");
        Check(Act(s,"a","FinishReview").Accepted&&Shots(s,"b")==0,"no shot while shopping");
        s.State.Phase="Loading";s.State.Npcs.Clear();Act(s,"a","MapReady");Act(s,"b","MapReady");
        Check(Shots(s,"b")==0,"no shot until the whole crew has loaded in");
        Act(s,"c","MapReady");Check(s.State.Phase=="Playing","the next level starts");
        Check(Worst(awards[0])&&Worst(awards[1])&&Shots(s,"b")==1&&Shots(s,"c")==1&&Shots(s,"a")==0,"each worst winner takes a shot; the best winner does not");
        var shot=s.Player("b").Effects.Find(e=>e.Id=="shot");var only=new PlayerState();only.Effects.Add(shot);
        Check(Math.Abs(Intoxication.MovementMultiplier(only)-.9f)<1e-6,"a shot slows walking to 90%");
        Check(Math.Abs(Intoxication.LateralDrift(only,shot.StartSeconds+1))>.01,"a shot makes steps drift");
        s.Tick(60);s.Tick(29.5);Check(Shots(s,"b")==1&&Shots(s,"c")==1&&s.State.Phase=="Playing","the shot lasts the first 90 seconds");
        s.Tick(.6);Check(Shots(s,"b")==0&&Shots(s,"c")==0,"and wears off at 90 seconds");
    }

    static void BadgesLastOneLevel()
    {
        var s=Campfire(80,2);Vote(s,"a","b","b","a");Vote(s,"b","b","b","a");
        Check(s.State.ReviewWinners.Count==3,"the crew's votes are revealed");
        string both=s.State.ReviewAwards[0]+", "+s.State.ReviewAwards[1];
        Check(s.Player("b").Badge==both&&s.Player("a").Badge==s.State.ReviewAwards[2],"a double winner wears both awards");
        NextLevel(s);
        Check(Shots(s,"b")==2&&Shots(s,"a")==0,"a double worst winner takes two shots");
        Check(s.Player("b").Badge==both,"badges are worn through the next level");
        s.State.ElapsedSeconds=s.State.DurationSeconds;s.Tick(.1);
        Check(s.State.Phase=="Results"&&s.Player("a").Badge==""&&s.Player("b").Badge=="","badges come off at that level's results");
        Campfire(s,s.State.Result);
        Check(s.State.ReviewAwards.Count==3&&s.State.ReviewWinners.Count==0&&s.State.ReviewVotes.Count==0,"the next debrief starts a fresh vote");
    }

    static void SoloSkipsTheVote()
    {
        var s=Campfire(90,1);
        Check(s.State.ReviewAwards.Count==0&&FestivalSimulation.ReviewRevealed(s.State),"solo practice draws no awards");
        Check(!Act(s,"a","ReviewVote","a",0).Accepted,"there is nothing to vote on");
        NextLevel(s);
        Check(Shots(s,"a")==0&&s.Player("a").Badge=="","no shot and no badge");
        s=Level(91,2);s.Disconnect("b");Campfire(s);
        Check(s.State.ReviewAwards.Count==0&&Act(s,"a","FinishReview").Accepted,"the last player left at camp skips the vote too");
    }

    static void ALeavingHoldoutCompletesTheVote()
    {
        var s=Campfire(100,3);Vote(s,"a","c","c","a");Vote(s,"b","c","c","b");
        Check(s.State.ReviewWinners.Count==0,"waiting on c");
        s.Disconnect("c");
        Check(FestivalSimulation.ReviewRevealed(s.State)&&s.State.ReviewWinners[0]=="c"&&s.State.ReviewWinners[1]=="c","when the holdout leaves, the votes already in are revealed");
        Check(Act(s,"a","FinishReview").Accepted,"and the host can move on");
    }

    static void SnapshotsKeepTheDebrief()
    {
        var json=new JsonSerializerOptions{IncludeFields=true};
        RoundState Copy(RoundState state)=>JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(state,json),json);
        bool Refused(RoundState state){try{new FestivalSimulation().Restore(state);return false;}catch(ArgumentException){return true;}}
        var s=Campfire(110,2);Vote(s,"a","b","b","a");Vote(s,"b","b","b","a");
        var restored=new FestivalSimulation();restored.Restore(Copy(s.State));
        Check(string.Join(",",restored.State.ReviewAwards)==string.Join(",",s.State.ReviewAwards)&&string.Join(",",restored.State.ReviewWinners)=="b,b,a","the awards and the verdict survive a snapshot");
        Check(restored.Player("b").Badge==s.Player("b").Badge&&restored.State.ReviewVotes.Find(v=>v.PlayerId=="a"&&v.Award==2).TargetId=="a","badges and votes survive a snapshot");

        s=Campfire(111,2);Vote(s,"a","b");
        var old=JsonNode.Parse(JsonSerializer.Serialize(s.State,json)).AsObject();
        foreach(var field in new[]{"ReviewAwards","ReviewWinners"}){Check(old.ContainsKey(field),field+" is a serialised public field");old.Remove(field);}
        foreach(var player in old["Players"].AsArray()){Check(player.AsObject().Remove("Badge"),"Badge is a serialised public field");}
        foreach(var vote in old["ReviewVotes"].AsArray()){Check(vote.AsObject().Remove("TargetId"),"TargetId is a serialised public field");}
        restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(old.ToJsonString(),json));
        Check(restored.State.Phase=="CampReview"&&FestivalSimulation.ReviewRevealed(restored.State)&&restored.Execute("a",new GameCommand{Id="old",Kind="FinishReview"}).Accepted,"a debrief saved before player votes lets the host move on");

        s=Campfire(112,2);Vote(s,"a","b","b","a");Vote(s,"b","b","b","a");
        var tamper=new Action<RoundState>[]{x=>x.ReviewAwards=null,x=>x.ReviewWinners=null,x=>x.ReviewWinners.RemoveAt(0),x=>x.ReviewWinners.Add("a")};
        for(int i=0;i<tamper.Length;i++){var bad=Copy(s.State);tamper[i](bad);Check(Refused(bad),"a snapshot with a broken verdict is refused (case "+i+")");}
    }
}
