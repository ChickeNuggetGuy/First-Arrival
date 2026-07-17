shader_type spatial;

render_mode blend_mix, depth_draw_never, cull_disabled, unshaded;

uniform vec4 highlight_color : source_color = vec4(0.15, 0.75, 1.0, 0.38);
uniform vec4 border_color : source_color = vec4(0.55, 0.95, 1.0, 0.9);
uniform float border_width : hint_range(0.0, 0.5, 0.005) = 0.08;
uniform float pulse_speed : hint_range(0.0, 10.0, 0.1) = 2.0;
uniform float pulse_amount : hint_range(0.0, 1.0, 0.01) = 0.15;

void fragment() {
	vec2 edge_distance = min(UV, vec2(1.0) - UV);
	float nearest_edge = min(edge_distance.x, edge_distance.y);
	float border = 1.0 - smoothstep(border_width, border_width + 0.015, nearest_edge);
	float pulse = 1.0 - pulse_amount + pulse_amount * (0.5 + 0.5 * sin(TIME * pulse_speed));
	vec4 color = mix(highlight_color, border_color, border);

	ALBEDO = color.rgb;
	ALPHA = color.a * pulse;
}
