shader_type spatial;
render_mode blend_mix, depth_prepass_alpha, cull_disabled, diffuse_burley,
	world_vertex_coords;

uniform sampler3D visibility_texture : filter_nearest, repeat_disable;
uniform vec3 grid_origin_world = vec3(0.0);
uniform vec3 cell_size_world = vec3(1.0);
uniform vec3 visibility_grid_min = vec3(0.0);
uniform vec3 visibility_grid_size = vec3(1.0);

group_uniforms appearance;
uniform vec4 grass_color : source_color = vec4(0.11, 0.34, 0.055, 1.0);
uniform vec4 grass_tip_color : source_color = vec4(0.32, 0.62, 0.12, 1.0);
uniform float color_variation : hint_range(0.0, 0.5, 0.01) = 0.16;
uniform bool stylized_unshaded = true;
uniform bool use_terrain_base_color = false;
uniform vec4 terrain_base_color : source_color = vec4(0.31, 0.52, 0.31, 1.0);
uniform float blade_curvature : hint_range(0.0, 0.4, 0.005) = 0.13;
uniform float curvature_power : hint_range(0.5, 4.0, 0.05) = 1.65;
uniform float curvature_drop : hint_range(0.0, 1.0, 0.01) = 0.22;

group_uniforms wind;
uniform vec2 wind_direction = vec2(0.9, 0.35);
uniform float wind_strength : hint_range(0.0, 0.5, 0.005) = 0.12;
uniform float wind_speed : hint_range(0.0, 5.0, 0.05) = 1.35;
uniform float wind_scale : hint_range(0.01, 4.0, 0.01) = 0.48;
uniform float gust_strength : hint_range(0.0, 1.0, 0.01) = 0.38;

group_uniforms fog_of_war;
uniform float explored_brightness : hint_range(0.0, 1.0, 0.01) = 0.35;

varying vec3 fog_world_position;
varying float blade_color_variation;

float hash_position(vec2 position) {
	vec3 value = fract(vec3(position.xyx) * 0.1031);
	value += dot(value, value.yzx + 33.33);
	return fract((value.x + value.y) * value.z);
}

void vertex() {
	vec3 instance_origin = MODEL_MATRIX[3].xyz;
	float random_value = hash_position(instance_origin.xz);
	float phase = (random_value + COLOR.a * 0.22) * TAU;
	vec2 direction = wind_direction / max(length(wind_direction), 0.001);

	float spatial_phase = dot(instance_origin.xz, direction) * wind_scale;
	float main_wave = sin(spatial_phase + TIME * wind_speed + phase);
	float gust_wave = sin(
		spatial_phase * 0.37 - TIME * wind_speed * 0.43 + phase * 2.17
	);
	float gust = mix(main_wave, main_wave * gust_wave, gust_strength);

	// The mesh stores a different local bend direction and amount in each card's
	// vertex color, so cards in one clump no longer share the same straight profile.
	vec2 encoded_curve_direction = COLOR.rg * 2.0 - 1.0;
	vec3 local_curve_direction = vec3(
		encoded_curve_direction.x,
		0.0,
		encoded_curve_direction.y
	);
	vec3 curve_direction = normalize(
		mat3(MODEL_MATRIX) * local_curve_direction
	);

	// UV.y is one at the root and zero at the tip. Segmented geometry lets this
	// nonlinear offset form an arc instead of shearing one flat quad.
	float tip_weight = clamp(1.0 - UV.y, 0.0, 1.0);
	float bend = pow(tip_weight, curvature_power);
	float curve_variation = mix(0.55, 1.35, COLOR.b)
		* mix(0.82, 1.18, random_value);
	float curve_offset = blade_curvature * curve_variation * bend;

	float sway = gust * wind_strength
		* mix(0.82, 1.18, random_value)
		* mix(0.88, 1.12, COLOR.a);
	vec3 world_offset = vec3(direction.x, 0.0, direction.y) * sway * bend;
	VERTEX += curve_direction * curve_offset + world_offset;
	VERTEX.y -= curve_offset * tip_weight * curvature_drop;

	// Bias card normals upward so crossed planes receive more even outdoor light.
	NORMAL = normalize(mix(NORMAL, vec3(0.0, 1.0, 0.0), 0.55));
	// Sample fog at the root so a swaying tip does not flicker across grid cells.
	fog_world_position = instance_origin;
	blade_color_variation = random_value;
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
	vec3 base_color = use_terrain_base_color
		? terrain_base_color.rgb
		: grass_color.rgb;
	vec3 blade_color = mix(grass_tip_color.rgb, base_color, UV.y);
	float variation = mix(
		1.0 - color_variation,
		1.0 + color_variation,
		blade_color_variation
	);
	vec3 final_color = blade_color * lightness * variation;
	ALBEDO = stylized_unshaded ? vec3(0.0) : final_color;
	EMISSION = stylized_unshaded ? final_color : vec3(0.0);
	SPECULAR = stylized_unshaded ? 0.0 : 0.5;
	ROUGHNESS = 1.0;
}
