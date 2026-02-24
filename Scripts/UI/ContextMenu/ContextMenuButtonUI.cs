using Godot;
using System;

[GlobalClass]
public partial class ContextMenuButtonUI : Button
{
	protected ContextMenuUI contextMenuUI;
	public Callable callable {get; private set;}

	public void Init(ContextMenuUI contextMenuUI, Callable callable, string name)
	{
		Text = name;
		this.callable = callable;
		this.contextMenuUI = contextMenuUI;
		Pressed += OnPressed;
	}

	private void OnPressed()
	{
		callable.Call();
		contextMenuUI.HideCall();
	}
}
