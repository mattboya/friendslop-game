import {spawnSync} from 'node:child_process';
import {existsSync, mkdirSync, writeFileSync, readdirSync, rmSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const dotnet=process.env.FESTIVAL_DOTNET || (existsSync('/tmp/friendslop-dotnet/dotnet')?'/tmp/friendslop-dotnet/dotnet':'dotnet');
const mode=process.argv[2]||'all';
// Each run builds in its own folder so parallel runs never overwrite each other's Domain.dll.
const out=path.join(root,'artifacts/domain-runner',String(process.pid));
const options={cwd:root,encoding:'utf8',env:{...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',DOTNET_NOLOGO:'1'},timeout:180000,maxBuffer:8*1024*1024};
let run=spawnSync(dotnet,['run','--project',path.join(root,'tests/Domain/Domain.csproj'),'--configuration','Release','--artifacts-path',out,'--',mode],options);
// Unity ships both Roslyn and a .NET runtime. Use these when a separate SDK is
// absent; no installation or temporary downloaded toolchain is required.
if(run.error?.code==='ENOENT'){
  const scripting='/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/Resources/Scripting';
  const runtime=path.join(scripting,'NetCoreRuntime/dotnet');
  const frameworks=path.join(scripting,'NetCoreRuntime/shared/Microsoft.NETCore.App');
  const compiler=path.join(scripting,'DotNetSdkRoslyn/csc.dll');
  if(existsSync(runtime)&&existsSync(compiler)&&existsSync(frameworks)){
    const version=readdirSync(frameworks).sort().at(-1);
    const framework=path.join(frameworks,version);
    mkdirSync(out,{recursive:true});
    const collect=dir=>readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?collect(path.join(dir,e.name)):e.name.endsWith('.cs')?[path.join(dir,e.name)]:[]);
    const sources=[...collect(path.join(root,'Assets/Festival/Runtime/Core')),...readdirSync(path.join(root,'tests/Domain')).filter(n=>n.endsWith('.cs')).map(n=>path.join(root,'tests/Domain',n)),path.join(root,'Assets/Festival/Tests/EditMode/MissionTests.cs')];
    const output=path.join(out,'Domain.dll');
    const rsp=path.join(out,'compile.rsp');
    writeFileSync(rsp,['-nologo','-target:exe','-langversion:9','-warnaserror+',`-out:"${output}"`,...readdirSync(framework).filter(n=>n.endsWith('.dll')).map(n=>`-r:"${path.join(framework,n)}"`),...sources.map(n=>`"${n}"`)].join('\n'));
    run=spawnSync(runtime,['exec',compiler,`@${rsp}`],options);
    if(run.status===0){
      writeFileSync(path.join(out,'Domain.runtimeconfig.json'),JSON.stringify({runtimeOptions:{tfm:'net6.0',framework:{name:'Microsoft.NETCore.App',version}}}));
      run=spawnSync(runtime,[output,mode],options);
    }
  }
}
rmSync(out,{recursive:true,force:true});
mkdirSync(path.join(root,'artifacts'),{recursive:true});
writeFileSync(path.join(root,'artifacts',`domain-${mode}.log`), `${new Date().toISOString()}\n${run.stdout||''}${run.stderr||''}${run.error||''}`);
process.stdout.write(run.stdout||''); process.stderr.write(run.stderr||'');
if(run.error) console.error(run.error.message);
process.exit(run.status??1);
