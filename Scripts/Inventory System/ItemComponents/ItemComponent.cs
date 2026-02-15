using Godot;
using Godot.Collections;

[GlobalClass]
public abstract partial class ItemComponent : Resource
{
	public void SetupCall()
	{
		_setup();
	}

	protected abstract void _setup();
	
	
	public virtual Dictionary<string,Callable> GetContextActions()
	{
		return null;
	}
}