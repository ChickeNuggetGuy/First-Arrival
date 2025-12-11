using Godot;

namespace FirstArrival.Scripts.Utility;

public class PhysicsLayer
{
	public const int DEFAULT = 1 << 0;    // Layer 1
	public const int TERRAIN = 1 << 1;    // Layer 2
	public const int GRIDOBJECT = 1 << 2;     // Layer 3
	public const int ENEMY = 1 << 3;      // Layer 4
	public const int OBSTACLE = 1 << 4;   // Layer 5
	
	public static int get_layer_bit(int layerNumber)
	{
		if (layerNumber < 1 || layerNumber > 32)
		{
			GD.PushError("Invalid layer number: ", layerNumber);
			return 0;
		}
		return 1 << (layerNumber - 1);
	}
}