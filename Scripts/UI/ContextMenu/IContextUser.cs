using Godot;
using System;
using System.Collections.Generic;

public  interface IContextUser<T> : IContextUserBase
where T : Node
{
	public T parent {get; set;}
}

public  interface IContextUserBase
{
	public Dictionary<String,Callable> GetContextActions();
}
