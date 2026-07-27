shader_type spatial;

render_mode blend_mix, depth_draw_never, depth_test_disabled, cull_disabled, unshaded;

uniform vec4 highlight_color : source_color = vec4(0.15, 0.75, 1.0, 0.38);
uniform vec4 border_color : source_color = vec4(0.55, 0.95, 1.0, 0.9);
uniform float border_width : hint_range(0.0, 0.5, 0.005) = 0.08;
uniform float pulse_speed : hint_range(0.0, 10.0, 0.1) = 2.0;
uniform float pulse_amount : hint_range(0.0, 1.0, 0.01) = 0.15;
uniform float grass_height_allowance : hint_range(0.1, 3.0, 0.05) = 0.9;
uniform float horizontal_occlusion_allowance : hint_range(0.1, 5.0, 0.05) = 1.45;
uniform sampler2D depth_texture : hint_depth_texture, repeat_disable, filter_nearest;

varying vec3 highlight_world_position;

void vertex() {
	highlight_world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	// Ignore grass and other short surface detail, but keep the overlay hidden by
	// taller geometry and by terrain that lies between this cell and the camera.
	float depth = textureLod(depth_texture, SCREEN_UV, 0.0).r;

#if CURRENT_RENDERER == RENDERER_COMPATIBILITY
	vec3 ndc = vec3(SCREEN_UV, depth) * 2.0 - 1.0;
#else
	vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, depth);
#endif

	vec4 view_position = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
	view_position.xyz /= view_position.w;
	vec3 scene_world_position =
		(INV_VIEW_MATRIX * vec4(view_position.xyz, 1.0)).xyz;
	float height_above_cell =
		scene_world_position.y - highlight_world_position.y;
	float horizontal_separation = distance(
		scene_world_position.xz,
		highlight_world_position.xz
	);
	if (
		height_above_cell > grass_height_allowance
		|| horizontal_separation > horizontal_occlusion_allowance
	) {
		discard;
	}

	vec2 edge_distance = min(UV, vec2(1.0) - UV);
	float nearest_edge = min(edge_distance.x, edge_distance.y);
	float border = 1.0 - smoothstep(border_width, border_width + 0.015, nearest_edge);
	float pulse = 1.0 - pulse_amount + pulse_amount * (0.5 + 0.5 * sin(TIME * pulse_speed));
	vec4 color = mix(highlight_color, border_color, border);

	ALBEDO = color.rgb;
	ALPHA = color.a * pulse;
}
