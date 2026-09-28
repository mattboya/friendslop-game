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
    [Serializable] internal sealed class Hello { public string Name="Friend", Token=""; public int Protocol=1; }
    [Serializable] internal sealed class Welcome { public string PlayerId="", Token=""; }
    [Serializable] internal sealed class DialogueHistoryPacket { public List<string> Seen=new List<string>(); }
    [Serializable] internal sealed class MoveIntent { public float X,Z,Yaw; public bool Sprint; public int Sequence; }
    [Serializable] internal sealed class SnapshotPacket { public RoundState State; public double ServerSeconds; }
    internal sealed class PeerInput { public MoveIntent Intent=new MoveIntent(); public double ReceivedAt; public int Commands; public double Window; }

    /// <summary>Native Unity Transport session. Host owns all rules; clients submit input and render filtered state.</summary>
    public sealed class FestivalSession : MonoBehaviour
    {
        public RoundState State {get;private set;}
        public string LocalPlayerId {get;private set;}="";
        public PlayerState LocalPlayer => State?.Players.Find(p=>p.Id==LocalPlayerId);
        public FestivalCharacter LocalWorldCharacter => actors.TryGetValue(LocalPlayerId,out var actor)?actor.GetComponent<FestivalCharacter>():null;
        public bool IsHost => manager!=null && manager.IsHost;
        public bool Connected => manager!=null && manager.IsConnectedClient;
        public bool Connecting {get;private set;}
        public string Message {get;private set;}="Gather your friends. Make the last shuttle.";
        public FestivalInput Controls {get;private set;}
        public LocalProfile Profile {get;private set;}
        public IVoiceSession Voice {get;}=new UnavailableVoiceSession();
        public bool MenuOpen=true;
        public Camera ViewCamera {get;private set;}
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
        private string lastCampVisit="";
        private double accumulator,nextSnapshot,nextInput,snapshotReceivedAt,serverClockAtSnapshot,connectAt;
        private int movementSequence;
        private string token="", endpoint="", loadedRound="", acknowledgedDialogue="", profileId="default";
        private NavMeshPath navigationPath;
        private Transform actorRoot;
        private FestivalWorld world;
        private FestivalHands firstPersonHands;
        private bool closing;
        private void Awake()
        {
            navigationPath=new NavMeshPath();
            Application.runInBackground=true;
            Application.targetFrameRate=60;
            var args=Environment.GetCommandLineArgs();
            profileId=Argument(args,"--profile","default");
            Profile=new LocalProfile(profileId);Controls=new FestivalInput(profileId);
            gameObject.AddComponent<FestivalRhythmAudio>();
            gameObject.AddComponent<FestivalCampAudio>();
            var cameraObject=new GameObject("First-person camera");ViewCamera=cameraObject.AddComponent<Camera>();cameraObject.AddComponent<AudioListener>();
            ViewCamera.nearClipPlane=.05f;ViewCamera.farClipPlane=130;ViewCamera.fieldOfView=75;
            FestivalCharacter.ViewTransform=ViewCamera.transform;
            ViewCamera.cullingMask &= ~(1<<31); // Hide the local world body from its first-person camera.
            ViewCamera.clearFlags=CameraClearFlags.Skybox;ViewCamera.backgroundColor=new Color(.13f,.10f,.22f);
            ViewCamera.transform.position=new Vector3(0,1.65f,-29);
            // The world owns the single dusk/lighting Volume. The camera only
            // opts into it, avoiding a second, lower-priority bloom override.
            ViewCamera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var graphics=cameraObject.AddComponent<DevelopmentGraphicsDiagnostics>();graphics.Session=this;
            DevelopmentDiagnostics.GraphicsEvent("Rendering","camera_ready","post_processing=true world_volume_priority=20");
#endif
            actorRoot=new GameObject("Authoritative actor presentation").transform;
            world=FindFirstObjectByType<FestivalWorld>();if(world!=null)world.Build();
            gameObject.AddComponent<FestivalClueVisuals>();
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
                simulation=new FestivalSimulation(Environment.TickCount & int.MaxValue);
                simulation.HasLineOfSight=(x,z,xx,zz)=>!Physics.Linecast(new Vector3(x,1.2f,z),new Vector3(xx,1.2f,zz),~0,QueryTriggerInteraction.Ignore);
                simulation.Navigate=Navigate;
                Configure(port,"127.0.0.1",true);
                manager.NetworkConfig.ConnectionData=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Hello{Name=CleanName(name)}));
                if(!manager.StartHost())throw new InvalidOperationException("Could not listen on that port.");
                RegisterMessages();Message="Lobby open on port "+port+". Friends can join by LAN address.";
                State=ViewFor(LocalPlayerId);MenuOpen=false;
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
        private void Approve(NetworkManager.ConnectionApprovalRequest request,NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject=false;response.Pending=false;
            try
            {
                if(request.Payload==null||request.Payload.Length>1024)throw new ArgumentException("Invalid connection request.");
                var hello=JsonUtility.FromJson<Hello>(Encoding.UTF8.GetString(request.Payload));
                if(hello==null||hello.Protocol!=1||hello.Token==null||hello.Token.Length>64)throw new ArgumentException("Incompatible game version.");
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
                player.X=0;player.Z=19;friend.X=0;friend.Z=19;
                preview.Execute(player.Id,new GameCommand{Id="preview_ready",Kind="Ready"});
                preview.Execute(friend.Id,new GameCommand{Id="preview_friend_ready",Kind="Ready"});
                preview.Execute(player.Id,new GameCommand{Id="preview_start",Kind="Start"});
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
                State=ViewFor(LocalPlayerId);snapshotReceivedAt=Time.realtimeSinceStartupAsDouble;serverClockAtSnapshot=manager.ServerTime.Time;
                if(Time.realtimeSinceStartupAsDouble>=nextSnapshot){Broadcast();nextSnapshot=Time.realtimeSinceStartupAsDouble+1.0/10;}
            }
            var player=LocalPlayer;if(player==null)return;
            if(world!=null){world.SetPhase(State.Phase);world.SetInterior(player.CampVisitId);world.SetCampAntics(player.CampAntics);world.UpdateShop(State);}
            if(State.Phase=="Loading" && loadedRound!=State.RoundId)
            {
                if(world!=null&&world.IsReady&&world.NavigationReady){loadedRound=State.RoundId;Command("MapReady");}
                else Message="Waiting for festival navigation to finish…";
            }
            if((State.Phase=="Playing"||State.Phase=="CampReview") && MenuOpen && lastPhase!=State.Phase)MenuOpen=false;
            lastPhase=State.Phase;
            if(Controls.Menu.WasPressedThisFrame()&&!Controls.Rebinding)MenuOpen=!MenuOpen;
            Cursor.lockState=MenuOpen?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=MenuOpen;
            if(!MenuOpen && Application.isFocused)
            {
                var look=Controls.Look.ReadValue<Vector2>();yaw+=look.x*Profile.Data.MouseSensitivity;pitch=Mathf.Clamp(pitch-look.y*Profile.Data.MouseSensitivity,-80,80);
            }
            if(Time.realtimeSinceStartupAsDouble>=nextInput && (IsHost||LastSnapshotAge<5))
            {
                var active=State.Interactions.Find(i=>i.Id==player.InteractionId&&i.Status=="Active");
                var move=MenuOpen||!Application.isFocused||FestivalInput.IsRhythmKind(active?.Kind)?Vector2.zero:Controls.Move.ReadValue<Vector2>();
                var intent=new MoveIntent{X=move.x,Z=move.y,Yaw=yaw,Sprint=Controls.Sprint.IsPressed(),Sequence=++movementSequence};
                if(IsHost)AcceptInput(LocalPlayerId,intent);else SendJson("festival.move",NetworkManager.ServerClientId,intent);
                nextInput=Time.realtimeSinceStartupAsDouble+.05;
            }
            if(player.HasCosmetic)Profile.AwardFirstRescue();
            UpdateActors();UpdateCamera(player);
        }
        private string lastPhase="";
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
                if(Time.realtimeSinceStartupAsDouble-pair.Value.ReceivedAt>.25)continue;
                var active=simulation.Interaction(player.InteractionId);
                float speed=(input.Sprint?6:4)*Intoxication.MovementMultiplier(player);if(player.InteractionId!="")speed=1;if(player.DragTargetId!="")speed=2;if(player.Life=="Downed")speed=.8f;
                var local=active!=null&&active.Status=="Active"&&FestivalInput.IsRhythmKind(active.Kind)?Vector3.zero:Vector3.ClampMagnitude(new Vector3(input.X,0,input.Z),1);
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
        private WorldPoint Navigate(float x,float z,float tx,float tz,double maxDistance)
        {
            var from=new Vector3(x,0,z);var target=new Vector3(tx,0,tz);
            if(NavMesh.SamplePosition(from,out var a,2,NavMesh.AllAreas)&&NavMesh.SamplePosition(target,out var b,3,NavMesh.AllAreas)&&NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,navigationPath)&&navigationPath.corners.Length>1)
                target=navigationPath.corners[1];
            var delta=Vector3.ClampMagnitude(target-from,(float)maxDistance);delta.y=0;var next=Slide(from,delta);return new WorldPoint(next.x,next.z);
        }
        private void Broadcast()
        {
            foreach(var peer in peers)
            {
                if(peer.Key==manager.LocalClientId)continue;
                SendJson("festival.snapshot",peer.Key,new SnapshotPacket{State=ViewFor(peer.Value),ServerSeconds=manager.ServerTime.Time});
            }
        }
        private RoundState ViewFor(string viewer)
        {
            // DTO whitelist: never ship command history, hidden evidence, other players' dialogue or credentials.
            var source=simulation.State;var local=simulation.Player(viewer);bool spirit=local?.Life=="Spirit";
            var view=new RoundState{SchemaVersion=source.SchemaVersion,Seed=source.Seed,RoundId=source.RoundId,Phase=source.Phase,Result=source.Result,HostPlayerId=source.HostPlayerId,
                SimulationSeconds=source.SimulationSeconds,ElapsedSeconds=source.ElapsedSeconds,DurationSeconds=source.DurationSeconds,LaunchAtSeconds=source.LaunchAtSeconds,Tick=source.Tick,TransactionSequence=source.TransactionSequence,GrossSales=source.GrossSales,StashCash=source.StashCash,CampMusicTrack=source.CampMusicTrack,
                ReviewResult=source.ReviewResult,ReviewSales=source.ReviewSales,ReviewSurvivors=source.ReviewSurvivors,ReviewAntics=source.ReviewAntics,ReviewVotes=source.ReviewVotes,
                FriendFound=!spirit&&source.FriendFound,FriendLeaderId=spirit?"":source.FriendLeaderId,FriendPosition=!spirit&&source.FriendFound?source.FriendPosition:new WorldPoint(0,0),
                VendorOffers=source.VendorOffers,ShopStock=source.ShopStock,CluesRead=source.CluesRead,GateOpened=source.GateOpened,PrivateClue=FestivalSimulation.ClueHint(source,local),ObjectiveReward=source.ObjectiveReward,SurvivorBonus=source.SurvivorBonus,Survivors=source.Survivors,ConnectedCrewCount=source.Players.FindAll(p=>p.Connected).Count};
            foreach(var p in source.Players)
            {
                if(spirit && p.Life!="Spirit")continue;
                var copy=JsonUtility.FromJson<PlayerState>(JsonUtility.ToJson(p));copy.Dialogue=new DialogueHistory();copy.VisualPose=p.Ready&&source.Phase=="Shopping"?"Dance":p.DragTargetId!=""?"Drag":source.Interactions.Find(i=>i.Id==p.InteractionId&&i.Status=="Active")?.Kind??(p.Effects.Count>0?"Intoxicated":"Idle");
                copy.WearingLittleSpoon=p.Inventory.Exists(item=>item.ItemId=="little_spoon"&&item.Count>0);
                copy.VisualWideEyes=p.Effects.Exists(effect=>effect.Id=="lsd"||effect.Id=="mushrooms"||effect.Id=="ecstasy");
                copy.VisualRedEyes=p.Effects.Exists(effect=>effect.Id=="weed");
                if(p.Id!=viewer){copy.Inventory.Clear();copy.Effects.Clear();copy.Wristbands.Clear();copy.Cash=0;copy.NpcSpeech="";copy.NpcSpeaker="";copy.NpcSpeechUntil=0;}view.Players.Add(copy);
            }
            if(!spirit)
            {
                foreach(var npc in source.Npcs)
                {
                    bool targetsViewer=npc.TargetId==viewer;
                    var copy=new NpcState{Id=npc.Id,Kind=npc.Kind,Mode=targetsViewer?npc.Mode:(npc.Kind=="Cop"?"Patrol":"Blending"),TargetId=targetsViewer?viewer:"",X=npc.X,Z=npc.Z,Yaw=npc.Yaw,IdlePose=npc.IdlePose,HighlyIntoxicated=npc.HighlyIntoxicated,RedEyes=npc.RedEyes,CanTalk=npc.CanTalk};
                    var observer=npc.Observers.Find(o=>o.PlayerId==viewer);copy.Suspicion=observer?.Suspicion??0;view.Npcs.Add(copy);
                }
                // The undiscovered friend is revealed only at local sight range, never on the full map.
                if(source.GateOpened && !view.FriendFound && local!=null && Vector2.Distance(new Vector2(local.X,local.Z),new Vector2(source.FriendPosition.X,source.FriendPosition.Z))<12 && simulation.HasLineOfSight(local.X,local.Z,source.FriendPosition.X,source.FriendPosition.Z))view.FriendPosition=source.FriendPosition;
                view.Drops=source.Drops;view.Stashes=source.Stashes;view.Transfers=source.Transfers.FindAll(t=>t.FromId==viewer||t.ToId==viewer);
            }
            view.Interactions=source.Interactions.FindAll(i=>i.PlayerId==viewer&&i.Status=="Active");return view;
        }
        private void UpdateActors()
        {
            var local=LocalPlayer;bool spirit=local.Life=="Spirit";var seen=new HashSet<string>();
            foreach(var p in State.Players)
            {
                if(!p.Connected||(spirit!=(p.Life=="Spirit")))continue;
                bool togetherInside=p.CampVisitId!=""&&p.CampVisitId==local.CampVisitId;
                if(!togetherInside&&(p.CampVisitId!=""||local.CampVisitId!=""))continue;
                Actor(p.Id,p.Name,togetherInside?p.CampInteriorX:p.X,togetherInside?p.CampInteriorZ:p.Z,p.VisualPose=="Dj"?0:p.Yaw,
                    p.Life=="Downed"?new Color(.9f,.3f,.3f):PlayerColor(p.Id),p.Life=="Downed"?.4f:.9f,
                    p.Life=="Alive"?p.VisualPose:p.Life,"Attendee",0,p.WearingLittleSpoon,p.VisualWideEyes,p.VisualRedEyes,p.EquippedItemId);
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
            if(!spirit && (State.Phase=="Playing"||State.Phase=="Results"))
            {
                foreach(var n in State.Npcs){Actor(n.Id,n.Kind=="Cop"?"SECURITY":"Festivalgoer",n.X,n.Z,n.Yaw,n.Kind=="Cop"?new Color(.25f,.4f,.7f):new Color(.8f,.5f,.3f),.85f,n.Mode=="Blending"?n.IdlePose:n.Mode,n.Kind=="Cop"?"Security":"Attendee",n.Kind=="Cop"?0:(float)n.Suspicion/100f,false,n.HighlyIntoxicated,n.RedEyes,"",false);seen.Add(n.Id);}
                var fp=State.FriendPosition;
                if(fp!=null && (fp.X!=0||fp.Z!=0) && (State.FriendFound||Vector2.Distance(new Vector2(local.X,local.Z),new Vector2(fp.X,fp.Z))<12)){Actor("mission_friend","MISSING FRIEND",fp.X,fp.Z,0,Color.cyan,.9f,"Idle","Friend");seen.Add("mission_friend");}
                foreach(var d in State.Drops){Actor(d.Id,d.ItemId,d.X,d.Z,0,Color.yellow,.2f);seen.Add(d.Id);}
            }
            foreach(var pair in actors)pair.Value.gameObject.SetActive(seen.Contains(pair.Key));
            ViewCamera.backgroundColor=spirit?new Color(.08f,.2f,.24f):new Color(.13f,.1f,.22f);
        }
        private void Actor(string id,string label,float x,float z,float angle,Color color,float height,string pose="Idle",string role="Attendee",float threat=0,bool littleSpoon=false,bool highlyIntoxicated=false,bool redEyes=false,string equippedItem="",bool showLabel=true)
        {
            if(!actors.TryGetValue(id,out var tr))
            {
                GameObject go;
                if(height>.25f)go=FestivalCharacter.Create(actorRoot,id,color,role).gameObject;
                else
                {
                    string resource=DropModel(label);
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
            var character=tr.GetComponent<FestivalCharacter>();if(character!=null){character.Pose=pose;character.DjConsole=pose=="Dj"?world?.PlayerDjConsole:null;character.Threat=threat;character.SetLittleSpoon(littleSpoon);character.SetHighlyIntoxicated(highlyIntoxicated);character.SetRedEyes(redEyes);character.SetEquippedItem(equippedItem);}
            tr.gameObject.SetActive(true);tr.localScale=height<=.25f?Vector3.one*(tr.name.StartsWith("Festival") ? .7f : .3f):Vector3.Scale(Vector3.one*.82f,character?.ShapeScale??Vector3.one);var target=new Vector3(x,height<=.25f?.2f:pose=="Downed"?-.4f:0,z);
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
            if(item=="stock_lsd"||item=="stock_mushrooms")return "FestivalStock";
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
            if(firstPersonHands!=null){firstPersonHands.gameObject.SetActive(State!=null && (State.Phase=="Playing"||State.Phase=="Shopping") && player.VisualPose!="Dance");firstPersonHands.SetState(player);}
            var visit=CampFeatures.Find(player.CampVisitId);
            var target=visit==null?new Vector3(player.X,player.Life=="Downed"?.55f:1.65f,player.Z):new Vector3(player.CampInteriorX,1.65f,player.CampInteriorZ);
            ViewCamera.transform.position=Vector3.Distance(ViewCamera.transform.position,target)>5?target:Vector3.Lerp(ViewCamera.transform.position,target,1-Mathf.Exp(-20*Time.unscaledDeltaTime));
            float roll=0;
            if(!Profile.Data.ReducedMotion)
            {
                foreach(var effect in player.Effects)
                {
                    if(effect.Id=="lsd")roll+=Mathf.Sin((float)State.SimulationSeconds*2.1f)*2.2f;
                    else if(effect.Id=="mushrooms")roll+=Mathf.Sin((float)State.SimulationSeconds*1.3f+1.1f)*1.5f;
                }
            }
            ViewCamera.transform.rotation=Quaternion.Euler(pitch,yaw,Mathf.Clamp(roll,-3.5f,3.5f));
        }
        private void TransportFailed(){if(!closing)Fail("Network transport failed. Leave and try another port or host address.");}
        private void Fail(string message){Leave();Message=message;}
        public void SetMessage(string message){Message=message;}
        public void Leave()
        {
            closing=true;Connecting=false;Voice.Leave();
            if(manager!=null){manager.OnClientConnectedCallback-=PeerConnected;manager.OnClientDisconnectCallback-=PeerDisconnected;manager.OnTransportFailure-=TransportFailed;manager.Shutdown();Destroy(manager.gameObject);manager=null;}
            State=null;simulation=null;LocalPlayerId="";peers.Clear();pending.Clear();inputs.Clear();loadedRound="";lastPhase="";lastCampVisit="";MenuOpen=true;accumulator=0;
            if(firstPersonHands!=null){Destroy(firstPersonHands.gameObject);firstPersonHands=null;}
            foreach(var tr in actors.Values)if(tr!=null)Destroy(tr.gameObject);actors.Clear();displayedDanceSteps.Clear();names.Clear();foreach(var mat in actorMaterials)Destroy(mat);actorMaterials.Clear();
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        private void OnDestroy(){Leave();Controls?.Dispose();if(actorRoot!=null)Destroy(actorRoot.gameObject);if(ViewCamera!=null)Destroy(ViewCamera.gameObject);}
    }
}
