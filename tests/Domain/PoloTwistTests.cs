using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Festival.Core;

// POLO-1: Palm Mirage (festival 0) has three twists, and Ember Playa has none of them. Influencers film along their phone's
// 8 m, 40 degree cone, and a player in frame puts every wook within 10 m of them on alert. Two VIP zones are roped off to
// anyone without a vip_wristband ($15 at the night market, or talked into by the VIP guard), and inside, buyers pay double.
// The Ferris wheel locks its rider for one 20 s turn, and at night shows them roughly where each lost friend is (POLO-3).
public static class PoloTwistTests
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    static int sequence;
    static void Check(bool pass,string message){if(!pass)throw new Exception("PoloTwist: "+message);}
    static bool Same(double a,double b)=>Math.Abs(a-b)<1e-9;
    static CommandResult Act(FestivalSimulation s,string id,string kind,string target="",string item="")=>s.Execute(id,new GameCommand{Id="polo"+(sequence++),Kind=kind,TargetId=target,ItemId=item});

    // Every scenario runs, so one red run shows each missing rule.
    public static void Run()
    {
        var failures=new List<string>();
        foreach(var test in new Action[]{ThreeInfluencersFilmOnPalmMirageOnly,APlayerInFrameAlertsEveryWookNearby,OnlyTheFrameCounts,
            FramesAreAPassiveGain,TwistTagsSurviveASnapshot,VipZonesKeepOutAnyoneWithoutAWristband,VipSalesPayDouble,
            TheNightMarketSellsWristbands,TheVipGuardTalksAnyoneIn,EveryNightHasAVipGuard,TheFerrisWheelLocksItsRiderForOneTurn,TheRiderSeesTheFriendAtNight,TheWheelShowsOnlyARoughArea})
            try{test();}catch(Exception error){failures.Add(test.Method.Name+" -> "+error.Message);}
        if(failures.Count>0)throw new Exception(failures.Count+" polo twist test(s) failed:\n"+string.Join("\n",failures));
    }

    // Two friends (or `crew`) ready up at camp, the wheels spin and the crew loads into the crowd.
    static FestivalSimulation Start(int seed,int level,int festival,int crew=2,int encore=0)
    {
        var s=new FestivalSimulation(seed);for(int k=0;k<crew;k++)s.AddPlayer("p"+k,"P"+k);
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.LevelIndex=level;s.State.EncoreTier=encore;
        foreach(var p in s.State.Players){p.X=0;p.Z=19;Check(Act(s,p.Id,"Ready").Accepted,"setup: "+p.Id+" readies");}
        s.Tick(5.2);s.Tick(s.State.SpinEndsAt-s.State.SimulationSeconds+.1);foreach(var p in s.State.Players)Act(s,p.Id,"MapReady");
        Check(s.State.Phase=="Playing","setup: the crew is at "+Festivals.Name(festival));
        return s;
    }
    static List<NpcState> Tagged(FestivalSimulation s,string twist)=>s.State.Npcs.FindAll(n=>n.Twist==twist);

    // A hand-built Day 1 on Palm Mirage: an influencer at the origin filming north (+Z) and player p0 on camera 4 m in front
    // of them. A second wook stands 7 m from p0 with its back to them, one more stands 11 m away, and a cop is right beside
    // p0. Nobody is suspicious yet, and nobody is sprinting, so the only thing that can raise suspicion is the camera.
    static FestivalSimulation Filming(int crew=1,int level=0,int encore=0)
    {
        var s=new FestivalSimulation(3);for(int k=0;k<crew;k++)s.AddPlayer("p"+k,"P"+k);
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.LevelIndex=level;s.State.EncoreTier=encore;s.State.Phase="Playing";
        s.State.Npcs.Clear();
        s.State.Npcs.Add(new NpcState{Id="cam",Twist=FestivalSimulation.Influencer});
        s.State.Npcs.Add(new NpcState{Id="near",Z=-3,Yaw=180});
        s.State.Npcs.Add(new NpcState{Id="far",Z=15});
        s.State.Npcs.Add(new NpcState{Id="cop",Kind="Cop",Mode="Patrol",X=1,Z=4,Yaw=90});
        Place(s,0,0,4);
        // The rest of the crew, if any, bunches up just beside p0, on camera too.
        for(int k=1;k<crew;k++)Place(s,k,k%2==0?-1:1,4.5);
        return s;
    }
    static void Place(FestivalSimulation s,int k,double x,double z){var p=s.Player("p"+k);p.X=(float)x;p.Z=(float)z;}
    static NpcState Npc(FestivalSimulation s,string id)=>s.State.Npcs.Find(n=>n.Id==id);
    static double Heat(FestivalSimulation s,string npc,int k=0)=>Npc(s,npc).Observers.Find(o=>o.PlayerId=="p"+k)?.Suspicion??0;
    // What the near wook, who cannot see p0, gains on them in one second.
    static double Filmed(FestivalSimulation s){double before=Heat(s,"near");s.Tick(1);return Heat(s,"near")-before;}

    static void ThreeInfluencersFilmOnPalmMirageOnly()
    {
        var casts=new HashSet<string>();var filmers=new HashSet<string>();
        for(int seed=1;seed<=12;seed++)
        {
            var palm=Start(seed,seed%Festivals.LevelCount,0);var cast=Tagged(palm,FestivalSimulation.Influencer);
            Check(cast.Count==Festivals.Influencers&&Festivals.Influencers==3,"seed "+seed+": three festivalgoers film on Palm Mirage, got "+cast.Count);
            Check(cast.TrueForAll(n=>n.Kind=="Wook"),"seed "+seed+": influencers are festivalgoers, never cops");
            casts.Add(string.Join(",",cast.ConvertAll(n=>n.Id)));foreach(var n in cast)filmers.Add(n.Id);
            var playa=Start(seed,seed%Festivals.LevelCount,1);
            Check(playa.State.Npcs.TrueForAll(n=>n.Twist==""),"seed "+seed+": nobody films on Ember Playa");
        }
        Check(casts.Count==12&&filmers.Count>=FestivalCrowdLayout.Count/2,"every level casts its own influencers, and over a dozen levels the cameras move around the crowd: "+casts.Count+" casts, "+filmers.Count+" filmers");
    }

    static void APlayerInFrameAlertsEveryWookNearby()
    {
        var s=Filming();double gained=Filmed(s);
        Check(Festivals.FilmSuspicionPerSecond>0&&Same(gained,Festivals.FilmSuspicionPerSecond),"a wook 7 m from p0 who can't see them gains "+Festivals.FilmSuspicionPerSecond+" a second while p0 is on camera, got "+gained);
        Check(Same(Heat(s,"cam"),Festivals.FilmSuspicionPerSecond),"so does the influencer, got "+Heat(s,"cam"));
        Check(Heat(s,"far")==0,"a wook 11 m away does not, got "+Heat(s,"far"));
        Check(Npc(s,"cop").Observers.Count==0,"and cops ignore the influencers");
        Check(Heat(s,"near")<25&&Npc(s,"near").Mode=="Blending","one second on camera is not enough to be watched");
        s.Tick(9);Check(Npc(s,"near").Mode=="Watching","ten seconds is ("+Heat(s,"near")+")");
    }

    static void OnlyTheFrameCounts()
    {
        // The influencer is always within 10 m of anyone they could film, so their own suspicion shows whether p0 was on camera.
        double Cam(Action<FestivalSimulation> arrange){var s=Filming();arrange(s);s.Tick(1);return Heat(s,"cam");}
        void Nothing(string why,Action<FestivalSimulation> arrange){double gained=Cam(arrange);Check(gained==0,why+": no suspicion, got "+gained);}
        void Something(string why,Action<FestivalSimulation> arrange){double gained=Cam(arrange);Check(Same(gained,Festivals.FilmSuspicionPerSecond),why+": on camera, got "+gained);}
        double Rad(double degrees)=>degrees*Math.PI/180;
        Something("7.9 m straight ahead",s=>Place(s,0,0,7.9));
        Nothing("8.1 m straight ahead is out of range",s=>Place(s,0,0,8.1));
        Something("5 m away, 19 degrees off the phone's axis",s=>Place(s,0,5*Math.Sin(Rad(19)),5*Math.Cos(Rad(19))));
        Nothing("5 m away, 21 degrees off the phone's axis",s=>Place(s,0,5*Math.Sin(Rad(21)),5*Math.Cos(Rad(21))));
        Nothing("behind the influencer",s=>Place(s,0,0,-2));
        Something("where the influencer has turned to",s=>{Place(s,0,4,0);Npc(s,"cam").Yaw=90;});
        Nothing("behind a wall",s=>s.HasLineOfSight=(x,z,xx,zz)=>false);
        Nothing("downed",s=>s.Player("p0").Life="Downed");
        Nothing("when nobody is an influencer",s=>Npc(s,"cam").Twist="");
    }

    // L4 Crowd: influencer frames are a passive gain, so they scale with the level's multiplier and the filmed player's pack,
    // the two together capped at x3.
    static void FramesAreAPassiveGain()
    {
        double alone=Filmed(Filming());Check(alone>0,"setup: a lone player on camera draws suspicion");
        double bunched=Filmed(Filming(crew:3));
        Check(Same(bunched,alone*1.35),"three bunched on camera: x1.35 (got x"+bunched/alone+")");
        double night=Filmed(Filming(level:3));
        Check(Same(night,alone*Festivals.Level(0,3,0).SuspicionMultiplier)&&night>alone,"Night 2 films hotter: x"+Festivals.Level(0,3,0).SuspicionMultiplier+" (got x"+night/alone+")");
        double capped=Filmed(Filming(crew:3,level:3,encore:5));
        Check(Same(capped,alone*3),"a bunched crew on an encore Night 2 hits the x3 cap (got x"+capped/alone+")");
        var spooned=Filming();spooned.Player("p0").Inventory.Add(new ItemStack{ItemId="little_spoon",Count=1});
        Check(Same(Filmed(spooned),alone*.9),"the little spoon softens it like any passive gain");
    }

    static void TwistTagsSurviveASnapshot()
    {
        var s=Start(7,0,0);var cast=string.Join(",",Tagged(s,FestivalSimulation.Influencer).ConvertAll(n=>n.Id));Check(cast!="","setup: someone is filming");
        var restored=new FestivalSimulation();restored.Restore(JsonSerializer.Deserialize<RoundState>(JsonSerializer.Serialize(s.State,Json),Json));
        Check(string.Join(",",Tagged(restored,FestivalSimulation.Influencer).ConvertAll(n=>n.Id))==cast,"the influencers survive a snapshot");
        var legacy=JsonNode.Parse(JsonSerializer.Serialize(s.State,Json)).AsObject();
        foreach(var npc in legacy["Npcs"].AsArray())npc.AsObject().Remove("Twist");
        var old=new FestivalSimulation();old.Restore(JsonSerializer.Deserialize<RoundState>(legacy.ToJsonString(),Json));
        Check(old.State.Npcs.TrueForAll(n=>n.Twist==""),"a snapshot from before twists restores with nobody filming");
        old.Tick(1);Check(old.State.Phase=="Playing","and plays on");
    }

    static int Bands(PlayerState p)=>p.Inventory.Find(i=>i.ItemId==FestivalSimulation.VipWristband)?.Count??0;
    static void Band(PlayerState p)=>p.Inventory.Add(new ItemStack{ItemId=FestivalSimulation.VipWristband,Count=1});
    // A hand-built Day 1 with nobody tripping (so sales pay x1): p0 stands inside the west VIP zone beside a buyer, and
    // nobody else is around to see a deal.
    static FestivalSimulation Vip(int festival=0)
    {
        var s=new FestivalSimulation(3);s.AddPlayer("p0","P0");
        s.State.UnlockedFestivalCount=Festivals.Count;s.State.FestivalIndex=festival;s.State.Phase="Playing";
        s.State.Npcs.Clear();s.State.Npcs.Add(new NpcState{Id="buyer",Role="Buyer",X=-11,Z=22});Place(s,0,-11,23);
        return s;
    }
    // p0 steps from (x,22) to (to,22) in a tenth of a second.
    static bool Step(FestivalSimulation s,float x,float to){Place(s,0,x,22);return s.TryMove("p0",to,22,0,.1);}
    // p sells a Tongue Stamp to n, hitting every note on the beat; returns the cash it made.
    static int Sell(FestivalSimulation s,PlayerState p,NpcState n)
    {
        p.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});int before=p.Cash;
        var started=Act(s,p.Id,"StartSale",n.Id,"stock_lsd");Check(started.Accepted,"setup: the sale starts: "+started.Reason);
        var sale=s.Interaction(p.InteractionId);var notes=RhythmChart.For(sale).Notes;int next=0;
        for(int guard=0;sale.Status=="Active"&&guard<400;guard++)
        {
            double now=s.State.SimulationSeconds-sale.StartSeconds;
            for(;next<notes.Count&&notes[next].TimeSeconds<=now;next++)
                Check(s.Execute(p.Id,new GameCommand{Id="polo"+(sequence++),Kind="Rhythm",Direction=notes[next].Direction,TimeSeconds=notes[next].TimeSeconds}).Accepted,"setup: note "+next+" lands");
            s.Tick(.1);
        }
        Check(sale.Status=="Complete"&&sale.Score>=.75,"setup: a perfect sale");
        return p.Cash-before;
    }
    // Stand p a metre in front of n, in n's view.
    static void Beside(PlayerState p,NpcState n){double yaw=n.Yaw*Math.PI/180;p.X=n.X+(float)Math.Sin(yaw);p.Z=n.Z+(float)Math.Cos(yaw);}

    static void VipZonesKeepOutAnyoneWithoutAWristband()
    {
        Check(Festivals.VipZones.Length==2,"two VIP zones");
        var s=Vip();var p=s.Player("p0");
        // The west zone's aisle rope runs along x = -8, the east zone's along x = 8.
        foreach(var side in new[]{-1f,1f})
        {
            string zone=side<0?"west":"east";
            Check(!Step(s,7.9f*side,8.2f*side)&&p.X==7.9f*side,"without a wristband the "+zone+" rope stops p0");
            Band(p);Check(Step(s,7.9f*side,8.2f*side)&&p.X==8.2f*side,"with one they step past the "+zone+" rope");
            p.Inventory.Clear();
            Check(Step(s,8.2f*side,8.5f*side)&&Step(s,8.2f*side,7.9f*side),"someone already inside the "+zone+" zone without one moves about and steps out freely");
        }
        var playa=Vip(1);Check(Step(playa,-7.9f,-8.2f),"Ember Playa has no ropes");
        var camp=Vip();camp.State.Phase="Shopping";Check(Step(camp,-7.9f,-8.2f),"camp has no ropes either");
        var spirit=Vip();spirit.Player("p0").Life="Spirit";Check(Step(spirit,-7.9f,-8.2f),"and a spirit floats through them");
    }

    static void VipSalesPayDouble()
    {
        int Sale(int festival,float sellerX,float buyerX){var s=Vip(festival);var buyer=Npc(s,"buyer");buyer.X=buyerX;Place(s,0,sellerX,22.5);return Sell(s,s.Player("p0"),buyer);}
        int plain=Sale(0,0,0);
        Check(plain>0&&Festivals.VipPayoutFactor==2,"setup: a perfect sale on open ground pays "+plain);
        Check(Sale(0,-11,-11)==2*plain,"inside the west zone the buyer pays double, got "+Sale(0,-11,-11));
        Check(Sale(0,11,11)==2*plain,"inside the east zone too, got "+Sale(0,11,11));
        Check(Sale(0,-7.5f,-8.5f)==plain,"selling over the rope from outside pays the usual, got "+Sale(0,-7.5f,-8.5f));
        Check(Sale(0,-8.5f,-7.5f)==plain,"so does selling out over the rope to a buyer outside, got "+Sale(0,-8.5f,-7.5f));
        Check(Sale(1,-11,-11)==plain,"Ember Playa has no VIP zones, got "+Sale(1,-11,-11));
        var s=Vip();int doubled=Sell(s,s.Player("p0"),Npc(s,"buyer"));
        Check(s.State.LevelSales==doubled&&s.State.GrossSales==doubled,"the double pay counts toward the day's quota");
    }

    static void TheNightMarketSellsWristbands()
    {
        var band=Catalog.FindItem(FestivalSimulation.VipWristband);
        Check(band!=null&&band.Price==15&&band.StackLimit==1,"a VIP wristband costs $15, one to a player");
        FestivalSimulation AtStall(int festival=0,string phase="Playing"){var s=Vip(festival);s.State.Phase=phase;Place(s,0,Festivals.VipStallX,Festivals.VipStallZ+2);return s;}
        var s=AtStall();var p=s.Player("p0");
        var bought=Act(s,"p0","Buy",item:band.Id);
        Check(bought.Accepted&&p.Cash==5&&Bands(p)==1,"at the night market's VIP stall $20 buys one with $5 left: "+bought.Reason);
        Check(!Act(s,"p0","Buy",item:band.Id).Accepted&&p.Cash==5&&Bands(p)==1,"one is enough");
        var far=AtStall();Place(far,0,Festivals.VipStallX,Festivals.VipStallZ+Festivals.VipStallRange+.2);
        Check(!Act(far,"p0","Buy",item:band.Id).Accepted&&Bands(far.Player("p0"))==0,"only at the stall");
        var broke=AtStall();broke.Player("p0").Cash=14;
        Check(!Act(broke,"p0","Buy",item:band.Id).Accepted&&broke.Player("p0").Cash==14,"not for $14");
        var full=AtStall();foreach(var item in new[]{"confetti","merch_bag","map"})full.Player("p0").Inventory.Add(new ItemStack{ItemId=item,Count=1});
        Check(!Act(full,"p0","Buy",item:band.Id).Accepted&&full.Player("p0").Cash==20,"not with three things in hand");
        var camp=AtStall(phase:"Shopping");
        Check(!Act(camp,"p0","Buy",item:band.Id).Accepted&&camp.Player("p0").Cash==20,"the camp shop doesn't sell them");
        var playa=AtStall(1);
        Check(!Act(playa,"p0","Buy",item:band.Id).Accepted&&playa.Player("p0").Cash==20,"and Ember Playa has no VIP stall");
    }

    // TRIP-10: the guard is whoever stood nearest the post, made a regular by trading roles with one (DealSecretTests). A big crew
    // on an encore night deals so many narcs and clue holders (two trails) that no regular is left. The guard post is still
    // manned, by a buyer, never by a narc or a link in a trail.
    static void EveryNightHasAVipGuard()
    {
        foreach(int crew in new[]{5,8})foreach(int level in new[]{1,3})for(int encore=0;encore<=4;encore++)
        {
            var s=Start(7,level,0,crew,encore);var guards=Tagged(s,FestivalSimulation.VipGuard);string where="crew "+crew+", level "+level+", encore "+encore;
            Check(guards.Count==1,where+": one festivalgoer guards the VIP ropes, got "+guards.Count+" ("+s.State.Npcs.FindAll(n=>n.Role=="Regular").Count+" regulars)");
            Check(guards[0].Role=="Regular"||guards[0].Role=="Buyer"&&!s.State.Npcs.Exists(n=>n.Role=="Regular"),where+": a regular guards, or a buyer when no regular is left, got a "+guards[0].Role);
            Check(guards[0].X==Festivals.VipGuardPostX&&guards[0].Z==Festivals.VipGuardPostZ,where+": at the post");
        }
    }
    static void TheVipGuardTalksAnyoneIn()
    {
        for(int seed=1;seed<=8;seed++)
        {
            var guards=Tagged(Start(seed,seed%Festivals.LevelCount,0),FestivalSimulation.VipGuard);
            Check(guards.Count==1&&guards[0].Role=="Regular","seed "+seed+": one regular festivalgoer guards the VIP ropes");
            Check(guards[0].X==Festivals.VipGuardPostX&&guards[0].Z==Festivals.VipGuardPostZ&&!Festivals.InVipZone(0,guards[0].X,guards[0].Z),"seed "+seed+": at the post outside the west rope");
        }
        var s=Start(2,0,0);var guard=Tagged(s,FestivalSimulation.VipGuard)[0];
        var tripper=s.Player(s.State.TripperId);var friend=s.State.Players.Find(p=>p!=tripper);
        var planted=new VisionState{Id="planted",Kind="Buyer",NpcId=guard.Id,Tell=true};s.State.Visions.Add(planted);
        Beside(friend,guard);
        Check(!Act(s,friend.Id,"ConfirmDance",guard.Id).Accepted&&friend.InteractionId=="","the guard doesn't dance anyone in");
        var chat=Act(s,friend.Id,"ConfirmChat",guard.Id);
        Check(chat.Accepted&&friend.InteractionId!="","a sober friend can talk to the VIP guard: "+chat.Reason);
        // POLO-4: and it is no check. The guard's opener, as their persona talks, then one doorman line, and nothing to ask.
        var said=s.Interaction(friend.InteractionId).Chat;
        Check(said.Questions.Length==0&&said.Answers.Length==0,"a sober friend's chat with the guard asks nothing and hears no answers, got "+said.Questions.Length+" questions");
        // DIALOG-4: openers are built from parts, thousands per persona, so it is checked against the persona's own opener for this
        // chat's seed, which no other persona would say.
        string guardPersona=DialogueGrammar.PersonaFor(guard.Id,guard.Twist);int chatSeed=s.Interaction(friend.InteractionId).ChartSeed;
        Check(said.Opener==DialogueGrammar.Build("Regular",guardPersona,0,chatSeed).Opener&&DialogueGrammar.Personas.All(other=>other==guardPersona||DialogueGrammar.Build("Regular",other,0,chatSeed).Opener!=said.Opener),"it opens as the guard's persona ("+guardPersona+") does: "+said.Opener);
        Check(said.Doorman!=""&&!DialogueGrammar.HasNarcTell(said.Opener+" "+said.Doorman),"then one doorman line: "+said.Doorman);
        s.Tick(FestivalSimulation.ConfirmChatSeconds-.1);Check(Bands(friend)==0,"not in yet "+(FestivalSimulation.ConfirmChatSeconds-.1)+" s in");
        s.Tick(.2);Check(Bands(friend)==1,"after the "+FestivalSimulation.ConfirmChatSeconds+" s chat they wear a VIP wristband");
        Check(!planted.Confirmed,"the friend's chat checks none of the tripper's visions");
        var again=Act(s,friend.Id,"ConfirmChat",guard.Id);
        Check(!again.Accepted&&again.Reason.Contains("already"),"one wristband is enough: "+again.Reason);
        friend.X=-7.9f;friend.Z=22;Check(s.TryMove(friend.Id,-8.2f,22,0,.1),"the wristband gets them past the rope");

        Beside(tripper,guard);Check(Act(s,tripper.Id,"ConfirmChat",guard.Id).Accepted,"setup: the tripper talks to the guard too");
        Check(s.Interaction(tripper.InteractionId).Chat.Questions.Length==3,"the tripper's chat checks their vision of the guard, so it asks the three questions");
        s.Tick(2);tripper.X=guard.X+5;s.Tick(.1);Check(tripper.InteractionId==""&&Bands(tripper)==0,"walking off mid-chat gets nothing");
        Beside(tripper,guard);Check(Act(s,tripper.Id,"ConfirmChat",guard.Id).Accepted,"setup: the tripper tries again");s.Tick(FestivalSimulation.ConfirmChatSeconds+.1);
        Check(Bands(tripper)==1&&planted.Confirmed,"the tripper talks their way in, and their chat checks their vision of the guard");

        var crowded=Start(3,0,0);var full=crowded.State.Players.Find(p=>p.Id!=crowded.State.TripperId);var doorman=Tagged(crowded,FestivalSimulation.VipGuard)[0];
        foreach(var item in new[]{"confetti","merch_bag","map"})full.Inventory.Add(new ItemStack{ItemId=item,Count=1});Beside(full,doorman);
        var refused=Act(crowded,full.Id,"ConfirmChat",doorman.Id);
        Check(!refused.Accepted&&refused.Reason.Contains("hand"),"with three things in hand there's no hand for a wristband: "+refused.Reason);
        var dancer=crowded.Player(crowded.State.TripperId);crowded.State.Visions.Add(new VisionState{Id="planted",Kind="Narc",NpcId=doorman.Id,Tell=true});
        Beside(dancer,doorman);Check(Act(crowded,dancer.Id,"ConfirmDance",doorman.Id).Accepted,"setup: the tripper checks a vision of the guard by dancing");
        crowded.Tick(6);Check(dancer.InteractionId==""&&Bands(dancer)==0,"a dance with the guard checks the vision but gets nobody in");

        // POLO-4: a chat that checks nothing never hints at the guard's role. Whatever they are, the same seed says the same.
        var regular=RopeChat("Regular",false);
        foreach(var role in new[]{"Buyer","Narc"})
        {
            var c=RopeChat(role,false);
            Check(c.Opener==regular.Opener&&c.Doorman==regular.Doorman&&c.Questions.Length==0&&c.Answers.Length==0,"a "+role+" guard says just what a regular one would: "+c.Opener+" / "+c.Doorman);
        }
        var uncheckedChat=RopeChat("Narc",true);
        Check(uncheckedChat.Questions.Length==0&&uncheckedChat.Opener==regular.Opener&&uncheckedChat.Doorman==regular.Doorman,"the tripper with no vision of the guard is only talking their way in too");
        foreach(var persona in DialogueGrammar.Personas)for(int festival=0;festival<Festivals.Count;festival++)for(int seed=0;seed<60;seed++)
        {
            var rope=DialogueGrammar.Rope(persona,festival,seed);
            Check(rope.Questions.Length==0&&rope.Answers.Length==0,"a rope chat asks nothing on festival "+festival);
            foreach(var line in new[]{rope.Opener,rope.Doorman})
                Check(line!=""&&line.IndexOf('{')<0&&!line.StartsWith(festival+":")&&!DialogueGrammar.HasNarcTell(line),persona+" on festival "+festival+", seed "+seed+": a filled-in line with no narc tell: "+line);
        }
    }
    // A fresh Palm Mirage Day 1 (seed 2) where the guard's role is set to `role`, then a sober friend (or the tripper, with no
    // vision of the guard) talks to them. The conversation they get.
    static Conversation RopeChat(string role,bool tripper)
    {
        var s=Start(2,0,0);var guard=Tagged(s,FestivalSimulation.VipGuard)[0];guard.Role=role;s.State.Visions.RemoveAll(v=>v.NpcId==guard.Id);
        var p=tripper?s.Player(s.State.TripperId):s.State.Players.Find(x=>x.Id!=s.State.TripperId);Beside(p,guard);
        Check(Act(s,p.Id,"ConfirmChat",guard.Id).Accepted,"setup: "+p.Id+" talks to a "+role+" guard");
        return s.Interaction(p.InteractionId).Chat;
    }

    // Stand p at the Ferris wheel's base and get on.
    static CommandResult Board(FestivalSimulation s,PlayerState p,float metres=1){p.X=Festivals.WheelX;p.Z=Festivals.WheelZ+metres;return Act(s,p.Id,"RideWheel");}

    static void TheFerrisWheelLocksItsRiderForOneTurn()
    {
        var s=Start(4,0,0);var p=s.Player("p0");var friend=s.Player("p1");
        Check(!Board(s,p,Festivals.WheelReach+.2f).Accepted&&!FestivalSimulation.OnWheel(s.State,p.Id),"the wheel is boarded at its base");
        var boarded=Board(s,p);
        Check(boarded.Accepted&&FestivalSimulation.OnWheel(s.State,p.Id),"at the base the rider gets on: "+boarded.Reason);
        Check(Festivals.WheelRideSeconds==20,"one turn takes 20 s");
        float x=p.X,z=p.Z;
        Check(!s.TryMove(p.Id,x+.05f,z,0,.1)&&p.X==x&&p.Z==z,"the rider can't step off");
        Check(s.TryMove(p.Id,x,z,90,.1)&&p.Yaw==90,"but can look around");
        var off=Act(s,p.Id,"Cancel");Check(!off.Accepted&&FestivalSimulation.OnWheel(s.State,p.Id),"nor jump off mid-turn: "+off.Reason);
        Check(Board(s,friend,-1).Accepted,"a friend takes the next gondola");
        s.Tick(19.9);Check(FestivalSimulation.OnWheel(s.State,p.Id)&&!s.TryMove(p.Id,x+.05f,z,0,.1),"19.9 s in, still up there");
        s.Tick(.2);Check(!FestivalSimulation.OnWheel(s.State,p.Id)&&p.InteractionId=="","after one turn the rider is back on the ground");
        Check(s.TryMove(p.Id,x+.05f,z,0,.1),"and walks off");
        var playa=Start(4,0,1);var refused=Board(playa,playa.Player("p0"));
        Check(!refused.Accepted&&!FestivalSimulation.OnWheel(playa.State,"p0"),"Ember Playa has no Ferris wheel: "+refused.Reason);
    }

    static void TheRiderSeesTheFriendAtNight()
    {
        var night=Start(5,1,0);var rider=night.Player("p0");var friend=night.Player("p1");
        Check(!FestivalSimulation.WheelShowsFriend(night.State,rider.Id),"on the ground the rider sees nothing special");
        Check(Areas(night,rider.Id)=="","nor makes out any area");
        Check(Board(night,rider).Accepted,"setup: the rider gets on at night");
        Check(FestivalSimulation.WheelShowsFriend(night.State,rider.Id),"from the wheel at night the rider sees where the lost friend is");
        var lost=night.State.FriendPosition;var area=FestivalSimulation.WheelArea(lost);
        Check(Areas(night,rider.Id)==area.X+","+area.Z,"POLO-3: what they make out is the friend's rough area, not their spot "+lost.X+","+lost.Z+": got ["+Areas(night,rider.Id)+"]");
        Check(!FestivalSimulation.WheelShowsFriend(night.State,friend.Id)&&Areas(night,friend.Id)=="","their friend on the ground doesn't");
        night.State.FriendFound=true;Check(Areas(night,rider.Id)=="","a found friend follows their escort, so there is no area to make out");night.State.FriendFound=false;
        night.Tick(20.1);Check(!FestivalSimulation.WheelShowsFriend(night.State,rider.Id)&&Areas(night,rider.Id)=="","and the view ends with the ride");
        var day=Start(5,0,0);Check(Board(day,day.Player("p0")).Accepted,"setup: a rider by day");
        Check(!FestivalSimulation.WheelShowsFriend(day.State,"p0")&&Areas(day,"p0")=="","nobody is lost by day, so there is no friend to see");
        var crowd=Start(5,1,0,5);var second=crowd.State.SecondFriend;Check(second.Active,"setup: a crew of five loses two friends");
        Check(Board(crowd,crowd.Player("p0")).Accepted,"setup: one of the five rides at night");
        var first=FestivalSimulation.WheelArea(crowd.State.FriendPosition);var other=FestivalSimulation.WheelArea(second.Position);
        Check(Areas(crowd,"p0")==first.X+","+first.Z+" "+other.X+","+other.Z,"both lost friends' areas, in order: got ["+Areas(crowd,"p0")+"]");
        second.Found=true;Check(Areas(crowd,"p0")==first.X+","+first.Z,"only the friends still lost: got ["+Areas(crowd,"p0")+"]");
    }
    // FestivalSimulation.WheelFriendAreas, written out as "x,z x,z".
    static string Areas(FestivalSimulation s,string viewer)=>string.Join(" ",FestivalSimulation.WheelFriendAreas(s.State,viewer).Select(a=>a.X+","+a.Z));

    // POLO-3: from up there a lost friend is only a rough area, the centre of the WheelAreaSize square they stand in. The grid
    // is fixed to the map, so riding again never narrows it down, and only a friend lost right at a square's centre is told
    // exactly where they are.
    static void TheWheelShowsOnlyARoughArea()
    {
        Check(Festivals.WheelAreaSize==20,"the areas are 20 m squares");
        string At(float x,float z){var a=FestivalSimulation.WheelArea(new WorldPoint(x,z));return a.X+","+a.Z;}
        // The three spots a friend gets lost at (SplitObjective.FriendSpots), and spots on, beside and just inside square edges.
        foreach(var (x,z,area) in new[]{(-24f,25f,"-30,30"),(25f,24f,"30,30"),(18f,5f,"10,10"),(0f,0f,"10,10"),(19.9f,-.1f,"10,-10"),(-.5f,-39.5f,"-10,-30"),(40f,-20f,"50,-10")})
            Check(At(x,z)==area,"the spot "+x+","+z+" lies in the square round "+area+": got "+At(x,z));
        Check(At(21,39)==At(39,21)&&At(21,21)==At(39,39),"two spots in one square give the same area: "+At(21,39)+", "+At(39,21)+", "+At(21,21)+", "+At(39,39));
        Check(At(10,10)=="10,10"&&At(-30,30)=="-30,30","a square's own centre is its area");
        for(float x=-50;x<=50;x+=2.5f)for(float z=-50;z<=50;z+=2.5f)
        {
            var spot=new WorldPoint(x,z);var area=FestivalSimulation.WheelArea(spot);
            Check(Math.Abs(area.X-x)<=Festivals.WheelAreaSize/2&&Math.Abs(area.Z-z)<=Festivals.WheelAreaSize/2,"the area round "+x+","+z+" is the square they stand in: got "+area.X+","+area.Z);
            Check(At(area.X,area.Z)==area.X+","+area.Z,"riding again names the same area for "+x+","+z);
            Check((area.X==x&&area.Z==z)==(Math.Abs(x%20)==10&&Math.Abs(z%20)==10),"a spot is its own area only at a square's centre: "+x+","+z+" gives "+area.X+","+area.Z);
        }
    }
}
