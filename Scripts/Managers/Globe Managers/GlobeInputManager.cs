// using Godot;
// using System;
// using System.Threading.Tasks;
// using FirstArrival.Scripts.Managers;
// using Godot.Collections;
//
// [GlobalClass]
// public partial class GlobeInputManager : Manager<GlobeInputManager>
// {
// 	
// 	
// 	[Export] public Node3D mouseMarker;
// 	public HexCellData? CurrentCell {get; private set;}
// 	
//
//
// 	public override void _PhysicsProcess(double delta)
// 	{
// 		Vector3? mousePos = GetMouseGlobePosition();
//
// 		if (mousePos != null)
// 		{
// 			// Vector2 latLon = GetLatLonFromPosition(mousePos.Value);
//
// 			HexCellData? cell = GlobeHexGridManager.Instance.GetCellFromPosition(mousePos.Value);
//
// 			if (cell != null)
// 			{
// 				CurrentCell = cell;
// 			}
// 		}
// 		else
// 		{
// 		}
//
// 		if (CurrentCell != null)
// 		{
// 			mouseMarker.GlobalPosition = CurrentCell.Value.Center;
// 		}
// 	}
//
// 	public override string GetManagerName() => "GlobeInputManager";
//
// 	protected override async Task _Setup(bool loadingData)
// 	{
// 		return;
// 		throw new NotImplementedException();
// 	}
//
// 	protected override async Task _Execute(bool loadingData)
// 	{
// 		return;
// 	}
//
// 	public override Dictionary<string, Variant> Save()
// 	{
// 		return new Dictionary<string, Variant>();
// 	}
//
// 	public override void Load(Dictionary<string, Variant> data)
// 	{
// 		return;
// 	}
//
//
// 	
// 	public override void Deinitialize()
// 	{
// 		return;
// 	}
//
// 	
// }
