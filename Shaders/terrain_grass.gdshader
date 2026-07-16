shader_type spatial;
render_mode blend_mix, depth_prepass_alpha, cull_disabled, diffuse_burley;

uniform sampler3D visibility_texture : filter_nearest, repeat_disable;
uniform vec3 grid_origin_world = vec3(0.0);
uniform vec3 cell_size_world = vec3(1.0);
uniform vec3 visibility_grid_min = vec3(0.0);
uniform vec3 visibility_grid_size = vec3(1.0);

uniform vec4 grass_color : source_color = vec4(0.12, 0.43, 0.08, 1.0);
uniform float explored_brightness : hint_range(0.0, 1.0, 0.01) = 0.35;

varying vec3 fog_world_position;

void vertex() {
	fog_world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	// Taper each quad into a grass blade instead of a visible rectangle.
	// QuadMesh UV.y is 1.0 at the base and 0.0 at the tip.
	float blade_width = mix(0.18, 0.95, UV.y);
	if (abs(UV.x - 0.5) > blade_width * 0.5) {
		discard;
	}

	vec3 grid_cell = floor((fog_world_position - grid_origin_world) / cell_size_world);
	vec3 local_cell = grid_cell - visibility_grid_min;
	if (any(lessThan(local_cell, vec3(0.0))) ||
		any(greaterThanEqual(local_cell, visibility_grid_size))) {
		discard;
	}

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
	ALBEDO = grass_color.rgb * lightness * mix(0.8, 1.15, UV.y);
	ROUGHNESS = 1.0;
}
