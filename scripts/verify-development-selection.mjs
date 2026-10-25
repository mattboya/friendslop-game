import {spawnSync} from 'node:child_process';
import {existsSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync} from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

// Runs the production host bootstrap from built managed assemblies, first Development then Release, on one disposable
// profile with one earned festival. Development must offer locked Ember Playa and TEST MODE; Release keeps the same functional
// code but must refuse both. Neither may change the earned count. Usage: node scripts/verify-development-selection.mjs [devManaged releaseManaged]
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const managed=app=>path.join(app,'Contents/Resources/Data/Managed');
const [developmentArg,releaseArg]=process.argv.slice(2);
const variants=[
  ['development',developmentArg??managed(process.env.FESTIVAL_MAC_DEV_OUTPUT??path.join(root,'Builds/macOS/Development/FestivalCoop.app'))],
  ['release',releaseArg??managed(path.join(root,'Builds/macOS/Release/FestivalCoop.app'))],
];
const editor=process.env.UNITY_EDITOR||'/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity';
const scripting=path.join(path.dirname(editor),'../Resources/Scripting');
const runtime=path.join(scripting,'NetCoreRuntime/dotnet'),compiler=path.join(scripting,'DotNetSdkRoslyn/csc.dll');
const frameworks=path.join(scripting,'NetCoreRuntime/shared/Microsoft.NETCore.App');
for(const file of [runtime,compiler,frameworks,...variants.flatMap(([,dir])=>['Festival.Core.dll','Festival.Runtime.dll'].map(n=>path.join(dir,n)))])
  if(!existsSync(file)){console.error(`Missing ${file}; build both variants first.`);process.exit(1);}

const harness=String.raw`
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Festival.Core;
using Festival.Network;
using Festival.Presentation;

public static class Program
{
    static string managed;
    public static int Main(string[] args)
    {
        managed=args[1];
        AppDomain.CurrentDomain.AssemblyResolve+=(_,e)=>{string file=Path.Combine(managed,new AssemblyName(e.Name).Name+".dll");return File.Exists(file)?Assembly.LoadFrom(file):null;};
        try{Run(args[0]=="development",args[2]);return 0;}
        catch(Exception e){Console.Error.WriteLine(args[0]+": "+e.Message);return 1;}
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    [MethodImpl(MethodImplOptions.NoInlining)] static void Run(bool development,string profileFile)
    {
        string variant=development?"Development":"Release";
        HostRunsTheBootstrapAfterUnlocks(variant);
        // LocalProfile's constructor reads through Unity; the unlock methods are plain C#, so they run on the profile's own data.
        var profile=(LocalProfile)RuntimeHelpers.GetUninitializedObject(typeof(LocalProfile));
        typeof(LocalProfile).GetProperty("Data").SetValue(profile,JsonSerializer.Deserialize<ProfileData>(File.ReadAllText(profileFile),new JsonSerializerOptions{IncludeFields=true}));
        var game=new FestivalSimulation(1);profile.ApplyUnlocks(game.State);FestivalHostBootstrap.Configure(game);
        var host=game.AddPlayer("host","Host");
        Check(game.State.UnlockedFestivalCount==1,variant+" host should start with the profile's one earned festival, not "+game.State.UnlockedFestivalCount);
        Check(game.DevelopmentFestivalSelection==development,variant+" bootstrap left the test-build pick "+(game.DevelopmentFestivalSelection?"on":"off"));
        var pick=game.Execute(host.Id,new GameCommand{Id="verify-pick",Kind="ChooseFestival",Amount=1});
        Check(pick.Accepted==development&&game.State.FestivalIndex==(development?1:0),variant+" locked Ember Playa pick was "+(pick.Accepted?"accepted":"refused: "+pick.Reason));
        // TEST MODE rides the same capability, so a release host must refuse it.
        var test=game.Execute(host.Id,new GameCommand{Id="verify-test-mode",Kind=FestivalSimulation.TestModeKind});
        Check(test.Accepted==development&&game.State.TestMode==development,variant+" TEST MODE was "+(test.Accepted?"accepted":"refused: "+test.Reason));
        profile.RememberUnlocks(game.State);
        Check(profile.Data.UnlockedFestivalCount==1&&game.State.UnlockedFestivalCount==1,variant+" changed the earned count");
        Console.WriteLine(variant+": capability="+game.DevelopmentFestivalSelection+", locked pick "+(pick.Accepted?"accepted":"refused ("+pick.Reason+")")+", test mode "+(test.Accepted?"accepted":"refused")+", earned count 1");
    }
    // The real Host, not a test path, must call the bootstrap, after the profile's unlocks so they can never come from it.
    static void HostRunsTheBootstrapAfterUnlocks(string variant)
    {
        using var pe=new PEReader(File.OpenRead(Path.Combine(managed,"Festival.Runtime.dll")));var md=pe.GetMetadataReader();
        var codes=typeof(OpCodes).GetFields().Select(f=>(OpCode)f.GetValue(null)).ToDictionary(o=>(ushort)o.Value);
        var host=md.TypeDefinitions.Select(md.GetTypeDefinition).Where(t=>md.GetString(t.Namespace)=="Festival.Network"&&md.GetString(t.Name)=="FestivalSession")
            .SelectMany(t=>t.GetMethods()).Single(h=>md.GetString(md.GetMethodDefinition(h).Name)=="Host");
        byte[] il=pe.GetMethodBody(md.GetMethodDefinition(host).RelativeVirtualAddress).GetILBytes();var calls=new List<string>();
        for(int i=0;i<il.Length;)
        {
            var code=codes[il[i]==0xFE?(ushort)(0xFE00|il[i+1]):il[i]];i+=code.Size;
            if(code.OperandType==OperandType.InlineMethod&&MetadataTokens.EntityHandle(BitConverter.ToInt32(il,i)) is var token&&token.Kind==HandleKind.MethodDefinition)
            {
                var method=md.GetMethodDefinition((MethodDefinitionHandle)token);
                calls.Add(md.GetString(md.GetTypeDefinition(method.GetDeclaringType()).Name)+"."+md.GetString(method.Name));
            }
            i+=code.OperandType switch{OperandType.InlineNone=>0,OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar=>1,
                OperandType.InlineVar=>2,OperandType.InlineI8 or OperandType.InlineR=>8,OperandType.InlineSwitch=>4+4*BitConverter.ToInt32(il,i),_=>4};
        }
        int unlocks=calls.IndexOf("LocalProfile.ApplyUnlocks"),bootstrap=calls.IndexOf("FestivalHostBootstrap.Configure");
        Check(unlocks>=0&&bootstrap>unlocks,variant+" FestivalSession.Host never runs FestivalHostBootstrap.Configure after applying earned unlocks");
    }
}
`;

