using System;
using System.Collections.Generic;
using System.Text.Json;
using Festival.Core;
public static class SimulationTests {
 static int sequence;
 static GameCommand C(string kind,string item="",string target="",int amount=0){return new GameCommand{Id="test_"+(++sequence),Kind=kind,ItemId=item,TargetId=target,Amount=amount};}
 static void Assert(bool condition,string why){if(!condition)throw new Exception(why);}
 static FestivalSimulation Game(){var s=new FestivalSimulation();var a=s.AddPlayer("a","A");var b=s.AddPlayer("b","B");a.X=b.X=0;a.Z=b.Z=19;s.Execute("a",C("Ready"));s.Execute("b",C("Ready"));Assert(s.Execute("a",C("Start")).Accepted,"start");s.Tick(FestivalSimulation.SpinSeconds+.1);s.Execute("a",C("MapReady"));s.Execute("b",C("MapReady"));Assert(s.State.Phase=="Playing","map ready");return s;}
 // The spinner doses one of the pair; tests about sober walking and consumables use the other.
 static PlayerState Sober(FestivalSimulation s){return s.State.Players.Find(x=>x.Id!=s.State.TripperId);}
 static void ConfettiTellsThePlayerHowManyItDistracted(){
  var s=Game();var p=s.Player("a");p.X=p.Z=0;s.State.Npcs.Clear();
  var first=new NpcState{Id="calm-one",X=0,Z=2};var second=new NpcState{Id="calm-two",X=2,Z=0};
  s.State.Npcs.Add(first);s.State.Npcs.Add(second);p.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});
  var command=C("Use","confetti");var fired=s.Execute(p.Id,command);
  Assert(fired.Accepted,"setup: confetti use accepted");
  Assert(fired.Reason=="Confetti! 2 festivalgoers look away for 4 s","confetti receipt must explain affected count; got "+fired.Reason);
  Assert(first.DistractedUntil==s.State.SimulationSeconds+4&&second.DistractedUntil==s.State.SimulationSeconds+4,"both calm festivalgoers distracted for four seconds");
  Assert(p.Inventory.Find(i=>i.ItemId=="confetti")==null,"festival use consumes one cannon");
  Assert(s.Execute(p.Id,command).Reason==fired.Reason,"duplicate command returns same receipt");
  Assert(p.VisualConfettiBurstSequence==1,"one accepted use is one public burst, never two for a replayed command; got "+p.VisualConfettiBurstSequence);
 }
 // Accusers and swarmers ignore the cannon, cops are no festivalgoers and 8 m is its reach; every festival use still spends one and bursts.
 static void ConfettiSaysWhyItFooledNobody(){
  var s=Game();var p=s.Player("a");p.X=p.Z=0;s.State.Npcs.Clear();p.Inventory.Add(new ItemStack{ItemId="confetti",Count=3});
  var swarming=new NpcState{Id="swarming",X=0,Z=1,Mode="Swarming",TargetId=p.Id};var accusing=new NpcState{Id="accusing",X=1,Z=0,Mode="Accusing",TargetId=p.Id};
  var far=new NpcState{Id="far",X=0,Z=-8.5f};var cop=new NpcState{Id="cop",Kind="Cop",X=-1,Z=0};
  s.State.Npcs.AddRange(new[]{swarming,accusing,far,cop});
  var worked=s.Execute(p.Id,C("Use","confetti"));
  Assert(worked.Accepted&&worked.Reason=="Confetti! They're too worked up to care","only accusers and swarmers in reach; got "+worked.Reason);
  Assert(swarming.DistractedUntil==0&&accusing.DistractedUntil==0&&far.DistractedUntil==0&&cop.DistractedUntil==0,"nobody is distracted");
  Assert(Count(p,"confetti")==2&&p.VisualConfettiBurstSequence==1,"the cannon is still spent and still bursts");
  s.State.Npcs.Remove(swarming);s.State.Npcs.Remove(accusing);
  var nobody=s.Execute(p.Id,C("Use","confetti"));
  Assert(nobody.Reason=="Confetti! Nobody close enough to fool","no festivalgoer within 8 m, a cop beside you; got "+nobody.Reason);
  var edge=new NpcState{Id="edge",X=8,Z=0};s.State.Npcs.Add(edge);s.State.Npcs.Add(swarming);
  var one=s.Execute(p.Id,C("Use","confetti"));
  Assert(one.Reason=="Confetti! 1 festivalgoer looks away for 4 s","a mixed crowd counts only the calm one; got "+one.Reason);
  Assert(edge.DistractedUntil==s.State.SimulationSeconds+4&&swarming.DistractedUntil==0&&far.DistractedUntil==0,"exactly 8 m away is in reach, the swarmer stays immune");
  Assert(Count(p,"confetti")==0&&p.VisualConfettiBurstSequence==3,"three uses, three cannons, three bursts");
 }
 // Fooled festivalgoers turn to the burst at once and keep facing where it went off while they dance; the immune keep their own facing.
 static void FooledFestivalgoersFaceTheBurst(){
  var s=Game();var p=s.Player("a");p.X=3;p.Z=-2;s.State.Npcs.Clear();p.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});
  var north=new NpcState{Id="north",X=3,Z=2,Yaw=0};var west=new NpcState{Id="west",X=-1,Z=-2,Yaw=0};var swarming=new NpcState{Id="swarming",X=3,Z=-3,Yaw=42,Mode="Swarming",TargetId=p.Id};
  s.State.Npcs.AddRange(new[]{north,west,swarming});
  Assert(s.Execute(p.Id,C("Use","confetti")).Accepted,"setup: fire");
  Assert(Math.Abs(north.Yaw-180)<.01&&Math.Abs(west.Yaw-90)<.01,"each fooled festivalgoer faces the burst; got "+north.Yaw+", "+west.Yaw);
  Assert(swarming.Yaw==42,"a swarmer is not turned by it");
  p.X=10;p.Z=10;s.Tick(.5);
  Assert(north.Mode=="Distracted"&&west.Mode=="Distracted","they dance distracted");
  Assert(Math.Abs(north.Yaw-180)<.01&&Math.Abs(west.Yaw-90)<.01,"facing where it went off, not following the player; got "+north.Yaw+", "+west.Yaw);
 }
 // Camp's only Use: alive and outdoors the cannon fires free. It stays in hand and fools nobody, though camp has festivalgoers.
 static void CampConfettiIsAFreePartyTrick(){
  var s=new FestivalSimulation();var p=s.AddPlayer("a","A");var b=s.AddPlayer("b","B");p.X=0;p.Z=0;p.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});
  var near=s.State.Npcs.Find(n=>n.Kind=="Wook");near.X=0;near.Z=1;float yaw=near.Yaw;
  Assert(s.State.Phase=="Shopping","setup: camp");
  var command=C("Use","confetti");var fired=s.Execute(p.Id,command);
  Assert(fired.Accepted&&fired.Reason=="Confetti! A camp party trick: your cannon stays loaded","camp fires free; got "+fired.Reason);
  Assert(Count(p,"confetti")==1,"the cannon is not used up at camp");
  Assert(near.DistractedUntil==0&&near.Yaw==yaw&&near.Mode=="Blending","nobody at camp is distracted or turned");
  Assert(p.VisualConfettiBurstSequence==1&&s.Execute(p.Id,command).Reason==fired.Reason&&p.VisualConfettiBurstSequence==1,"it bursts once, never again for a replay");
  Assert(s.Execute(p.Id,C("Use","confetti")).Accepted&&p.VisualConfettiBurstSequence==2,"and fires again");
  p.Inventory.Add(new ItemStack{ItemId="stash_box",Count=1});
  Assert(s.Execute(p.Id,C("Use","stash_box")).Reason=="Start the round first","other gear still waits for the festival");
  Assert(s.Execute(b.Id,C("Use","confetti")).Reason=="Item not owned"&&b.VisualConfettiBurstSequence==0,"only an owned cannon fires");
  p.X=-14;p.Z=-15;Assert(s.Execute(p.Id,C("EnterCamp",target:"car_1")).Accepted,"setup: inside the car");
  Assert(s.Execute(p.Id,C("Use","confetti")).Reason=="Leave the camp interior first","never inside a tent, car or potty");
  Assert(s.Execute(p.Id,C("ExitCamp")).Accepted,"setup: back outside");
  p.Life="Downed";Assert(s.Execute(p.Id,C("Use","confetti")).Reason=="Requires a living, free player","only a living player");p.Life="Alive";
  s.State.Phase="CampReview";Assert(s.Execute(p.Id,C("Use","confetti")).Reason=="Round is not interactive","not in the review");
  Assert(p.VisualConfettiBurstSequence==2&&Count(p,"confetti")==1,"refusals neither burst nor spend");
 }
 // A snapshot from before the burst count restores at zero and counts on from there.
 static void OlderSnapshotsCountBurstsFromZero(){
  var s=new FestivalSimulation();var p=s.AddPlayer("a","A");p.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});Assert(s.Execute(p.Id,C("Use","confetti")).Accepted,"setup: a burst");
  var options=new JsonSerializerOptions{IncludeFields=true};var json=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(s.State,options));
  foreach(var player in json["Players"].AsArray())player.AsObject().Remove("VisualConfettiBurstSequence");
  var old=new FestivalSimulation();old.Restore(JsonSerializer.Deserialize<RoundState>(json.ToJsonString(),options));var q=old.Player(p.Id);
  Assert(q.VisualConfettiBurstSequence==0,"an older snapshot counts no burst");
  Assert(old.Execute(q.Id,C("Use","confetti")).Accepted&&q.VisualConfettiBurstSequence==1,"its next burst counts one, got "+q.VisualConfettiBurstSequence);
 }
 // Restore refuses a negative event count on any player, as it refuses a negative CampEggSequence, and keeps the
 // round it had. A count of zero or more restores as saved.
 static void RestoreRefusesANegativeEventCount(){
  var options=new JsonSerializerOptions{IncludeFields=true};var s=new FestivalSimulation();s.AddPlayer("a","A");s.AddPlayer("b","B");
  string json=JsonSerializer.Serialize(s.State,options);
  foreach(var counter in new (string Name,Action<PlayerState,long> Set,Func<PlayerState,long> Get)[]{
   ("VisualGiggleTakeSequence",(p,v)=>p.VisualGiggleTakeSequence=(int)v,p=>p.VisualGiggleTakeSequence),
   ("VisualConfettiBurstSequence",(p,v)=>p.VisualConfettiBurstSequence=(int)v,p=>p.VisualConfettiBurstSequence),
   ("GiggleTankBankSequence",(p,v)=>p.GiggleTankBankSequence=(int)v,p=>p.GiggleTankBankSequence),
   ("SpeechSequence",(p,v)=>p.SpeechSequence=v,p=>p.SpeechSequence),
   ("VisualDanceStepSequence",(p,v)=>p.VisualDanceStepSequence=(int)v,p=>p.VisualDanceStepSequence),
   ("VisualReceiptSequence",(p,v)=>p.VisualReceiptSequence=(int)v,p=>p.VisualReceiptSequence),
   ("DialogueRevision",(p,v)=>p.DialogueRevision=v,p=>p.DialogueRevision),
   ("ConversationOutcomeSequence",(p,v)=>p.ConversationOutcomeSequence=v,p=>p.ConversationOutcomeSequence),
   ("ConversationDismissedSequence",(p,v)=>p.ConversationDismissedSequence=v,p=>p.ConversationDismissedSequence),
   ("MoneyNewsSequence",(p,v)=>p.MoneyNewsSequence=v,p=>p.MoneyNewsSequence)}){
   var good=JsonSerializer.Deserialize<RoundState>(json,options);counter.Set(good.Players[1],3);
   var restored=new FestivalSimulation();restored.Restore(good);Assert(counter.Get(restored.Player("b"))==3,counter.Name+" = 3 restores as saved");
   var bad=JsonSerializer.Deserialize<RoundState>(json,options);counter.Set(bad.Players[1],-1);
   var game=new FestivalSimulation();var kept=game.State;bool refused=false;
   try{game.Restore(bad);}catch(ArgumentException){refused=true;}
   Assert(refused,"Restore refuses a snapshot with "+counter.Name+" = -1");Assert(game.State==kept,"and keeps the round it had after refusing "+counter.Name);
  }
 }
 static int Count(PlayerState p,string item)=>p.Inventory.Find(i=>i.ItemId==item)?.Count??0;
 // FestivalSession.ViewFor gives the host's own view these copies, so each copy carries every field and shares no list,
 // array or object with the host's state, all the way down.
 static void ViewCopiesCarryEveryFieldAndShareNothing(){
  var options=new JsonSerializerOptions{IncludeFields=true};
  var doing=new InteractionState{Id="sale",PlayerId="a",TargetId="n",Kind="Sale",ChartSeed=3,Hunch="h",HeadStart=2,HintedReply=1,Ask=10,TrustDance=true};
  doing.Inputs.Add(new RhythmInput{Direction=2,TimeSeconds=1.5,Release=true});doing.WitnessIds.Add("w");doing.Chat.Opener="o";doing.Chat.Questions[1]="q";doing.Chat.Answers[2]="an";
  var turn=new ConversationTurn{Reaction="re",Line="li",Pick=1};turn.Replies[0]="r";turn.Grades[2]=-1;turn.Garbled[1]=true;turn.GrammarKeys.Add("k");doing.Turns.Add(turn);
  var stash=new StashState{Id="s",X=1,Z=2};stash.Items.Add(new ItemStack{ItemId="x",Count=2});
  var shop=new ShopStockState{ItemId="x",CampAvailable=1,MarketAvailable=2};var dose=new PlayerDose{PlayerId="a",Substance="lsd",Dose=3};var body=new BodyState{PlayerId="b",X=1,Z=2};
  var offer=new TransferOffer{Id="t",FromId="a",ToId="b",ItemId="x",Amount=1,ExpiresAt=9};var vote=new CampReviewVote{PlayerId="a",TargetId="b",Award=2};
  foreach(var (source,copy) in new (object,object)[]{(doing,doing.Copy()),(stash,stash.Copy()),(shop,shop.Copy()),(dose,dose.Copy()),(body,body.Copy()),(offer,offer.Copy()),(vote,vote.Copy())}){
   string name=source.GetType().Name,was=JsonSerializer.Serialize(source,options);
   Assert(JsonSerializer.Serialize(copy,options)==was,"a "+name+" copy carries every field: "+JsonSerializer.Serialize(copy,options));
   var reached=new HashSet<object>(ReferenceEqualityComparer.Instance);Reach(source,reached);var mine=new HashSet<object>(ReferenceEqualityComparer.Instance);Reach(copy,mine);
   Assert(!mine.Overlaps(reached),"a "+name+" copy shares a list, array or object with its source");
  }
 }
 static void Reach(object at,HashSet<object> seen){
  if(at==null||at is string||at.GetType().IsValueType||!seen.Add(at))return;
  if(at is System.Collections.IList list){foreach(var item in list)Reach(item,seen);return;}
  foreach(var f in at.GetType().GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance))if(!f.FieldType.IsValueType)Reach(f.GetValue(at),seen);
 }
 public static void Run(){
  ViewCopiesCarryEveryFieldAndShareNothing();
  ConfettiTellsThePlayerHowManyItDistracted();ConfettiSaysWhyItFooledNobody();FooledFestivalgoersFaceTheBurst();CampConfettiIsAFreePartyTrick();OlderSnapshotsCountBurstsFromZero();RestoreRefusesANegativeEventCount();
  var capacity=new FestivalSimulation();for(int i=0;i<8;i++)capacity.AddPlayer(i.ToString(),"P");bool denied=false;try{capacity.AddPlayer("9","P");}catch(InvalidOperationException){denied=true;}Assert(denied&&capacity.State.Players.Count==8,"capacity");
  var shop=new FestivalSimulation();var p=shop.AddPlayer("a","A");var shelf=Catalog.ShopPoint(true,shop.State.VendorOffers.IndexOf("medical_voucher"));p.X=shelf.X;p.Z=shelf.Z;Assert(shop.Execute("a",C("HoldOffer","medical_voucher")).Accepted,"hold shelf copy");Assert(!shop.Execute("a",C("HoldOffer","poi_practice")).Accepted,"one held item");p.X=0;p.Z=7;var buy=C("Buy","medical_voucher",amount:1);Assert(shop.Execute("a",buy).Accepted,"buy");shop.Execute("a",buy);Assert(p.Cash==15&&p.Inventory[0].Count==1,"buy exactly once");Assert(!shop.Execute("a",C("Buy","medical_voucher",amount:-1)).Accepted,"negative buy");
  var spoonShelf=Catalog.ShopPoint(true,shop.State.VendorOffers.IndexOf("little_spoon"));p.X=spoonShelf.X;p.Z=spoonShelf.Z;Assert(shop.Execute("a",C("HoldOffer","little_spoon")).Accepted,"spoon pickup");p.X=0;p.Z=7;Assert(shop.Execute("a",C("Buy","little_spoon",amount:1)).Accepted,"spoon purchase");Assert(p.EquippedItemId!="little_spoon"&&!shop.Execute("a",C("Equip","little_spoon")).Accepted,"spoon remains passive necklace");
  Assert(!shop.Execute("a",C("EnterCamp",target:"car_1")).Accepted,"camp entry checks distance");p.X=-14;p.Z=-15;Assert(shop.Execute("a",C("EnterCamp",target:"car_1")).Accepted&&p.CampVisitId=="car_1","enter car");float outsideX=p.X;Assert(shop.TryMove("a",p.CampInteriorX+.2f,p.CampInteriorZ,0,.1)&&p.X==outsideX,"walk inside without moving exterior avatar");Assert(!shop.TryMove("a",39,-12,0,.1),"interior walls constrain movement");Assert(shop.Execute("a",C("CampAntic")).Accepted&&p.CampGag.Contains("horn"),"car antic");Assert(shop.Execute("a",C("ExitCamp")).Accepted&&p.CampVisitId=="","exit car");
  p.X=CampFeatures.DjX;p.Z=CampFeatures.DjZ;Assert(shop.Execute("a",C("ChooseCampTrack",amount:2)).Reason=="Camp DJ: DISCO HOUSE"&&shop.State.CampMusicTrack==2,"shared DJ selection: track 2 is DISCO HOUSE");Assert(shop.Execute("a",C("ChooseCampTrack",amount:5)).Reason=="Camp DJ: DUB REGGAE"&&!shop.Execute("a",C("ChooseCampTrack",amount:8)).Accepted&&!shop.Execute("a",C("ChooseCampTrack",amount:-1)).Accepted&&shop.State.CampMusicTrack==5,"MUSIC-5: SILENCE and seven recorded tracks; the DJ rejects unknown tracks");
  Assert(shop.Execute("a",C("ChooseCampTrack",amount:6)).Reason=="Camp DJ: DESERT RHYTHM"&&shop.State.CampMusicTrack==6,"additional camp choice 6 is DESERT RHYTHM");
  Assert(shop.Execute("a",C("ChooseCampTrack",amount:7)).Reason=="Camp DJ: INDUSTRIAL TECHNO"&&shop.State.CampMusicTrack==7,"additional camp choice 7 is INDUSTRIAL TECHNO");
  Assert(shop.Execute("a",C("ChooseCampTrack",amount:0)).Accepted&&shop.State.CampMusicTrack==0,"SILENCE remains choice zero");
  var s=Game();p=Sober(s);Assert(!s.TryMove(p.Id,float.NaN,0,0,.1),"NaN movement");Assert(!s.TryMove(p.Id,20,20,0,.1),"teleport");p.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=2});p.Inventory.Add(new ItemStack{ItemId="stock_mushrooms",Count=1});Assert(s.Execute(p.Id,C("Consume","stock_lsd")).Accepted,"LSD");Assert(!s.Execute(p.Id,C("Consume","stock_lsd")).Accepted&&p.Inventory[0].Count==1,"repeat effect retains stock");Assert(s.Execute(p.Id,C("Consume","stock_mushrooms")).Accepted&&p.Effects.Count==2,"two effects");s.Tick(60);Assert(p.Effects.Count==0,"expiry");
  s=Game();p=Sober(s);float start=p.X;Assert(s.TryMove(p.Id,start+.59f,p.Z,0,.1),"sober stride accepted");p.X=start;p.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});Assert(s.Execute(p.Id,C("Consume","stock_lsd")).Accepted,"take tabs");Assert(!s.TryMove(p.Id,start+.59f,p.Z,0,.1),"impaired stride is host limited");Assert(Math.Abs(Intoxication.LateralDrift(p,1))>.01,"impaired direction wanders");
  s=Game();p=s.Player("a");var b=s.Player("b");p.X=b.X=0;p.Z=b.Z=-20;p.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=2});Assert(s.Execute("a",C("Transfer","stock_lsd","b",1)).Accepted,"offer");var offer=s.State.Transfers[0];Assert(b.Inventory.Count==0,"voluntary recipient");Assert(s.Execute("b",C("AcceptTransfer",target:offer.Id)).Accepted,"accept");Assert(b.Inventory[0].Count==1&&p.Inventory[0].Count==1,"handoff conserved");
  s=Game();p=s.Player("a");b=s.Player("b");b.Life="Detained";p.Cash=0;p.X=-28;p.Z=16;Assert(s.Execute("a",C("BeginRelease",target:"b")).Accepted,"free release");s.Tick(10.1);Assert(b.Life=="Alive"&&p.Cash==0,"broke pair release");
  s=Game();p=s.Player("a");b=s.Player("b");b.Life="Downed";b.DownedRemaining=.1;b.X=0;b.Z=-20;s.Tick(.2);Assert(b.Life=="Spirit"&&s.State.Drops.FindAll(x=>x.ItemId=="wristband").Count==1,"one death band");p.X=0;p.Z=-20;Assert(s.Execute("a",C("Pickup",target:s.State.Drops.Find(x=>x.ItemId=="wristband").Id)).Accepted,"band pickup");p.X=24;p.Z=-20;p.Cash=0;var revive=C("BeginRevival",target:"b");Assert(s.Execute("a",revive).Accepted,"free revival");s.Tick(10.1);s.Execute("a",revive);Assert(b.Life=="Alive"&&b.Health==40&&b.RevivalCount==1&&p.Wristbands.Count==0,"exactly one revival");
  s=Game();s.State.LevelIndex=1;s.State.GateOpened=true;p=s.Player("a");p.X=s.State.FriendPosition.X;p.Z=s.State.FriendPosition.Z;Assert(s.Execute("a",C("FindFriend")).Accepted,"recruit");s.Tick(2.1);p.X=0;p.Z=-32;s.State.FriendPosition.X=0;s.State.FriendPosition.Z=-32;Assert(s.Execute("a",C("Extract")).Accepted,"extract");s.Tick(3.1);Assert(s.State.Result=="Success"&&p.HasCosmetic,"one win reward");Assert(s.Execute("a",C("Reset")).Accepted&&s.State.Phase=="CampReview"&&s.State.GrossSales==0&&s.Player("a").HasCosmetic,"return to camp review preserves cosmetic");
  Assert(!s.Execute("a",C("FinishReview")).Accepted,"review blocks shopping until votes");for(int award=0;award<3;award++)Assert(s.Execute("a",C("ReviewVote",target:"b",amount:award)).Accepted,"host votes a friend for each award");Assert(!s.Execute("a",C("FinishReview")).Accepted,"wait for connected crew");for(int award=0;award<3;award++)Assert(s.Execute("b",C("ReviewVote",target:"a",amount:award)).Accepted,"crew votes a friend for each award");Assert(!s.Execute("b",C("FinishReview")).Accepted,"only host opens shop");Assert(s.Execute("a",C("FinishReview")).Accepted&&s.State.Phase=="Shopping","review precedes next shop");
  s=Game();s.State.LevelIndex=1;s.State.DurationSeconds=.2;s.Tick(.3);Assert(s.State.Result=="Time expired","night deadline");
  s=Game();p=s.Player("a");var witness=s.State.Npcs[0];witness.X=p.X;witness.Z=p.Z+1;witness.Yaw=180;var nonWitness=s.State.Npcs[10];nonWitness.X=35;nonWitness.Z=35;
  Assert(s.Execute("a",C("Dance",target:witness.Id)).Accepted,"local dance starts");for(int k=0;k<400&&p.InteractionId!="";k++)s.Tick(.05);
  var localSuspicion=witness.Observers.Find(o=>o.PlayerId==p.Id);var remoteSuspicion=nonWitness.Observers.Find(o=>o.PlayerId==p.Id);
  Assert(localSuspicion!=null&&localSuspicion.Suspicion>0&&remoteSuspicion!=null&&remoteSuspicion.Suspicion==0,"suspicion is observer-local");
  s=Game();p=s.Player("a");s.State.Npcs.Clear();p.X=0;p.Z=0;var listener=new NpcState{Id="listener",X=0,Z=1,Yaw=180,CanTalk=true};listener.Observers.Add(new ObserverState{PlayerId=p.Id,Suspicion=40});s.State.Npcs.Add(listener);
  // DIALOG-3: wary at 40, the listener asks for a dance before they talk; danced well, the talk follows, each reply smooth.
  Assert(s.Execute("a",C("Conversation",target:listener.Id)).Accepted,"start NPC conversation");var talk=s.Interaction(p.InteractionId);Assert(talk.TrustDance&&talk.DialogueText==FestivalSimulation.TrustAsk,"a wary NPC asks for a dance first");
  foreach(var note in RhythmChart.For(talk).Notes)talk.Inputs.Add(new RhythmInput{Direction=note.Direction,TimeSeconds=note.TimeSeconds});
  for(int guard=0;talk.Turns.Count==0&&guard<100;guard++)s.Tick(.1);Assert(talk.Turns.Count==1&&!string.IsNullOrWhiteSpace(talk.Turns[0].Line),"NPC responds with dialogue");
  while(talk.Status=="Active"){var turn=talk.Turns[talk.Turns.Count-1];Assert(s.Execute("a",new GameCommand{Id="test_"+(++sequence),Kind="Reply",Amount=Array.IndexOf(turn.Grades,ConversationLines.Smooth),Direction=talk.Turns.Count-1}).Accepted,"a smooth reply");}
  Assert(p.LastRhythmScore>.99&&listener.Observers[0].Suspicion<40,"conversation improves local trust");
  p.Inventory.Add(new ItemStack{ItemId="confetti",Count=1});Assert(s.Execute("a",C("Use","confetti")).Accepted,"confetti works");
  Assert(p.Inventory.Find(x=>x.ItemId=="confetti")==null&&listener.DistractedUntil>s.State.SimulationSeconds,"item consumed and wook distracted");s.Tick(.1);Assert(listener.Mode=="Distracted","wook visibly changes mode");
  s=Game();p=s.Player("a");p.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});var npc=s.State.Npcs[0];npc.Role="Buyer";npc.X=p.X;npc.Z=p.Z+1;npc.Yaw=180;var distant=s.State.Npcs[10];distant.X=35;distant.Z=35;var cop=s.State.Npcs.Find(n=>n.Kind=="Cop");cop.X=p.X;cop.Z=p.Z+2;cop.Yaw=180;Assert(s.Execute("a",C("StartSale","stock_lsd",npc.Id)).Accepted,"sale reserve");s.Tick(4.1);Assert(p.Life=="Detained"&&p.Inventory.Count==0&&s.State.GrossSales==0&&p.Cash==10,"bust precedes settlement");
  s=Game();p=s.Player("a");p.X=-28;p.Z=16;var quest=C("LostProperty");s.Execute("a",quest);s.Tick(5.1);s.Execute("a",quest);Assert(p.Cash==25&&s.State.GrossSales==0,"income distinct from sales");
  s=Game();p=s.Player("a");p.Inventory.Add(new ItemStack{ItemId="stock_lsd",Count=1});var consume=C("Consume","stock_lsd");Assert(s.Execute("a",consume).Accepted,"snapshot setup");s.Tick(1.25);
  var json=JsonSerializer.Serialize(s.State,new JsonSerializerOptions{IncludeFields=true});var saved=JsonSerializer.Deserialize<RoundState>(json,new JsonSerializerOptions{IncludeFields=true});
  var restored=new FestivalSimulation();restored.Restore(saved);double before=restored.State.ElapsedSeconds;int effectCount=restored.Player("a").Effects.Count;restored.Execute("a",consume);restored.Tick(.5);
  Assert(restored.Player("a").Effects.Count==effectCount&&restored.State.ElapsedSeconds>before&&restored.State.Commands.Exists(c=>c.Id==consume.Id),"snapshot continuity and dedupe");
 }
}
