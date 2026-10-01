using System;
using Festival.Core;

// HUD-4: a night's escort is headed for the way back to camp, as the rest of the HUD calls it; "the shuttle" is only the old
// stop's sign. The directions keep the openings other tests and players know them by.
public static class WayBackToCampTests
{
    static void Check(bool pass,string message){if(!pass)throw new Exception("WayBackToCamp: "+message);}

    public static void Run()
    {
        TheEscortsDirectionsNameTheWayBackToCamp();
        TheNightsRefusalNamesTheWayBackToCamp();
    }

    // Night 1: you found the friend and lead them, Sam follows, then the escort is gone and the friend needs one. The way back
    // to camp is 32 m south of where both of you stand.
    static void TheEscortsDirectionsNameTheWayBackToCamp()
    {
        var s=new RoundState{Phase="Playing",LevelIndex=1,FriendFound=true,FriendLeaderId="you"};
        var you=new PlayerState{Id="you",Name="You"};var sam=new PlayerState{Id="sam",Name="Sam"};s.Players.Add(you);s.Players.Add(sam);
        string Headline=FestivalGuidance.Headline(s,you);
        Check(Headline=="ESCORT FRIEND BACK TO CAMP","the headline matches a big crew's ESCORT BOTH FRIENDS BACK TO CAMP: "+Headline);
        Check(FestivalGuidance.Hint(s,you)=="Lead your friend to the way back to camp 32 m S. Stay close.","the escort leads: "+FestivalGuidance.Hint(s,you));
        Check(FestivalGuidance.Hint(s,sam)=="Follow the escort to the way back to camp 32 m S.","the rest follow: "+FestivalGuidance.Hint(s,sam));
        you.Life="Downed";
        Check(FestivalGuidance.Hint(s,sam)=="Friend needs an escort. Reach them and press E, then lead them to the way back to camp 32 m S.","an escort who falls lets go: "+FestivalGuidance.Hint(s,sam));
    }

    // A night's extract with the friend still lost says where they have to be brought.
    static void TheNightsRefusalNamesTheWayBackToCamp()
    {
        var s=new FestivalSimulation(3);s.AddPlayer("p0","P0");s.State.LevelIndex=1;
        var p=s.Player("p0");p.X=0;p.Z=19;
        Check(s.Execute("p0",new GameCommand{Id="ready",Kind="Ready"}).Accepted,"setup: ready at the trailhead");
        s.Tick(5.2);s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);s.Execute("p0",new GameCommand{Id="loaded",Kind="MapReady"});
        Check(s.State.Phase=="Playing"&&!s.State.SecondFriend.Active,"setup: a crew of one reaches Night 1, one friend lost");
        p.X=Festivals.CampGateX;p.Z=Festivals.CampGateZ;
        var refused=s.Execute("p0",new GameCommand{Id="extract",Kind="Extract"});
        Check(!refused.Accepted&&refused.Reason=="Bring the friend and a living survivor to the way back to camp","the refusal: "+refused.Reason);
    }
}