const work=mkdtempSync(path.join(os.tmpdir(),'festival-dev-selection-'));
let failed=false;
try
{
  const version=readdirSync(frameworks).sort().at(-1),framework=path.join(frameworks,version);
  const program=path.join(work,'VerifyDevelopmentSelection.dll'),source=path.join(work,'Program.cs'),rsp=path.join(work,'compile.rsp');
  writeFileSync(source,harness);
  // Compiled against Release, so a Release build that drops the functional bootstrap fails here.
  const release=variants[1][1];
  writeFileSync(rsp,['-nologo','-target:exe','-langversion:9','-warnaserror+',`-out:"${program}"`,
    ...readdirSync(framework).filter(n=>n.endsWith('.dll')).map(n=>`-r:"${path.join(framework,n)}"`),
    `-r:"${path.join(release,'Festival.Core.dll')}"`,`-r:"${path.join(release,'Festival.Runtime.dll')}"`,`"${source}"`].join('\n'));
  const compiled=spawnSync(runtime,['exec',compiler,`@${rsp}`],{encoding:'utf8'});
  if(compiled.status!==0)throw new Error(`The harness does not compile against the Release assemblies:\n${compiled.stdout}${compiled.stderr}`);
  writeFileSync(path.join(work,'VerifyDevelopmentSelection.runtimeconfig.json'),JSON.stringify({runtimeOptions:{tfm:'net6.0',framework:{name:'Microsoft.NETCore.App',version}}}));
  const profile=path.join(work,'profiles','default.json');
  mkdirSync(path.dirname(profile));writeFileSync(profile,JSON.stringify({SchemaVersion:2,UnlockedFestivalCount:1},null,4));
  const saved=readFileSync(profile);
  for(const [variant,dir] of variants)
  {
    const run=spawnSync(runtime,[program,variant,dir,profile],{encoding:'utf8'});
    process.stdout.write(run.stdout||'');process.stderr.write(run.stderr||'');
    if(run.status!==0)throw new Error(`${variant} check failed (exit ${run.status}).`);
  }
  if(!readFileSync(profile).equals(saved))throw new Error('The profile changed on disk.');
  console.log('DEVELOPMENT SELECTION VERIFIED: Development offers locked festivals and TEST MODE, Release refuses both, the earned count holds');
}
catch(error){console.error(error.message);failed=true;}
finally{rmSync(work,{recursive:true,force:true});}
process.exit(failed?1:0);
