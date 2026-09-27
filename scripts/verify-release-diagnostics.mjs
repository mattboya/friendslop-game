import {readFileSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const assembly=variant=>readFileSync(path.join(root,`Builds/macOS/${variant}/FestivalCoop.app/Contents/Resources/Data/Managed/Festival.Runtime.dll`));
const development=assembly('Development'),release=assembly('Release');
const forbidden=['DevelopmentGraphicsDiagnostics','DevelopmentSmoke','DevelopmentSimulation','DevelopmentAnimationUpdates'];
for(const marker of forbidden)
{
  if(!development.includes(Buffer.from(marker)))throw new Error(`Development assembly is missing ${marker}; rebuild it before comparison.`);
  if(release.includes(Buffer.from(marker)))throw new Error(`Release assembly contains ${marker}.`);
}
console.log('MAC RELEASE DIAGNOSTIC EXCLUSION PASSED');
