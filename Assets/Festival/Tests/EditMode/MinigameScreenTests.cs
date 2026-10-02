using Festival.Core;
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

            var state=new RoundState{Phase="Playing",Booth=new MinigameState{Kind=FestivalSimulation.StubGame,Spot=1}};var at=Festivals.BoothSpots[1];
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
    }
}
