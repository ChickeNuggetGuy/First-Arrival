using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public abstract partial class UIElement : Control
{
	
	public async Task SetupCall()
	{
		await _Setup();
	}

	protected abstract Task _Setup();
}
