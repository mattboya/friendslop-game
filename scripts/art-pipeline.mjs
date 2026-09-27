import {createHash,randomUUID} from 'node:crypto';
import {existsSync,mkdirSync,readFileSync,writeFileSync,copyFileSync,renameSync,rmSync,statSync} from 'node:fs';
import {spawnSync} from 'node:child_process';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const stagingRoot=path.join(root,'artifacts/asset-staging');
const groups={
  character:{script:'generate_modular_character.py',source:'FestivalCharacter.blend',manifest:'character-manifest.json',exports:['FestivalCharacter.fbx','FestivalCharacterDistant.fbx']},
  hands:{script:'generate_festival_hands.py',source:'FestivalHands.blend',manifest:'hands-manifest.json',exports:['FestivalHands.fbx']},
  world:{script:'generate_festival_world_assets.py',source:'FestivalWorld.blend',manifest:'world-manifest.json',exports:null},
};
const hash=file=>createHash('sha256').update(readFileSync(file)).digest('hex');
const fail=message=>{throw new Error(message);};

function expectedFiles(dir,kinds)
{
  const files=[];
  for(const kind of kinds)
  {
    const group=groups[kind];
    const manifestPath=path.join(dir,'ArtSource',group.manifest);
    if(!existsSync(manifestPath))fail(`Missing ${group.manifest}`);
    const manifest=JSON.parse(readFileSync(manifestPath,'utf8'));
    const exports=kind==='world'?Object.keys(manifest.models||{}).map(name=>name+'.fbx'):group.exports;
    if(kind==='world'&&exports.length<27)fail('World manifest is incomplete.');
    if(kind==='character'&&(!manifest.distantGroups||manifest.bones?.length!==13))fail('Character rig or LOD manifest is incomplete.');
    if(kind==='hands'&&manifest.skinShapes!==3)fail('Hand shape contract changed.');
    files.push({stage:path.join('ArtSource',group.source),target:path.join('ArtSource/Generated',group.source)});
    files.push({stage:path.join('ArtSource',group.manifest),target:path.join('ArtSource',group.manifest)});
    for(const name of exports)files.push({stage:path.join('Resources',name),target:path.join('Assets/Festival/Art/Resources',name)});
  }
  for(const file of files)
  {
    const staged=path.join(dir,file.stage);
    const minimumBytes=file.stage.endsWith('.json')?32:1024;
    if(!existsSync(staged)||statSync(staged).size<minimumBytes)fail(`Missing or empty staged file: ${file.stage}`);
    if(file.stage.endsWith('.fbx')&&!readFileSync(staged).subarray(0,20).toString().startsWith('Kaydara FBX Binary'))
      fail(`Invalid FBX header: ${file.stage}`);
    if(file.target.endsWith('.fbx')&&!existsSync(path.join(root,file.target+'.meta')))
      fail(`Stable Unity GUID is missing: ${file.target}.meta`);
  }
  return files;
}

function writeReceipt(dir,kinds)
{
  const files=expectedFiles(dir,kinds);
  const receipt={kinds,files:files.map(file=>({...file,sha256:hash(path.join(dir,file.stage))}))};
  writeFileSync(path.join(dir,'receipt.json'),JSON.stringify(receipt,null,2)+'\n');
  process.stdout.write(`Staged ${files.length} source, manifest and FBX files at ${dir}\nInspect them, then run: node scripts/art-pipeline.mjs publish '${dir}'\n`);
}

function stage(kinds)
{
  const blender=process.env.BLENDER_BINARY||'/Applications/Blender.app/Contents/MacOS/Blender';
  if(!existsSync(blender))fail('Blender executable not found; set BLENDER_BINARY.');
  const dir=path.join(stagingRoot,new Date().toISOString().replaceAll(':','-')+'-'+randomUUID().slice(0,8));
  mkdirSync(dir,{recursive:true});
  for(const kind of kinds)
  {
    const result=spawnSync(blender,['--background','--factory-startup','--python',path.join(root,'scripts',groups[kind].script)],
      {cwd:root,env:{...process.env,FESTIVAL_ASSET_STAGE:dir},encoding:'utf8',maxBuffer:64*1024*1024});
    writeFileSync(path.join(dir,`blender-${kind}.log`),(result.stdout||'')+(result.stderr||''));
    if(result.status!==0)fail(`Blender ${kind} failed; inspect ${path.join(dir,`blender-${kind}.log`)}`);
  }
  writeReceipt(dir,kinds);
}

function publish(dir)
{
  const resolved=path.resolve(dir);
  if(!resolved.startsWith(stagingRoot+path.sep))fail('Stage must be inside artifacts/asset-staging.');
  const receipt=JSON.parse(readFileSync(path.join(resolved,'receipt.json'),'utf8'));
  const files=expectedFiles(resolved,receipt.kinds);
  if(files.length!==receipt.files.length)fail('Staged file list changed since validation.');
  for(let i=0;i<files.length;i++)
    if(files[i].stage!==receipt.files[i].stage||files[i].target!==receipt.files[i].target||
      hash(path.join(resolved,files[i].stage))!==receipt.files[i].sha256)
      fail(`Staged asset changed: ${files[i].stage}`);
  const backup=path.join(resolved,'backup');
  const prepared=[];
  try
  {
    for(const file of files)
    {
      const target=path.join(root,file.target),previous=path.join(backup,file.target),temp=target+'.art-pipeline-tmp';
      mkdirSync(path.dirname(target),{recursive:true});
      if(existsSync(target)){mkdirSync(path.dirname(previous),{recursive:true});copyFileSync(target,previous);}
      copyFileSync(path.join(resolved,file.stage),temp);
      prepared.push({target,previous,temp,hadPrevious:existsSync(target)});
    }
    for(const item of prepared)renameSync(item.temp,item.target);
  }
  catch(error)
  {
    for(const item of prepared)
    {
      rmSync(item.temp,{force:true});
      if(existsSync(item.previous))copyFileSync(item.previous,item.target);
      else if(!item.hadPrevious)rmSync(item.target,{force:true});
    }
    throw error;
  }
  process.stdout.write(`Published ${files.length} validated files. Previous versions are in ${backup}\nUnity .meta files were preserved. Run Unity validation and inspect native renders before acceptance.\n`);
}

try
{
  const [action,arg]=process.argv.slice(2);
  if(action==='stage')stage(arg==='all'?Object.keys(groups):groups[arg]?[arg]:fail('Use stage character|hands|world|all.'));
  else if(action==='validate'&&arg)
  {
    const dir=path.resolve(arg);
    if(!dir.startsWith(stagingRoot+path.sep))fail('Stage must be inside artifacts/asset-staging.');
    const kinds=Object.keys(groups).filter(kind=>existsSync(path.join(dir,`blender-${kind}.log`)));
    if(kinds.length===0)fail('No Blender run logs found in the stage.');
    writeReceipt(dir,kinds);
  }
  else if(action==='publish'&&arg)publish(arg);
  else fail('Usage: node scripts/art-pipeline.mjs stage character|hands|world|all | validate <stage-directory> | publish <stage-directory>');
}
catch(error){console.error(error.message);process.exitCode=1;}
