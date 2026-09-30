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
            Assert.That(FestivalHudText.Condition(s,you),Does.StartWith("SECURITY ARRESTWARNING"),"a dosed tripper still sees the cop about to detain them");
            Assert.That(FestivalHudText.Condition(s,you),Does.Contain("DOSE 431S"),"and still sees the dose after it");
            s.Npcs.Add(new NpcState{Id="wook",Kind="Wook",Mode="Swarming",Suspicion=80});you.Effects.Add(new ActiveEffect{Id="shot",RemainingSeconds=60});
            Assert.That(FestivalHudText.Condition(s,you),Does.StartWith("CROWD 80 / SWARMING"),"a dosed friend with a shot in them still sees the crowd turn");
            Assert.That(FestivalHudText.Condition(s,you),Does.Contain("DOSE 431S").And.Contain("SHOT 60S"),"with both effects after it");
            s.Npcs.Clear();you.Effects.Clear();
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("CROWD CLEAR"),"nothing to report");
        }
    }
}
