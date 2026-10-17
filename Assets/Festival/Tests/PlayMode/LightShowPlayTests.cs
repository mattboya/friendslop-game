using System;
using System.Collections;
using System.Linq;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Festival.Tests
{
    // MINI-3 between real sessions: a joined friend and the dosed host stand in a Light Show booth's ring. The friend's view turns to
    // the glover in first person, level, free only 40 degrees either way, whatever their saved view, which V cannot change meanwhile.
    // The show plays on the game's clock while the world holds, with the friend's comfort settings and none of the host's fakes. Then
    // the friend answers their own three questions with clicks, alone seeing them, until the reveal brings everyone's answers at once,
    // then the banner. As play resumes, and whenever the session fails, is switched off or goes, the view, pointer and time come back.
    public sealed class LightShowPlayTests:FestivalPlayModeTest
    {
        static IEnumerator Wait(double seconds){double until=Time.realtimeSinceStartupAsDouble+seconds;while(Time.realtimeSinceStartupAsDouble<until)yield return null;}
        static Transform Hud(GameObject root,string path)=>root.transform.Find("Festival HUD/"+path);
        static bool Shown(GameObject root,string path){var t=Hud(root,path);return t!=null&&t.gameObject.activeInHierarchy;}
        static string Text(GameObject root,string path)=>Hud(root,path)?.GetComponent<Text>()?.text;
        static Transform Answer(GameObject root,int k)=>Hud(root,"Light Show questions/Light Show answer "+k);
        static Transform Show(GameObject root,string path)=>root.transform.Find(FestivalLightShowView.RootName+"/"+path);
        // How far the view turns from the glover, and how far it tips from level, in degrees.
        static float Offset(FestivalSession session)
        {
            Assert.That(FestivalLightShowView.FacesGlover(session.State,session.LocalPlayer,out float heading),Is.True,"setup: the show holds this view");
            return Mathf.DeltaAngle(heading,session.ViewCamera.transform.eulerAngles.y);
        }
        static float Tip(FestivalSession session)=>Mathf.DeltaAngle(0,session.ViewCamera.transform.eulerAngles.x);
        // Turns the view yaw degrees right and pitch degrees down through the session's own look.
        static void Look(FestivalSession session,float yaw,float pitch){float speed=session.Profile.Data.MouseSensitivity;session.ApplyLook(new Vector2(yaw/speed,(session.Profile.Data.InvertY?1:-1)*pitch/speed));}
        static Quaternion[] Bones(FestivalCharacter actor)=>FestivalMotionLibrary.Bones.Select(name=>actor.GetComponentsInChildren<Transform>(true).First(t=>t.name==name).localRotation).ToArray();
        static void Same(Vector3 euler,Vector3 expected,string what)
        {
            Assert.That(Mathf.DeltaAngle(euler.y,expected.y),Is.EqualTo(0).Within(.5f),what+": the same way round");
            Assert.That(Mathf.DeltaAngle(euler.x,expected.x),Is.EqualTo(0).Within(.5f),what+": the same way up");
        }

        // Nobody on a dose: the spin's dose (the tripper may be anyone) would sway or trail the view and add its own fakes.
        static void Sober(FestivalSimulation game){foreach(var p in game.State.Players)p.Effects.RemoveAll(e=>e.Id==FestivalSimulation.DoseEffect);}
        // A level with a Light Show booth, the friend joined and sober; the host takes a dose of two before anyone opts in.
        IEnumerator Gather(FestivalSession host,FestivalSession owner)
        {
            ushort port=FreePort();host.Host("Host",port);yield return null;Assert.That(host.LocalPlayer,Is.Not.Null,host.Message);host.Controls.Map.Disable();
            owner.Join("Owner","127.0.0.1",port);
            for(double deadline=Time.realtimeSinceStartupAsDouble+30;owner.LocalPlayer==null&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
            Assert.That(owner.LocalPlayer,Is.Not.Null,owner.Message);
            var game=Simulation(host);var mate=game.Player(owner.LocalPlayerId);mate.X=0;mate.Z=19;owner.Command("Ready");
            for(double deadline=Time.realtimeSinceStartupAsDouble+5;!mate.Ready&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
            Assert.That(mate.Ready,Is.True,owner.Message);yield return StartLevel(host,owner.LocalPlayerId);
            game.State.Booth.Kind=FestivalSimulation.LightShowGame;Sober(game);
            game.Player(host.LocalPlayerId).Effects.Add(new ActiveEffect{Id=FestivalSimulation.DoseEffect,Intensity=2,RemainingSeconds=300});
            yield return Wait(.3);
        }
        // Both stand in front of the booth, a metre or so either side of its middle, until the show starts in the friend's view.
        static IEnumerator Ring(FestivalSession host,FestivalSession owner)
        {
            var game=Simulation(host);var at=FestivalSimulation.BoothAt(game.State);var front=new Vector2(-at.X,-at.Z).normalized;var side=new Vector2(front.y,-front.x);
            foreach(var session in new[]{host,owner})
            {
                if(session==null)continue;var p=game.Player(session.LocalPlayerId);var place=new Vector2(at.X,at.Z)+front*2.5f+side*(session==host?-1.2f:1.2f);p.X=place.x;p.Z=place.y;
            }
            var watcher=owner??host;
            for(double deadline=Time.realtimeSinceStartupAsDouble+8;watcher.State.Phase!=FestivalSimulation.MinigamePhase&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
            Assert.That(watcher.State.Phase,Is.EqualTo(FestivalSimulation.MinigamePhase),"the ring starts the Light Show: "+watcher.Message);
            Assert.That(watcher.State.Booth.Kind,Is.EqualTo(FestivalSimulation.LightShowGame),"setup: the booth deals the Light Show");
            yield return Wait(.2);
        }

        [UnityTest]public IEnumerator AJoinedFriendWatchesTheGloverAnswersInPrivateAndGetsTheirViewBack()
        {
            UseAVirtualKeyboard("impl:U64 light keyboard");
            var world=new GameObject("impl:U64 light world");world.AddComponent<FestivalWorld>();yield return null;
            var hostRoot=new GameObject("impl:U64 light host");var host=hostRoot.AddComponent<FestivalSession>();yield return null;
            var ownerRoot=new GameObject("impl:U64 light owner");var owner=ownerRoot.AddComponent<FestivalSession>();ownerRoot.AddComponent<FestivalHud>();yield return null;
            try
            {
                yield return Gather(host,owner);
                var game=Simulation(host);string id=owner.LocalPlayerId;
                // The friend's own view before the game: over the shoulder (V saves it), turned and tipped up.
                yield return Tap(Key.V);yield return null;
                Assert.That(owner.Profile.Data.ThirdPerson,Is.True,"setup: V reaches the friend's session");Assert.That(owner.ThirdPersonView,Is.True);
                Look(owner,130,-25);yield return null;yield return null;var before=owner.ViewCamera.transform.eulerAngles;

                yield return Ring(host,owner);
                Assert.That(owner.ThirdPersonView,Is.False,"the show is watched in first person");
                Assert.That(Offset(owner),Is.EqualTo(0).Within(1),"facing the glover");Assert.That(Tip(owner),Is.EqualTo(0).Within(.5f),"level");
                Look(owner,100,30);yield return null;yield return null;
                Assert.That(Offset(owner),Is.EqualTo(40).Within(.5f),"the look stops 40 degrees right");Assert.That(Tip(owner),Is.EqualTo(0).Within(.5f),"and stays level");
                Look(owner,-150,-30);yield return null;yield return null;
                Assert.That(Offset(owner),Is.EqualTo(-40).Within(.5f),"and 40 degrees left");
                Look(owner,40,0);yield return null;yield return null;Assert.That(Offset(owner),Is.EqualTo(0).Within(.5f));
                yield return Tap(Key.V);
                Assert.That(owner.Profile.Data.ThirdPerson,Is.True,"V changes no saved view during the show");Assert.That(owner.ThirdPersonView,Is.False);
                Assert.That(owner.PointerFree,Is.False,"the pointer stays locked for the look");
                Assert.That(Shown(ownerRoot,"Light Show questions"),Is.False,"no question during the show");
                Assert.That(Text(ownerRoot,"Minigame card/Minigame title"),Is.EqualTo("LIGHT SHOW"));

                // The show on the game's clock while the world holds, and none of the host's fakes on the friend's screen.
                Assert.That(Time.timeScale,Is.Zero,"the festival holds");
                var glover=ownerRoot.GetComponent<FestivalMinigameView>().Booth.GetComponentInChildren<FestivalCharacter>(true);var mate=owner.WorldCharacter(host.LocalPlayerId);
                Assert.That(glover.Pose,Is.EqualTo(FestivalCharacter.GlovePose),"the stranger gloves");Assert.That(mate,Is.Not.Null,"setup: the friend draws the host");
                var gloving=Bones(glover);var held=Bones(mate);yield return Wait(.4);
                Assert.That(Bones(glover),Is.Not.EqualTo(gloving),"the glover moves on the game's clock");Assert.That(Bones(mate),Is.EqualTo(held),"while the host's rig holds still");
                var show=owner.State.Booth.LightShow;
                Assert.That(show.Players.Select(p=>p.PlayerId),Is.EqualTo(new[]{id}),"the friend's view carries their own record alone");Assert.That(show.Players[0].Fakes,Is.Empty,"a sober friend has no fakes");
                Assert.That(game.State.Booth.LightShow.Players.Find(p=>p.PlayerId==host.LocalPlayerId).Fakes.Count,Is.EqualTo(2),"setup: the dosed host has two");
                Assert.That(hostRoot.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("Fake ")),Is.EqualTo(2),"the host's own show draws them");
                Assert.That(ownerRoot.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("Fake ")),Is.Zero,"the friend's never does");

                // The friend's comfort settings reach their show: shapes appear whole, and a brighter screen gets a softer glow.
                owner.Profile.Data.ReducedMotion=true;owner.Profile.Data.Brightness=1;
                var shape=Show(ownerRoot,"Shape sign/Shape");Assert.That(shape,Is.Not.Null,"setup: the friend's show has its sign");
                // Just before the second sign shape and the second sweep (8 to 10 s).
                game.State.Booth.Seconds=7.9;float popped=-1;
                for(double deadline=Time.realtimeSinceStartupAsDouble+3;popped<0&&Time.realtimeSinceStartupAsDouble<deadline;){yield return null;if(shape.gameObject.activeInHierarchy&&shape.GetComponent<Renderer>().enabled)popped=shape.localScale.x;}
                Assert.That(popped,Is.EqualTo(.8f).Within(1e-3f),"reduced motion shows the friend a shape whole from its first frame");
                for(double deadline=Time.realtimeSinceStartupAsDouble+4;!(Show(ownerRoot,"Sweep 1").gameObject.activeSelf&&Show(hostRoot,"Sweep 1").gameObject.activeSelf)&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
                yield return null;float Glow(GameObject root)=>Show(root,"Sweep 1/Beam").GetComponent<Renderer>().sharedMaterial.GetColor("_EmissionColor").maxColorComponent;
                Assert.That(Glow(ownerRoot),Is.EqualTo(Glow(hostRoot)*.5f).Within(.02f),"the friend's brighter screen gets half the glow, so each colour reads the same");

                // The questions: the friend's own, five answers to click, the pointer free and the view still on the glover.
                game.State.Booth.Seconds=19.9;
                for(double deadline=Time.realtimeSinceStartupAsDouble+3;!Shown(ownerRoot,"Light Show questions")&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
                yield return null;yield return null;
                Assert.That(Shown(ownerRoot,"Light Show questions"),Is.True,"the friend's first question comes up");
                var mine=owner.State.Booth.LightShow.Players.Single();var record=game.State.Booth.LightShow.Players.Find(p=>p.PlayerId==id);
                Assert.That(mine.PlayerId,Is.EqualTo(id),"still the friend's own record alone");Assert.That(mine.Questions.Select(q=>q.CorrectIndex),Is.EqualTo(new[]{-1}),"with no answer key");
                Assert.That(Text(ownerRoot,"Light Show questions/Light Show prompt"),Is.EqualTo(record.Questions[0].Prompt));
                for(int k=0;k<4;k++)Assert.That(Answer(ownerRoot,k+1).Find("Text").GetComponent<Text>().text,Is.EqualTo(record.Questions[0].Choices[k]),"answer "+(k+1));
                Assert.That(Answer(ownerRoot,5).Find("Text").GetComponent<Text>().text,Is.EqualTo("Not sure"));
                Assert.That(Text(ownerRoot,"Minigame card/Minigame title"),Is.EqualTo("QUESTION 1 / 3"));
                Assert.That(owner.PointerFree,Is.True,"the pointer is free to click an answer");Assert.That(Shown(ownerRoot,"Aim dot"),Is.False,"with no aim dot over the answers");
                Assert.That(Offset(owner),Is.InRange(-40.5f,40.5f),"the view still holds on the glover");Assert.That(owner.ThirdPersonView,Is.False);

                // Two clicks in one frame send one answer, which locks the question in.
                Answer(ownerRoot,2).GetComponent<Button>().onClick.Invoke();Answer(ownerRoot,3).GetComponent<Button>().onClick.Invoke();
                for(double deadline=Time.realtimeSinceStartupAsDouble+3;record.Answers.Count==0&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
                yield return Wait(.3);
                Assert.That(record.Answers,Is.EqualTo(new[]{1}),"the click sends the second answer, once");Assert.That(owner.Message,Does.Not.Contain("already"),"no second answer was sent");
                for(int k=1;k<=5;k++)Assert.That(Answer(ownerRoot,k).GetComponent<Button>().interactable,Is.False,"answer "+k+" is locked");
                Assert.That(Answer(ownerRoot,2).GetComponent<Image>().color,Is.Not.EqualTo(Answer(ownerRoot,1).GetComponent<Image>().color),"the sent answer stands out");
                Assert.That(Text(ownerRoot,"Minigame card/Minigame detail"),Does.StartWith("Locked in."));
                Answer(ownerRoot,4).GetComponent<Button>().onClick.Invoke();yield return Wait(.3);
                Assert.That(record.Answers,Is.EqualTo(new[]{1}),"a later click sends nothing");

                host.Command(FestivalSimulation.MinigameKind,item:FestivalSimulation.LightShowAnswer,amount:0,direction:0);Assert.That(host.LastLocalCommandResult.Accepted,Is.True,host.Message);
                for(double deadline=Time.realtimeSinceStartupAsDouble+3;FestivalHudText.LightShowOpen(owner.State,id).Slot!=1&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
                yield return null;yield return null;
                Assert.That(Text(ownerRoot,"Light Show questions/Light Show prompt"),Is.EqualTo(record.Questions[1].Prompt),"the second question follows");
                Assert.That(Answer(ownerRoot,1).GetComponent<Button>().interactable,Is.True,"with its answers open");
                Answer(ownerRoot,5).GetComponent<Button>().onClick.Invoke();
                for(double deadline=Time.realtimeSinceStartupAsDouble+3;record.Answers.Count<2&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
                Assert.That(record.Answers,Is.EqualTo(new[]{1,-1}),"Not sure is the fifth");
                host.Command(FestivalSimulation.MinigameKind,item:FestivalSimulation.LightShowAnswer,amount:0,direction:1);
                for(double deadline=Time.realtimeSinceStartupAsDouble+3;FestivalHudText.LightShowOpen(owner.State,id).Slot!=2&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
                yield return null;yield return null;
                int right=record.Questions[2].CorrectIndex;Answer(ownerRoot,right+1).GetComponent<Button>().onClick.Invoke();
                for(double deadline=Time.realtimeSinceStartupAsDouble+3;record.Answers.Count<3&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
                Assert.That(record.Answers,Is.EqualTo(new[]{1,-1,right}));
                Assert.That(owner.State.Booth.LightShow.Players.Count,Is.EqualTo(1),"the host's answers stay hidden until they answer too");
                var hostRecord=game.State.Booth.LightShow.Players.Find(p=>p.PlayerId==host.LocalPlayerId);
                host.Command(FestivalSimulation.MinigameKind,item:FestivalSimulation.LightShowAnswer,amount:(hostRecord.Questions[2].CorrectIndex+1)%4,direction:2);

                // The reveal: everyone's answers at once, the friend's own beside the right ones, and the banner with the crew's points.
                for(double deadline=Time.realtimeSinceStartupAsDouble+3;owner.State.Booth.ScoredAt<0&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
                Assert.That(owner.State.Booth.ScoredAt,Is.GreaterThanOrEqualTo(0),"the third answer scores the show");yield return Wait(.2);
                Assert.That(owner.State.Booth.LightShow.Players.Select(p=>p.PlayerId),Is.EquivalentTo(new[]{host.LocalPlayerId,id}),"the reveal brings every player's answers at once");
                Assert.That(Text(ownerRoot,"Light Show questions/Light Show prompt"),Is.EqualTo("YOUR ANSWERS"));
                var reveal=Text(ownerRoot,"Light Show questions/Light Show reveal");
                foreach(var question in record.Questions)Assert.That(reveal,Does.Contain(question.Prompt));
                Assert.That(reveal,Does.Contain("Not sure. It was ").And.Contain("Right: +1."),"each of the friend's answers beside the right one");
                Assert.That(Shown(ownerRoot,"Light Show questions/Light Show answer 1"),Is.False,"the answers go");
                Assert.That(Text(ownerRoot,"Minigame card/Minigame detail"),Does.StartWith("Crew score ").And.Contain("Host ").And.Contain("You "),"the banner names every player's points");
                Assert.That(Text(ownerRoot,"Minigame card/Minigame title"),Is.EqualTo(FestivalHudText.MinigameCard(owner.State,owner.LocalPlayer).Title));
                Assert.That(owner.PointerFree,Is.False,"the pointer locks again for the banner");Assert.That(Offset(owner),Is.InRange(-40.5f,40.5f),"the view holds through the banner");

                // Play resumes: the friend looks where they did before, over the shoulder, with time running again.
                for(double deadline=Time.realtimeSinceStartupAsDouble+6;owner.State.Phase==FestivalSimulation.MinigamePhase&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
                Assert.That(owner.State.Phase,Is.EqualTo("Playing"),"play resumes");yield return Wait(.3);
                Same(owner.ViewCamera.transform.eulerAngles,before,"the friend's view after the show");
                Assert.That(owner.ThirdPersonView,Is.True,"over the shoulder again, as the friend's setting says");Assert.That(owner.Profile.Data.ThirdPerson,Is.True);
                Assert.That(owner.PointerFree,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1));Assert.That(Shown(ownerRoot,"Light Show questions"),Is.False);
            }
            finally{owner.Leave();host.Leave();Object.Destroy(ownerRoot);Object.Destroy(hostRoot);Object.Destroy(world);}
            yield return null;
        }

        // The host goes mid-show: the friend's session fails, its time runs and its pointer is free, and its next game starts looking
        // straight ahead rather than where the friend looked before the show.
        [UnityTest]public IEnumerator AFailedFriendGetsTimeAndPointerBackAndTheNextGameLooksAhead()
        {
            var world=new GameObject("impl:U64 light fail world");world.AddComponent<FestivalWorld>();yield return null;
            var hostRoot=new GameObject("impl:U64 light fail host");var host=hostRoot.AddComponent<FestivalSession>();yield return null;
            var ownerRoot=new GameObject("impl:U64 light fail owner");var owner=ownerRoot.AddComponent<FestivalSession>();yield return null;
            try
            {
                yield return Gather(host,owner);
                Look(owner,130,-25);yield return null;yield return null;
                yield return Ring(host,owner);
                Assert.That(Offset(owner),Is.EqualTo(0).Within(1),"setup: the show holds the friend's view");Assert.That(Time.timeScale,Is.Zero);
                host.Leave();
                for(double deadline=Time.realtimeSinceStartupAsDouble+10;owner.State!=null&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
                Assert.That(owner.State,Is.Null,"the friend's session fails with its host gone: "+owner.Message);
                yield return null;yield return null;
                Assert.That(Time.timeScale,Is.EqualTo(1),"time runs again");Assert.That(owner.PointerFree,Is.True,"the pointer is free for the title");
                owner.Host("Again",FreePort());
                for(double deadline=Time.realtimeSinceStartupAsDouble+10;owner.LocalPlayer==null&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
                Assert.That(owner.LocalPlayer,Is.Not.Null,owner.Message);yield return null;yield return null;yield return null;
                Same(owner.ViewCamera.transform.eulerAngles,Vector3.zero,"the next game's first view");
            }
            finally{owner.Leave();host.Leave();Object.Destroy(ownerRoot);Object.Destroy(hostRoot);Object.Destroy(world);}
            yield return null;Assert.That(Time.timeScale,Is.EqualTo(1));
        }

        // Switched off mid-question, the session gives back the view it held, over the shoulder as its setting says, and the festival's
        // time; switched on again mid-game it holds the view and frees the pointer once more, and gone, it leaves time and pointer free.
        [UnityTest]public IEnumerator ASessionSwitchedOffMidQuestionGivesBackTheViewPointerAndTime()
        {
            var world=new GameObject("impl:U64 light switch world");world.AddComponent<FestivalWorld>();yield return null;
            var root=new GameObject("impl:U64 light switch host");var session=root.AddComponent<FestivalSession>();root.AddComponent<FestivalHud>();yield return null;
            try
            {
                session.Host("Host",HostPort);yield return null;yield return StartLevel(session);var game=Simulation(session);game.State.Booth.Kind=FestivalSimulation.LightShowGame;Sober(game);
                session.Profile.Data.ThirdPerson=true;Look(session,130,-25);yield return null;yield return null;var before=session.ViewCamera.transform.eulerAngles;
                yield return Ring(session,null);
                Assert.That(Offset(session),Is.EqualTo(0).Within(1),"setup: the show holds the view");Assert.That(session.ThirdPersonView,Is.False);
                Assert.That(session.PointerFree||Cursor.visible,Is.False,"setup: the show keeps the pointer for the look");
                game.State.Booth.Seconds=19.9;
                for(double deadline=Time.realtimeSinceStartupAsDouble+3;!session.PointerFree&&Time.realtimeSinceStartupAsDouble<deadline;)yield return null;
                yield return null;
                Assert.That(FestivalHudText.LightShowOpen(session.State,session.LocalPlayerId).Slot,Is.EqualTo(0),"setup: the first question is open");
                Assert.That(Cursor.visible,Is.True,"the question frees the pointer");

                root.SetActive(false);yield return null;
                Same(session.ViewCamera.transform.eulerAngles,before,"the view the switched-off session gives back");
                Assert.That(session.ThirdPersonView,Is.True,"over the shoulder, as the setting says");
                Assert.That(Vector3.Distance(session.ViewCamera.transform.position,session.EyePosition),Is.GreaterThan(.5f),"the camera back on its boom");
                Assert.That(Time.timeScale,Is.EqualTo(1),"time runs");
                var glover=root.GetComponent<FestivalMinigameView>().Booth.GetComponentInChildren<FestivalCharacter>(true);
                Assert.That(glover.Pose,Is.EqualTo("Idle"),"the glover is the world's again");

                root.SetActive(true);yield return null;yield return null;yield return null;
                Assert.That(Offset(session),Is.EqualTo(0).Within(1),"switched on mid-game, the show holds the view again");Assert.That(session.ThirdPersonView,Is.False);
                Assert.That(session.PointerFree,Is.True,"with the question's pointer");Assert.That(Time.timeScale,Is.Zero);

                Object.Destroy(root);yield return null;
                Assert.That(Time.timeScale,Is.EqualTo(1),"a session gone mid-game leaves time running");Assert.That(Cursor.visible,Is.True,"and the pointer free");
            }
            finally{if(root!=null){session.Leave();Object.Destroy(root);}Object.Destroy(world);}
            yield return null;
        }
    }
}
