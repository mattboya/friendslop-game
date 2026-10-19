using NUnit.Framework;
using Festival.Core;
using Festival.Presentation;

namespace Festival.Tests
{
    // The player card's warning line, built from a crafted view the way a client holds it.
    public sealed class HudVitalsTests
    {
        // ESC-2: a stop says what you can do about it: talk, ditch the stock or get away, or 20 s on it ends in arrest.
        const string Stop="SECURITY STOP • TALK (E) • DITCH IT (G) • OR WALK AWAY";

        // The spinner's dose lasts the whole level, and on Night 2 everyone is dosed and the debrief's losers take shots: none of it
        // may take the place of the only on-screen warning that the crowd is turning or a cop is about to detain you.
        [Test] public void EffectsNeverHideTheCrowdOrSecurity()
        {
            var s=new RoundState{Phase="Playing",LevelIndex=3};var you=new PlayerState{Id="you",Name="You"};s.Players.Add(you);
            you.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=2,RemainingSeconds=431});
            s.Npcs.Add(new NpcState{Id="cop",Kind="Cop",Mode="ArrestWarning",TargetId="you"});
            Assert.That(FestivalHudText.Condition(s,you),Does.StartWith("ARREST WARNING"),"a dosed tripper still sees the cop about to detain them");
            Assert.That(FestivalHudText.Condition(s,you),Does.Contain("DOSE 431S"),"and still sees the dose after it");
            s.Npcs.Clear();s.Npcs.Add(new NpcState{Id="wook",Kind="Wook",Mode="Swarming",Suspicion=80});you.Effects.Add(new ActiveEffect{Id="shot",RemainingSeconds=60});
            Assert.That(FestivalHudText.Condition(s,you),Does.StartWith("CROWD 80 / SWARMING"),"a dosed friend with a shot in them still sees the crowd turn");
            Assert.That(FestivalHudText.Condition(s,you),Does.Contain("DOSE 431S").And.Contain("SHOT 60S"),"with both effects after it");
            s.Npcs.Clear();you.Effects.Clear();
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("CROWD CLEAR"),"nothing to report");
        }

        // Security on you outranks the crowd: after a deal in view some festivalgoer nearly always holds a little suspicion, and the
        // card is the only warning that a cop will detain you in seconds. The worse of two cops on you shows.
        [Test] public void SecurityOnYouOutranksTheCrowd()
        {
            var s=new RoundState{Phase="Playing"};var you=new PlayerState{Id="you",Name="You"};s.Players.Add(you);
            var wook=new NpcState{Id="wook",Kind="Wook",Mode="Blending",Suspicion=3};
            var stopper=new NpcState{Id="cop_0",Kind="Cop",Mode="Stop",TargetId="you"};var arrester=new NpcState{Id="cop_1",Kind="Cop",Mode="ArrestWarning",TargetId="you"};
            s.Npcs.Add(wook);s.Npcs.Add(stopper);s.Npcs.Add(arrester);
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("ARREST WARNING"),"a cop about to detain you, whatever the crowd holds");
            arrester.TargetId="friend";
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo(Stop),"a cop stopping you over visible stock, and what you can do");
            Assert.That(FestivalHudText.Condition(s,you,"R","K"),Is.EqualTo("SECURITY STOP • TALK (R) • DITCH IT (K) • OR WALK AWAY"),"the bound Interact and Drop keys");
            stopper.Mode="Patrol";
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("CROWD 3 / BLENDING"),"a cop back on patrol, or on a friend, leaves the card to the crowd");
            s.Npcs.Remove(wook);
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("CROWD CLEAR"),"and a patrol alone is nothing to report");
        }

        // HUD-5: a festivalgoer swarming you lunges for 20 HP a hit, so it outranks a cop stopping you over visible stock; a cop about
        // to detain you still wins. Only a swarm on you counts (a view shows a festivalgoer's real mode only to the player it targets):
        // one on a friend, or one a distraction has broken off, leaves the card to the stop.
        [Test] public void SwarmOnYouOutranksASecurityStop()
        {
            var s=new RoundState{Phase="Playing"};var you=new PlayerState{Id="you",Name="You"};s.Players.Add(you);s.Players.Add(new PlayerState{Id="friend",Name="Friend"});
            var swarm=new NpcState{Id="wook_0",Kind="Wook",Mode="Swarming",TargetId="you",Suspicion=90};var arrester=new NpcState{Id="cop_1",Kind="Cop",Mode="ArrestWarning",TargetId="you"};
            s.Npcs.Add(new NpcState{Id="cop_0",Kind="Cop",Mode="Stop",TargetId="you"});s.Npcs.Add(swarm);
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("CROWD 90 / SWARMING"),"a swarm on you shows over a cop stopping you");
            s.Npcs.Add(arrester);
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("ARREST WARNING"),"a cop about to detain you still wins");
            s.Npcs.Remove(arrester);swarm.TargetId="friend";
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo(Stop),"a swarm on a friend doesn't outrank a stop on you");
            swarm.TargetId="you";swarm.Mode="Distracted";
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo(Stop),"a distracted festivalgoer is no longer swarming");
            swarm.Mode="Swarming";s.Npcs.Add(new NpcState{Id="wook_1",Kind="Wook",Mode="Blending",Suspicion=95});
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("CROWD 90 / SWARMING"),"the swarm on you shows its own suspicion, not the crowd's worst");
            you.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=2,RemainingSeconds=431});
            Assert.That(FestivalHudText.Condition(s,you),Does.StartWith("CROWD 90 / SWARMING").And.Contain("DOSE 431S"),"the effects still follow the warning");
        }

        // PLAYA-3: in an Ember Playa dust storm everyone, security too, sees only 5 m, so the card says so where it would read
        // CROWD CLEAR. A client works the storm out from its own view (festival, spin seed and clock), as the host does. Any real
        // warning still outranks it, and effects still trail it.
        [Test] public void ADustStormTakesThePlaceOfCrowdClear()
        {
            var s=new RoundState{Phase="Playing",FestivalIndex=Festivals.PlayaFestival,SpinSeed=12345};var you=new PlayerState{Id="you",Name="You"};s.Players.Add(you);
            double storm=-1;for(double t=0;t<Festivals.NightSeconds&&storm<0;t++){s.ElapsedSeconds=t;if(FestivalSimulation.DustStorm(s))storm=t;}
            Assert.That(storm,Is.GreaterThan(0),"setup: this level calms at first, then a storm rolls in");
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("DUST STORM: SEEN ONLY WITHIN 5 M"),"a storm with nothing else to report");
            // Nothing sees a spirit, or a detained or downed player, at any range (Danger.Sees), so their card stays clear.
            foreach(var life in new[]{"Spirit","Detained","Downed"}){you.Life=life;Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("CROWD CLEAR"),"a storm for a "+life+" player");}
            you.Life="Alive";
            s.ElapsedSeconds=0;
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("CROWD CLEAR"),"calm air is clear");
            s.ElapsedSeconds=storm;var arrester=new NpcState{Id="cop",Kind="Cop",Mode="ArrestWarning",TargetId="you"};s.Npcs.Add(arrester);
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("ARREST WARNING"),"a cop about to detain you outranks the storm");
            s.Npcs.Remove(arrester);var wook=new NpcState{Id="wook",Kind="Wook",Mode="Blending",Suspicion=3};s.Npcs.Add(wook);
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("CROWD 3 / BLENDING"),"so does any suspicion in the crowd");
            s.Npcs.Remove(wook);you.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=2,RemainingSeconds=431});
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("DUST STORM: SEEN ONLY WITHIN 5 M  •  DOSE 431S"),"a dosed player's effects follow the storm line");
            you.Effects.Clear();s.Phase="CampReview";
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("CROWD CLEAR"),"back at camp the storm stays at the festival");
            s.Phase="Playing";s.FestivalIndex=Festivals.PoloFestival;
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("CROWD CLEAR"),"Palm Mirage has no dust at the same moment");
        }

        // POLO-6: an influencer filming you puts the festivalgoers round you on alert, so the card says ON CAMERA ahead of the crowd,
        // as one warning (single-spaced, like a stop's ways out). Your own view names who films you (VisualFilmedBy). An arrest
        // warning, a swarm on you or a security stop still takes the whole line, and effects still trail it.
        [Test] public void OnCameraLeadsTheCrowdLine()
        {
            var s=new RoundState{Phase="Playing"};var you=new PlayerState{Id="you",Name="You"};s.Players.Add(you);
            s.Npcs.Add(new NpcState{Id="wook_2",Kind="Wook",Twist=FestivalSimulation.Influencer});you.VisualFilmedBy.Add("wook_2");
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("ON CAMERA • CROWD CLEAR"),"on camera with nobody suspicious yet");
            s.Npcs.Add(new NpcState{Id="wook_0",Kind="Wook",Mode="Watching",Suspicion=30});
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("ON CAMERA • CROWD 30 / WATCHING"),"ahead of the crowd it is turning");
            you.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=2,RemainingSeconds=431});
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("ON CAMERA • CROWD 30 / WATCHING  •  DOSE 431S"),"effects still trail it");
            you.Effects.Clear();var cop=new NpcState{Id="cop_0",Kind="Cop",Mode="Stop",TargetId="you"};s.Npcs.Add(cop);
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo(Stop),"a security stop takes the whole line");
            cop.Mode="ArrestWarning";
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("ARREST WARNING"),"so does an arrest warning");
            s.Npcs.Remove(cop);var swarm=new NpcState{Id="wook_1",Kind="Wook",Mode="Swarming",TargetId="you",Suspicion=80};s.Npcs.Add(swarm);
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("CROWD 80 / SWARMING"),"and a swarm on you");
            s.Npcs.Remove(swarm);you.VisualFilmedBy.Clear();
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("CROWD 30 / WATCHING"),"out of the shot the card is back to the crowd");
        }
    }
}
