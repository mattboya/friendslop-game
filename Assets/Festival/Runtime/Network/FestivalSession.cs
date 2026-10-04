using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Festival.Core;
using Festival.Integrations;
using Festival.Presentation;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

namespace Festival.Network
{
    [Serializable] internal sealed class Hello { public string Name="Friend", Token=""; public int Protocol=FestivalSession.ProtocolVersion; }
    [Serializable] internal sealed class Welcome { public string PlayerId="", Token=""; }
    [Serializable] internal sealed class DialogueHistoryPacket { public List<string> Seen=new List<string>(); }
    [Serializable] internal sealed class MoveIntent { public float X,Z,Yaw; public bool Sprint,SpinPoi; public int Sequence; }
    [Serializable] internal sealed class SnapshotPacket { public RoundState State; public double ServerSeconds; }
    internal sealed class PeerInput { public MoveIntent Intent=new MoveIntent(); public double ReceivedAt; public int Commands; public double Window; }

    /// <summary>Native Unity Transport session. Host owns all rules; clients submit input and render filtered state.</summary>
    public sealed class FestivalSession : MonoBehaviour
    {
        // The wire contract a client's hello names. Bump it whenever snapshots or commands change meaning, so a mismatched build
        // is turned away with "Incompatible game version." rather than joining a game it cannot play.
        // 2: festival weekends (the Spinning phase, the weekend's new commands, and a ReviewVote that names a friend).
        // 3: wave 2026-09-30 (2): clients build DANCE-5's busier rhythm charts from the same ChartSeed, and the spin runs longer.
        // 4: wave 2026-10-01 (2): snapshots carry when each Ferris wheel rider boarded (POLO-5), and clients paint and land the
        // 30/30/30/10 dose wheel (TRIP-12). ECON-2: shelves sell gear only, and festivalgoers sell stock through StartBuy.
        // POLO-4: a chat with the VIP guard that checks no vision carries a Doorman line and no questions or answers.
        // DIALOG-5: a chat check, and the VIP rope chat, lasts 8 s instead of 5 (FestivalSimulation.ConfirmChatSeconds).
        // ESC-2: a security stop ignored for 20 s becomes an arrest warning, and G ditches stock first during one; drops carry
        // the cop who claimed ditched stock (DropState.ClaimedBy), and the host's evidence its start (EvidenceState.StartSeconds).
        // MUSIC-2: CampMusicTrack picks SILENCE or one of five recorded tracks (track 2 is DISCO HOUSE, no longer MOONLIT DISCO).
        // DIALOG-3: sales, buys, chats and security talks are conversations, not charts: a Reply command (Amount the reply,
        // Direction the turn), and talks carry their Turns, whether a TrustDance came first and a chat's or a buy's Hunch. A narc
        // asked to sell talks instead of busting at the ask; finishing the buy busts.
        // POLO-6: a player's own copy in their snapshot names the influencers filming them (PlayerState.VisualFilmedBy).
        // CROWD-3: every client sees a festivalgoer scattering from an arrest (Mode "Scattering"), never whom it suspects.
        // MINI-1: snapshots carry the level's minigame booth (RoundState.Booth), a game under way is the "Minigame" phase, and
        // the players in it send its own Minigame command. MINI-4: the booth carries a Mystery Cups game (the cups, the turns, the
        // pot, and the tripper's hint on the tripper's own), and the Minigame command's ItemId drinks a cup (Amount) or calls it.
        // LIGHT-3: each client works the day sky's shapes out of its own spin seed, now from 12 shapes, not 8 (CloudShapes.Shapes),
        // so only builds on the same version show the same shape at the same moment.
        // 5: wave 2026-10-03: VISION-6: the tripper's snapshot marks the visions a creature hints at (VisionState.Hinted). Only
        // true ones are hinted, so the mark tells a truth before it is checked. TRIP-8: snapshots name who found the cloud's stash
        // (RoundState.CloudStashFinderId), for their notice. POI-1: a move intent says whether its player holds the poi spin
        // (MoveIntent.SpinPoi), and snapshots show who is spinning (PlayerState.VisualPoiSpin).
        // 6: wave 2026-10-04, DIALOG-9: Sale, Buy and Conversation reply turns and ConfirmChat use finite-zero untimed
        // durations. EndChat names an owned active interaction: it completes a valid check or cancels a reply talk with the
        // existing refund and witness cost. Police replies and trust/rhythm charts retain their deadlines. Version 5 peers
        // cannot interpret these durations or commands and are rejected. Later wave units append their actual version-6 changes.
        // CAMP-3: CampEgg targets one of eight stable camp IDs. RoundState.CampEggs publishes host-picked points, activation
        // sequences/times, chair ownership, deflation and dust words. Player last-egg events are public; FoundCampEggMask is
        // cumulative session discovery and copied only to its owner. All new fields default safely in older schema-2 rounds.
        // ECON-6: camp offers append two starter kinds with eight copies each and zero market supply.
        // PlayerState.StarterStockBought persists one paid combined allowance per fresh camp and is owner-only in views.
        // DANCE-10 chart/judge: Night 1 onward selects one- or two-beat holds with covered rests from ChartSeed. RhythmNote
        // carries HoldSeconds; Day 1 and explicit QuarterNotes keep their tap shapes. Hold heads remain one scored note.
        public const int ProtocolVersion=6;
        public RoundState State {get;private set;}
        public string LocalPlayerId {get;private set;}="";
        public PlayerState LocalPlayer => State?.Players.Find(p=>p.Id==LocalPlayerId);
        public FestivalCharacter LocalWorldCharacter => WorldCharacter(LocalPlayerId);
        public FestivalCharacter WorldCharacter(string playerId) => actors.TryGetValue(playerId,out var actor)?actor.GetComponent<FestivalCharacter>():null;
        public bool IsHost => manager!=null && manager.IsHost;
        public bool Connected => manager!=null && manager.IsConnectedClient;
        public bool Connecting {get;private set;}
        /// <summary>The title's line before any game, and again after leaving one (MENU-2).</summary>
        public const string OpeningMessage="Gather your friends. Make it back to camp.";
        public string Message {get;private set;}=OpeningMessage;
        public FestivalInput Controls {get;private set;}
        public LocalProfile Profile {get;private set;}
        public IVoiceSession Voice {get;}=new UnavailableVoiceSession();
        public bool MenuOpen=true;
        /// <summary>The pointer is free, and the view holds still, for the menu and for clicking a friend at the debrief (HUD-3).</summary>
        public bool PointerFree=>MenuOpen||State!=null&&FestivalHudText.VoteOpen(State);
        public Camera ViewCamera {get;private set;}
        /// <summary>CAM-1: where your eyes are, eased after your body as the first-person view always was. Over the shoulder the
        /// camera hangs from it; whatever aims with the view (shelves, visions) aims from here along the camera's forward, so both
        /// views pick the same thing.</summary>
        public Vector3 EyePosition {get;private set;}
        /// <summary>CAM-1: whether the view is over your shoulder right now: your setting, except lying down, in a camp visit or on the
        /// Ferris wheel (FestivalThirdPerson.Applies). Your own body is on screen then.</summary>
        public bool ThirdPersonView {get;private set;}
        public double EstimatedSimulationSeconds => State==null?0:State.SimulationSeconds
            +(State.Phase=="Results"||manager==null?0:Math.Max(0,Math.Min(.4,manager.ServerTime.Time-serverClockAtSnapshot)));
        public double LastSnapshotAge => Time.realtimeSinceStartupAsDouble-snapshotReceivedAt;
        public float RoundTripMs => manager!=null && Connected ? manager.GetComponent<UnityTransport>().GetCurrentRtt(NetworkManager.ServerClientId) : 0;
        private NetworkManager manager;
        private FestivalSimulation simulation;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // The native smoke harness may place actors between stages. Release
        // players never expose the authoritative simulation to presentation.
        internal FestivalSimulation DevelopmentSimulation => simulation;
#endif
        private readonly Dictionary<ulong,string> peers=new Dictionary<ulong,string>();
        private readonly Dictionary<string,string> tokens=new Dictionary<string,string>();
        private readonly Dictionary<ulong,Hello> pending=new Dictionary<ulong,Hello>();
        private readonly Dictionary<string,PeerInput> inputs=new Dictionary<string,PeerInput>();
        private readonly Dictionary<string,Transform> actors=new Dictionary<string,Transform>();
        private readonly Dictionary<string,int> displayedDanceSteps=new Dictionary<string,int>();
        private readonly List<Material> actorMaterials=new List<Material>();
        private readonly Dictionary<string,TextMesh> names=new Dictionary<string,TextMesh>();
        private readonly Dictionary<string,Transform> nameBubbles=new Dictionary<string,Transform>();
        private float yaw,pitch,preVisitYaw,preVisitPitch;
        // TRIP-5: where the camera looks, trailing yaw and pitch (the mouse) by the trip's lag; the same as them without one.
        private float viewYaw,viewPitch;
        private string lastCampVisit="";
        // TRIP-4: lying on the grass the view drops to LyingEyeHeight and looks up at the sky; getting up looks where it did before.
        private const float LyingEyeHeight=.3f,LyingPitch=-80;
        private bool wasLying;private float preLiePitch;
        private double accumulator,nextSnapshot,nextInput,snapshotReceivedAt,serverClockAtSnapshot,connectAt;
        private int movementSequence;
        private string token="", endpoint="", loadedRound="", acknowledgedDialogue="", profileId="default";
        private static NavMeshPath navigationPath;
        private Transform actorRoot;
        private FestivalWorld world;
        private FestivalHands firstPersonHands;
        private bool closing;
        private void Awake()
        {
            Application.runInBackground=true;
            Application.targetFrameRate=60;
            var args=Environment.GetCommandLineArgs();
            profileId=Argument(args,"--profile","default");
            Profile=new LocalProfile(profileId);Controls=new FestivalInput(profileId);
            gameObject.AddComponent<FestivalRhythmAudio>();
            gameObject.AddComponent<FestivalCampAudio>();
            gameObject.AddComponent<FestivalStageAudio>();
            var cameraObject=new GameObject("First-person camera");ViewCamera=cameraObject.AddComponent<Camera>();cameraObject.AddComponent<AudioListener>();
            ViewCamera.nearClipPlane=.05f;ViewCamera.farClipPlane=130;ViewCamera.fieldOfView=75;
            FestivalCharacter.ViewTransform=ViewCamera.transform;
            ViewCamera.cullingMask &= ~(1<<31); // Hide the local world body from its first-person camera.
            ViewCamera.clearFlags=CameraClearFlags.Skybox;ViewCamera.backgroundColor=new Color(.13f,.10f,.22f);
            ViewCamera.transform.position=EyePosition=new Vector3(0,1.65f,-29);
            // The world owns the single dusk/lighting Volume. The camera only
            // opts into it, avoiding a second, lower-priority bloom override.
            ViewCamera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var graphics=cameraObject.AddComponent<DevelopmentGraphicsDiagnostics>();graphics.Session=this;
            DevelopmentDiagnostics.GraphicsEvent("Rendering","camera_ready","post_processing=true world_volume_priority=20");
#endif
            actorRoot=new GameObject("Authoritative actor presentation").transform;
            world=FindFirstObjectByType<FestivalWorld>();if(world!=null)world.Build();
            gameObject.AddComponent<FestivalVisionMarkers>();
            gameObject.AddComponent<FestivalCreatures>();
            gameObject.AddComponent<FestivalTrip>();
            gameObject.AddComponent<FestivalSpinner>();
            gameObject.AddComponent<FestivalMinigameView>();
        }
        // NGO registers its message types after scene Awake and before Start.
        private void Start()
        {
            var args=Environment.GetCommandLineArgs();
            var portText=Argument(args,"--port","7777");ushort.TryParse(portText,out var port);if(port==0)port=7777;
            string host=Argument(args,"--join","");
            if(Array.IndexOf(args,"--host")>=0)Host(Argument(args,"--name","Host"),port);
            else if(host!="")Join(Argument(args,"--name","Friend"),host,port);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(Array.IndexOf(args,"--smoke-test")>=0||Array.IndexOf(args,"--solo-smoke-test")>=0||Array.IndexOf(args,"--ui-screens")>=0)gameObject.AddComponent<DevelopmentSmoke>();
            if(Array.IndexOf(args,"--art-review")>=0||Array.IndexOf(args,"--motion-review")>=0)gameObject.AddComponent<ArtReviewCapture>();
#endif
        }
        static string Argument(string[] args,string key,string fallback){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        private void Configure(ushort port,string address,bool host)
        {
            if(manager!=null)throw new InvalidOperationException("Leave the existing session first.");
            var go=new GameObject("Festival native session");
            var transport=go.AddComponent<UnityTransport>();
            transport.SetConnectionData(address,port,host?"0.0.0.0":null);
            manager=go.AddComponent<NetworkManager>();
            manager.NetworkConfig=new NetworkConfig {NetworkTransport=transport,ConnectionApproval=true,EnableSceneManagement=false,ForceSamePrefabs=false,TickRate=30};
            manager.ConnectionApprovalCallback=Approve;
            manager.OnClientConnectedCallback+=PeerConnected;
            manager.OnClientDisconnectCallback+=PeerDisconnected;
            manager.OnTransportFailure+=TransportFailed;
            connectAt=Time.realtimeSinceStartupAsDouble;Connecting=true;closing=false;
        }
        public void Host(string name,ushort port)
        {
            try
            {
                token="";endpoint="127.0.0.1:"+port;peers.Clear();tokens.Clear();inputs.Clear();pending.Clear();
                simulation=new FestivalSimulation(Environment.TickCount & int.MaxValue);Profile.ApplyUnlocks(simulation.State);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                simulation.ChatDiagnostic=DevelopmentDiagnostics.Transition;
                simulation.CampEggDiagnostic=DevelopmentDiagnostics.Transition;
#endif
                simulation.HasLineOfSight=LineOfSight;
                simulation.Navigate=Navigate;
                // TRIP-7: each level's hidden deal is mixed with a fresh secret no view carries. A Guid, never System.Random, which
                // seeds from the clock as the public Seed does.
                simulation.DealSecret=()=>BitConverter.ToInt32(Guid.NewGuid().ToByteArray(),0);
                Configure(port,"127.0.0.1",true);
                manager.NetworkConfig.ConnectionData=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Hello{Name=CleanName(name)}));
                if(!manager.StartHost())throw new InvalidOperationException("Could not listen on that port.");
                RegisterMessages();Message="Lobby open on port "+port+". Friends can join by LAN address.";
                State=ViewFor(simulation,LocalPlayerId);MenuOpen=false;
            }
            catch(Exception e){Fail("Host failed: "+e.Message);}
        }
        public void Join(string name,string address,ushort port)
        {
            if(string.IsNullOrWhiteSpace(address)||address.Length>128){Message="Enter the host's address.";return;}
            try
            {
                var nextEndpoint=address.Trim()+":"+port;if(endpoint!=nextEndpoint)token="";endpoint=nextEndpoint;
                Configure(port,address.Trim(),false);
                manager.NetworkConfig.ConnectionData=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Hello{Name=CleanName(name),Token=token}));
                if(!manager.StartClient())throw new InvalidOperationException("Could not start connection.");
                RegisterMessages();Message="Connecting…";
            }
            catch(Exception e){Fail("Join failed: "+e.Message);}
        }
        static string CleanName(string value)
        {
            value=(value??"Friend").Trim().Replace("<","").Replace(">","").Replace("\n","").Replace("\r","");
            return value.Length==0?"Friend":value.Substring(0,Math.Min(24,value.Length));
        }
        // A joining client's hello, or null when its build speaks another protocol or its token is malformed.
        static Hello ReadHello(string json){var hello=JsonUtility.FromJson<Hello>(json);return hello==null||hello.Protocol!=ProtocolVersion||hello.Token==null||hello.Token.Length>64?null:hello;}
        /// <summary>Whether the host lets in a client whose connection payload is this hello JSON.</summary>
        public static bool AcceptsHello(string json)=>ReadHello(json)!=null;
        private void Approve(NetworkManager.ConnectionApprovalRequest request,NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject=false;response.Pending=false;
            try
            {
                if(request.Payload==null||request.Payload.Length>1024)throw new ArgumentException("Invalid connection request.");
                var hello=ReadHello(Encoding.UTF8.GetString(request.Payload));
                if(hello==null)throw new ArgumentException("Incompatible game version.");
                bool reconnect=hello.Token!=""&&tokens.TryGetValue(hello.Token,out var unused);
                if(reconnect && peers.ContainsValue(tokens[hello.Token]))throw new ArgumentException("This session identity is already connected.");
                if(!reconnect && simulation.State.Phase!="Shopping" && simulation.State.Phase!="Lobby")throw new ArgumentException("Round in progress. Join the next round.");
                if(!reconnect && simulation.State.Players.Count+pending.Count>=8)throw new ArgumentException("Festival full: eight players including host.");
                pending[request.ClientNetworkId]=hello;response.Approved=true;
            }
            catch(Exception e){response.Approved=false;response.Reason=e is ArgumentException?e.Message:"Invalid connection request.";}
        }
        private void PeerConnected(ulong clientId)
        {
            if(manager.IsServer)
            {
                if(!pending.TryGetValue(clientId,out var hello))hello=new Hello();pending.Remove(clientId);
                string id;
                if(hello.Token!=""&&tokens.TryGetValue(hello.Token,out var oldId))id=oldId;
                else {id=Guid.NewGuid().ToString("N");hello.Token=Guid.NewGuid().ToString("N");tokens[hello.Token]=id;}
                var p=simulation.AddPlayer(id,CleanName(hello.Name));peers[clientId]=id;inputs[id]=new PeerInput();
                if(clientId==manager.LocalClientId)
                {
                    LocalPlayerId=id;token=hello.Token;p.Dialogue.Seen=new List<string>(Profile.Data.SeenDialogue);
                }
                else SendJson("festival.welcome",clientId,new Welcome{PlayerId=id,Token=hello.Token});
            }
            if(clientId==manager.LocalClientId)
            {
                Connecting=false;MenuOpen=false;Message="Connected. Shop, then ready up.";Voice.Join(endpoint);
                if(!manager.IsServer)SendJson("festival.history",NetworkManager.ServerClientId,new DialogueHistoryPacket{Seen=new List<string>(Profile.Data.SeenDialogue)});
            }
        }
        private void PeerDisconnected(ulong clientId)
        {
            pending.Remove(clientId);
            if(manager!=null && manager.IsServer && peers.TryGetValue(clientId,out var id)){simulation.Disconnect(id);peers.Remove(clientId);inputs.Remove(id);}
            if(manager!=null && clientId==manager.LocalClientId && !closing)
            {
                string reason=manager.DisconnectReason;
                Fail(string.IsNullOrWhiteSpace(reason)?"Host connection lost. This prototype cannot migrate the host; reconnect or start a new session.":reason);
            }
        }
        private void RegisterMessages()
        {
            var messages=manager.CustomMessagingManager;
            messages.RegisterNamedMessageHandler("festival.welcome",(sender,reader)=>{
                if(sender!=NetworkManager.ServerClientId||IsHost)return;
                var w=ReadJson<Welcome>(reader,1024);if(w==null)return;LocalPlayerId=w.PlayerId;token=w.Token;
            });
            messages.RegisterNamedMessageHandler("festival.snapshot",(sender,reader)=>{
                if(sender!=NetworkManager.ServerClientId||IsHost)return;
                var packet=ReadJson<SnapshotPacket>(reader,512*1024);if(packet?.State==null)return;
                if(State!=null && State.RoundId==packet.State.RoundId && packet.State.Tick<State.Tick)return;
                State=packet.State;snapshotReceivedAt=Time.realtimeSinceStartupAsDouble;serverClockAtSnapshot=packet.ServerSeconds;
            });
            messages.RegisterNamedMessageHandler("festival.command",(sender,reader)=>{
                if(!IsHost||!peers.TryGetValue(sender,out var id)||!Admit(id,60))return;
                var command=ReadJson<GameCommand>(reader,2048);if(command==null)return;
                var result=simulation.Execute(id,command);SendJson("festival.result",sender,result);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(command.Kind!="Rhythm")DevelopmentDiagnostics.Transition(command.Kind,result.Reason,simulation.State.RoundId,simulation.State.SimulationSeconds);
#endif
            });
            messages.RegisterNamedMessageHandler("festival.move",(sender,reader)=>{
                if(!IsHost||!peers.TryGetValue(sender,out var id)||!Admit(id,90))return;
                var input=ReadJson<MoveIntent>(reader,512);if(input==null)return;AcceptInput(id,input);
            });
            messages.RegisterNamedMessageHandler("festival.result",(sender,reader)=>{
                if(sender!=NetworkManager.ServerClientId||IsHost)return;
                var result=ReadJson<CommandResult>(reader,2048);if(result!=null)Message=result.Accepted?(result.Reason=="Accepted"?"":result.Reason):result.Reason;
            });
            messages.RegisterNamedMessageHandler("festival.history",(sender,reader)=>{
                if(!IsHost||!peers.TryGetValue(sender,out var id))return;
                var packet=ReadJson<DialogueHistoryPacket>(reader,65536);var player=simulation.Player(id);
                if(packet?.Seen==null||player==null||packet.Seen.Count>512)return;
                var safe=new List<string>();
                foreach(var line in packet.Seen)if(!string.IsNullOrEmpty(line)&&line.Length<=64&&!safe.Contains(line))safe.Add(line);
                player.Dialogue.Seen=safe;
            });
        }
        bool Admit(string id,int limit)
        {
            if(!inputs.TryGetValue(id,out var peer))return false;double now=Time.realtimeSinceStartupAsDouble;
            if(now-peer.Window>=1){peer.Window=now;peer.Commands=0;}return ++peer.Commands<=limit;
        }
        private static T ReadJson<T>(FastBufferReader reader,int maxBytes) where T:class
        {
            if(reader.Length<4||reader.Length>maxBytes)return null;
            try{reader.ReadValueSafe(out string json);return JsonUtility.FromJson<T>(json);}catch(Exception e)when(e is ArgumentException||e is OverflowException||e is InvalidCastException){return null;}
        }
        private void SendJson(string name,ulong recipient,object value)
        {
            if(manager?.CustomMessagingManager==null)return;
            string json=JsonUtility.ToJson(value);int size=Encoding.Unicode.GetByteCount(json)+16;
            if(size>512*1024){Message="Session update exceeds prototype capacity.";return;}
            using(var writer=new FastBufferWriter(size,Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                manager.CustomMessagingManager.SendNamedMessage(name,recipient,writer,NetworkDelivery.ReliableFragmentedSequenced);
            }
        }
        public void Command(string kind,string target="",string item="",int amount=0,int direction=0,double time=0)
        {
            if(!Connected||LocalPlayer==null)return;
            var command=new GameCommand{Id=Guid.NewGuid().ToString("N"),Kind=kind,TargetId=target??"",ItemId=item??"",Amount=amount,Direction=direction,TimeSeconds=time};
            if(IsHost)
            {
                var result=simulation.Execute(LocalPlayerId,command);Message=result.Accepted?(result.Reason=="Accepted"?"":result.Reason):result.Reason;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(kind!="Rhythm")DevelopmentDiagnostics.Transition(kind,result.Reason,simulation.State.RoundId,simulation.State.SimulationSeconds);
#endif
            }
            else SendJson("festival.command",NetworkManager.ServerClientId,command);
        }
        public string PreviewItem(string itemId)
        {
            var definition=Catalog.FindItem(itemId);
            if(definition==null)return "Preview unavailable.";
            if(itemId=="little_spoon")return definition.Name+" — Preview, not owned\n"+definition.Description+"\nWorn as a necklace. No active use.";
            try
            {
                // This authority has its own wallet, inventory, NPCs and command history.
                // It has no path to the live round, profile or transport.
                var preview=new FestivalSimulation(1729);
                var player=preview.AddPlayer("preview","Preview");
                var friend=preview.AddPlayer("preview_friend","Preview Friend");
                player.Cash=999;
                // ECON-2: no shelf sells stock (festivalgoers do), so the preview player is simply handed it.
                if(definition.Category=="Stock")player.Inventory.Add(new ItemStack{ItemId=itemId,Count=1});
                else
                {
                    if(!preview.State.VendorOffers.Contains(itemId))
                    {
                        preview.State.VendorOffers.Add(itemId);
                        preview.State.ShopStock.Add(new ShopStockState{ItemId=itemId,CampAvailable=1,MarketAvailable=1});
                    }
                    var shelf=Catalog.ShopPoint(true,preview.State.VendorOffers.IndexOf(itemId));
                    player.X=shelf.X;player.Z=shelf.Z;
                    preview.Execute(player.Id,new GameCommand{Id="preview_hold",Kind="HoldOffer",ItemId=itemId});
                    player.X=0;player.Z=7;
                    var buy=preview.Execute(player.Id,new GameCommand{Id="preview_buy",Kind="Buy",ItemId=itemId,Amount=1});
                    if(definition.Price>0 && !buy.Accepted)return definition.Description+"\nPreview rule: "+buy.Reason;
                }
                player.X=0;player.Z=19;friend.X=0;friend.Z=19;
                preview.Execute(player.Id,new GameCommand{Id="preview_ready",Kind="Ready"});
                preview.Execute(friend.Id,new GameCommand{Id="preview_friend_ready",Kind="Ready"});
                preview.Execute(player.Id,new GameCommand{Id="preview_start",Kind="Start"});
                preview.Tick(FestivalSimulation.SpinSeconds+.1);
                preview.Execute(player.Id,new GameCommand{Id="preview_loaded",Kind="MapReady"});
                preview.Execute(friend.Id,new GameCommand{Id="preview_friend_loaded",Kind="MapReady"});
                if(itemId=="stage_pass"){player.X=Catalog.StageTakeoverX;player.Z=Catalog.StageTakeoverZ;}
                if(itemId=="medical_voucher")
                {
                    player.X=24;player.Z=-20;
                    player.Effects.Add(new ActiveEffect{Id="lsd",InstanceId="preview_effect",RemainingSeconds=60});
                }
                var use=preview.Execute(player.Id,new GameCommand{Id="preview_use",Kind=!string.IsNullOrEmpty(definition.EffectId)?"Consume":"Use",ItemId=itemId});
                var interaction=preview.Interaction(player.InteractionId);
                string outcome=use.Accepted
                    ? "Authoritative preview result: "+(interaction!=null?interaction.Kind+" started":player.Effects.Count>0?player.Effects[player.Effects.Count-1].Id+" effect started":preview.State.Stashes.Count>1?"shared stash placed":use.Reason)+"."
                    : "Authoritative preview limit: "+use.Reason+".";
                if(!string.IsNullOrEmpty(definition.EffectId))outcome+=" "+Catalog.FindEffect(definition.EffectId).PresentationContract;
                return definition.Name+" — Preview, not owned\n"+definition.Description+"\n"+outcome+"\nLimit: "+definition.CancellationRule;
            }
            catch(Exception error){return definition.Description+"\nPreview could not initialize: "+error.Message;}
        }
        private void AcceptInput(string id,MoveIntent value)
        {
            if(!inputs.TryGetValue(id,out var peer)||value.Sequence<=peer.Intent.Sequence||!Finite(value.X)||!Finite(value.Z)||!Finite(value.Yaw))return;
            value.X=Mathf.Clamp(value.X,-1,1);value.Z=Mathf.Clamp(value.Z,-1,1);value.Yaw=Mathf.Repeat(value.Yaw,360);
            peer.Intent=value;peer.ReceivedAt=Time.realtimeSinceStartupAsDouble;
        }
        static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        private void Update()
        {
            if(Controls==null)return;
            if(Connecting && Time.realtimeSinceStartupAsDouble-connectAt>15){Fail("Connection timed out. Check the host address and port.");return;}
            if(!Connected)return;
            if(!IsHost && LastSnapshotAge>5 && State!=null){Message="Host updates stalled. Inputs paused; leave or wait for recovery.";if(LastSnapshotAge>20){Fail("Host stopped responding. No host migration is available in this prototype.");return;}}
            if(IsHost)
            {
                accumulator+=Math.Min(Time.unscaledDeltaTime,.2f);int steps=0;
                while(accumulator>=1.0/30 && steps++<6){ApplyMovement(1.0/30);simulation.Tick(1.0/30);accumulator-=1.0/30;}
                State=ViewFor(simulation,LocalPlayerId);snapshotReceivedAt=Time.realtimeSinceStartupAsDouble;serverClockAtSnapshot=manager.ServerTime.Time;
                Profile.RememberUnlocks(simulation.State);
                if(Time.realtimeSinceStartupAsDouble>=nextSnapshot){Broadcast();nextSnapshot=Time.realtimeSinceStartupAsDouble+1.0/10;}
            }
            var player=LocalPlayer;if(player==null)return;
            if(world!=null){world.SetPhase(State.Phase);world.SetLighting(State,LocalPlayerId);world.SetTwists(State);world.SetInterior(player.CampVisitId);world.SetCampAntics(player.CampAntics);world.UpdateShop(State,player);}
            if(State.Phase=="Loading" && loadedRound!=State.RoundId)
            {
                if(world!=null&&world.IsReady&&world.NavigationReady){loadedRound=State.RoundId;Command("MapReady");}
                else Message="Waiting for festival navigation to finish…";
            }
            // HUD-4: never while a key is being rebound, so the menu does not close on a player in the middle of it.
            if(MenuOpen&&!Controls.Rebinding&&ClosesMenu(State.Phase,lastPhase))MenuOpen=false;
            lastPhase=State.Phase;
            if(Controls.Menu.WasPressedThisFrame()&&!Controls.Rebinding)MenuOpen=!MenuOpen;
            // CAM-1: V flips your view between first person and over the shoulder, and remembers it; never in the menu or mid-rebind.
            if(Controls.View.WasPressedThisFrame()&&!MenuOpen&&!Controls.Rebinding){Profile.Data.ThirdPerson=!Profile.Data.ThirdPerson;string error=Profile.Save();if(error!="")Message=error;}
            Cursor.lockState=PointerFree?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=PointerFree;
            // POI-1: left click held, with the pointer locked (never a click on the menu or the vote). No focus check: the Input
            // System lets go of the mouse when the window loses focus, and batchmode tests run unfocused. One reading per frame feeds
            // the intent, your hands and your own body, so your own spin shows with no round trip.
            spinHeld=Controls.Spin.IsPressed()&&!PointerFree;
            if(!PointerFree && Application.isFocused)
            {
                var look=Controls.Look.ReadValue<Vector2>();yaw+=look.x*Profile.Data.MouseSensitivity;pitch=Mathf.Clamp(pitch-look.y*Profile.Data.MouseSensitivity,-80,80);
            }
            if(Time.realtimeSinceStartupAsDouble>=nextInput && (IsHost||LastSnapshotAge<5))
            {
                var active=State.Interactions.Find(i=>i.Id==player.InteractionId&&i.Status=="Active");
                var move=MenuOpen||!Application.isFocused||FestivalInput.Roots(active?.Kind)?Vector2.zero:Controls.Move.ReadValue<Vector2>();
                var intent=new MoveIntent{X=move.x,Z=move.y,Yaw=yaw,Sprint=Controls.Sprint.IsPressed(),SpinPoi=spinHeld,Sequence=++movementSequence};
                if(IsHost)AcceptInput(LocalPlayerId,intent);else SendJson("festival.move",NetworkManager.ServerClientId,intent);
                nextInput=Time.realtimeSinceStartupAsDouble+.05;
            }
            if(player.HasCosmetic)Profile.AwardFirstRescue();
            UpdateActors();UpdateCamera(player);
        }
        private string lastPhase="";
        private bool spinHeld;
        /// <summary>Whether a menu left open closes itself as the round moves from lastPhase into phase: as the wheels start (HUD-4:
        /// the spinner hides under the menu), the festival opens or the debrief begins.</summary>
        public static bool ClosesMenu(string phase,string lastPhase)=>phase!=lastPhase&&(phase=="Spinning"||phase=="Playing"||phase=="CampReview");
        public void AcknowledgeDisplayedDialogue(InteractionState interaction)
        {
            if(interaction==null||string.IsNullOrEmpty(interaction.DialogueId)||acknowledgedDialogue==interaction.Id+interaction.DialogueId)return;
            acknowledgedDialogue=interaction.Id+interaction.DialogueId;
            Command("DialogueAck",interaction.DialogueId);
            Profile.Data.SeenDialogue.RemoveAll(id=>id==interaction.DialogueId);
            Profile.Data.SeenDialogue.Add(interaction.DialogueId);
            while(Profile.Data.SeenDialogue.Count>512)Profile.Data.SeenDialogue.RemoveAt(0);
            Profile.Save();
        }
        private void ApplyMovement(double dt)
        {
            foreach(var pair in inputs)
            {
                var player=simulation.Player(pair.Key);if(player==null)continue;var input=pair.Value.Intent;
                bool fresh=Time.realtimeSinceStartupAsDouble-pair.Value.ReceivedAt<=.25;
                // POI-1: every step, before a stale input skips the move, so a silent client or a new interaction stops the spin.
                player.VisualPoiSpin=fresh&&input.SpinPoi&&FestivalSimulation.CanSpinPoi(player);
                if(!fresh)continue;
                var active=simulation.Interaction(player.InteractionId);
                float speed=(input.Sprint?6:4)*Intoxication.MovementMultiplier(player);if(player.InteractionId!="")speed=1;if(player.DragTargetId!="")speed=2;if(player.Life=="Downed")speed=.8f;
                speed=Mathf.Min(speed,(float)simulation.CarrySpeed(player));
                var local=active!=null&&active.Status=="Active"&&FestivalInput.Roots(active.Kind)?Vector3.zero:Vector3.ClampMagnitude(new Vector3(input.X,0,input.Z),1);
                if(local.sqrMagnitude>.0001f)local=Vector3.ClampMagnitude(local+new Vector3(Intoxication.LateralDrift(player,simulation.State.SimulationSeconds)*local.magnitude,0,0),1);
                var delta=Quaternion.Euler(0,input.Yaw,0)*local*(speed*(float)dt);
                var origin=player.CampVisitId!=""?new Vector3(player.CampInteriorX,0,player.CampInteriorZ):new Vector3(player.X,0,player.Z);
                var target=player.CampVisitId!=""||player.Life=="Spirit"?origin+delta:Slide(origin,delta);
                var previousCamp=player.CampVisitId;
                if(simulation.TryMove(player.Id,target.x,target.z,input.Yaw,dt)&&previousCamp!=""&&player.CampVisitId=="")
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    DevelopmentDiagnostics.Transition("ExitCampDoor",previousCamp,simulation.State.RoundId,simulation.State.SimulationSeconds);
#endif
                }
            }
        }
        private static Vector3 Slide(Vector3 origin,Vector3 delta)
        {
            if(delta.sqrMagnitude<.000001f)return origin;
            var bottom=origin+Vector3.up*.38f;var top=origin+Vector3.up*1.4f;
            if(Physics.CapsuleCast(bottom,top,.32f,delta.normalized,out var hit,delta.magnitude+.025f,~0,QueryTriggerInteraction.Ignore))
            {
                delta=Vector3.ProjectOnPlane(delta,hit.normal);delta.y=0;
                if(delta.sqrMagnitude<.000001f||Physics.CapsuleCast(bottom,top,.32f,delta.normalized,delta.magnitude+.025f,~0,QueryTriggerInteraction.Ignore))return origin;
            }
            return origin+delta;
        }
        // How the host sees and moves NPCs. Public so play tests drive a simulation exactly as a hosted game does.
        public static bool LineOfSight(float x,float z,float xx,float zz)=>!Physics.Linecast(new Vector3(x,1.2f,z),new Vector3(xx,1.2f,zz),~0,QueryTriggerInteraction.Ignore);
        public static WorldPoint Navigate(float x,float z,float tx,float tz,double maxDistance)
        {
            var from=new Vector3(x,0,z);var target=new Vector3(tx,0,tz);navigationPath??=new NavMeshPath();
            // ESC-1: head for the first corner that is not where the walker already stands. One that stops exactly on a corner,
            // or at the end of a partial path, would otherwise get a 0 m step every tick and never move again. With no such
            // corner it heads straight for the target.
            if(NavMesh.SamplePosition(from,out var a,2,NavMesh.AllAreas)&&NavMesh.SamplePosition(target,out var b,3,NavMesh.AllAreas)&&NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,navigationPath))
            {
                var corners=navigationPath.corners;
                for(int i=1;i<corners.Length;i++)if(new Vector2(corners[i].x-x,corners[i].z-z).sqrMagnitude>.0025f){target=corners[i];break;}
            }
            var delta=Vector3.ClampMagnitude(target-from,(float)maxDistance);delta.y=0;var next=Slide(from,delta);return new WorldPoint(next.x,next.z);
        }
        private void Broadcast()
        {
            foreach(var peer in peers)
            {
                if(peer.Key==manager.LocalClientId)continue;
                SendJson("festival.snapshot",peer.Key,new SnapshotPacket{State=ViewFor(simulation,peer.Value),ServerSeconds=manager.ServerTime.Time});
            }
        }
        // Static so EditMode tests can check the whitelist without a network session.
        public static RoundState ViewFor(FestivalSimulation simulation,string viewer)
        {
            // DTO whitelist: never ship command history, hidden evidence, other players' dialogue or credentials.
            var source=simulation.State;var local=simulation.Player(viewer);bool spirit=local?.Life=="Spirit";
            // TRIP-4: everyone sees the clue cloud; only the tripper, once they have read it, is shown what it pictures, and when.
            int landmark=FestivalSimulation.VisibleCloudLandmark(source,viewer);
            var view=new RoundState{SchemaVersion=source.SchemaVersion,Seed=source.Seed,RoundId=source.RoundId,Phase=source.Phase,Result=source.Result,HostPlayerId=source.HostPlayerId,
                SimulationSeconds=source.SimulationSeconds,ElapsedSeconds=source.ElapsedSeconds,DurationSeconds=source.DurationSeconds,LaunchAtSeconds=source.LaunchAtSeconds,Tick=source.Tick,TransactionSequence=source.TransactionSequence,GrossSales=source.GrossSales,LevelSales=source.LevelSales,StashCash=source.StashCash,CampMusicTrack=source.CampMusicTrack,
                CampEggs=source.CampEggs.ConvertAll(e=>JsonUtility.FromJson<CampEggState>(JsonUtility.ToJson(e))),
                ReviewResult=source.ReviewResult,ReviewSales=source.ReviewSales,ReviewFestivalIndex=source.ReviewFestivalIndex,ReviewSurvivors=source.ReviewSurvivors,ReviewAntics=source.ReviewAntics,ReviewVotes=FestivalSimulation.VisibleReviewVotes(source,viewer),ReviewAwards=source.ReviewAwards,ReviewWinners=source.ReviewWinners,
                FriendFound=!spirit&&source.FriendFound,FriendLeaderId=spirit?"":source.FriendLeaderId,FriendPosition=Spot(source.GateOpened,source.FriendFound,source.FriendPosition),
                // CROWD-2: a big crew's second lost friend, shown like the first. Its clue chain stays on the host.
                SecondFriend=new LostFriendState{Active=source.SecondFriend.Active,GateOpened=source.SecondFriend.GateOpened,CluesRead=source.SecondFriend.CluesRead,Found=!spirit&&source.SecondFriend.Found,LeaderId=spirit?"":source.SecondFriend.LeaderId,Position=Spot(source.SecondFriend.GateOpened,source.SecondFriend.Found,source.SecondFriend.Position)},
                // POLO-3: a Ferris wheel rider at night makes out only the square each lost friend is in, never their spot.
                WheelFriendAreas=FestivalSimulation.WheelFriendAreas(source,viewer),
                VendorOffers=source.VendorOffers,ShopStock=source.ShopStock,CluesRead=source.CluesRead,GateOpened=source.GateOpened,ObjectiveReward=source.ObjectiveReward,SurvivorBonus=source.SurvivorBonus,Survivors=source.Survivors,ConnectedCrewCount=source.Players.FindAll(p=>p.Connected).Count,
                FestivalIndex=source.FestivalIndex,LevelIndex=source.LevelIndex,EncoreTier=source.EncoreTier,UnlockedFestivalCount=source.UnlockedFestivalCount,
                TripperId=source.TripperId,SpinSeed=source.SpinSeed,SpinEndsAt=source.SpinEndsAt,Doses=source.Doses,Bodies=source.Bodies,
                CloudClueStart=source.CloudClueStart,CloudClueLandmark=landmark,CloudClueReadAt=landmark>=0?source.CloudClueReadAt:-1,
                // TRIP-8: who found the cloud's stash, for their notice; it has gone by then, so this gives no spot away.
                CloudStashFinderId=source.CloudStashFinderId,
                // MINI-1: the booth and its game are public, and spirits watch; the weekend's bag of games stays here. MINI-4: all but
                // which Mystery Cup is spiked, which stays here too, and the cup the tripper is shown as spiked, sent to them alone.
                Booth=FestivalSimulation.VisibleBooth(source,viewer)};
            foreach(var p in source.Players)
            {
                if(spirit && p.Life!="Spirit")continue;
                var copy=JsonUtility.FromJson<PlayerState>(JsonUtility.ToJson(p));copy.Dialogue=new DialogueHistory();copy.VisualPose=p.Ready&&source.Phase=="Shopping"?"Dance":p.DragTargetId!=""||p.CarryBodyId!=""?"Drag":source.Interactions.Find(i=>i.Id==p.InteractionId&&i.Status=="Active") switch{null=>p.Effects.Exists(e=>e.Id!=FestivalSimulation.GiggleGasEffect)?"Intoxicated":"Idle",var doing when FestivalSimulation.Dancing(doing)=>"Dance",var doing=>doing.Kind};
                if(FestivalSimulation.CampSeated(source,p.Id))copy.VisualPose=FestivalSimulation.CampChairPose;
                var pendingOffer=source.Transfers.Find(t=>t.FromId==p.Id);
                copy.VisualOfferItem=pendingOffer?.ItemId??"";copy.VisualOfferTarget=pendingOffer?.ToId??"";
                copy.WearingLittleSpoon=p.Inventory.Exists(item=>item.ItemId=="little_spoon"&&item.Count>0);
                copy.VisualWideEyes=p.Effects.Exists(effect=>effect.Id=="lsd"||effect.Id=="mushrooms"||effect.Id=="ecstasy"||effect.Id==FestivalSimulation.DoseEffect);
                copy.VisualRedEyes=p.Effects.Exists(effect=>effect.Id=="weed");
                // GAS-1: Giggle Gas is no Intoxicated pose (above); friends see a giggle instead, once it has kicked in.
                copy.VisualGiggling=FestivalSimulation.Giggling(p,source.SimulationSeconds);
                // POLO-5: when their turn on the Ferris wheel began, for every client to seat them in the same gondola; -1 off it.
                copy.VisualWheelSince=source.Interactions.Find(i=>i.PlayerId==p.Id&&i.Kind==FestivalSimulation.RideWheelKind&&i.Status=="Active")?.StartSeconds??-1;
                // POLO-6: the influencers filming this player, on the viewer's own copy alone: nobody learns who else is on camera.
                copy.VisualFilmedBy=p.Id==viewer?source.Npcs.FindAll(n=>simulation.Films(n,p)).ConvertAll(n=>n.Id):new List<string>();
                if(p.Id!=viewer){copy.Inventory.Clear();copy.Effects.Clear();copy.Wristbands.Clear();copy.Cash=0;copy.StarterStockBought=0;copy.NpcSpeech="";copy.NpcSpeaker="";copy.NpcSpeechUntil=0;copy.FoundCampEggMask=0;}view.Players.Add(copy);
            }
            if(!spirit)
            {
                foreach(var npc in source.Npcs)
                {
                    // CROWD-3: a festivalgoer scattering from an arrest does so in every client's view; whom it suspects stays masked.
                    bool scattering=npc.Mode==FestivalSimulation.Scattering,targetsViewer=!scattering&&npc.TargetId==viewer;
                    var copy=new NpcState{Id=npc.Id,Kind=npc.Kind,Mode=targetsViewer||scattering?npc.Mode:(npc.Kind=="Cop"?"Patrol":"Blending"),TargetId=targetsViewer?viewer:"",X=npc.X,Z=npc.Z,Yaw=npc.Yaw,IdlePose=npc.IdlePose,HighlyIntoxicated=npc.HighlyIntoxicated,RedEyes=npc.RedEyes,CanTalk=npc.CanTalk,Twist=npc.Twist,Busy=FestivalSimulation.Engaged(source,npc)};
                    var observer=npc.Observers.Find(o=>o.PlayerId==viewer);copy.Suspicion=observer?.Suspicion??0;view.Npcs.Add(copy);
                }
                view.Visions=FestivalSimulation.VisibleVisions(source,viewer);
                view.Drops=source.Drops;view.Stashes=source.Stashes;view.Transfers=source.Transfers.FindAll(t=>t.FromId==viewer||t.ToId==viewer);
                // GAS-2: the level's Giggle Tank, public to all but spirits.
                view.GiggleTankSpot=source.GiggleTankSpot;view.GiggleTankFound=source.GiggleTankFound;
            }
            view.Interactions=source.Interactions.FindAll(i=>i.PlayerId==viewer&&i.Status=="Active");return view;
            // A lost friend's spot reaches the living once they are found. Before that, only once their trail is finished, and then
            // only the tripper (the trail's last link, TRIP-2) or someone within local sight range: never the full map, not even from
            // the Ferris wheel (POLO-3, WheelFriendAreas above).
            WorldPoint Spot(bool trailDone,bool found,WorldPoint at)=>!spirit&&(found||trailDone&&local!=null&&(viewer==source.TripperId||Vector2.Distance(new Vector2(local.X,local.Z),new Vector2(at.X,at.Z))<12&&simulation.HasLineOfSight(local.X,local.Z,at.X,at.Z)))?at:new WorldPoint(0,0);
        }
        /// <summary>How a client poses an NPC it is shown: its Mode, or its idle pose while blending in (CROWD-3: "Scattering" runs).</summary>
        public static string NpcPose(NpcState n)=>n.Mode=="Blending"?n.IdlePose:n.Mode;
        private void UpdateActors()
        {
            var local=LocalPlayer;bool spirit=local.Life=="Spirit";var seen=new HashSet<string>();
            foreach(var p in State.Players)
            {
                if(!p.Connected||(spirit!=(p.Life=="Spirit")))continue;
                bool togetherInside=p.CampVisitId!=""&&p.CampVisitId==local.CampVisitId;
                if(!togetherInside&&(p.CampVisitId!=""||local.CampVisitId!=""))continue;
                // POLO-5: a Ferris wheel rider is drawn seated in their gondola, facing away from its bench (-z), and at
                // FestivalTwistVisuals.RiderScale so they fit under its roof, while the rules keep them at its base. They are aimed
                // ahead of the seat for Actor's glide (rate 15) so it keeps them there at any frame rate (POLO-7).
                bool wheel=p.VisualWheelSince>=0&&world!=null;
                var at=togetherInside?new Vector3(p.CampInteriorX,0,p.CampInteriorZ):wheel?world.WheelSeat(State,p,15):new Vector3(p.X,0,p.Z);
                // A debrief winner wears their awards over their head until the next level's results (HUD-3).
                Actor(p.Id,FestivalHudText.Nameplate(State,p),at.x,at.z,p.VisualPose=="Dj"?0:wheel?180:p.Yaw,
                    p.Life=="Downed"?new Color(.9f,.3f,.3f):PlayerColor(p.Id),p.Life=="Downed"?.4f:.9f,
                    p.Life=="Alive"?p.VisualPose:p.Life,"Attendee",0,p.WearingLittleSpoon,p.VisualWideEyes,p.VisualRedEyes,
                    string.IsNullOrEmpty(p.HeldOfferId)?p.EquippedItemId:p.HeldOfferId,y:at.y);
                var exchangeCharacter=actors[p.Id].GetComponent<FestivalCharacter>();
                if(exchangeCharacter!=null)
                {
                    // Before anything here can remake a held item, so the body's scale and its RideScale always agree.
                    exchangeCharacter.Ride(wheel?FestivalTwistVisuals.RiderScale:1);
                    var peer=string.IsNullOrEmpty(p.VisualOfferTarget)?State.Players.Find(other=>other.VisualOfferTarget==p.Id):State.Players.Find(other=>other.Id==p.VisualOfferTarget);
                    bool exchange=peer!=null&&peer.Connected&&p.Life=="Alive"&&peer.Life=="Alive"&&p.CampVisitId==peer.CampVisitId;
                    var contact=exchange?new Vector3(togetherInside?(p.CampInteriorX+peer.CampInteriorX)*.5f:(p.X+peer.X)*.5f,1.08f,togetherInside?(p.CampInteriorZ+peer.CampInteriorZ)*.5f:(p.Z+peer.Z)*.5f):Vector3.zero;
                    exchangeCharacter.SetExchange(exchange,p.VisualOfferItem,contact);
                    exchangeCharacter.SetReceipt(p.VisualReceiptSequence,p.VisualReceivedItem,(float)(State.SimulationSeconds-p.VisualReceiptAt));
                    exchangeCharacter.Giggling=p.VisualGiggling;
                    // POI-1: your own body spins from your click at once; everyone else's from the host's view.
                    exchangeCharacter.SpinningPoi=p.Id==LocalPlayerId?spinHeld&&FestivalSimulation.CanSpinPoi(p):p.VisualPoiSpin;
                }
                seen.Add(p.Id);
                if(p.Id==LocalPlayerId)
                {
                    if(actors[p.Id].gameObject.layer!=31)
                        foreach(var child in actors[p.Id].GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
                    names[p.Id].transform.parent.gameObject.SetActive(false);
                }
                displayedDanceSteps.TryGetValue(p.Id,out int previousStep);
                if(p.VisualPose=="Dance"&&p.VisualDanceStepSequence>previousStep&&actors[p.Id].TryGetComponent<FestivalCharacter>(out var dancer))dancer.PulseDanceStep(p.VisualDanceStepDirection);
                displayedDanceSteps[p.Id]=p.VisualDanceStepSequence;
            }
            // Festivalgoers, lost friends, drops and bodies while the festival shows: MINI-1's freeze-frame too, each where they stood.
            if(!spirit && FestivalWorld.ShowsFestival(State.Phase))
            {
                foreach(var n in State.Npcs){Actor(n.Id,n.Kind=="Cop"?"SECURITY":"Festivalgoer",n.X,n.Z,n.Yaw,n.Kind=="Cop"?new Color(.25f,.4f,.7f):new Color(.8f,.5f,.3f),.85f,NpcPose(n),n.Kind=="Cop"?"Security":"Attendee",n.Kind=="Cop"?0:(float)n.Suspicion/100f,false,n.HighlyIntoxicated,n.RedEyes,"",false,
                    filming:n.Twist==FestivalSimulation.Influencer,recording:local.VisualFilmedBy.Contains(n.Id));seen.Add(n.Id);}
                // A lost friend (a big crew's two, CROWD-2) shows once found, or once the view knows their spot and they are within 12 m.
                // The Ferris wheel draws nobody (POLO-3): its rider reads only a rough area on the objective card.
                void LostFriend(string id,WorldPoint at,bool found){if(at!=null && (at.X!=0||at.Z!=0) && (found||Vector2.Distance(new Vector2(local.X,local.Z),new Vector2(at.X,at.Z))<12)){Actor(id,"MISSING FRIEND",at.X,at.Z,0,Color.cyan,.9f,"Idle","Friend");seen.Add(id);}}
                LostFriend("mission_friend",State.FriendPosition,State.FriendFound);LostFriend("mission_friend_2",State.SecondFriend.Position,State.SecondFriend.Found);
                // A drop floats its display name ("Tongue Stamps", THEME-2); its id still picks the model.
                foreach(var d in State.Drops){Actor(d.Id,Catalog.FindItem(d.ItemId)?.Name??d.ItemId,d.X,d.Z,0,Color.yellow,.2f,model:d.ItemId);seen.Add(d.Id);}
                // Night 2 bodies lie in the downed pose (and play the dragged motion while carried) until a revival lifts them.
                foreach(var b in State.Bodies){string id="body_"+b.PlayerId;Actor(id,(State.Players.Find(x=>x.Id==b.PlayerId)?.Name??"Friend")+"'s body",b.X,b.Z,0,new Color(.45f,.45f,.5f),.4f,"Downed");seen.Add(id);}
            }
            foreach(var pair in actors)pair.Value.gameObject.SetActive(seen.Contains(pair.Key));
            ViewCamera.backgroundColor=spirit?new Color(.08f,.2f,.24f):new Color(.13f,.1f,.22f);
        }
        private void Actor(string id,string label,float x,float z,float angle,Color color,float height,string pose="Idle",string role="Attendee",float threat=0,bool littleSpoon=false,bool highlyIntoxicated=false,bool redEyes=false,string equippedItem="",bool showLabel=true,string model=null,float y=0,bool filming=false,bool recording=false)
        {
            if(!actors.TryGetValue(id,out var tr))
            {
                GameObject go;
                if(height>.25f)go=FestivalCharacter.Create(actorRoot,id,color,role).gameObject;
                else
                {
                    string resource=DropModel(model??label);
                    go=resource==null?null:FestivalArtView.Create(actorRoot,resource);
                    if(go==null){go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(actorRoot);var c=go.GetComponent<Collider>();c.enabled=false;Destroy(c);}
                }
                tr=go.transform;tr.position=new Vector3(x,0,z);actors[id]=tr;
                var textObject=new GameObject("Label");textObject.transform.SetParent(tr,false);textObject.transform.localPosition=new Vector3(0,2.65f,0);
                var bubble=GameObject.CreatePrimitive(PrimitiveType.Cube);bubble.name="Name bubble";bubble.transform.SetParent(textObject.transform,false);
                bubble.transform.localPosition=new Vector3(0,0,.035f);bubble.transform.localScale=new Vector3(.9f,.29f,.05f);
                bubble.GetComponent<Renderer>().sharedMaterial=FestivalArtView.MaterialFor("Dark");
                var bubbleCollider=bubble.GetComponent<Collider>();bubbleCollider.enabled=false;Destroy(bubbleCollider);nameBubbles[id]=bubble.transform;
                var face=new GameObject("Text");face.transform.SetParent(textObject.transform,false);
                face.transform.localPosition=new Vector3(0,0,-.025f);
                var text=face.AddComponent<TextMesh>();text.fontSize=64;text.characterSize=.04f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=new Color(.96f,.94f,.84f);
                var festivalFont=Resources.Load<Font>("FestivalDisplay");if(festivalFont!=null){text.font=festivalFont;text.GetComponent<MeshRenderer>().sharedMaterial=festivalFont.material;}
                names[id]=text;
                foreach(var child in textObject.GetComponentsInChildren<Transform>(true))child.gameObject.layer=30;
            }
            var character=tr.GetComponent<FestivalCharacter>();if(character!=null){character.Pose=pose;character.DjConsole=pose=="Dj"?world?.PlayerDjConsole:null;character.Threat=threat;character.SetLittleSpoon(littleSpoon);character.SetHighlyIntoxicated(highlyIntoxicated);character.SetRedEyes(redEyes);character.SetEquippedItem(equippedItem);character.SetFilming(filming,recording);}
            tr.gameObject.SetActive(true);tr.localScale=height<=.25f?Vector3.one*(tr.name.StartsWith("Festival") ? .7f : .3f):Vector3.Scale(Vector3.one*.82f,character?.ShapeScale??Vector3.one);var target=new Vector3(x,height<=.25f?.2f:y,z);
            tr.position=Vector3.Distance(tr.position,target)>5?target:Vector3.Lerp(tr.position,target,1-Mathf.Exp(-15*Time.unscaledDeltaTime));tr.rotation=Quaternion.Euler(0,angle,0);
            var nameTag=names[id];nameTag.text=label;nameTag.transform.parent.rotation=ViewCamera.transform.rotation;
            nameBubbles[id].localScale=new Vector3(Mathf.Max(.9f,label.Length*.033f+.28f),.29f,.05f);
            float labelDistance=Vector3.Distance(ViewCamera.transform.position,tr.position);
            nameTag.transform.parent.localScale=Vector3.one*Mathf.Clamp(labelDistance/6f,.14f,.7f);
            nameTag.transform.parent.gameObject.SetActive(showLabel&&labelDistance>1.1f&&labelDistance<7f);
        }
        public static string DropModel(string item)
        {
            if(item=="wristband")return "FestivalWristband";
            if(item=="little_spoon")return "FestivalLittleSpoon";
            if(item=="confetti")return "FestivalConfetti";
            if(item=="merch_bag")return "FestivalMerchBag";
            if(item=="map")return "FestivalMap";
            if(item=="stage_pass")return "FestivalStagePass";
            if(item=="medical_voucher")return "FestivalVoucher";
            if(item=="stash_box")return "FestivalStash";
            if(item=="poi_led"||item=="poi_practice")return "FestivalPoi";
            if(item=="stock_lsd")return "FestivalStockPrism";
            if(item=="stock_mushrooms")return "FestivalStockMoon";
            return null;
        }
        private static Color PlayerColor(string id){int h=0;foreach(char c in id)h=unchecked(h*31+c);return Color.HSVToRGB((h&0xffff)/65536f,.6f,.95f);}
        private void UpdateCamera(PlayerState player)
        {
            if(player.CampVisitId!=lastCampVisit)
            {
                if(player.CampVisitId!=""){preVisitYaw=yaw;preVisitPitch=pitch;yaw=0;pitch=0;}
                else {yaw=preVisitYaw;pitch=preVisitPitch;}
                lastCampVisit=player.CampVisitId;
            }
            if(firstPersonHands==null && ViewCamera!=null && !string.IsNullOrEmpty(LocalPlayerId))firstPersonHands=FestivalHands.Create(ViewCamera,LocalPlayerId);
            bool lying=State!=null&&FestivalSimulation.LyingDown(State,player.Id);
            if(lying!=wasLying)
            {
                if(lying){preLiePitch=pitch;pitch=LyingPitch;}
                else pitch=preLiePitch;
                wasLying=lying;
            }
            ThirdPersonView=FestivalThirdPerson.Applies(Profile.Data.ThirdPerson,lying,player.CampVisitId!="",player.VisualWheelSince>=0);
            if(firstPersonHands!=null){firstPersonHands.gameObject.SetActive(State!=null && (State.Phase=="Playing"||State.Phase=="Shopping") && player.VisualPose!="Dance" && !lying && !ThirdPersonView);firstPersonHands.ReducedMotion=Profile.Data.ReducedMotion;firstPersonHands.SetState(player,State.SimulationSeconds,State.Transfers.Exists(t=>t.ToId==player.Id),spinHeld&&FestivalSimulation.CanSpinPoi(player));}
            var visit=CampFeatures.Find(player.CampVisitId);
            // POLO-5: on the Ferris wheel the eye rides seated in the rider's gondola, under its canopy, aimed ahead of the seat for
            // the eye's glide (rate 20) so it keeps it there as it turns, at any frame rate (POLO-7); the mouse still aims it, and the
            // turn ends back by the base.
            var target=visit!=null?new Vector3(player.CampInteriorX,1.65f,player.CampInteriorZ)
                :player.VisualWheelSince>=0&&world!=null?world.WheelSeat(State,player,20)+Vector3.up*FestivalTwistVisuals.SeatedEyeHeight
                :new Vector3(player.X,player.Life=="Downed"?.55f:lying?LyingEyeHeight:1.65f,player.Z);
            EyePosition=Vector3.Distance(EyePosition,target)>5?target:Vector3.Lerp(EyePosition,target,1-Mathf.Exp(-20*Time.unscaledDeltaTime));
            float roll=0;
            if(!Profile.Data.ReducedMotion)
            {
                foreach(var effect in player.Effects)
                {
                    if(effect.Id=="lsd")roll+=Mathf.Sin((float)State.SimulationSeconds*2.1f)*2.2f;
                    else if(effect.Id=="mushrooms")roll+=Mathf.Sin((float)State.SimulationSeconds*1.3f+1.1f)*1.5f;
                }
            }
            // TRIP-5: Pony Dust's camera trails the mouse; movement still faces where the mouse points (yaw).
            float lag=FestivalTrip.For(State,LocalPlayerId,Profile.Data.ReducedMotion,0).Lag;
            viewYaw=FestivalTrip.Follow(viewYaw,yaw,lag,Time.unscaledDeltaTime);viewPitch=FestivalTrip.Follow(viewPitch,pitch,lag,Time.unscaledDeltaTime);
            ViewCamera.transform.rotation=Quaternion.Euler(viewPitch,viewYaw,Mathf.Clamp(roll,-3.5f,3.5f));
            // CAM-1: over the shoulder the camera is set straight on its boom from the eye (no ease of its own, so a switch is a cut), and
            // shows your body unless a wall has pulled the boom into your head; in first person it is the eye, your body hidden.
            float boom=0;
            ViewCamera.transform.position=ThirdPersonView?FestivalThirdPerson.Place(EyePosition,viewYaw,viewPitch,out boom):EyePosition;
            ViewCamera.cullingMask=FestivalThirdPerson.Mask(ViewCamera.cullingMask,boom);
        }
        private void TransportFailed(){if(!closing)Fail("Network transport failed. Leave and try another port or host address.");}
        private void Fail(string message){Leave();Message=message;}
        public void SetMessage(string message){Message=message;}
        // NET-3: NGO shuts a manager down on its next network update (a host first waits up to 5 s for its friends to drop). Destroyed
        // before then, the transport went first and the manager's own shutdown sent a friend's disconnect through the disposed driver,
        // logging an ObjectDisposedException. So the manager's GameObject goes once NGO reports it stopped. That hangs on the manager,
        // not this session, so it still runs if this session is destroyed first; and manager is let go at once so Host or Join work
        // straight away. ponytail: OnClientStopped alone, as a session is always a host or a client, never a dedicated server.
        public void Leave()
        {
            closing=true;Connecting=false;Voice.Leave();
            if(manager!=null)
            {
                var stopping=manager;manager=null;
                stopping.OnClientConnectedCallback-=PeerConnected;stopping.OnClientDisconnectCallback-=PeerDisconnected;stopping.OnTransportFailure-=TransportFailed;stopping.Shutdown();
                if(stopping.IsListening)stopping.OnClientStopped+=_=>Destroy(stopping.gameObject);else Destroy(stopping.gameObject);
            }
            State=null;simulation=null;LocalPlayerId="";peers.Clear();pending.Clear();inputs.Clear();loadedRound="";lastPhase="";lastCampVisit="";wasLying=false;MenuOpen=true;accumulator=0;
            // MENU-2: the title shows the opening line, not the game's last reply (Fail sets its reason after this), and the next game
            // looks straight ahead as the first did, not up from the grass or round a tent's inside (TRIP-8).
            Message=OpeningMessage;yaw=pitch=viewYaw=viewPitch=0;
            if(firstPersonHands!=null){Destroy(firstPersonHands.gameObject);firstPersonHands=null;}
            foreach(var tr in actors.Values)if(tr!=null)Destroy(tr.gameObject);actors.Clear();displayedDanceSteps.Clear();names.Clear();foreach(var mat in actorMaterials)Destroy(mat);actorMaterials.Clear();
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        private void OnDestroy(){Leave();Controls?.Dispose();if(actorRoot!=null)Destroy(actorRoot.gameObject);if(ViewCamera!=null)Destroy(ViewCamera.gameObject);}
    }
}
