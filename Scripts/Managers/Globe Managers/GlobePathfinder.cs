using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstArrival.Scripts.Managers;

[GlobalClass]
public partial class GlobePathfinder : Manager<GlobePathfinder>
{
	#region Fields & Properties

	private int? fromCellIndex = null;
	private int? toCellIndex = null;
	
	private int[][] _neighborMap;
	private GlobeHexGridManager _gridManager;
	#endregion

	#region Manager Lifecycle

	public override string GetManagerName() => "GlobePathfinder";

	protected override async Task _Setup(bool loadingData)
	{
		_gridManager = GlobeHexGridManager.Instance;
		
		if (!_gridManager.SetupComplete)
			await ToSignal(_gridManager, "SetupCompleted");

		GenerateNeighborMap();
		
		SetupComplete = true;
		EmitSignal(SignalName.SetupCompleted);
	}

	protected override async Task _Execute(bool loadingData)
	{
		ExecuteComplete = true;
		EmitSignal(SignalName.ExecuteCompleted);
		await Task.CompletedTask;
	}

	
	public override void Deinitialize()
	{
		return;
	}
	public override Godot.Collections.Dictionary<string, Variant> Save() => new();
	public override void Load(Godot.Collections.Dictionary<string, Variant> data) { }

	#endregion

	#region Optimized Grid Analysis

	private void GenerateNeighborMap()
	{
		int count = _gridManager.GetTotalCells();
		_neighborMap = new int[count][];

		// Parallel generation using "Edge Nudging"
		Parallel.For(0, count, i =>
		{
			var current = _gridManager.GetCellFromIndex(i);
			List<int> neighbors = new();
			Vector3[] corners = current.Value.Corners;

			// Iterate through edges (pairs of corners)
			for (int j = 0; j < corners.Length; j++)
			{
				Vector3 c1 = corners[j];
				Vector3 c2 = corners[(j + 1) % corners.Length];
				
				// Find midpoint of the edge
				Vector3 edgeMid = (c1 + c2) * 0.5f;
				
				// Create a direction from center through edge midpoint
				Vector3 dirAcrossEdge = (edgeMid - current.Value.Center).Normalized();
				
				// Nudge the probe point slightly across the boundary (0.1 units)
				Vector3 probePoint = edgeMid + (dirAcrossEdge * 0.05f);
				
				var neighbor = _gridManager.GetCellFromPosition(probePoint);
				
				if (neighbor != null && neighbor.Value.Index != i)
				{
					if (!neighbors.Contains(neighbor.Value.Index))
						neighbors.Add(neighbor.Value.Index);
				}
			}
			_neighborMap[i] = neighbors.ToArray();
		});

		GD.Print($"{GetManagerName()}: Neighbor Map Generated for {count} cells.");
	}

	#endregion

	#region Pathfinding API

	public List<int> GetPath(int startIdx, int endIdx)
	{
		if (startIdx == endIdx) return new List<int> { startIdx };

		// Using PriorityQueue for O(log n) efficiency
		var openSet = new PriorityQueue<int, float>();
		var cameFrom = new Dictionary<int, int>();
		var gScore = new Dictionary<int, float>(); 
		
		openSet.Enqueue(startIdx, 0);
		gScore[startIdx] = 0;

		while (openSet.Count > 0)
		{
			int current = openSet.Dequeue();

			if (current == endIdx) return ReconstructPath(cameFrom, current);

			foreach (int neighbor in _neighborMap[current])
			{
				// Cost is 1 per hop (can be weighted by terrain later)
				float tentativeGScore = gScore[current] + 1; 

				if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
				{
					cameFrom[neighbor] = current;
					gScore[neighbor] = tentativeGScore;
					
					Vector3 posA = _gridManager.GetCellFromIndex(neighbor).Value.Center;
					Vector3 posB = _gridManager.GetCellFromIndex(endIdx).Value.Center;
					
					// Admissible heuristic: Euclidean distance
					float fScore = tentativeGScore + posA.DistanceTo(posB);
					openSet.Enqueue(neighbor, fScore);
				}
			}
		}
		return null;
	}

	#endregion

	#region Input & Debug

	public override void _Input(InputEvent @event)
	{
		var inputManager = InputManager.Instance;
		if (_gridManager == null || inputManager == null || inputManager.CurrentCell == null) return;

		if (DebugMode)
		{
			// Handle selection
			if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
			{
				if (mouseEvent.ButtonIndex == MouseButton.Left)
				{
					fromCellIndex = inputManager.CurrentCell.Value.Index;
					GD.Print($"Path Start Set: {fromCellIndex}");
				}
				else if (mouseEvent.ButtonIndex == MouseButton.Right)
				{
					toCellIndex = inputManager.CurrentCell.Value.Index;
					GD.Print($"Path End Set: {toCellIndex}");
				}
			}

			// Handle Execution
			if (fromCellIndex.HasValue && toCellIndex.HasValue)
			{
				if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Space)
				{
					var path = GetPath(fromCellIndex.Value, toCellIndex.Value);

					if (path != null)
					{
						GD.Print($"Path found! Length: {path.Count}");
						foreach (int cellIdx in path)
						{
							var cell = _gridManager.GetCellFromIndex(cellIdx);
							// Visual alignment offset
							Vector3 displayPos = ApplyOffset(cell.Value.Center);

							DebugDraw3D.DrawBox(displayPos, Quaternion.Identity,
								Vector3.One * 0.15f, Colors.Chartreuse, true, 10.0f);
						}
					}
					else
					{
						GD.PrintErr("Pathfinder: No path exists between selected cells.");
					}
				}
			}
		}
	}

	/// <summary>
	/// Aligns the pathfinding visual center with your mesh's Lat/Lon rotation.
	/// </summary>
	private Vector3 ApplyOffset(Vector3 position)
	{
		GlobeHexGridManager hexGridManager = GlobeHexGridManager.Instance;
		if (hexGridManager == null) return Vector3.Zero;
		
		Vector3 norm = position.Normalized();
		float lat = Mathf.RadToDeg(Mathf.Asin(norm.Y));
		float lon = Mathf.RadToDeg(Mathf.Atan2(norm.X, norm.Z));
		
		float rLat = Mathf.DegToRad(lat);
		float rLon = Mathf.DegToRad(lon);
		
		return new Vector3(
			Mathf.Cos(rLat) * Mathf.Sin(rLon),
			Mathf.Sin(rLat),
			Mathf.Cos(rLat) * Mathf.Cos(rLon)
		) * _gridManager.Radius;
	}

	private List<int> ReconstructPath(Dictionary<int, int> cameFrom, int current)
	{
		List<int> path = new() { current };
		while (cameFrom.ContainsKey(current))
		{
			current = cameFrom[current];
			path.Add(current);
		}
		path.Reverse();
		return path;
	}

	#endregion
}