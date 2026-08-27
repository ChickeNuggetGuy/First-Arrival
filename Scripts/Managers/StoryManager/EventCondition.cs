using Godot;
using System;

[GlobalClass]
public abstract partial class EventCondition : Resource
{

	public abstract bool Check();

}
