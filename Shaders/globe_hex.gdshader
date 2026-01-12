shader_type spatial;

// 1. The map that tells us which Hex Index is at this UV coordinate
uniform sampler2D index_map : filter_nearest; 

// 2. The database: X = HexIndex. 
// R = Base Terrain Texture Index
// G = Feature Texture Index (City, Base, etc. - 0 for none)
// B = Owner ID (For borders)
// A = Unused
uniform sampler2D hex_data_map : filter_nearest;

// 3. The Visual Layers (0 = Water, 1 = Grass, 2 = City, 3 = Base, etc.)
uniform sampler2DArray layer_textures : source_color;

uniform float total_hex_count;
uniform vec2 index_map_size;

// Settings
uniform vec4 border_color : source_color = vec4(1.0, 0.0, 0.0, 1.0);
uniform float border_thickness = 0.02;

void fragment() {
	// --- Step 1: Identify the Hex ---
	// Read the ID from the Red channel (32-bit float texture)
	float hex_id_float = texture(index_map, UV).r;
	int hex_index = int(hex_id_float);

	// --- Step 2: Retrieve Hex Data ---
	// Map the index to a UV coordinate for the 1D Data Texture
	vec2 data_uv = vec2((float(hex_index) + 0.5) / total_hex_count, 0.5);
	vec4 hex_data = texture(hex_data_map, data_uv);
	
	int terrain_layer_idx = int(round(hex_data.r * 255.0)); // 0-255
	int feature_layer_idx = int(round(hex_data.g * 255.0)); // 0-255
	int owner_id = int(round(hex_data.b * 255.0));          // 0-255

	// --- Step 3: Layering Logic (Painter's Algorithm) ---
	vec3 final_color = vec3(0.0);
	float alpha = 1.0;
	
	// A. Draw Base Terrain
	vec3 base_col = texture(layer_textures, vec3(UV * 5.0, float(terrain_layer_idx))).rgb; // UV * 5.0 for tiling
	final_color = base_col;

	// B. Draw Feature (City/Base) if it exists (Index > 0)
	if (feature_layer_idx > 0) {
		vec4 feature_tex = texture(layer_textures, vec3(UV, float(feature_layer_idx)));
		// Alpha blend the feature on top
		final_color = mix(final_color, feature_tex.rgb, feature_tex.a);
	}

	// C. Draw Borders (Edge Detection)
	// We check neighbors in screen space or UV space to see if Owner ID changes
	// Simple UV-based neighbor check (Right and Down)
	float pixel_w = 1.0 / index_map_size.x;
	float pixel_h = 1.0 / index_map_size.y;
	
	float neighbor_right = texture(index_map, UV + vec2(pixel_w, 0.0)).r;
	float neighbor_down  = texture(index_map, UV + vec2(0.0, pixel_h)).r;
	
	// If ID changed, check the Owner ID of the neighbor
	if (neighbor_right != hex_id_float || neighbor_down != hex_id_float) {
		// This is an edge of the hex. Now check if it's a country border.
		// Note: For perfect borders, you'd fetch the neighbor's data from hex_data_map
		// But strictly separating hexes is often enough for a "grid" look.
		
		// Fetch neighbor owner
		vec2 n_uv = vec2((neighbor_right + 0.5) / total_hex_count, 0.5);
		int n_owner = int(round(texture(hex_data_map, n_uv).b * 255.0));
		
		if (n_owner != owner_id) {
			final_color = mix(final_color, border_color.rgb, 0.8);
		}
	}

	ALBEDO = final_color;
	ROUGHNESS = 0.8;
}