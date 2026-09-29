import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--godot', default=shutil.which('godot') or shutil.which('godot4'))
    parser.add_argument('--ship', action='store_true', help='Check city ship placement and reload using the battle prefabs.')
    args = parser.parse_args()
    if not args.godot:
        parser.error('Specify --godot with the Godot .NET executable.')
    root = Path(__file__).resolve().parents[1]
    subprocess.run(['dotnet', 'build', '--no-restore', '-v', 'quiet'], cwd=root, check=True)
    with tempfile.TemporaryDirectory(prefix='first-arrival-terrain-') as directory:
        project = Path(directory)
        for source in root.iterdir():
            if source.name not in ('project.godot', '.git'):
                (project / source.name).symlink_to(source, target_is_directory=source.is_dir())
        settings = '''config_version=5
[application]
config/name="TerrainChunkSizeRegression"
run/main_scene="res://Tests/TerrainChunkSizeRegression.tscn"
[dotnet]
project/assembly_name="First Arrival"
[rendering]
renderer/rendering_method="gl_compatibility"
'''
        if args.ship:
            settings = settings.replace('TerrainChunkSizeRegression', 'TerrainShipRegression')
        (project / 'project.godot').write_text(settings)
        result = subprocess.run([
            args.godot, '--headless', '--path', str(project),
            '--log-file', str(project / 'regression.log')
        ], text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=60)
        print(result.stdout)
        expected = 'PASS: terrain ship regression.' if args.ship else 'PASS: terrain chunk sizes ('
        if result.returncode or expected not in result.stdout:
            raise SystemExit(result.returncode or 1)


if __name__ == '__main__':
    main()
