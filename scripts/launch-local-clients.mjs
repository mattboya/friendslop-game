import {existsSync, mkdirSync, writeFileSync} from 'node:fs';
import {spawn} from 'node:child_process';
import path from 'node:path';

function value(name,fallback)
{
  const index=process.argv.indexOf(name);
  return index>=0&&index+1<process.argv.length?process.argv[index+1]:fallback;
}
const binary=path.resolve(value('--binary','Builds/macOS/Development/FestivalCoop.app/Contents/MacOS/Festival Co-op Prototype'));
const players=Number(value('--players','2'));
const port=Number(value('--port','7777'));
if(!existsSync(binary))throw new Error(`Native player not found: ${binary}`);
if(!Number.isInteger(players)||players<2||players>8)throw new Error('--players must be 2 through 8');
if(!Number.isInteger(port)||port<1||port>65535)throw new Error('--port must be 1 through 65535');

const processes=[];
for(let index=0;index<players;index++)
{
  const args=index===0
    ? ['--host','--port',String(port),'--profile','local-host','--name','Host']
    : ['--join','127.0.0.1','--port',String(port),'--profile',`local-client-${index}`,'--name',`Friend ${index}`];
  const child=spawn(binary,args,{cwd:path.dirname(binary),detached:true,stdio:'ignore'});
  child.unref();processes.push({role:index===0?'host':'client',index,pid:child.pid,args});
}
mkdirSync('artifacts',{recursive:true});
writeFileSync('artifacts/local-clients.json',JSON.stringify({startedAt:new Date().toISOString(),binary,port,processes},null,2)+'\n');
console.log(`Started one host and ${players-1} client process(es). PIDs recorded in artifacts/local-clients.json.`);
