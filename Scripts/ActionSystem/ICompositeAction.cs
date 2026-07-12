using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirstArrival.Scripts.Utility;

public interface ICompositeAction
{
	public ActionBase ParentActionBase { get; set; }
	public List<ActionBase> SubActions { get; set; }
	
}
