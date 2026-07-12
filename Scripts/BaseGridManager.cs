using Godot;
using System;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;
using Godot.Collections;

[GlobalClass]
public partial class BaseGridManager : Manager<BaseGridManager>
{
	private const int SIZEX = 10;
	private const int SIZEZ = 10;
	private const int CELLSIZE = 15;
	
	private BaseCell[,] cells;
	
	[Export] private Dictionary<String, Mesh> cellMeshes = new Dictionary<string, Mesh>();
	public override string GetManagerName() => "BaseGridManager";

	protected override async Task _Setup(bool loadingData)
	{
		await Task.CompletedTask;
	}

	protected override async Task _Execute(bool loadingData)
	{
		CreateGrid();
		await Task.CompletedTask;
	}


	private void CreateGrid()
	{
		cells = new BaseCell[SIZEX, SIZEZ];
		for (int x = 0; x < SIZEX; x++)
		{
			for (int z = 0; z < SIZEZ; z++)
			{
				cells[x,z] = CreateBaseCell(x,z);
			}
		}
	}

	private BaseCell CreateBaseCell(int x, int z)
	{
		Vector3 pos = new Vector3(x * CELLSIZE, 0, z * CELLSIZE);
		
		BaseCell cell = new BaseCell(x,z, pos, cellMeshes["test"]);
		AddChild(cell);
		return cell;
	}

	public override void Deinitialize()
	{
		return;
	}

	#region Save/Loading

	public override Dictionary<string, Variant> Save()
	{
		return new Dictionary<string, Variant>();
	}

	public override Task Load(Dictionary<string, Variant> data)
	{
		return Task.CompletedTask;
	}

	#endregion
}
