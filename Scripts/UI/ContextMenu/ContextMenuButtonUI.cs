using Godot;
using System;

[GlobalClass]
public partial class ContextMenuButtonUI : Button
{
	public Callable callable {get; private set;}

	public void Init(Callable callable, string name)
	{
		Text = name;
		this.callable = callable;
		Pressed += OnPressed;
	}

	private void OnPressed()
	{
		callable.Call();
	}
}
