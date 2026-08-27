using FirstArrival.Scripts.Managers;
using Godot;

[GlobalClass]
public partial class ManagerInstanceCondition : EventCondition
{
	[Export] public string ManagerName { get; private set; } = string.Empty;

	public override bool Check()
	{
		if (string.IsNullOrWhiteSpace(ManagerName)) return false;

		GameManager gameManager = GameManager.Instance;
		if (gameManager == null) return false;
		if (gameManager.GetManagerName() == ManagerName) return true;

		foreach (ManagerBase manager in gameManager.activeSceneManagers)
		{
			if (GodotObject.IsInstanceValid(manager) &&
			    manager.GetManagerName() == ManagerName)
				return true;
		}

		return false;
	}
}
