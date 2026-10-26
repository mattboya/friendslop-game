import {existsSync, mkdirSync, readFileSync, realpathSync, unlinkSync} from 'node:fs';
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

// A Unity run must leave the stock TextMesh Pro files as committed, or the clean-tree checks waves rely on fail. A test run
// (EditMode or PlayMode) must also leave the editor's saved quality and graphics settings as committed.
// A plain copy inside another checkout (a builder snapshot) has no own .git to compare against, so it is skipped.
function changedStockFiles()
{
  const top=spawnSync('git',['-C',root,'rev-parse','--show-toplevel'],{encoding:'utf8'});
  if(top.status!==0||top.stdout.trim()!==realpathSync(root))return '';
  const stock=['Assets/TextMesh Pro',...(process.argv[2]==='test-edit'||process.argv[2]==='test-play'?['ProjectSettings/QualitySettings.asset','ProjectSettings/GraphicsSettings.asset']:[])];
  const diff=spawnSync('git',['-C',root,'diff','--name-only','HEAD','--',...stock],{encoding:'utf8'});
  return diff.status===0?diff.stdout.trim():'';
}

// Not `checkout --`: that restores from the index and needs the repo root. This form also undoes a staged change or new file.
function restoreAdvice(paths)
{
  return `Restore them with: git -C ${JSON.stringify(root)} restore --source=HEAD --staged --worktree -- ${paths.split('\n').map(p=>JSON.stringify(p)).join(' ')}`;
}

function run(label,args,expectedMarker='')
{
  // A file that already differs would fail the check after Unity and blame this run.
  const dirty=changedStockFiles();
  if(dirty)
  {
    console.error(`Committed stock files differ from HEAD before Unity ran:\n${dirty}\nUnity did not start. ${restoreAdvice(dirty)}`);
    process.exit(3);
  }
  const log=path.join(artifacts,`unity-${label}.log`);
  if(existsSync(log))unlinkSync(log);
  const command=['-batchmode','-accept-apiupdate','-projectPath',root,'-logFile',log,...args];
  const result=spawnSync(editor,command,{cwd:root,encoding:'utf8',timeout:30*60*1000,maxBuffer:16*1024*1024});
  if(result.stdout)process.stdout.write(result.stdout);
  if(result.stderr)process.stderr.write(result.stderr);
  if(result.error)console.error(result.error.message);
  // Checked before the exit code, so a run that fails its tests still reports the files it changed.
  const changed=changedStockFiles();
  if(changed)console.error(`Unity changed committed stock files (they differ from HEAD after this run):\n${changed}\n${restoreAdvice(changed)}`);
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
  if(changed)process.exit(3);
}

// `test-edit|test-play -testFilter <filter>` runs only the tests Unity's filter matches (a regular expression on full test
// names, e.g. "VisionMarkerSessionTests|HudTripperPlayTests").
const filterAt=process.argv.indexOf('-testFilter',3);
const filter=filterAt<0?null:process.argv[filterAt+1];
if(filterAt>=0&&!filter)
{
  console.error('Usage: -testFilter needs a filter');
  process.exit(2);
}

function runTests(label,platform)
{
  const results=path.join(artifacts,`${label}-results.xml`);
  if(existsSync(results))unlinkSync(results);
  // Unity's test runner exits batch mode after the run. Supplying -quit can
  // terminate before the Test Framework writes its result file.
  run(label,['-buildTarget','OSXUniversal','-runTests','-testPlatform',platform,'-testResults',results,...(filter?['-testFilter',filter]:[])]);
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
  // A filter that matches nothing runs nothing, and nothing fails.
  if(!/<test-run\b[^>]*\btotal="[1-9]/i.test(xml))
  {
    console.error(`Unity ran no tests${filter?` matching ${filter}`:''}; inspect ${results}`);
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
else if(action==='build-mac-release')
{
  run('build-mac-release',['-buildTarget','OSXUniversal','-executeMethod','Festival.Editor.BuildEntry.BuildMacRelease','-quit'],'FESTIVAL BUILD PASSED');
  console.log('FESTIVAL RELEASE BUILD PASSED');
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
  console.error('Usage: node scripts/unity.mjs generate|validate|test-edit|test-play [-testFilter <filter>]|build-mac-development|build-mac-release|build-windows-development|build-windows-release');
  process.exit(2);
}
