import {existsSync, mkdirSync, readFileSync, unlinkSync} from 'node:fs';
import {spawnSync} from 'node:child_process';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const artifacts=path.join(root,'artifacts');
mkdirSync(artifacts,{recursive:true});

function findEditor()
{
  const candidates=[
    process.env.UNITY_EDITOR,
    '/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity',
    '/Applications/Unity/Unity.app/Contents/MacOS/Unity',
  ].filter(Boolean);
  const found=candidates.find(existsSync);
  if(found)return found;
  const search=spawnSync('mdfind',['kMDItemFSName == "Unity.app"'],{encoding:'utf8'});
  if(search.status===0)
  {
    for(const app of search.stdout.split(/\r?\n/).filter(Boolean))
    {
      const binary=path.join(app,'Contents/MacOS/Unity');
      if(existsSync(binary))return binary;
    }
  }
  return null;
}

const editor=findEditor();
if(!editor)
{
  console.error('UNITY EDITOR NOT FOUND: install/activate Unity 6000.3.24f1 with Windows Build Support, or set UNITY_EDITOR.');
  process.exit(2);
}

function run(label,args,expectedMarker='')
{
  const log=path.join(artifacts,`unity-${label}.log`);
  if(existsSync(log))unlinkSync(log);
  const command=['-batchmode','-accept-apiupdate','-projectPath',root,'-logFile',log,...args];
  const result=spawnSync(editor,command,{cwd:root,encoding:'utf8',timeout:30*60*1000,maxBuffer:16*1024*1024});
  if(result.stdout)process.stdout.write(result.stdout);
  if(result.stderr)process.stderr.write(result.stderr);
  if(result.error)console.error(result.error.message);
  if(result.status!==0)
  {
    if(existsSync(log))console.error(`Unity failed; inspect ${log}`);
    process.exit(result.status??1);
  }
  if(expectedMarker)
  {
    if(!existsSync(log)||!readFileSync(log,'utf8').includes(expectedMarker))
    {
      console.error(`Unity exited without the expected ${expectedMarker} marker; inspect ${log}`);
      process.exit(1);
    }
  }
}

function runTests(label,platform)
{
  const results=path.join(artifacts,`${label}-results.xml`);
  if(existsSync(results))unlinkSync(results);
  // Unity's test runner exits batch mode after the run. Supplying -quit can
  // terminate before the Test Framework writes its result file.
  run(label,['-buildTarget','OSXUniversal','-runTests','-testPlatform',platform,'-testResults',results]);
  if(!existsSync(results))
  {
    console.error(`Unity reported success but did not create ${results}`);
    process.exit(1);
  }
  const xml=readFileSync(results,'utf8');
  const passed=/<test-run\b[^>]*\bfailed="0"[^>]*>/i.test(xml)
    && /<test-run\b[^>]*\bresult="Passed"[^>]*>/i.test(xml);
  if(!passed)
  {
    console.error(`Unity tests did not pass; inspect ${results}`);
    process.exit(1);
  }
  console.log(`${platform.toUpperCase()} TESTS PASSED`);
}

const action=process.argv[2]||'validate';
if(action==='generate')run('generate',['-buildTarget','OSXUniversal','-executeMethod','Festival.Editor.ProjectBootstrap.EnsureGeneratedContent','-quit'],'Starter content ready');
else if(action==='validate')
{
  run('generate',['-buildTarget','OSXUniversal','-executeMethod','Festival.Editor.ProjectBootstrap.EnsureGeneratedContent','-quit'],'Starter content ready');
  run('validate',['-buildTarget','OSXUniversal','-executeMethod','Festival.Editor.ProjectValidation.Validate','-quit'],'FESTIVAL VALIDATION PASSED');
  console.log('FESTIVAL VALIDATION PASSED');
}
else if(action==='test-edit')runTests('editmode','EditMode');
else if(action==='test-play')runTests('playmode','PlayMode');
else if(action==='build-mac-development')
{
  run('build-mac-development',['-buildTarget','OSXUniversal','-executeMethod','Festival.Editor.BuildEntry.BuildMacDevelopment','-quit'],'FESTIVAL BUILD PASSED');
  console.log('FESTIVAL BUILD PASSED');
}
else if(action==='build-windows-development')
{
  run('build-windows-development',['-buildTarget','Win64','-executeMethod','Festival.Editor.BuildEntry.BuildWindowsDevelopment','-quit'],'FESTIVAL BUILD PASSED');
  console.log('FESTIVAL BUILD PASSED');
}
else if(action==='build-windows-release')
{
  run('build-windows-release',['-buildTarget','Win64','-executeMethod','Festival.Editor.BuildEntry.BuildWindowsRelease','-quit'],'FESTIVAL BUILD PASSED');
  console.log('FESTIVAL BUILD PASSED');
}
else
{
  console.error('Usage: node scripts/unity.mjs generate|validate|test-edit|test-play|build-mac-development|build-windows-development|build-windows-release');
  process.exit(2);
}
