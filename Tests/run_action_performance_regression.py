"""Run against imported project assets and a Godot .NET build, without game autoloads.

Usage: python3 Tests/run_action_performance_regression.py --godot /path/to/Godot
Requires Python 3, dotnet, and the project's matching Godot .NET version.
"""
import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--benchmark-only', action='store_true')
    parser.add_argument('--godot', default=shutil.which('godot') or shutil.which('godot4'))
    args = parser.parse_args()
    if not args.godot:
        parser.error('Specify --godot with the path to the Godot .NET executable.')
    root = Path(__file__).resolve().parents[1]
    subprocess.run(['dotnet', 'build', '--no-restore', '-p:UseSharedCompilation=false'], cwd=root, check=True)
    with tempfile.TemporaryDirectory(prefix='first-arrival-action-perf-') as directory:
        project = Path(directory)
        for source in root.iterdir():
            if source.name not in ('project.godot', '.git'):
                (project / source.name).symlink_to(source, target_is_directory=source.is_dir())
        (project / 'project.godot').write_text('''config_version=5
[application]
config/name="ActionPerformanceRegression"
run/main_scene="res://Tests/ActionPerformanceRegression.tscn"
[dotnet]
project/assembly_name="First Arrival"
[rendering]
renderer/rendering_method="gl_compatibility"
''')
        result = subprocess.run([
            args.godot, '--headless', '--path', str(project),
            '--max-fps', '120', '--log-file', str(project / 'regression.log'),
            *(['--', '--benchmark-only'] if args.benchmark_only else [])
        ], text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=120)
        print(result.stdout)
        if result.returncode or 'PASS: action performance regression.' not in result.stdout:
            raise SystemExit(result.returncode or 1)


if __name__ == '__main__':
    main()
