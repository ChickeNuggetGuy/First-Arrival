using Godot;

namespace FirstArrival.Scripts.Utility;

public class PhysicsLayer
{

	// Define your collision layers here using bit shifts
	// Layer 1 (index 0)
	public const int DEFAULT = 1 << 0; 
	// Layer 2 (index 1)
	public const int TERRAIN = 1 << 1; // Binary 0000_0010
	// Layer 3 (index 2)
	public const int PLAYER = 1 << 2;  // Binary 0000_0100
	// Layer 4 (index 3)
	public const int ENEMY = 1 << 3;  // Binary 0000_1000
	// ... add more layers as needed

	// Optionally, a helper function to set a single layer value
	// (Though set_collision_layer_value/mask_value are clear enough)
		public static int get_layer_bit(int layerNumber )
		{
			// Converts a 1-based layer number to its bitmask
			if (layerNumber < 1 || layerNumber > 32) // Godot supports up to 32 layers
			{
				GD.PushError("Invalid layer number: ", layerNumber);
				return 0;
			}

			return 1 << (layerNumber - 1);
		}

}