using System.Collections.Generic;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Festival.Tests
{
    // The confetti cannon's pop is made in code, a held cannon fires from the open end of its barrel, and a session reports each
    // accepted burst once, only for a player it draws, and never one from before it first saw that player in this round.
    public sealed class ConfettiBurstTests
    {
        static int commands;
        static FestivalSimulation Camp(int seed)
        {
            var game=new FestivalSimulation(seed);
            foreach(var id in new[]{"host","friend"})game.AddPlayer(id,id).Inventory.Add(new ItemStack{ItemId=FestivalSimulation.ConfettiItem,Count=1});
            return game;
        }
        // At camp the cannon fires free, so each player can fire as often as a test needs.
        static bool Fire(FestivalSimulation game,string player)=>game.Execute(player,new GameCommand{Id="confetti"+commands++,Kind="Use",ItemId=FestivalSimulation.ConfettiItem}).Accepted;
        static RoundState View(FestivalSimulation game,string viewer)=>JsonUtility.FromJson<RoundState>(JsonUtility.ToJson(FestivalSession.ViewFor(game,viewer)));
        static float[] Samples(AudioClip clip){var samples=new float[clip.samples*clip.channels];Assert.That(clip.GetData(samples,0),Is.True,"setup: the clip's samples");return samples;}
        static float Rms(float[] samples,int from,int to){double sum=0;for(int i=from;i<to;i++)sum+=samples[i]*samples[i];return (float)System.Math.Sqrt(sum/System.Math.Max(1,to-from));}
        static float Loudest(float[] samples,int from,int to){float peak=0;for(int i=from;i<to;i++)peak=Mathf.Max(peak,Mathf.Abs(samples[i]));return peak;}

        GameObject host;FestivalSession session;readonly List<string> seen=new List<string>();
        [SetUp]public void Follow()
        {
            host=new GameObject("Confetti follower");session=host.AddComponent<FestivalSession>();seen.Clear();
            session.ConfettiFired+=(player,count)=>seen.Add(player+" x"+count);
        }
        [TearDown]public void Drop()=>Object.DestroyImmediate(host);

        [Test]public void APopIsAShortMonoBangThatDiesAwayWithoutAClick()
        {
            var clip=FestivalConfettiBurst.CreatePop();
            try
            {
                Assert.That(clip.channels,Is.EqualTo(1),"mono");
                Assert.That(clip.length,Is.InRange(.25f,.6f),"a short pop");
                var samples=Samples(clip);int ms=clip.frequency/1000,half=samples.Length/2;float peak=Loudest(samples,0,samples.Length);
                Assert.That(peak,Is.InRange(.3f,1f),"loud, and never clipped");
                Assert.That(Loudest(samples,0,30*ms),Is.EqualTo(peak),"it bangs at once: its loudest moment is in its first 30 ms");
                Assert.That(Rms(samples,0,50*ms),Is.GreaterThan(Rms(samples,half,samples.Length)*4),"then dies away");
                Assert.That(Rms(samples,half,samples.Length),Is.GreaterThan(.005f),"through a crackle of paper, not straight to silence");
                Assert.That(Loudest(samples,samples.Length-5*ms,samples.Length),Is.LessThan(.01f),"and ends without a click");
            }
            finally{Object.DestroyImmediate(clip);}
        }

        [Test]public void EveryPopIsTheSame()
        {
            var first=FestivalConfettiBurst.CreatePop();var second=FestivalConfettiBurst.CreatePop();
            try{Assert.That(Samples(second),Is.EqualTo(Samples(first)),"the same samples each time");}
            finally{Object.DestroyImmediate(first);Object.DestroyImmediate(second);}
        }

        [Test]public void AHeldCannonFiresFromTheOpenEndOfItsBarrel()
        {
            var hand=new GameObject("Confetti hand");hand.transform.position=new Vector3(2,1,3);
            try
            {
                Assert.That(FestivalHeldItem.Muzzle(hand.transform,out _),Is.False,"an empty hand has no muzzle");
                var grip=FestivalHeldItem.Create(hand.transform,FestivalSimulation.ConfettiItem,new Vector3(0,-.05f,0),false);
                Assert.That(grip,Is.Not.Null,"setup: the cannon's art loads");
                var barrel=System.Array.Find(grip.GetComponentsInChildren<Renderer>(),r=>r.sharedMaterial==FestivalArtView.MaterialFor("Rose"));
                Assert.That(barrel,Is.Not.Null,"setup: the cannon's rose barrel");
                var b=barrel.bounds;
                Assert.That(b.size.y,Is.GreaterThan(Mathf.Max(b.size.x,b.size.z)),"setup: held upright, the barrel stands along y");
                Assert.That(FestivalHeldItem.Muzzle(hand.transform,out var muzzle),Is.True,"a cannon in hand has a muzzle");
                Assert.That(Vector3.Distance(muzzle.position,new Vector3(b.center.x,b.max.y,b.center.z)),Is.LessThan(.005f),"in the middle of the barrel's open end: "+muzzle.position+" in "+b);
                Assert.That(Vector3.Angle(muzzle.up,Vector3.up),Is.LessThan(1f),"aimed along the barrel");
                grip.SetActive(false);
                Assert.That(FestivalHeldItem.Muzzle(hand.transform,out _),Is.False,"a hidden cannon has none");
            }
            finally{Object.DestroyImmediate(hand);}
        }

        [Test]public void EachBurstIsReportedOnceAfterAFirstSnapshotBaselines()
        {
            var game=Camp(0);Assert.That(Fire(game,"host"),Is.True,"setup: a burst before this view first looks");
            Assert.That(game.Player("host").VisualConfettiBurstSequence,Is.EqualTo(1),"setup: the burst counts");
            session.FollowPlayerEvents(View(game,"friend"),"friend");
            Assert.That(seen,Is.Empty,"a first snapshot never replays an older burst");
            Assert.That(Fire(game,"host"),Is.True);var current=View(game,"friend");
            session.FollowPlayerEvents(current,"friend");session.FollowPlayerEvents(current,"friend");
            Assert.That(seen,Is.EqualTo(new[]{"host x1"}),"a new burst is reported once, however often its snapshot is read");
            Assert.That(Fire(game,"friend"),Is.True);session.FollowPlayerEvents(View(game,"friend"),"friend");
            Assert.That(seen,Is.EqualTo(new[]{"host x1","friend x1"}),"the viewer's own burst is reported too");
            Assert.That(Fire(game,"host")&&Fire(game,"host"),Is.True,"setup: two bursts between snapshots");
            session.FollowPlayerEvents(View(game,"friend"),"friend");
            Assert.That(seen,Is.EqualTo(new[]{"host x1","friend x1","host x2"}),"two bursts in one snapshot are reported together");
        }

        [Test]public void HiddenReturningAndNewRoundBurstsNeverReplay()
        {
            var game=Camp(0);var friend=game.Player("friend");
            session.FollowPlayerEvents(View(game,"friend"),"friend");
            friend.CampVisitId="tent_1";Assert.That(Fire(game,"host"),Is.True);session.FollowPlayerEvents(View(game,"friend"),"friend");
            Assert.That(seen,Is.Empty,"a burst outside is not shown inside a tent");
            friend.CampVisitId="";session.FollowPlayerEvents(View(game,"friend"),"friend");
            Assert.That(seen,Is.Empty,"nor once the viewer comes back out");
            Assert.That(Fire(game,"host"),Is.True);game.Disconnect("host");session.FollowPlayerEvents(View(game,"friend"),"friend");
            Assert.That(seen,Is.Empty,"a burst just before its shooter drops is not shown");
            game.AddPlayer("host","Host");session.FollowPlayerEvents(View(game,"friend"),"friend");
            Assert.That(seen,Is.Empty,"nor when they reconnect");
            Assert.That(Fire(game,"host"),Is.True);session.FollowPlayerEvents(View(game,"friend"),"friend");
            Assert.That(seen,Is.EqualTo(new[]{"host x1"}),"setup: the next drawn burst is shown");
            var next=Camp(1);Assert.That(Fire(next,"host")&&Fire(next,"host"),Is.True);
            session.FollowPlayerEvents(View(next,"friend"),"friend");
            Assert.That(seen.Count,Is.EqualTo(1),"a new round baselines its own counts instead of comparing with the last");
            session.FollowPlayerEvents(null,"");Assert.That(Fire(next,"host"),Is.True,"setup: a burst while this view is away");
            session.FollowPlayerEvents(View(next,"friend"),"friend");
            Assert.That(seen.Count,Is.EqualTo(1),"leaving and joining again baselines afresh");
        }

        [Test]public void OptInDiagnosticsNameEachPassedOverCountButNoPlayer()
        {
            bool visible=DevelopmentDiagnostics.Visible;var lines=new List<string>();
            Application.LogCallback trace=(text,stack,type)=>{if(text.StartsWith("[Festival.PlayerEvents]"))lines.Add(text);};
            Application.logMessageReceived+=trace;
            try
            {
                DevelopmentDiagnostics.Visible=false;
                var game=Camp(0);Assert.That(Fire(game,"host"),Is.True,"setup: a burst before this view first looks");
                session.FollowPlayerEvents(View(game,"friend"),"friend");
                Assert.That(lines,Is.Empty,"silent unless opted in");
                DevelopmentDiagnostics.Visible=true;
                game.Player("friend").CampVisitId="tent_1";Assert.That(Fire(game,"host"),Is.True,"setup: a burst the tent hides");
                session.FollowPlayerEvents(View(game,"friend"),"friend");session.FollowPlayerEvents(View(game,"friend"),"friend");
                var next=Camp(1);Assert.That(Fire(next,"host"),Is.True,"setup: a burst before a new round's first look");
                session.FollowPlayerEvents(View(next,"friend"),"friend");
                Assert.That(lines,Is.EqualTo(new[]{"[Festival.PlayerEvents] action=suppress kind=ConfettiBurst count=2","[Festival.PlayerEvents] action=baseline kind=ConfettiBurst count=1"}),
                    "each passed-over count once, by kind and count alone");
            }
            finally{Application.logMessageReceived-=trace;DevelopmentDiagnostics.Visible=visible;}
        }
    }
}
