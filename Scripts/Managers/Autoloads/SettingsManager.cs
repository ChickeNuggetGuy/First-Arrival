using System.Threading.Tasks;
using Godot;
using Godot.Collections;

namespace FirstArrival.Scripts.Managers;

public partial class SettingsManager : Manager<SettingsManager>
{
	[Export(PropertyHint.Range, "1,8,1")]
	public int animationSpeed = 1;

	[Export(PropertyHint.Range, "1,8,1")]
	public int projectileSpeed = 2;

	/// <summary>
	/// Runtime-safe animation multiplier. A positive minimum prevents awaited
	/// tweens from being paused forever by an invalid zero value.
	/// </summary>
	public float AnimationSpeedMultiplier => Mathf.Max(animationSpeed, 1);

	public override string GetManagerName() => "SettingsManager";

	protected override Task _Setup(bool loadingData)
	{
		return Task.CompletedTask;
	}

	protected override Task _Execute(bool loadingData)
	{
		return Task.CompletedTask;
	}

	public override Dictionary<string, Variant> Save()
	{
		return new Dictionary<string, Variant>
		{
			{ nameof(animationSpeed), animationSpeed },
			{ nameof(projectileSpeed), projectileSpeed }
		};
	}

	public override Task Load(Dictionary<string, Variant> data)
	{
		if (data == null)
			return Task.CompletedTask;

		if (data.TryGetValue(nameof(animationSpeed), out Variant savedAnimationSpeed))
			animationSpeed = Mathf.Max(savedAnimationSpeed.AsInt32(), 1);
		if (data.TryGetValue(nameof(projectileSpeed), out Variant savedProjectileSpeed))
			projectileSpeed = Mathf.Max(savedProjectileSpeed.AsInt32(), 1);

		return Task.CompletedTask;
	}

	public override void Deinitialize()
	{
	}
}
