using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using Godot;

public partial class TutorialGameRegression : Node
{
	public override async void _Ready()
	{
		try
		{
			GameManager game = GameManager.Instance;
			await ToSignal(game, GameManager.SignalName.CoreManagersLoaded);
			GetTree().CurrentScene = null;
			Check(await game.StartNewGame(GameManager.GameScene.GlobeScene), "Campaign must load.");
			TutorialManager tutorial = TutorialManager.Instance;
			await Until(() => tutorial.ActivePromptId == "controls_keyboard_pan");
			Check(!StoryManager.Instance.IsEventStarted("intro_alien_invasions"), "Story must not start during controls.");
			if (DisplayServer.GetName() != "headless")
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				GetViewport().GetTexture().GetImage().SavePng("/tmp/first-arrival-tutorial.png");
			}
			Input.ActionPress("cameraRight");
			await Until(() => tutorial.ActivePromptId == "controls_mouse_pan");
			Input.ActionRelease("cameraRight");
			Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			while (tutorial.ActivePromptId == "controls_mouse_pan")
			{
				OrbitalCamera.Instance._UnhandledInput(new InputEventMouseMotion { Relative = new Vector2(2, 0) });
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			}
			Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
			await Until(() => tutorial.ActivePromptId == "controls_context_menu");
			Vector2 center = GetViewport().GetVisibleRect().Size / 2;
			Input.WarpMouse(center);
			Input.ParseInputEvent(new InputEventMouseMotion { Position = center, GlobalPosition = center });
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			Input.ParseInputEvent(new InputEventMouseButton
			{
				ButtonIndex = MouseButton.Right, Pressed = true, Position = center, GlobalPosition = center
			});
			Input.ParseInputEvent(new InputEventMouseButton
			{
				ButtonIndex = MouseButton.Right, Pressed = false, Position = center, GlobalPosition = center
			});
			await Until(() => tutorial.ActivePromptId == "controls_focus_cell");
			ContextMenuUI menu = UIManager.Instance.GetWindow<ContextMenuUI>();
			Check(menu.IsShown, "Right-clicking a globe cell must open its menu.");
			Button focus = FindFocusButton(menu);
			Check(focus != null, "The cell menu must offer Focus.");
			focus.EmitSignal(Button.SignalName.Pressed);
			await Until(() => tutorial.IsEventCompleted("controls_tutorial"));
			await Until(() => StoryManager.Instance.IsEventStarted("intro_alien_invasions"));
			Check(GlobeMissionManager.Instance.GetActiveMissions().Count == 3,
				"Completing controls must launch the opening story missions.");
			GD.Print("PASS: full-game controls tutorial (autoload, real cell menu, focus, story handoff).");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PrintErr(exception);
			GetTree().Quit(1);
		}
	}

	private async Task Until(Func<bool> condition)
	{
		ulong deadline = Time.GetTicksMsec() + 10000;
		while (!condition())
		{
			if (Time.GetTicksMsec() >= deadline) throw new TimeoutException("Tutorial did not advance.");
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
	}

	private static Button FindFocusButton(Node root)
	{
		if (root is Button button && button.Text == "Focus") return button;
		foreach (Node child in root.GetChildren())
		{
			Button found = FindFocusButton(child);
			if (found != null) return found;
		}
		return null;
	}

	private static void Check(bool value, string message)
	{
		if (!value) throw new InvalidOperationException(message);
	}
}
