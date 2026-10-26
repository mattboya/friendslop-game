import {readFileSync, existsSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const required=[
  'Packages/manifest.json','ProjectSettings/ProjectVersion.txt',
  'Assets/Festival/Runtime/Core/Festival.Core.asmdef','Assets/Festival/Runtime/Festival.Runtime.asmdef',
  'Assets/Festival/Runtime/Core/Simulation/FestivalSimulation.cs',
  'Assets/Festival/Runtime/Core/Content/Catalog.cs',
  'Assets/Festival/Runtime/Network/FestivalSession.cs',
  'Assets/Festival/Runtime/Presentation/FestivalHud.cs',
  'Assets/Festival/Runtime/Presentation/FestivalWorld.cs',
  'Assets/Festival/Editor/ProjectBootstrap.cs','Assets/Festival/Editor/ProjectValidation.cs','Assets/Festival/Editor/BuildEntry.cs',
];
for(const file of required)if(!existsSync(path.join(root,file)))throw new Error(`Missing ${file}`);

const manifest=JSON.parse(readFileSync(path.join(root,'Packages/manifest.json'),'utf8'));
const expected={
  'com.unity.netcode.gameobjects':'2.13.2','com.unity.transport':'2.6.0','com.unity.inputsystem':'1.20.0',
  'com.unity.render-pipelines.universal':'17.3.0','com.unity.ai.navigation':'2.0.14','com.unity.test-framework':'1.4.6','com.unity.ugui':'2.0.0'
};
for(const [name,version] of Object.entries(expected))if(manifest.dependencies[name]!==version)throw new Error(`${name} must be pinned to ${version}`);
if(!readFileSync(path.join(root,'ProjectSettings/ProjectVersion.txt'),'utf8').includes('6000.3.24f1'))throw new Error('Unity editor version is not pinned');
for(const file of required.filter(x=>x.endsWith('.asmdef')))JSON.parse(readFileSync(path.join(root,file),'utf8'));

const sourceFiles=[];
// Keep the checker dependency-free and bounded to known source trees.
import {readdirSync,statSync} from 'node:fs';
function collect(directory)
{
  for(const name of readdirSync(directory))
  {
    const full=path.join(directory,name);const stat=statSync(full);
    if(stat.isDirectory())collect(full);else if(name.endsWith('.cs'))sourceFiles.push(full);
  }
}
collect(path.join(root,'Assets/Festival'));
const sources=sourceFiles.map(file=>readFileSync(file,'utf8')).join('\n');
const forbidden=/(TODO|NotImplementedException|claude-of-tanks|Claude of Tanks)/i;
if(!forbidden.test('TODO positive control'))throw new Error('Forbidden-token negative-control checker is broken');
if(forbidden.test(sources))throw new Error('Placeholder or copied reference marker found in Festival source');
if(!sources.includes('#if UNITY_EDITOR || DEVELOPMENT_BUILD'))throw new Error('Development-only diagnostics guard missing');
if(!sources.includes('FESTIVAL VALIDATION PASSED'))throw new Error('Validation success marker missing');
if(!sources.includes('ConnectionApprovalCallback'))throw new Error('Connection admission boundary missing');
if(!sources.includes('PreviewItem'))throw new Error('Isolated item preview boundary missing');

const core=sourceFiles.filter(file=>file.includes(`${path.sep}Runtime${path.sep}Core${path.sep}`)).map(file=>readFileSync(file,'utf8')).join('\n');
if(/UnityEngine|UnityEditor|Unity\.Netcode/.test(core))throw new Error('Core simulation is coupled to Unity runtime');

// Each check reads a claim in the docs and the code it names, so it fails when either one moves.
const text=file=>readFileSync(path.join(root,file),'utf8');
const readme=text('README.md'),status=text('IMPLEMENTATION_STATUS.md'),docProblems=[];
const versionRow=status.split('\n').find(line=>line.includes('a different game version is turned away'))??'';
const versionSuites=[...versionRow.matchAll(/`(\w+Tests)`/g)].map(match=>match[1]);
if(!sourceFiles.some(file=>versionSuites.includes(path.basename(file,'.cs'))&&readFileSync(file,'utf8').includes('FestivalSession.AcceptsHello(')))docProblems.push('IMPLEMENTATION_STATUS.md needs a row that says a different game version is turned away and names a suite that calls FestivalSession.AcceptsHello (DebriefSessionTests, ConfirmSessionTests)');
if(readme.includes('PLAYTEST_2026-09-24'))docProblems.push('README.md sends readers to docs/PLAYTEST_2026-09-24.md, the guide to the 24 September build');
const smokeEnd='CONNECTION UI CAPTURE PASSED';
if(!readme.includes(smokeEnd)||!text('scripts/native-smoke.mjs').includes(`'${smokeEnd} in '`))docProblems.push(`README.md must name ${smokeEnd}, the last line that scripts/native-smoke.mjs prints on a pass`);
if(!text('docs/CHARACTER_QUALITY_PASS.md').includes('GPU (Batched) mesh deformation')||!text('Assets/Festival/Editor/ProjectBootstrap.cs').includes('MeshDeformation.GPUBatched'))docProblems.push('docs/CHARACTER_QUALITY_PASS.md must name the GPU (Batched) mesh deformation mode that ProjectBootstrap.cs sets');
for(const [file,body] of [['README.md',readme],['IMPLEMENTATION_STATUS.md',status]])
  for(const [,target] of body.matchAll(/\]\(([^)\s]+)\)/g))
    if(!/^[a-z]+:/i.test(target)&&!existsSync(path.join(root,path.dirname(file),target.split('#')[0])))docProblems.push(`${file} links ${target}, which does not exist`);
if(docProblems.length)throw new Error('Docs do not match this commit:\n- '+docProblems.join('\n- '));
console.log(`Verified ${sourceFiles.length} Festival C# source files and pinned native packages.`);
console.log('PROJECT STATIC CHECKS PASSED');
