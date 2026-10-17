using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Festival.Tests
{
    // ECON-8: the host's own schedule drops a random find. A real joined friend and the host both see it glint; grabbed by both in
    // the same moment, only one gets the cash and only they are told so. A joined friend alone at the next find is told in turn.
    public sealed class CashFindPlayTests:FestivalPlayModeTest
    {
        [UnityTest]public IEnumerator AJoinedFriendAndTheHostRaceForAFindAndOnlyOneGetsIt()
        {
            var world=new GameObject("Cash find world");world.AddComponent<FestivalWorld>();yield return null;
            var hostObject=new GameObject("Cash find host");var host=hostObject.AddComponent<FestivalSession>();var hostHud=hostObject.AddComponent<FestivalHud>();
            var friendObject=new GameObject("Cash find friend");var friend=friendObject.AddComponent<FestivalSession>();var friendHud=friendObject.AddComponent<FestivalHud>();yield return null;
            var failures=new List<string>();
            int Glints(GameObject owner)=>owner.GetComponentsInChildren<Transform>(false).Count(t=>t.name==FestivalCashGlint.GlintName);
            Button Action(FestivalHud hud,string label)=>hud.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.gameObject.activeSelf&&b.name=="Action:"+label);
            string Notice(FestivalHud hud)=>hud.GetComponentsInChildren<Text>(false).FirstOrDefault(t=>t.name=="Notice")?.text??"(hidden)";
            try
            {
                ushort port=FreePort();host.Host("Host",port);
                float deadline=Time.realtimeSinceStartup+30;
                while(host.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(host.LocalPlayer,Is.Not.Null,host.Message);friend.Join("Friend","127.0.0.1",port);
                while(friend.LocalPlayer==null&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(friend.LocalPlayer,Is.Not.Null,friend.Message);
                var sim=Simulation(host);var mate=sim.Player(friend.LocalPlayerId);mate.X=0;mate.Z=19;friend.Command("Ready");
                while(!mate.Ready&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(mate.Ready,Is.True,"setup: the friend's Ready reached the host");yield return StartLevel(host,friend.LocalPlayerId);
                var me=sim.Player(host.LocalPlayerId);sim.State.Npcs.Clear();sim.State.Drops.Clear();
                // The host's own schedule drops the next find now.
                IEnumerator Spawn(System.Action<DropState> found)
                {
                    sim.State.Drops.RemoveAll(d=>d.CashFind);sim.State.NextCashFindAt=System.Math.Max(sim.State.ElapsedSeconds,.001);
                    for(float until=Time.realtimeSinceStartup+5;!sim.State.Drops.Exists(d=>d.CashFind)&&Time.realtimeSinceStartup<until;)yield return null;
                    found(sim.State.Drops.Find(d=>d.CashFind));
                }
                DropState find=null;yield return Spawn(d=>find=d);
                Assert.That(find,Is.Not.Null,"setup: the schedule dropped a find");
                me.X=find.X;me.Z=find.Z+1.5f;mate.X=find.X;mate.Z=find.Z-1.5f;
                yield return new WaitForSeconds(.5f);
                if(!friend.State.Drops.Exists(d=>d.Id==find.Id&&d.CashFind))failures.Add("the joined friend's view has no find");
                if(Glints(hostObject)!=1||Glints(friendObject)!=1)failures.Add("beside the find the host shows "+Glints(hostObject)+" glints and the friend "+Glints(friendObject));
                mate.Z=find.Z-20;yield return new WaitForSeconds(.4f);
                if(Glints(friendObject)!=0)failures.Add("20 m off, the friend still sees the glint");
                mate.Z=find.Z-1.5f;yield return new WaitForSeconds(.4f);
                if(Glints(friendObject)!=1)failures.Add("back beside it, the friend sees no glint");
                // Both grab it at once: the friend's grab goes over the wire, the host's is taken on the spot.
                string label="Pick up $"+find.Count;var mine=Action(hostHud,label);var theirs=Action(friendHud,label);
                Assert.That(mine!=null&&theirs!=null,Is.True,"setup: both are offered \""+label+"\", with:\n"+string.Join("\n",failures));
                int myCash=me.Cash,mateCash=mate.Cash,sales=sim.State.LevelSales,stash=sim.State.StashCash;
                theirs.onClick.Invoke();mine.onClick.Invoke();
                yield return new WaitForSeconds(.6f);
                if(me.Cash+mate.Cash!=myCash+mateCash+find.Count)failures.Add("the race paid "+(me.Cash+mate.Cash-myCash-mateCash)+", not the find's "+find.Count);
                if(me.Cash!=myCash+find.Count)failures.Add("the host, first to the host's rules, has "+me.Cash+", not "+(myCash+find.Count));
                if(sim.State.LevelSales!=sales||sim.State.StashCash!=stash)failures.Add("found cash counted as sales or stash");
                if(sim.State.Drops.Exists(d=>d.Id==find.Id)||friend.State.Drops.Exists(d=>d.Id==find.Id))failures.Add("the find is still lying there");
                if(Notice(hostHud)!="Found $"+find.Count+" on the ground!")failures.Add("the host is told \""+Notice(hostHud)+"\"");
                if(Notice(friendHud).StartsWith("Found"))failures.Add("the friend who lost the race is told \""+Notice(friendHud)+"\"");
                if(Glints(hostObject)!=0||Glints(friendObject)!=0)failures.Add("a glint is left over the empty spot");
                // The friend alone at the next find: the joined client is told when the host's answer comes back.
                DropState second=null;yield return Spawn(d=>second=d);
                Assert.That(second,Is.Not.Null,"setup: the schedule dropped a second find");
                me.X=second.X+20;me.Z=second.Z;mate.X=second.X;mate.Z=second.Z+1;mateCash=mate.Cash;
                yield return new WaitForSeconds(.5f);
                var alone=Action(friendHud,"Pick up $"+second.Count);
                if(alone==null)failures.Add("the friend alone is not offered the second find");
                else
                {
                    alone.onClick.Invoke();yield return new WaitForSeconds(.6f);
                    if(mate.Cash!=mateCash+second.Count)failures.Add("the friend has "+mate.Cash+" after the second find, not "+(mateCash+second.Count));
                    if(Notice(friendHud)!="Found $"+second.Count+" on the ground!")failures.Add("the joined friend is told \""+Notice(friendHud)+"\"");
                }
            }
            finally{friend.Leave();host.Leave();Object.Destroy(friendObject);Object.Destroy(hostObject);Object.Destroy(world);}
            yield return null;
            Assert.That(failures,Is.Empty,"A random find, raced for:\n"+string.Join("\n",failures));
        }
    }
}
