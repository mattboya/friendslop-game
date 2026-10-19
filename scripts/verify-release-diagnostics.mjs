import {readFileSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

// Usage: node scripts/verify-release-diagnostics.mjs [devManaged releaseManaged]
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const managed=app=>path.join(app,'Contents/Resources/Data/Managed');
const [developmentArg,releaseArg]=process.argv.slice(2);
const runtime=folder=>readFileSync(path.join(folder,'Festival.Runtime.dll'));
const development=runtime(developmentArg??managed(process.env.FESTIVAL_MAC_DEV_OUTPUT||path.join(root,'Builds/macOS/Development/FestivalCoop.app')));
const release=runtime(releaseArg??managed(path.join(root,'Builds/macOS/Release/FestivalCoop.app')));
const forbidden=['DevelopmentGraphicsDiagnostics','DevelopmentSmoke','DevelopmentSimulation',
  'DevelopmentAnimationUpdates','[Festival.Motion]','ArtReviewCapture','DevelopmentStageBenchmark','FESTIVAL_TRIP_SURFACES'];
for(const marker of forbidden)
{
  const signatures=[Buffer.from(marker,'utf8'),Buffer.from(marker,'utf16le')];
  if(!signatures.some(signature=>development.includes(signature)))throw new Error(`Development assembly is missing ${marker}; rebuild it before comparison.`);
  if(signatures.some(signature=>release.includes(signature)))throw new Error(`Release assembly contains ${marker}.`);
}
console.log('MAC RELEASE DIAGNOSTIC EXCLUSION PASSED');
