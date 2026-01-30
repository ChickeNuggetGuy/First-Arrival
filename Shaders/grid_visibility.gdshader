shader_type spatial;
render_mode blend_mix, depth_draw_opaque, world_vertex_coords;

uniform sampler3D visibility_texture : filter_nearest;
uniform vec3 grid_origin_world;
uniform vec2 cell_size_world;
uniform vec3 texture_resolution;
uniform bool flip_z = false;

varying vec3 world_pos_interp;

void vertex() {
    world_pos_interp = VERTEX;
}

void fragment() {
    vec3 relative_pos = world_pos_interp - grid_origin_world;

    
    vec3 uvw;
    
    uvw.x = (relative_pos.x / cell_size_world.x) / texture_resolution.x;
    
    uvw.y = (relative_pos.z / cell_size_world.x) / texture_resolution.y;
    
    uvw.z = (relative_pos.y / cell_size_world.y) / texture_resolution.z;

    if (flip_z) {
        uvw.y = 1.0 - uvw.y;
    }

    uvw = clamp(uvw, vec3(0.0), vec3(1.0));

    vec4 visibility_color = texture(visibility_texture, uvw);
	
    if (visibility_color.r > 0.9)
    {
        ALPHA = 1.0;
    }
    else if (visibility_color.r > 0.4) 
    {
       ALPHA = 0.5; 
    }
	else
	{
		ALPHA = 0.0;
	}
    
    // Debug: Uncomment to visualize coordinates if mesh is still invisible
    // ALBEDO = uvw; ALPHA = 1.0;
}