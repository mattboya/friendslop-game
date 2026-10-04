using System.Collections;
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
    public sealed class StockVisibilityPlayTests : FestivalPlayModeTest
    {
        [UnityTest] public IEnumerator HostedEquipAndStowDriveTheVisibleSecurityCard()
        {
            var world=new GameObject("impl:U16 stock world");world.AddComponent<FestivalWorld>();yield return null;
            var hud=new GameObject("impl:U16 stock HUD");var session=hud.AddComponent<FestivalSession>();hud.AddComponent<FestivalHud>();yield return null;
            string Vitals()=>hud.GetComponentsInChildren<Text>(false).FirstOrDefault(t=>t.name=="Vitals")?.text??"(hidden)";
            try
            {
                session.Host("Stock holder",HostPort);
                for(float deadline=Time.realtimeSinceStartup+30;session.LocalPlayer==null&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.LocalPlayer,Is.Not.Null,session.Message);yield return StartLevel(session);
                var game=Simulation(session);var player=game.Player(session.LocalPlayerId);game.State.Npcs.Clear();player.Inventory.Clear();player.EquippedItemId="";
                player.X=0;player.Z=5;player.Effects.Clear();game.State.Doses.Clear();player.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});
                var cop=new NpcState{Id="cop_0",Kind="Cop",X=0,Z=0,Yaw=0};game.State.Npcs.Add(cop);
                game.HasLineOfSight=(x,z,tx,tz)=>true;game.Navigate=(x,z,tx,tz,step)=>new WorldPoint(x,z);
                yield return new WaitForSeconds(.4f);
                Assert.That(cop.Evidence,Is.Empty,"a hosted player with stock in a pocket is not stopped");Assert.That(Vitals(),Does.Not.Contain("SECURITY STOP"));
                session.Command("Equip",item:"stock_lsd");
                for(float deadline=Time.realtimeSinceStartup+5;!Vitals().Contains("SECURITY STOP")&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(Vitals(),Does.Contain("SECURITY STOP"),"the host's equipped stock stop reaches the running HUD");
                session.Command("Equip");
                for(float deadline=Time.realtimeSinceStartup+5;(session.LocalPlayer.EquippedItemId!=""||Vitals().Contains("SECURITY STOP"))&&Time.realtimeSinceStartup<deadline;)yield return null;
                Assert.That(session.LocalPlayer.EquippedItemId,Is.Empty);Assert.That(cop.Evidence,Is.Empty,"stowing clears the visibility stop on the authority");
                Assert.That(Vitals(),Does.Not.Contain("SECURITY STOP"),"the running card clears after the owner's snapshot");Assert.That(player.Inventory[0].Count,Is.EqualTo(1));
            }
            finally{session.Leave();Object.Destroy(hud);Object.Destroy(world);}
        }
    }
}
