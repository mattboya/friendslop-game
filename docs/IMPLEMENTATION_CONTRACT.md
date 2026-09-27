# Implementation contract — first native pass

Root builds Unity NGO transport, UI/input and integration. Runtime data is plain serializable C# under Festival.Core. Keep public interfaces described here stable; add fields/methods as useful and notify root.

## Content leaf

`Catalog.Items` array/list of `ItemDefinition` with public string Id, Name, Description; int Price, StackLimit; string EffectId. `Catalog.FindItem(string)` returns definition or null.
`Catalog.Effects` definitions with Id, Name, DurationSeconds, LeadSeconds; `Catalog.FindEffect(string)`.
`Catalog.VendorOffers(int seed)` returns List<string> exactly seven distinct purchasable item IDs (four guarantees plus three extras).
`RhythmChart.Create(int seed, int count=8)` returns chart with `List<RhythmNote> Notes`, `double DurationSeconds`. Notes: int Id, Direction (0 left,1 down,2 up,3 right), double TimeSeconds. First note >=1s. `RhythmJudge(chart, double goodWindow=0.15)`; `Submit(int direction,double timeSeconds)` returns grade string; `Score` normalized with extra-press penalty, `Complete` should NOT require all notes hit (timer owns completion); expose hits/misses as appropriate. Deduplication of RPC IDs belongs to simulation.
`EffectPresentation.Path(string effectId,int noteId,double progress,bool reducedMotion)` returns a pure `NoteVisual` public double X,Y,Rotation,Alpha; Y reaches 0 and X reaches 0 at progress=1; no mutation of chart/time. X,Y normalized offsets.
`DialogueHistory` List<string> Seen, `Select(string context,int seed)` returns `DialogueLine` with Id, Context, Text, AwkwardText; `Acknowledge(string id)` once, LRU bounded 512. Catalog 48+ original lines across greeting,dance,suspicion,sale,police,medical. Serializable history.

## Simulation leaf

Provide `FestivalSimulation(int seed=1)` with public `RoundState State`, `PlayerState AddPlayer(string id,string name)` (throws at capacity), `Disconnect(string id)`, `CommandResult Execute(string playerId, GameCommand command)`, `void Tick(double deltaSeconds)`, `void Restore(RoundState state)`.
`GameCommand` public string Id, Kind, TargetId, ItemId; int Amount, Direction; double TimeSeconds; command types documented by leaf. `CommandResult` bool Accepted; string Reason. Sender comes from transport, not payload. Result can add fields.
`RoundState` public int SchemaVersion, Seed; string RoundId, Phase (Lobby/Shopping/Loading/Playing/Results), Result; double ElapsedSeconds, DurationSeconds; int GrossSales, StashCash; List<PlayerState> Players; List<NpcState> Npcs; List<string> VendorOffers; `WorldPoint FriendPosition`; string FriendLeaderId; bool FriendFound. Public fields serializable with JsonUtility; no dictionaries/hashsets in persistent DTOs.
`PlayerState` string Id, Name, Life (Alive/Downed/Spirit/Detained), InteractionId; bool Ready, Connected; float X,Z,Yaw; int Cash,Health,RevivalCount; List<ItemStack> Inventory; List<ActiveEffect> Effects. ItemStack: string ItemId; int Count. ActiveEffect: string Id; double RemainingSeconds. Add fields needed.
`NpcState` string Id,Kind (Wook/Cop),Mode,TargetId; float X,Z; double Suspicion; add per-observer-player records as needed. Root exposes only local relevant suspicion, filters spirit view.
`WorldPoint` float X,Z. Map coordinates -40..40. Stage (0,26), vendor (-18,-22), medical (24,-20), holding (27,5), stash (-25,-8), shuttle (0,-32), lost-property (-28,16), friend seed points (-24,25),(25,24),(18,5). 24 wooks around stage and 2 cops. Spawn eight players x=-7+2*i,z=-29.
`TryMove(string playerId,float x,float z,float yaw,double deltaSeconds)` validates finite/range/speed; returns bool. Root computes movement collision first, then validates. Spirits in separate presentation, their domain move cannot interact economically. Movement command excluded from normal command dedupe.

Simulation uses Content APIs but may defer chart calls until files exist. Publish exact command/interaction contract promptly so root can wire UI. Need full rescue/economy/danger lifecycle + serializable snapshot/dedupe; tests provide static `SimulationTests.Run()` (global namespace). Content test static `ContentTests.Run()`. Tests throw; don't add Main/csproj.

## Editor/world leaf

`Festival.Presentation.FestivalWorld : MonoBehaviour` provides `public void Build()` creating original compact festival geometry/lights/signage/cosmetic dancers, safe boundaries and collision. Idempotent owned children, never delete manual scene objects. No dependency on simulation. Static landmarks per coordinates above. Runtime can build in Awake or root call.
`Festival.Editor.ProjectBootstrap.EnsureGeneratedContent()` creates Assets/Festival/Generated/Bootstrap.unity, default URP asset, input asset and other needed generated resources; scene root has `Festival.Network.FestivalSession` + `Festival.Presentation.FestivalWorld` + `Festival.Presentation.FestivalHud`. Root implements these components. Session creates NetworkManager + UnityTransport at runtime (custom named messages, no network prefab required unless later changed). Generator must add camera? Root session owns camera, so no extra camera/audio listener. Root HUD uses programmatic uGUI.
`ProjectValidation.Validate()` validates artifacts and logs FESTIVAL VALIDATION PASSED only after success.
`BuildEntry.BuildWindowsDevelopment/BuildWindowsRelease/BuildMacDevelopment` invoke generation/validation then BuildPipeline with native targets, fail batch process on error; expected output Builds/Windows/Development/FestivalCoop.exe, Builds/Windows/Release/FestivalCoop.exe, Builds/macOS/Development/FestivalCoop.app.
Unity 6000.3.24f1 target. URP exact package to be confirmed by root; assume URP 17.x API. Include meaningful EditMode/PlayMode tests in separate folders (root owns asmdefs). Use Unity editor source/reference docs to verify APIs. Never invent executed Unity evidence.
