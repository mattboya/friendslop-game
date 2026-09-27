using System.Linq;
using NUnit.Framework;
using Festival.Core;
using Festival.Presentation;

namespace Festival.Tests
{
    public sealed class CrowdLayoutTests
    {
        [Test]public void InteractiveAttendeesStartInSocialZonesWithAnOpenStageAisle()
        {
            var npcs=new FestivalSimulation(17).State.Npcs.Where(n=>n.Kind=="Wook").ToArray();
            Assert.That(npcs.Length,Is.EqualTo(24));
            Assert.That(npcs.Count(n=>n.Z>17&&n.Z<27),Is.GreaterThanOrEqualTo(10),"Stage should draw the largest group");
            Assert.That(npcs.Count(n=>n.Z<0),Is.GreaterThanOrEqualTo(4),"Market and crosspaths need people");
            Assert.That(npcs.Count(n=>n.X<-19||n.X>19),Is.GreaterThanOrEqualTo(4),"Tree groves need small groups");
            Assert.That(npcs.All(n=>!(n.Z>15&&n.Z<27&&System.Math.Abs(n.X)<2.5)),Is.True,"Central route to DJ deck must stay open");
            float min=100,max=0;
            foreach(var npc in npcs)
            {
                float nearest=100;
                foreach(var other in npcs)if(other!=npc)
                {
                    float dx=npc.X-other.X,dz=npc.Z-other.Z;
                    nearest=System.Math.Min(nearest,(float)System.Math.Sqrt(dx*dx+dz*dz));
                }
                min=System.Math.Min(min,nearest);max=System.Math.Max(max,nearest);
            }
            Assert.That(max-min,Is.GreaterThan(1.2f),"Attendees should form uneven groups and open pockets");
        }
        [Test]public void AmbientPlanContainsPoiCirclesGrovesPicnicsAndUnevenStagePods()
        {
            var spots=FestivalAmbientCrowd.FixedSpots;
            Assert.That(spots.Count(s=>s.Zone=="Stage"),Is.EqualTo(26));
            Assert.That(spots.Count(s=>s.Zone=="PoiPerformer"),Is.EqualTo(2));
            Assert.That(spots.Count(s=>s.Zone=="PoiAudience"),Is.EqualTo(8));
            Assert.That(spots.Count(s=>s.Zone=="TreeHangout"),Is.EqualTo(8));
            Assert.That(spots.Count(s=>s.Zone=="Picnic"),Is.EqualTo(6));
            Assert.That(spots.Where(s=>s.Zone=="Stage").All(s=>System.Math.Abs(s.Position.x)>2.5f),Is.True);
            var stage=spots.Where(s=>s.Zone=="Stage").ToArray();
            Assert.That(stage.Any(a=>stage.Any(b=>a.Position!=b.Position && (a.Position-b.Position).magnitude<1.1f)),Is.True,"Friend pods need close pairs");
            Assert.That(stage.Any(a=>stage.All(b=>a.Position==b.Position || (a.Position-b.Position).magnitude>1.0f)),Is.True,"Crowd needs breathing room between pods");
        }
    }
}
