import {spawn} from 'node:child_process';
import {existsSync,readFileSync,mkdirSync,unlinkSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const binary=process.env.FESTIVAL_NATIVE_BINARY||path.join(root,'Builds/macOS/Development/FestivalCoop.app/Contents/MacOS/Festival Co-op Prototype');
const directory=path.join(root,'artifacts/native-smoke');
const log=path.join(directory,'solo.log');
mkdirSync(directory,{recursive:true});
if(!existsSync(binary))throw new Error('Build the macOS development player first.');
if(existsSync(log))unlinkSync(log);

const child=spawn(binary,['--host','--port','17780','--profile','smoke_solo','--name','Solo','--solo-smoke-test','-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-logFile',log],{cwd:root,stdio:'ignore'});
// The script has its own explicit failure deadline. Leave enough room for
// player startup and PNG writes on a loaded or screen-locked development Mac.
const timeout=setTimeout(()=>child.kill(),180000);
child.on('error',error=>{clearTimeout(timeout);console.error(error);process.exitCode=1;});
child.on('exit',code=>{
  clearTimeout(timeout);
  const output=existsSync(log)?readFileSync(log,'utf8'):'';
  if(code!==0||/Exception:|FESTIVAL SMOKE FAILED:/.test(output)||!output.includes('FESTIVAL SOLO SMOKE PASSED')){
    console.error('Solo native smoke failed; inspect '+log);
    process.exitCode=1;
    return;
  }
  console.log('SOLO NATIVE SMOKE PASSED');
});
