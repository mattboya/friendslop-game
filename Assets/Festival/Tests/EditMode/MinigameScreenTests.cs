using Festival.Core;
using System.Reflection;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // MINI-1's screen: the level's minigame booth on the festival grounds, a stranger under a sign in the middle of its glowing ring.
    public sealed class MinigameScreenTests
    {
        private GameObject root;
        [TearDown]public void Cleanup(){if(root!=null)Object.DestroyImmediate(root);}

        [Test]public void WheelSeatWithoutTwistsUsesTheSameWorldClockThroughPauseAndResume()
        {
            root=new GameObject("impl-U58 wheel fallback");var world=root.AddComponent<FestivalWorld>();
            var state=new RoundState{Phase="Playing",SimulationSeconds=70};var rider=new PlayerState{Id="you",VisualWheelSince=20};state.Players.Add(rider);
            float lead=FestivalWorld.GlideLead(6,Time.unscaledDeltaTime);
            var expected=FestivalTwistVisuals.Seat(state,rider,70+lead);
            Assert.That(Vector3.Distance(world.WheelSeat(state,rider,6),expected),Is.LessThan(.001f),"fallback advances on world clock, not boarding timestamp");
            state.Phase=FestivalSimulation.MinigamePhase;state.Booth.Seconds=35;state.SimulationSeconds=105;
            Assert.That(Vector3.Distance(world.WheelSeat(state,rider,6),expected),Is.LessThan(.001f),"game holds seat");
            state.Phase="Playing";rider.VisualWheelSince+=35;
            Assert.That(Vector3.Distance(world.WheelSeat(state,rider,6),expected),Is.LessThan(.001f),"shifted boarding deadline does not jump fallback seat");
        }

        // The booth is a festivalgoer rig under a MINIGAME sign, in a glowing ring the size of the rules' that nothing in it collides
        // with. It stands where the view's level has its booth, facing the festival's middle, while the festival shows, its game under
        // way too; camp, a played booth and a level with none show nothing.
        [Test]public void TheBoothStandsInItsRingWhereTheLevelHasOne()
        {
            root=new GameObject("Booth test");var booth=FestivalMinigameView.Build(root.transform);
            Assert.That(booth.name,Is.EqualTo(FestivalMinigameView.BoothName));
            Assert.That(booth.GetComponentInChildren<FestivalCharacter>(true),Is.Not.Null,"a stranger stands at the booth");
            var sign=booth.GetComponentInChildren<TextMesh>(true);
            Assert.That(sign!=null&&sign.text.StartsWith("MINIGAME"),Is.True,"under a MINIGAME sign");
            foreach(var collider in booth.GetComponentsInChildren<Collider>(true))Assert.That(collider.enabled,Is.False,collider.name+" would block sight lines or steps the rules don't know about");
            var ring=booth.Find(FestivalMinigameView.RingName);
            Assert.That(ring!=null&&ring.childCount>=16,Is.True,"a ring is drawn all the way round");
            foreach(Transform dash in ring)
            {
                Assert.That(new Vector2(dash.localPosition.x,dash.localPosition.z).magnitude,Is.EqualTo(FestivalSimulation.MinigameRingRadius).Within(.05f),"each dash lies on the rules' ring");
                Assert.That(dash.GetComponent<Renderer>().sharedMaterial,Is.EqualTo(FestivalArtView.MaterialFor("StageGlowMint")),"and glows, so the ring reads at night");
            }

            var state=new RoundState{Phase="Playing",Booth=new MinigameState{Kind=FestivalSimulation.MysteryCupsGame,Spot=1}};var at=Festivals.BoothSpots[1];
            FestivalMinigameView.Show(booth,state);
            Assert.That(booth.gameObject.activeSelf,Is.True,"the level's booth shows");
            Assert.That(Vector2.Distance(new Vector2(booth.position.x,booth.position.z),new Vector2(at.X,at.Z)),Is.LessThan(1e-4f),"on its spot");
            Assert.That(Vector3.Angle(booth.forward,new Vector3(-at.X,0,-at.Z)),Is.LessThan(1f),"facing the festival's middle");
            state.Phase=FestivalSimulation.MinigamePhase;FestivalMinigameView.Show(booth,state);
            Assert.That(booth.gameObject.activeSelf,Is.True,"with its game under way it still stands");
            foreach(var (phase,spent,spot,where) in new[]{("Shopping",false,1,"camp"),("Playing",true,1,"a played booth"),("Playing",false,-1,"a level with none")})
            {
                state.Phase=phase;state.Booth.Spent=spent;state.Booth.Spot=spot;FestivalMinigameView.Show(booth,state);
                Assert.That(booth.gameObject.activeSelf,Is.False,where+" shows no booth");
            }
        }

        // The sign names the game the booth deals, for every game a booth can deal and the two still to join the bag, and its board
        // holds the longest name; a game this build cannot name keeps the plain sign.
        [Test]public void TheBoothSignNamesItsSelectedGame()
        {
            root=new GameObject("Sign test");var booth=FestivalMinigameView.Build(root.transform);
            var sign=booth.Find("Sign text").GetComponent<TextMesh>();var board=booth.Find("Sign board").GetComponent<Renderer>();
            var state=new RoundState{Phase="Playing",Booth=new MinigameState{Spot=1}};
            foreach(var kind in FestivalSimulation.MinigameKinds)
            {
                FestivalMinigameView.NameGame(booth,kind);
                Assert.That(sign.text,Is.EqualTo(FestivalHudText.MinigameName(kind)+FestivalMinigameView.SignRule).And.Not.EqualTo(FestivalMinigameView.SignText),kind+" is dealt, so its sign names it");
            }
            foreach(var (kind,name) in new[]{(FestivalSimulation.MysteryCupsGame,"MYSTERY CUPS"),(FestivalSimulation.DrumCircleGame,"DRUM CIRCLE"),(FestivalSimulation.LightShowGame,"LIGHT SHOW"),("AnotherGame","MINIGAME")})
            {
                state.Booth.Kind=kind;FestivalMinigameView.Show(booth,state);FestivalMinigameView.NameGame(booth,kind);booth.rotation=Quaternion.identity;
                Assert.That(sign.text,Is.EqualTo(name+"\nALL IN THE RING"),kind+"'s sign");
                var text=sign.GetComponent<Renderer>().bounds.size.x;
                Assert.That(text,Is.GreaterThan(0).And.LessThan(board.bounds.size.x),name+" fits its board");
            }
        }

        // MINI-4: Mystery Cups' cups stand in a numbered row on a table in front of the stranger, as many as the crew will play
        // before the game and as were poured once it runs, each drunk one gone. In the tripper's view (the only one carrying the
        // hint) the cup their hunch marks glows; in anyone else's none does. The cup the player up looks at lifts off the table.
        [Test]public void TheCupsStandOnTheTableAndOnlyTheTrippersHunchGlows()
        {
            root=new GameObject("Cups test");var booth=FestivalMinigameView.Build(root.transform);var at=Festivals.BoothSpots[1];
            var state=new RoundState{Phase="Playing",Booth=new MinigameState{Kind=FestivalSimulation.MysteryCupsGame,Spot=1}};
            for(int k=0;k<3;k++)state.Players.Add(new PlayerState{Id="p"+k,Name="P"+k});
            state.Players.Add(new PlayerState{Id="ghost",Life="Spirit"});
            var cups=booth.Find(FestivalMinigameView.CupsName);Assert.That(cups,Is.Not.Null,"the booth has its cups");
            FestivalMinigameView.Show(booth,state);FestivalMinigameView.ShowCups(booth,state,-1,null);
            Assert.That(Shown(cups),Is.EqualTo(new[]{0,1,2,3,4}),"before the game, the 5 cups three living players will play");
            Assert.That(cups.Find(FestivalMinigameView.CupName(0)).GetComponentInChildren<TextMesh>().text,Is.EqualTo("1"),"numbered from 1, as the HUD names them");
            for(int i=0;i<5;i++)Assert.That(Vector3.Distance(cups.Find(FestivalMinigameView.CupName(i)).position,FestivalMinigameView.CupAt(at,i,5)),Is.LessThan(1e-4f),"cup "+(i+1)+" stands where it is aimed at");
            Assert.That(FestivalMinigameView.CupAt(at,0,5).y,Is.InRange(.7f,1.2f),"on a table, about waist high");
            Assert.That(Vector3.Dot(FestivalMinigameView.CupAt(at,2,5)-new Vector3(at.X,0,at.Z),booth.forward),Is.GreaterThan(.5f),"in front of the stranger");

            state.Phase=FestivalSimulation.MinigamePhase;var b=state.Booth;b.Cups=6;b.Drunk.Add(1);b.Hint=3;
            FestivalMinigameView.Show(booth,state);FestivalMinigameView.ShowCups(booth,state,-1,null);
            Assert.That(Shown(cups),Is.EqualTo(new[]{0,2,3,4,5}),"the game's 6 cups, the drunk one gone");
            var table=cups.Find("Cup table");var under=Vector3.up*.15f;var gap=FestivalMinigameView.CupAt(at,1,6)-FestivalMinigameView.CupAt(at,0,6);
            bool OnTable(Vector3 point){var local=table.InverseTransformPoint(point);return Mathf.Abs(local.x)<=.5f&&Mathf.Abs(local.y)<=.5f&&Mathf.Abs(local.z)<=.5f;}
            Assert.That(OnTable(FestivalMinigameView.CupAt(at,0,6)-under)&&OnTable(FestivalMinigameView.CupAt(at,5,6)-under),Is.True,"on a table as long as their row");
            Assert.That(OnTable(FestivalMinigameView.CupAt(at,0,6)-under-gap*2)||OnTable(FestivalMinigameView.CupAt(at,5,6)-under+gap*2),Is.False,"and no longer");
            for(int i=0;i<6;i++)if(i!=1)Assert.That(Look(cups,i),Is.EqualTo(FestivalArtView.MaterialFor(i==3?FestivalMinigameView.HunchLook:FestivalMinigameView.CupLook)),"in the tripper's view only cup 4 glows, cup "+(i+1));
            b.Hint=-1;FestivalMinigameView.ShowCups(booth,state,-1,null);
            for(int i=0;i<6;i++)if(i!=1)Assert.That(Look(cups,i),Is.EqualTo(FestivalArtView.MaterialFor(FestivalMinigameView.CupLook)),"in anyone else's none does, cup "+(i+1));
            FestivalMinigameView.ShowCups(booth,state,4,null);
            Assert.That(cups.Find(FestivalMinigameView.CupName(4)).position.y,Is.GreaterThan(FestivalMinigameView.CupAt(at,4,6).y+.03f),"the cup looked at lifts");
            Assert.That(cups.Find(FestivalMinigameView.CupName(3)).position.y,Is.EqualTo(FestivalMinigameView.CupAt(at,3,6).y).Within(1e-4f),"the others stay down");
            state.Booth.Kind="AnotherGame";FestivalMinigameView.ShowCups(booth,state,-1,null);
            Assert.That(cups.gameObject.activeSelf,Is.False,"another game's booth has no cups");
        }

        // MINI-4: the cup under the aim dot is the one the view looks most nearly at, within a few degrees of it; looking at a gap
        // where a cup was drunk, away from the table, or with no Mystery Cups under way picks none.
        [Test]public void TheCupUnderTheAimDotIsTheOneDrunk()
        {
            root=new GameObject("Aim test");var at=Festivals.BoothSpots[1];var view=new GameObject("View").transform;view.SetParent(root.transform,false);
            var state=new RoundState{Phase=FestivalSimulation.MinigamePhase,Booth=new MinigameState{Kind=FestivalSimulation.MysteryCupsGame,Spot=1,Cups=6}};state.Booth.Drunk.Add(1);
            var middle=FestivalMinigameView.CupAt(at,2,6);var toward=(middle-new Vector3(at.X,0,at.Z));toward.y=0;
            foreach(float far in new[]{1.2f,3.5f})
            {
                view.position=middle+toward.normalized*far+Vector3.up*.75f;
                for(int i=0;i<6;i++)
                {
                    view.rotation=Quaternion.LookRotation(FestivalMinigameView.CupAt(at,i,6)-view.position);
                    Assert.That(FestivalMinigameView.AimedCup(state,view),Is.EqualTo(i==1?-1:i),far+" m away, looking at cup "+(i+1));
                }
                view.rotation=Quaternion.LookRotation(Quaternion.Euler(0,60,0)*(middle-view.position));
                Assert.That(FestivalMinigameView.AimedCup(state,view),Is.EqualTo(-1),far+" m away, looking off the table");
            }
            view.rotation=Quaternion.LookRotation(middle-view.position);
            Assert.That(FestivalMinigameView.AimedCup(state,null),Is.EqualTo(-1),"no view, no cup");
            state.Booth.ScoredAt=4;Assert.That(FestivalMinigameView.AimedCup(state,view),Is.EqualTo(-1),"none once the game is scored");
            state.Booth.ScoredAt=-1;state.Phase="Playing";Assert.That(FestivalMinigameView.AimedCup(state,view),Is.EqualTo(-1),"none before it runs");
        }

        [TestCase(Festivals.PoloFestival)][TestCase(Festivals.PlayaFestival)]
        public void AmbientWalkersClearTheLargestRenderedCupsBoothForFullLaps(int festival)
        {
            root=new GameObject("Booth route test");var crowd=root.AddComponent<FestivalAmbientCrowd>();crowd.Build();
            var booth=FestivalMinigameView.Build(root.transform);
            var state=new RoundState{Phase=FestivalSimulation.MinigamePhase,FestivalIndex=festival,
                Booth=new MinigameState{Kind=FestivalSimulation.MysteryCupsGame,Spot=1,Cups=FestivalSimulation.CupsFor(8)}};
            FestivalMinigameView.Show(booth,state);FestivalMinigameView.ShowCups(booth,state,-1,null);
            var table=booth.Find(FestivalMinigameView.CupsName).Find("Cup table").GetComponent<Renderer>();
            Assert.That(table.bounds.size.magnitude,Is.GreaterThan(3),"the largest crew's actual rendered table");
            Assert.That(Shown(booth.Find(FestivalMinigameView.CupsName)).Length,Is.EqualTo(FestivalSimulation.CupsFor(8)));
            var actors=new System.Collections.Generic.List<Transform>();
            foreach(Transform child in root.transform)if(child.name.StartsWith("ambient_walk_"))actors.Add(child);
            Assert.That(actors.Count,Is.EqualTo(12));
            var parts=booth.GetComponentsInChildren<Renderer>();float nearest=100;string worst="";
            for(int frame=0;frame<=1000;frame++)
            {
                float clock=frame*.1f;TwistVisualsTests.UpdateCrowd(crowd,clock);
                foreach(var actor in actors)foreach(var part in parts)
                {
                    // The flat ring is a walking invitation, rather than a solid obstacle.
                    if(part.bounds.max.y<.12f||part.bounds.min.y>1.8f)continue;
                    float gap=TwistVisualsTests.RendererBodyGap(part,actor.position);
                    if(gap<nearest){nearest=gap;worst=actor.name+" / "+part.name+" at "+clock.ToString("0.0")+" s";}
                }
            }
            TestContext.WriteLine(Festivals.Name(festival)+" largest cups booth nearest sampled body-axis gap: "+nearest.ToString("0.000")+" m ("+worst+")");
            Assert.That(nearest,Is.GreaterThanOrEqualTo(.4f),"a 30 cm body and 10 cm sampling margin clear the rendered booth: "+worst);
        }

        private static int[] Shown(Transform cups)
        {
            var shown=new System.Collections.Generic.List<int>();
            for(int i=0;i<FestivalSimulation.CupsFor(8);i++)if(cups.Find(FestivalMinigameView.CupName(i)).gameObject.activeSelf)shown.Add(i);
            return shown.ToArray();
        }
        private static Material Look(Transform cups,int i)=>cups.Find(FestivalMinigameView.CupName(i)).GetComponentInChildren<MeshRenderer>().sharedMaterial;
    }
}
