import argparse
from pathlib import Path
import re
import shutil
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--godot', default=shutil.which('godot') or shutil.which('godot4'))
    parser.add_argument('--full-game', action='store_true', help='Run the full campaign input test in a game window.')
    parser.add_argument('--visible', action='store_true')
    args = parser.parse_args()
    if not args.godot:
        parser.error('Specify --godot with the Godot .NET executable.')
    root = Path(__file__).resolve().parents[1]
    subprocess.run(['dotnet', 'build', '--no-restore', '-v', 'quiet'], cwd=root, check=True)
    with tempfile.TemporaryDirectory(prefix='first-arrival-tutorial-') as directory:
        project = Path(directory)
        for source in root.iterdir():
            if source.name not in ('project.godot', '.git'):
                (project / source.name).symlink_to(source, target_is_directory=source.is_dir())
        settings = (root / 'project.godot').read_text()
        input_settings = settings.split('[input]', 1)[1].split('\n[', 1)[0]
        test_settings = '''config_version=5
[application]
config/name="TutorialRegression"
run/main_scene="res://Tests/TutorialRegression.tscn"
[dotnet]
project/assembly_name="First Arrival"
[rendering]
renderer/rendering_method="gl_compatibility"
[input]
''' + input_settings
        if args.full_game:
            test_settings = re.sub(r'run/main_scene=.*', 'run/main_scene="res://Tests/TutorialGameRegression.tscn"', settings)
        test_settings = re.sub(r'config/name=.*', 'config/name="TutorialRegression"', test_settings)
        (project / 'project.godot').write_text(test_settings)
        result = subprocess.run([
            args.godot, *([] if args.visible or args.full_game else ['--headless']), '--path', str(project),
            '--rendering-method', 'gl_compatibility', '--resolution', '1152x720',
            '--log-file', str(project / 'regression.log')
        ], text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=120)
        print(result.stdout)
        expected = 'PASS: full-game controls tutorial (' if args.full_game else 'PASS: controls tutorial regression ('
        if result.returncode or expected not in result.stdout:
            raise SystemExit(result.returncode or 1)


if __name__ == '__main__':
    main()
