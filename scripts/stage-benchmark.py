#!/usr/bin/env python3
"""Run one opt-in development player; never build, alter preferences, or edit smoke scripts."""
import argparse
import json
import plistlib
from pathlib import Path
import shutil
import subprocess
import time
import uuid


def running(name):
    return subprocess.run(['pgrep', '-x', name], capture_output=True, text=True).returncode == 0


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--port', type=int, default=17889)
    parser.add_argument('--timeout', type=float, default=95)
    opts = parser.parse_args()
    app = opts.app.resolve()
    with (app / 'Contents/Info.plist').open('rb') as info:
        executable = app / 'Contents/MacOS' / plistlib.load(info)['CFBundleExecutable']
    output = opts.output.resolve()
    if not executable.is_file() or output.exists():
        parser.error('Need an existing app and a fresh output directory.')
    if not 1 <= opts.port <= 65535 or not 1 <= opts.timeout <= 100:
        parser.error('Port must be valid and timeout must be at most 100 seconds.')
    # pgrep FestivalCoop alone misses the actual product executable 'Festival Co-op Prototype'.
    games = subprocess.run(['pgrep', '-f', r'[F]estival[^\n]*\.app/Contents/MacOS/'], capture_output=True).returncode == 0
    if games or running('FestivalCoop') or running('Festival Co-op Prototype') or running('Unity'):
        parser.error('Quiet machine required: another FestivalCoop or Unity process is running.')
    output.mkdir(parents=True)
    profile = 'stage-benchmark-' + uuid.uuid4().hex[:12]
    log = output / 'player.log'
    command = [str(executable), '--stage-benchmark', '--stage-benchmark-output', str(output),
               '--profile', profile, '--port', str(opts.port), '-screen-width', '1280',
               '-screen-height', '720', '-screen-fullscreen', '0', '-force-metal', '-logFile', str(log)]
    manifest = {'command': command, 'app': str(app), 'profile': profile,
                'port': opts.port, 'timeout_seconds': opts.timeout, 'utc_start': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())}
    (output / 'invocation.json').write_text(json.dumps(manifest, indent=2) + '\n')
    start = time.monotonic()
    error = ''
    process = None
    try:
        with (output / 'stdout.log').open('w') as stdout:
            process = subprocess.Popen(command, stdout=stdout, stderr=subprocess.STDOUT)
            while process.poll() is None:
                text = log.read_text(errors='replace') if log.exists() else ''
                if 'FESTIVAL STAGE BENCHMARK FAILED' in text or 'Exception:' in text:
                    error = 'player error; inspect player.log'
                elif time.monotonic() - start >= opts.timeout:
                    error = 'native timeout'
                elif running('Unity'):
                    error = 'Unity started during the measurement; comparison is invalid'
                if error:
                    process.terminate()
                    break
                time.sleep(.25)
            try:
                status = process.wait(timeout=3)
            except subprocess.TimeoutExpired:
                process.kill()
                status = process.wait()
        metrics = [output / (name + '.json') for name in ('PalmMirage', 'EmberPlaya')]
        if status == 0 and not error and not all(p.is_file() for p in metrics):
            error = 'player exited without both metrics files (driver missing?)'
        manifest.update(exit=status, error=error, wall_seconds=time.monotonic() - start,
                        metrics=[str(p) for p in metrics if p.exists()])
    finally:
        if process is not None and process.poll() is None:
            process.kill()
            process.wait()
        # This exact directory belongs to this fresh invocation. Never delete the output or real profiles.
        if (output / 'profile').is_dir():
            shutil.rmtree(output / 'profile')
        manifest['profile_removed_after_exit'] = not (output / 'profile').exists()
        (output / 'invocation.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print(json.dumps(manifest, indent=2))
    return 0 if manifest.get('exit') == 0 and not manifest.get('error') else 1


if __name__ == '__main__':
    raise SystemExit(main())
