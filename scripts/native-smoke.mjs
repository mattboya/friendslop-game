import {spawn} from 'node:child_process';
import {mkdirSync,readFileSync,existsSync,copyFileSync,unlinkSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const binary=path.join(root,'Builds/macOS/Development/FestivalCoop.app/Contents/MacOS/Festival Co-op Prototype');
const dir=path.join(root,'artifacts/native-smoke');mkdirSync(dir,{recursive:true});
if(!existsSync(binary))throw new Error('Build the macOS development player first.');
const processes=[];
const run=(role,args)=>new Promise((resolve,reject)=>{
  const log=path.join(dir,role+'.log');if(existsSync(log))unlinkSync(log);
  const profiling=process.env.FESTIVAL_GRAPHICS_PROFILE==='1'?['--graphics-profile']:[];
  const child=spawn(binary,[...args,'--port','17779','--profile','smoke_'+role,'--name',role,'--smoke-test',...profiling,'-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-logFile',log],{cwd:root,stdio:'ignore'});
  processes.push(child);
  child.on('error',reject);
  child.on('exit',code=>{
    const text=existsSync(log)?readFileSync(log,'utf8'):'';
    if(code!==0||/Exception:|FESTIVAL SMOKE FAILED:/.test(text)||!text.includes('FESTIVAL SMOKE EYE STATES PASSED')||!text.includes('FESTIVAL SMOKE CAMP PASSED')||!text.includes('FESTIVAL SMOKE CAMP SHOP PASSED')||!text.includes('FESTIVAL SMOKE PASS RENDER PASSED')||!text.includes('FESTIVAL SMOKE SETTINGS RENDER PASSED')||!text.includes('FESTIVAL SMOKE MAP RENDER PASSED')||!text.includes('FESTIVAL SMOKE HELD RENDER PASSED')||!text.includes('FESTIVAL SMOKE CAMP OVERVIEW PASSED')||!text.includes('FESTIVAL SMOKE TRANSPORT PASSED')||!text.includes('FESTIVAL SMOKE CROWD PASSED')||!text.includes('FESTIVAL SMOKE CLUE VISIBILITY PASSED')||!text.includes('FESTIVAL SMOKE MISSION PASSED')||!text.includes('FESTIVAL SMOKE RENDER PASSED')||!text.includes('FESTIVAL SMOKE FIT ROLES PASSED')||!text.includes('FESTIVAL SMOKE FIT OUTFITS PASSED'))return reject(new Error(role+' smoke failed; inspect '+log));
    const screenshot=text.match(/FESTIVAL SMOKE RENDER PASSED: (.+)/)?.[1]?.trim();
    if(!screenshot||!existsSync(screenshot))return reject(new Error(role+' screenshot missing'));
    if(role==='client'){
      const clue=text.match(/FESTIVAL SMOKE CLUE RENDER PASSED: (.+)/)?.[1]?.trim();
      if(!clue||!existsSync(clue))return reject(new Error('Client clue screenshot missing'));
      copyFileSync(clue,path.join(dir,'client-clue.png'));
      const market=text.match(/FESTIVAL SMOKE MARKET RENDER PASSED: (.+)/)?.[1]?.trim();
      if(!market||!existsSync(market))return reject(new Error('Client market screenshot missing'));
      copyFileSync(market,path.join(dir,'client-market.png'));
      const approach=text.match(/FESTIVAL SMOKE MARKET APPROACH PASSED: (.+)/)?.[1]?.trim();
      if(!approach||!existsSync(approach))return reject(new Error('Client market approach screenshot missing'));
      copyFileSync(approach,path.join(dir,'client-market-approach.png'));
      for(const [marker,name] of [['FESTIVAL SMOKE ACTIONS RENDER PASSED','actions'],['FESTIVAL SMOKE RHYTHM RENDER PASSED','rhythm'],['FESTIVAL SMOKE RHYTHM JUDGMENT RENDER PASSED','rhythm-judgment']]){
        const capture=text.match(new RegExp(marker+': (.+)'))?.[1]?.trim();
        if(!capture||!existsSync(capture))return reject(new Error('Client '+name+' screenshot missing'));
        copyFileSync(capture,path.join(dir,'client-'+name+'.png'));
      }
    }
    const captures=[['FESTIVAL SMOKE CAMP PASSED','camp'],['FESTIVAL SMOKE CAMP SHOP PASSED','camp-shop'],['FESTIVAL SMOKE PASS RENDER PASSED','pass'],['FESTIVAL SMOKE SETTINGS RENDER PASSED','settings'],['FESTIVAL SMOKE MAP RENDER PASSED','map'],['FESTIVAL SMOKE HELD RENDER PASSED','held'],['FESTIVAL SMOKE CAMP OVERVIEW PASSED','camp-overview'],['FESTIVAL SMOKE CROWD LIVE PASSED','crowd-live'],['FESTIVAL SMOKE CROWD STAGE PASSED','crowd-stage'],['FESTIVAL SMOKE CROWD GROVE PASSED','crowd-grove'],['FESTIVAL SMOKE FIT ROLES PASSED','fit-roles'],['FESTIVAL SMOKE FIT OUTFITS PASSED','fit-outfits'],['FESTIVAL SMOKE CHARACTER QUALITY PASSED','character-quality'],['FESTIVAL SMOKE FACE STATES PASSED','face-states'],['FESTIVAL SMOKE CHARACTER DISTANCE PASSED','character-distance']];
    if(role==='host')captures.push(['FESTIVAL SMOKE CAMP CAR PASSED','camp-car']);
    for(const [marker,suffix] of captures){
      const capture=text.match(new RegExp(marker+': (.+)'))?.[1]?.trim();
      if(!capture||!existsSync(capture))return reject(new Error(role+' '+suffix+' screenshot missing'));
      copyFileSync(capture,path.join(dir,role+'-'+suffix+'.png'));
    }
    copyFileSync(screenshot,path.join(dir,role+'.png'));console.log(role.toUpperCase()+' NATIVE SMOKE PASSED');resolve();
  });
});
const captureConnection=()=>new Promise((resolve,reject)=>{
  const log=path.join(dir,'connection.log');if(existsSync(log))unlinkSync(log);
  const child=spawn(binary,['--ui-screens','--profile','smoke_connection','-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-logFile',log],{cwd:root,stdio:'ignore'});
  processes.push(child);
  child.on('error',reject);
  child.on('exit',code=>{
    const output=existsSync(log)?readFileSync(log,'utf8'):'';
    const capture=output.match(/FESTIVAL SMOKE CONNECTION RENDER PASSED: (.+)/)?.[1]?.trim();
    const errorCapture=output.match(/FESTIVAL SMOKE CONNECTION ERROR RENDER PASSED: (.+)/)?.[1]?.trim();
    const createdCamp=output.match(/FESTIVAL SMOKE MENU CREATE PASSED: (.+)/)?.[1]?.trim();
    if(code!==0||!capture||!existsSync(capture)||!errorCapture||!existsSync(errorCapture)||!createdCamp||!existsSync(createdCamp))return reject(new Error('Connection UI capture failed; inspect '+log));
    copyFileSync(capture,path.join(dir,'connection.png'));
    copyFileSync(errorCapture,path.join(dir,'connection-error.png'));
    copyFileSync(createdCamp,path.join(dir,'created-camp.png'));resolve();
  });
});
const timeout=setTimeout(()=>{for(const child of processes)child.kill();},120000);
try{
  const host=run('host',['--host']);
  const client=run('client',['--join','127.0.0.1']);
  await Promise.all([host,client]);console.log('TWO-CLIENT NATIVE SMOKE PASSED');
  await captureConnection();console.log('CONNECTION UI CAPTURE PASSED');
}catch(error){for(const child of processes)child.kill();console.error(error.message);process.exitCode=1;}
finally{clearTimeout(timeout);}
