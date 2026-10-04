using System;
using System.Text.Json;
using Festival.Core;

public static class CampEggTests
{
    static void Check(bool pass,string message){if(!pass)throw new Exception("CampEgg: "+message);}
    static GameCommand Poke(string id,string command="poke")=>new GameCommand{Id=command,Kind="CampEgg",TargetId=id};
    public static void Run()
    {
        APlayerFindsTheGnomeAtItsVisitPoint();
        OnlyLivingUnreadyFreeOutdoorPlayersAtCampCanPoke();
        AllEightEggsUseTheirOwnHostPointAndLine();
        AcceptedPokesPersistPerEggAndDiscoveriesSurviveIntermediateEvents();
        TheChairHasOneOwnerForFourSecondsAndMovementReleasesIt();
        NewVisitsResetPropsButKeepOwnerDiscoveriesAndOlderSnapshotsRestore();
        RefusedEggsNeverChangePropsAndAllEightDiscoveriesAccumulate();
        RestoreKeepsActivePropsAndRejectsCorruptRecords();
        DustWordsHaveVariedHostDrawsAcrossVisits();
    }
    static void APlayerFindsTheGnomeAtItsVisitPoint()
    {
        var game=new FestivalSimulation(0);var player=game.AddPlayer("host","Host");player.X=-11;player.Z=16;
        var result=game.Execute(player.Id,Poke("gnome"));
        Check(result.Accepted,"a living outdoor player can find the gnome: "+result.Reason);
        Check(result.Reason=="The gnome whispers: the real festival was the snacks we hid along the way.","the queued gnome gag is returned");
    }
    static void OnlyLivingUnreadyFreeOutdoorPlayersAtCampCanPoke()
    {
        foreach(var life in new[]{"Downed","Detained","Spirit"})
        {
            var game=new FestivalSimulation(0);var p=game.AddPlayer("host","Host");p.X=-11;p.Z=16;p.Life=life;
            Check(!game.Execute(p.Id,Poke("gnome")).Accepted,life+" cannot poke an egg");
        }
        foreach(var blocked in new[]{"Ready","Interaction","Drag","Carry","HeldOffer","Interior","Disconnected","Outside"})
        {
            var game=new FestivalSimulation(0);var p=game.AddPlayer("host","Host");p.X=-11;p.Z=16;
            if(blocked=="Ready")p.Ready=true;
            if(blocked=="Interaction")p.InteractionId="busy";
            if(blocked=="Drag")p.DragTargetId="friend";
            if(blocked=="Carry")p.CarryBodyId="body";
            if(blocked=="HeldOffer")p.HeldOfferId="confetti";
            if(blocked=="Interior")p.CampVisitId="tent_1";
            if(blocked=="Disconnected")game.Disconnect(p.Id);
            if(blocked=="Outside")p.X=40;
            Check(!game.Execute(p.Id,Poke("gnome")).Accepted,blocked+" refuses the poke");
            Check(p.Cash==20&&p.CampAntics==0&&p.Inventory.Count==0,"refusals leave economy and interior antics alone");
        }
        foreach(var phase in new[]{"Lobby","Spinning","Loading","Playing",FestivalSimulation.MinigamePhase,"Results","CampReview","Shopping"})
        {
            var game=new FestivalSimulation(0);var p=game.AddPlayer("host","Host");p.X=-11;p.Z=16;game.State.Phase=phase;
            Check(game.Execute(p.Id,Poke("gnome")).Accepted==(phase=="Shopping"||phase=="CampReview"),"phase guard: "+phase);
        }
    }
    static readonly string[] EggIds={"gnome","button","chair","lost_found","hammock","flamingo","cone","wash_me"};
    static readonly float[] EggX={-11,4.85f,-10,10,-23,23,-7,-14};
    static readonly float[] EggZ={16,-5.6f,3.5f,7,-6,-6,-19,-11.2f};
    static void AllEightEggsUseTheirOwnHostPointAndLine()
    {
        var lines=new[]{"The gnome whispers: the real festival was the snacks we hid along the way.","DO NOT PRESS. You press it. Confetti and an air horn happen.","You feel famous for four seconds.","Fourteen left sandals, one right sandal, and a glow stick still glowing out of spite.","five more minutes… the bass hasn't dropped yet","The flamingo lets out a long, sad squeak.","You pay your respects to Cone. Cone is pleased.","You write BASS in the dust."};
        for(int i=0;i<EggIds.Length;i++)
        {
            var game=new FestivalSimulation(0);var p=game.AddPlayer("host","Host");p.X=EggX[i];p.Z=EggZ[i]+2.5f;
            var accepted=game.Execute(p.Id,Poke(EggIds[i]));
            Check(accepted.Accepted,"egg "+EggIds[i]+" accepts the exact 2.5 m boundary: "+accepted.Reason);
            Check(i==7?Array.Exists(new[]{"BASS","HELP","NARC?","MOM SAYS HI"},word=>accepted.Reason=="You write "+word+" in the dust."):accepted.Reason==lines[i],"egg "+EggIds[i]+" has its own queued gag: "+accepted.Reason);
            p.Z=EggZ[i]+2.51f;
            Check(!game.Execute(p.Id,Poke(EggIds[i],"too_far")).Accepted,"egg "+EggIds[i]+" rejects beyond 2.5 m");
            Check(p.Cash==20&&p.Inventory.Count==0&&game.State.Interactions.Count==0&&game.State.GrossSales==0,"eggs do not become transactions or festival tasks");
        }
        var unknown=new FestivalSimulation(0);unknown.AddPlayer("host","Host");
        Check(!unknown.Execute("host",Poke("not_an_egg")).Accepted,"unknown stable ids are refused");
    }
    static JsonElement Wire(object value)=>JsonSerializer.SerializeToElement(value,new JsonSerializerOptions{IncludeFields=true});
    static JsonElement Field(JsonElement value,string name){Check(value.TryGetProperty(name,out var field),"serialized gameplay field "+name+" is missing");return field;}
    static void AcceptedPokesPersistPerEggAndDiscoveriesSurviveIntermediateEvents()
    {
        var game=new FestivalSimulation(0);var p=game.AddPlayer("host","Host");game.AddPlayer("friend","Friend");
        p.X=4.85f;p.Z=-5.6f;var command=Poke("button");var first=game.Execute(p.Id,command);
        game.Execute(p.Id,command);
        var eggs=Field(Wire(game.State),"CampEggs");Check(eggs.GetArrayLength()==8,"the visit carries eight serializable records");
        var button=eggs[1];Check(Field(button,"ActivationCount").GetInt32()==1,"replayed command activates exactly once");
        Check(Field(button,"LastActorId").GetString()==p.Id&&Field(button,"Sequence").GetInt64()==first.Sequence,"the event identifies its actor and committed sequence");
        Check(Field(button,"LastActivationAt").GetDouble()==0,"the event uses the host clock");
        Check(game.Execute(p.Id,Poke("button","second")).Reason=="You were warned. Twice now.","button line rotation is per egg, host authored");
        game.Tick(1.2);p.X=23;p.Z=-6;Check(game.Execute(p.Id,Poke("flamingo","sad")).Accepted,"flamingo deflates");
        p.X=-14;p.Z=-11.2f;var dust=game.Execute(p.Id,Poke("wash_me","dust"));
        Check(dust.Reason=="You write "+game.State.CampEggs[7].Word+" in the dust.","first seeded dust word matches the shared state");
        var dust2=game.Execute(p.Id,Poke("wash_me","dust2"));string word2=game.State.CampEggs[7].Word;
        Check(dust2.Reason=="The dust now says "+word2+". Very professional.","host rotates the line independently of the word draw");
        eggs=Field(Wire(game.State),"CampEggs");Check(Field(eggs[5],"Deflated").GetBoolean(),"flamingo persists after later eggs");
        Check(Field(eggs[7],"Word").GetString()==word2,"the actual word is persistent state");
        var player=Wire(p);Check(Field(player,"FoundCampEggMask").GetInt32()==162,"button, flamingo and dust accumulate despite skipped intermediate snapshots");
        Check(Field(player,"LastCampEggId").GetString()=="wash_me"&&Field(player,"CampEggSequence").GetInt32()==5,"last-player event and sequence accompany cumulative discoveries");
        Check(Field(player,"LastCampEggAt").GetDouble()==game.State.SimulationSeconds&&Field(player,"LastCampEggLine").GetString()==dust2.Reason,"last-player event has authoritative time and wording");
        Check(Field(Wire(game.Player("friend")),"FoundCampEggMask").GetInt32()==0,"watching a friend grants no discovery");
        p.X=-11;p.Z=16;Check(game.Execute(p.Id,Poke("gnome","gnome_after_button")).Reason=="The gnome whispers: the real festival was the snacks we hid along the way.","one egg does not advance another egg's lines");
        p.X=-14;p.Z=-11.2f;game.Execute(p.Id,Poke("wash_me","dust3"));
        Check(Array.Exists(new[]{"BASS","HELP","NARC?","MOM SAYS HI"},word=>word==game.State.CampEggs[7].Word),"word draws stay in the queued pool");
    }
    static void TheChairHasOneOwnerForFourSecondsAndMovementReleasesIt()
    {
        var game=new FestivalSimulation(0);var host=game.AddPlayer("host","Host");var friend=game.AddPlayer("friend","Friend");
        host.X=friend.X=-10;host.Z=friend.Z=3.5f;
        Check(game.Execute(host.Id,Poke("chair")).Accepted,"owner sits");var chair=game.State.CampEggs.Find(e=>e.Id=="chair");
        Check(chair.SeatedPlayerId==host.Id&&chair.SeatedUntil==4,"the host publishes one owner and a four-second deadline");
        Check(!host.Ready&&host.InteractionId==""&&game.State.Interactions.Count==0,"sitting is a nonblocking camp pose, never ready or a festival task");
        Check(!game.Execute(friend.Id,Poke("chair","occupied")).Accepted&&chair.ActivationCount==1,"simultaneous occupants are refused without another gag");
        Check(!game.Execute(host.Id,Poke("chair","reseat")).Accepted&&chair.SeatedUntil==4,"owner cannot extend the gag while seated");
        Check(!game.TryMove(host.Id,host.X+5,host.Z,0,.1)&&chair.SeatedPlayerId==host.Id,"rejected movement does not unseat");
        Check(game.TryMove(host.Id,host.X,host.Z,90,.1)&&chair.SeatedPlayerId==host.Id,"looking around keeps the seat");
        Check(game.TryMove(host.Id,host.X+.2f,host.Z,90,.1)&&chair.SeatedPlayerId=="","a successful step stands up at normal camp speed");
        Check(game.Execute(friend.Id,Poke("chair","next")).Accepted,"another friend can sit after movement");
        game.Tick(3.9);Check(chair.SeatedPlayerId==friend.Id,"pose lasts nearly four seconds");
        game.Tick(.2);Check(chair.SeatedPlayerId==""&&chair.SeatedUntil==0,"deadline releases the seat without a festival interaction");
        Check(game.Execute(host.Id,Poke("chair","third")).Accepted,"seat can be used again");game.Disconnect(host.Id);
        Check(chair.SeatedPlayerId=="","disconnect releases the seat immediately");
        Check(game.Execute(friend.Id,Poke("chair","fourth")).Accepted,"seat can be reused after disconnect");
        game.State.Phase="Playing";game.Tick(.1);Check(chair.SeatedPlayerId=="","leaving camp clears seat ownership");
        var review=new FestivalSimulation(0);var reviewer=review.AddPlayer("host","Host");review.State.Phase="CampReview";reviewer.X=-10;reviewer.Z=3.5f;
        Check(review.Execute(reviewer.Id,Poke("chair")).Accepted,"chair works during debrief");review.Tick(4.1);
        Check(review.State.CampEggs.Find(e=>e.Id=="chair").SeatedPlayerId=="","debrief ticks also release the seat");
    }
    static void NewVisitsResetPropsButKeepOwnerDiscoveriesAndOlderSnapshotsRestore()
    {
        var game=new FestivalSimulation(0){DevelopmentFestivalSelection=true};var p=game.AddPlayer("host","Host");
        var visit=game.State.RoundId;var gnome=game.State.CampEggs.Find(e=>e.Id=="gnome");var gnomeX=gnome.X;var gnomeZ=gnome.Z;
        p.X=23;p.Z=-6;game.Execute(p.Id,Poke("flamingo","sad"));p.X=-14;p.Z=-11.2f;game.Execute(p.Id,Poke("wash_me","dust"));
        Check(game.Execute(p.Id,new GameCommand{Id="festival",Kind="ChooseFestival",Amount=1}).Accepted,"development festival switch is accepted");
        Check(game.State.RoundId==visit&&gnome.X==gnomeX&&gnome.Z==gnomeZ&&game.State.CampEggs[5].Deflated,"development selection doesn't reroll or erase the visit");
        game.State.Phase="Results";game.State.Result="Success";
        Check(game.Execute(p.Id,new GameCommand{Id="next_visit",Kind="Reset"}).Accepted,"return to camp through the production command");
        Check(game.State.RoundId!=visit&&game.State.Phase=="CampReview","a new round defines a new camp visit");
        Check(game.State.CampEggs[0].X!=gnomeX||game.State.CampEggs[0].Z!=gnomeZ,"consecutive visit seeds move the gnome");
        Check(game.State.CampEggs.TrueForAll(e=>e.ActivationCount==0&&e.Sequence==0&&e.LastActivationAt== -1&&!e.Deflated&&e.Word==""&&e.SeatedPlayerId==""),"all transient and persistent props reset for the new visit");
        Check(game.Player("host").FoundCampEggMask==160&&game.Player("host").CampEggSequence==0,"owner's cumulative discoveries carry across visits, event followers start fresh");
        for(int seed=-3;seed<4;seed++)
        {
            var first=new FestivalSimulation(seed);var second=new FestivalSimulation(seed+1);
            Check(first.State.CampEggs[0].X!=second.State.CampEggs[0].X||first.State.CampEggs[0].Z!=second.State.CampEggs[0].Z,"gnome does not repeat across signed visit seeds "+seed);
            var twin=new FestivalSimulation(seed);twin.State.DealSeed=731;
            Check(twin.State.CampEggs[0].X==first.State.CampEggs[0].X&&twin.State.CampEggs[0].Z==first.State.CampEggs[0].Z,"gnome uses the public seed, never private DealSeed");
        }
        var saved=JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(game.State,new JsonSerializerOptions{IncludeFields=true}),new JsonSerializerOptions{IncludeFields=true});
        var restored=new FestivalSimulation(){DevelopmentFestivalSelection=true};restored.Restore(saved);
        Check(restored.State.CampEggs.Count==8&&restored.State.CampEggs[0].X==game.State.CampEggs[0].X&&restored.Player("host").FoundCampEggMask==160,"new snapshot round-trip keeps the chosen spot and discoveries");
        var old=new FestivalSimulation(0);old.AddPlayer("legacy","Legacy");old.State.CampEggs=null;
        restored.Restore(old.State);Check(restored.State.CampEggs!=null&&restored.State.CampEggs.Count==8&&restored.Player("legacy").FoundCampEggMask==0,"legacy snapshots without records are migrated additively");
        old=new FestivalSimulation(0);old.AddPlayer("legacy","Legacy");old.State.CampEggs.Clear();restored.Restore(old.State);
        Check(restored.State.CampEggs.Count==8&&restored.State.CampEggs[0].X== -11,"default-empty legacy records reconstruct from the public seed");
    }
    static void RefusedEggsNeverChangePropsAndAllEightDiscoveriesAccumulate()
    {
        foreach(var egg in CampFeatures.Eggs)
        {
            foreach(var blocker in new[]{"Dead","Ready","Busy","Indoor","Festival","Reach"})
            {
                var game=new FestivalSimulation(0);var p=game.AddPlayer("host","Host");var point=CampFeatures.EggPoint(egg,0);p.X=point.X;p.Z=point.Z;
                if(blocker=="Dead")p.Life="Spirit";if(blocker=="Ready")p.Ready=true;if(blocker=="Busy")p.InteractionId="busy";
                if(blocker=="Indoor")p.CampVisitId="tent_1";if(blocker=="Festival")game.State.Phase="Playing";if(blocker=="Reach")p.Z+=2.51f;
                string props=Wire(game.State.CampEggs).ToString();
                Check(!game.Execute(p.Id,Poke(egg.Id)).Accepted,egg.Id+" refuses "+blocker);
                Check(Wire(game.State.CampEggs).ToString()==props&&p.FoundCampEggMask==0&&p.CampEggSequence==0&&p.LastCampEggAt== -1,egg.Id+" refusal doesn't alter shared state or discovery");
            }
        }
        var collection=new FestivalSimulation(0);var owner=collection.AddPlayer("host","Host");collection.AddPlayer("friend","Friend");
        foreach(var egg in CampFeatures.Eggs)
        {
            var record=collection.State.CampEggs.Find(e=>e.Id==egg.Id);owner.X=record.X;owner.Z=record.Z;
            Check(collection.Execute(owner.Id,Poke(egg.Id,egg.Id)).Accepted,"each stable ID can be found");if(egg.Id=="chair")collection.Tick(4.1);
        }
        Check(owner.FoundCampEggMask==255&&collection.Player("friend").FoundCampEggMask==0,"all eight distinct bits belong solely to their finder");
    }
    static void RestoreKeepsActivePropsAndRejectsCorruptRecords()
    {
        var game=new FestivalSimulation(0);var p=game.AddPlayer("host","Host");p.X=23;p.Z=-6;game.Execute(p.Id,Poke("flamingo","sad"));
        p.X=-14;p.Z=-11.2f;game.Execute(p.Id,Poke("wash_me","dust"));string word=game.State.CampEggs[7].Word;p.X=-10;p.Z=3.5f;game.Execute(p.Id,Poke("chair","seat"));
        var saved=JsonSerializer.Deserialize<RoundState>(Wire(game.State).ToString(),new JsonSerializerOptions{IncludeFields=true});
        saved.CampEggs.RemoveAll(e=>e.Id=="button"||e.Id=="cone");var restored=new FestivalSimulation();restored.Restore(saved);
        Check(restored.State.CampEggs.Count==8&&restored.State.CampEggs[5].Deflated&&restored.State.CampEggs[7].Word==word,"missing additive records are filled without erasing persistent state");
        Check(FestivalSimulation.CampSeated(restored.State,p.Id),"an unexpired chair owner resumes on restore");restored.Tick(4.1);
        Check(!FestivalSimulation.CampSeated(restored.State,p.Id),"the restored deadline still expires");
        foreach(var corrupt in new[]{"Duplicate","Unknown","Point","Count","Time","Word","Mask"})
        {
            var bad=new FestivalSimulation(0);var player=bad.AddPlayer("host","Host");
            if(corrupt=="Duplicate")bad.State.CampEggs.Add(bad.State.CampEggs[0]);if(corrupt=="Unknown")bad.State.CampEggs[0].Id="unknown";
            if(corrupt=="Point")bad.State.CampEggs[0].X=99;if(corrupt=="Count")bad.State.CampEggs[0].ActivationCount=-1;
            if(corrupt=="Time")bad.State.CampEggs[0].LastActivationAt=double.NaN;if(corrupt=="Word")bad.State.CampEggs[7].Word="NOT AUTHORED";
            if(corrupt=="Mask")player.FoundCampEggMask=256;
            var before=restored.State;bool rejected=false;try{restored.Restore(bad.State);}catch(ArgumentException){rejected=true;}
            Check(rejected&&restored.State==before,"corrupt "+corrupt+" is refused without replacing the live round");
        }
    }
    static void DustWordsHaveVariedHostDrawsAcrossVisits()
    {
        var pairs=new System.Collections.Generic.HashSet<string>();
        var words=new System.Collections.Generic.HashSet<string>();
        for(int seed=0;seed<64;seed++)
        {
            var game=new FestivalSimulation(seed);var p=game.AddPlayer("host","Host");p.X=-14;p.Z=-11.2f;
            game.Execute(p.Id,Poke("wash_me","first"));string first=game.State.CampEggs[7].Word;
            game.Execute(p.Id,Poke("wash_me","second"));string second=game.State.CampEggs[7].Word;pairs.Add(first+"|"+second);words.Add(first);words.Add(second);
            var restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(Wire(game.State).ToString(),new JsonSerializerOptions{IncludeFields=true}));
            var next=game.Execute(p.Id,Poke("wash_me","third"));var resumed=restored.Execute(p.Id,Poke("wash_me","third"));
            Check(next.Reason==resumed.Reason&&game.State.CampEggs[7].Word==restored.State.CampEggs[7].Word,"restoring retains the host's deterministic next draw");
        }
        Check(pairs.Count>4,"host word draws vary beyond a fixed four-word cycle across visits");
        Check(words.SetEquals(new[]{"BASS","HELP","NARC?","MOM SAYS HI"}),"host draws reach all four authored words and nothing else");
    }
}
