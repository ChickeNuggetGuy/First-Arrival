using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

public interface ICompositeAction
{
	public Action ParentAction { get; set; }
	public List<Action> SubActions { get; set; }
	
}
