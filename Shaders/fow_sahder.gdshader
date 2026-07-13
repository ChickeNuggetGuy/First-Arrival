shader_type spatial;
render_mode diffuse_burley, cull_back;

// The 3D texture is stored as: image X = grid X, image Y = grid Z, layer = grid Y.
uniform sampler3D visibility_texture : filter_nearest, repeat_disable;
uniform vec3 grid_origin_world = vec3(0.0);
uniform vec3 cell_size_world = vec3(1.0);
uniform vec3 visibility_grid_min = vec3(0.0);
uniform vec3 visibility_grid_size = vec3(1.0);

uniform vec4 terrain_color : source_color = vec4(0.0, 0.5608, 0.2239, 1.0);
uniform float explored_brightness : hint_range(0.0, 1.0, 0.01) = 0.45;

varying vec3 fog_world_position;

void vertex() {
	// Chunks have different local transforms, so sampling must use world space.
	fog_world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec3 grid_cell = floor((fog_world_position - grid_origin_world) / cell_size_world);
	vec3 local_cell = grid_cell - visibility_grid_min;

	// Terrain outside the generated visibility volume is also unexplored.
	if (any(lessThan(local_cell, vec3(0.0))) ||
		any(greaterThanEqual(local_cell, visibility_grid_size))) {
		discard;
	}

	// Texture axes are X, Z, Y; visibility_grid_size is expressed as X, Y, Z.
	vec3 visibility_uv = vec3(
		(local_cell.x + 0.5) / visibility_grid_size.x,
		(local_cell.z + 0.5) / visibility_grid_size.z,
		(local_cell.y + 0.5) / visibility_grid_size.y
	);
	float visibility = texture(visibility_texture, visibility_uv).r;

	if (visibility < 0.25) {
		discard;
	}

	float lightness = visibility < 0.75 ? explored_brightness : 1.0;
	ALBEDO = terrain_color.rgb * lightness;
	ROUGHNESS = 1.0;
}
