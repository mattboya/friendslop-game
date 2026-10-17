import {spawn} from 'node:child_process';
import {existsSync,readFileSync,mkdirSync,unlinkSync,copyFileSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const binary=process.env.FESTIVAL_NATIVE_BINARY||path.join(root,'Builds/macOS/Development/FestivalCoop.app/Contents/MacOS/Festival Co-op Prototype');
const directory=path.join(root,'artifacts/native-smoke');
const log=path.join(directory,'solo.log');
// The player times each gameplay step on the simulation clock, with a 60 s no-progress watchdog. This outer limit only stops a
// player that hangs; FESTIVAL_SMOKE_TIMEOUT_SECONDS raises it for a heavily loaded machine.
const timeoutSeconds=Number(process.env.FESTIVAL_SMOKE_TIMEOUT_SECONDS||900);
if(!(timeoutSeconds>0))throw new Error('FESTIVAL_SMOKE_TIMEOUT_SECONDS must be a positive number of seconds.');
mkdirSync(directory,{recursive:true});
if(!existsSync(binary))throw new Error('Build the macOS development player first.');
if(existsSync(log))unlinkSync(log);

// Each gameplay wait the player logged: its name, stage and simulation/real seconds.
const waits=text=>[...text.matchAll(/^\[Festival\.SmokeWait\] ([^\n]+) (start|end|fail) sim=([\d.]+)\/([\d.]+) real=([\d.]+)(?: failure=([^\n]+))?$/gm)]
  .map(([,name,stage,sim,budget,real,failure])=>({name,stage,sim:Number(sim),budget:Number(budget),real:Number(real),failure}));
const describe=text=>{
  const logged=waits(text),ended=logged.filter(w=>w.stage==='end'),failedWaits=logged.filter(w=>w.stage==='fail');
  const slowest=ended.reduce((worst,w)=>!worst||w.real>worst.real?w:worst,null);
  return ended.length+' gameplay waits ended, '+failedWaits.length+' failed'
    +(slowest?'; slowest '+slowest.name+' sim '+slowest.sim.toFixed(2)+'/'+slowest.budget.toFixed(2)+' s real '+slowest.real.toFixed(2)+' s':'')
    +failedWaits.map(w=>'; FAILED '+w.name+' ('+w.failure+') sim '+w.sim.toFixed(2)+' s real '+w.real.toFixed(2)+' s').join('');
};

const started=Date.now();let timedOut=false;
const child=spawn(binary,['--host','--port','17780','--profile','smoke_solo','--name','Solo','--solo-smoke-test','-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-logFile',log],{cwd:root,stdio:'ignore'});
const timeout=setTimeout(()=>{timedOut=true;child.kill();},timeoutSeconds*1000);
child.on('error',error=>{clearTimeout(timeout);console.error(error);process.exitCode=1;});
child.on('exit',code=>{
  clearTimeout(timeout);
  const output=existsSync(log)?readFileSync(log,'utf8'):'';
  const run='in '+((Date.now()-started)/1000).toFixed(1)+' s real; '+describe(output);
  if(timedOut){console.error('Solo native smoke timed out after '+timeoutSeconds+' s '+run+'; inspect '+log);process.exitCode=1;return;}
  if(code!==0||/Exception:|FESTIVAL SMOKE FAILED:/.test(output)||!output.includes('FESTIVAL SOLO SMOKE PASSED')||!output.includes('FESTIVAL SMOKE CAMP MUSIC PASSED')){
    console.error('Solo native smoke failed '+run+'; inspect '+log);
    process.exitCode=1;
    return;
  }
  for(const [marker,name] of [['FESTIVAL SMOKE SPINNER RENDER PASSED','spinner'],['FESTIVAL SMOKE NIGHT RENDER PASSED','night']]){
    const capture=output.match(new RegExp(marker+': (.+)'))?.[1]?.trim();
    if(!capture||!existsSync(capture)){console.error('Solo '+name+' screenshot missing; inspect '+log);process.exitCode=1;return;}
    copyFileSync(capture,path.join(directory,'solo-'+name+'.png'));
  }
  console.log('SOLO NATIVE SMOKE PASSED '+run);
});
