using NUnit.Framework;
using Festival.Core;
using Festival.Presentation;

namespace Festival.Tests
{
    // The player card's warning line, built from a crafted view the way a client holds it.
    public sealed class HudVitalsTests
    {
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
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("SECURITY STOP"),"a cop stopping you over visible stock");
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
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("SECURITY STOP"),"a swarm on a friend doesn't outrank a stop on you");
            swarm.TargetId="you";swarm.Mode="Distracted";
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("SECURITY STOP"),"a distracted festivalgoer is no longer swarming");
            swarm.Mode="Swarming";s.Npcs.Add(new NpcState{Id="wook_1",Kind="Wook",Mode="Blending",Suspicion=95});
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("CROWD 90 / SWARMING"),"the swarm on you shows its own suspicion, not the crowd's worst");
            you.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=2,RemainingSeconds=431});
            Assert.That(FestivalHudText.Condition(s,you),Does.StartWith("CROWD 90 / SWARMING").And.Contain("DOSE 431S"),"the effects still follow the warning");
        }
    }
}
