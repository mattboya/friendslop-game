import {readFileSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const assembly=variant=>readFileSync(path.join(root,`Builds/macOS/${variant}/FestivalCoop.app/Contents/Resources/Data/Managed/Festival.Runtime.dll`));
const development=process.env.FESTIVAL_MAC_DEV_OUTPUT
  ? readFileSync(path.join(process.env.FESTIVAL_MAC_DEV_OUTPUT,'Contents/Resources/Data/Managed/Festival.Runtime.dll'))
  : assembly('Development');
const release=assembly('Release');
const forbidden=['DevelopmentGraphicsDiagnostics','DevelopmentSmoke','DevelopmentSimulation',
  'DevelopmentAnimationUpdates','[Festival.Motion]','ArtReviewCapture'];
for(const marker of forbidden)
{
  const signatures=[Buffer.from(marker,'utf8'),Buffer.from(marker,'utf16le')];
  if(!signatures.some(signature=>development.includes(signature)))throw new Error(`Development assembly is missing ${marker}; rebuild it before comparison.`);
  if(signatures.some(signature=>release.includes(signature)))throw new Error(`Release assembly contains ${marker}.`);
}
console.log('MAC RELEASE DIAGNOSTIC EXCLUSION PASSED');
