using NUnit.Framework;
using Festival.Core;
using Festival.Presentation;

namespace Festival.Tests
{
    // The player card's warning line and threat bar, built from a crafted view the way a client holds it.
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
            s.Npcs.Clear();s.Npcs.Add(new NpcState{Id="wook",Kind="Wook",Mode="Swarming",TargetId="you",Suspicion=80});you.Effects.Add(new ActiveEffect{Id="shot",RemainingSeconds=60});
            Assert.That(FestivalHudText.ThreatBands[FestivalHudText.Threat(s,you).Band],Is.EqualTo("SWARMING"),"a dosed friend with a shot in them still sees the crowd turn, on the bar");
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("DOSE 431S, SHOT 60S"),"with both effects on the line");
            s.Npcs.Clear();you.Effects.Clear();
            Assert.That(FestivalHudText.Condition(s,you),Is.Empty,"nothing to report");
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
            Assert.That(FestivalHudText.Condition(s,you),Is.Empty,"a cop back on patrol, or on a friend, leaves the card to the crowd's bar");
            Assert.That(FestivalHudText.Threat(s,you),Is.EqualTo((.03f,0)),"which reads the crowd's 3");
            s.Npcs.Remove(wook);
            Assert.That(FestivalHudText.Condition(s,you),Is.Empty,"and a patrol alone is nothing to report");
        }

        // HUD-5: a festivalgoer swarming you lunges for 20 HP a hit. The threat bar says so (SWARMING, full) while a cop stopping
        // you over visible stock keeps the warning line, so both show at once. Only a swarm on you counts (a view shows a festivalgoer's
        // real mode only to the player it targets): one on a friend, or one a distraction has broken off, reads by suspicion alone.
        [Test] public void ASwarmOnYouFillsTheBarBesideASecurityStop()
        {
            var s=new RoundState{Phase="Playing"};var you=new PlayerState{Id="you",Name="You"};s.Players.Add(you);s.Players.Add(new PlayerState{Id="friend",Name="Friend"});
            var swarm=new NpcState{Id="wook_0",Kind="Wook",Mode="Swarming",TargetId="you",Suspicion=90};var arrester=new NpcState{Id="cop_1",Kind="Cop",Mode="ArrestWarning",TargetId="you"};
            s.Npcs.Add(new NpcState{Id="cop_0",Kind="Cop",Mode="Stop",TargetId="you"});s.Npcs.Add(swarm);
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo(Stop),"a cop stopping you keeps the line");
            Assert.That(FestivalHudText.Threat(s,you),Is.EqualTo((1f,4)),"while the bar fills with the swarm on you");
            s.Npcs.Add(arrester);
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("ARREST WARNING"),"a cop about to detain you still wins the line");
            s.Npcs.Remove(arrester);swarm.TargetId="friend";
            Assert.That(FestivalHudText.Threat(s,you),Is.EqualTo((.9f,3)),"a swarm on a friend reads only as its suspicion of you");
            swarm.TargetId="you";swarm.Mode="Distracted";
            Assert.That(FestivalHudText.Threat(s,you),Is.EqualTo((.9f,3)),"a distracted festivalgoer is no longer swarming");
            swarm.Mode="Swarming";you.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=2,RemainingSeconds=431});
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo(Stop+"  •  DOSE 431S"),"the effects still follow the warning");
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
            foreach(var life in new[]{"Spirit","Detained","Downed"}){you.Life=life;Assert.That(FestivalHudText.Condition(s,you),Is.Empty,"a storm for a "+life+" player");}
            you.Life="Alive";
            s.ElapsedSeconds=0;
            Assert.That(FestivalHudText.Condition(s,you),Is.Empty,"calm air is clear");
            s.ElapsedSeconds=storm;var arrester=new NpcState{Id="cop",Kind="Cop",Mode="ArrestWarning",TargetId="you"};s.Npcs.Add(arrester);
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("ARREST WARNING"),"a cop about to detain you outranks the storm");
            s.Npcs.Remove(arrester);var wook=new NpcState{Id="wook",Kind="Wook",Mode="Blending",Suspicion=3};s.Npcs.Add(wook);
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("DUST STORM: SEEN ONLY WITHIN 5 M"),"the crowd's suspicion is on the bar, so the storm keeps the line");
            s.Npcs.Remove(wook);you.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=2,RemainingSeconds=431});
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("DUST STORM: SEEN ONLY WITHIN 5 M  •  DOSE 431S"),"a dosed player's effects follow the storm line");
            you.Effects.Clear();s.Phase="CampReview";
            Assert.That(FestivalHudText.Condition(s,you),Is.Empty,"back at camp the storm stays at the festival");
            s.Phase="Playing";s.FestivalIndex=Festivals.PoloFestival;
            Assert.That(FestivalHudText.Condition(s,you),Is.Empty,"Palm Mirage has no dust at the same moment");
        }

        // POLO-6: an influencer filming you puts the festivalgoers round you on alert, so the card says ON CAMERA, ahead of a dust storm
        // as one warning (single-spaced, like a stop's ways out). Your own view names who films you (VisualFilmedBy). An arrest
        // warning or a security stop still takes the whole line, and effects still trail it. The crowd itself is on the bar.
        [Test] public void OnCameraLeadsTheCrowdLine()
        {
            var s=new RoundState{Phase="Playing"};var you=new PlayerState{Id="you",Name="You"};s.Players.Add(you);
            s.Npcs.Add(new NpcState{Id="wook_2",Kind="Wook",Twist=FestivalSimulation.Influencer});you.VisualFilmedBy.Add("wook_2");
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("ON CAMERA"),"on camera with nobody suspicious yet");
            s.Npcs.Add(new NpcState{Id="wook_0",Kind="Wook",Mode="Watching",Suspicion=30});
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("ON CAMERA"),"the crowd it is turning shows on the bar");
            Assert.That(FestivalHudText.Threat(s,you),Is.EqualTo((.3f,1)));
            you.Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=2,RemainingSeconds=431});
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("ON CAMERA  •  DOSE 431S"),"effects still trail it");
            you.Effects.Clear();var cop=new NpcState{Id="cop_0",Kind="Cop",Mode="Stop",TargetId="you"};s.Npcs.Add(cop);
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo(Stop),"a security stop takes the whole line");
            cop.Mode="ArrestWarning";
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("ARREST WARNING"),"so does an arrest warning");
            s.Npcs.Remove(cop);s.FestivalIndex=Festivals.PlayaFestival;s.SpinSeed=12345;
            for(double t=0;t<Festivals.NightSeconds&&!FestivalSimulation.DustStorm(s);t++)s.ElapsedSeconds=t;
            Assert.That(FestivalHudText.Condition(s,you),Is.EqualTo("ON CAMERA • DUST STORM: SEEN ONLY WITHIN 5 M"),"a storm joins the camera warning");
            s.ElapsedSeconds=0;you.VisualFilmedBy.Clear();
            Assert.That(FestivalHudText.Condition(s,you),Is.Empty,"out of the shot the line is clear");
        }
        // The threat bar fills with the crowd's worst suspicion of you, 0-100, and names its band: watching at 25, questioning at 45,
        // accusing at 70 (Danger.WookTick). A festivalgoer swarming you fills it. One scattering from an arrest never counts.
        [Test] public void TheThreatBarFillsWithTheCrowdsWorstSuspicionOfYou()
        {
            var s=new RoundState{Phase="Playing"};var you=new PlayerState{Id="you",Name="You"};s.Players.Add(you);
            var crowd=new NpcState{Id="wook_0",Kind="Wook",Mode="Blending"};s.Npcs.Add(crowd);s.Npcs.Add(new NpcState{Id="wook_1",Kind="Wook",Mode="Blending",Suspicion=10});
            s.Npcs.Add(new NpcState{Id="cop_0",Kind="Cop",Mode="Patrol",Suspicion=99});
            string Read(){var threat=FestivalHudText.Threat(s,you);return threat.Fill.ToString("0.00")+" "+FestivalHudText.ThreatBands[threat.Band];}
            foreach(var (level,expected) in new[]{(0.0,"0.10 CALM"),(30.0,"0.30 WATCHING"),(50.0,"0.50 QUESTIONING"),(75.0,"0.75 ACCUSING"),(24.9,"0.25 CALM"),(45.0,"0.45 QUESTIONING"),(70.0,"0.70 ACCUSING")})
            {crowd.Suspicion=level;Assert.That(Read(),Is.EqualTo(expected),"the crowd's worst at "+level+", never security's");}
            crowd.Mode="Swarming";crowd.TargetId="you";
            Assert.That(Read(),Is.EqualTo("1.00 SWARMING"),"a festivalgoer swarming you fills the bar");
            crowd.Mode=FestivalSimulation.Scattering;crowd.TargetId="";crowd.Suspicion=80;
            Assert.That(Read(),Is.EqualTo("0.10 CALM"),"one scattering from an arrest at 80 never counts");
            s.Npcs.Clear();
            Assert.That(Read(),Is.EqualTo("0.00 CALM"),"nobody around: calm and empty");
            Assert.That(FestivalHudText.Vitals(s,you),Does.Not.Contain("CROWD"),"the vitals line no longer carries the crowd");
        }

        // The bar eases toward its fill, and reduced motion jumps straight to it.
        [Test] public void TheThreatBarEasesUnlessMotionIsReduced()
        {
            float eased=FestivalHudText.EaseThreat(0,.5f,1/60f,false);
            Assert.That(eased,Is.GreaterThan(0).And.LessThan(.5f),"one frame moves part of the way");
            float later=eased;for(int frame=0;frame<120;frame++)later=FestivalHudText.EaseThreat(later,.5f,1/60f,false);
            Assert.That(later,Is.EqualTo(.5f).Within(.005f),"two seconds settle on the fill");
            Assert.That(FestivalHudText.EaseThreat(0,.5f,1/60f,true),Is.EqualTo(.5f),"reduced motion is instant");
            Assert.That(FestivalHudText.EaseThreat(.8f,.2f,1,false),Is.GreaterThanOrEqualTo(.2f),"easing never overshoots, however long the frame");
        }
    }
}
