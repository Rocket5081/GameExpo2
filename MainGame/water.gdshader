shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_lambert, specular_schlick_ggx;

uniform float wave_speed    : hint_range(0.0, 5.0)  = 1.2;
uniform float wave_height   : hint_range(0.0, 5.0)  = 0.8;
uniform float wave_frequency: hint_range(0.1, 10.0) = 2.5;
uniform float transparency  : hint_range(0.0, 1.0)  = 0.75;

uniform vec4 shallow_color  : source_color = vec4(0.1,  0.05, 0.4,  1.0);
uniform vec4 deep_color     : source_color = vec4(0.02, 0.0,  0.18, 1.0);
uniform vec4 foam_color     : source_color = vec4(0.55, 0.1,  1.0,  1.0);
uniform vec4 specular_color : source_color = vec4(0.8,  0.5,  1.0,  1.0);

float wave(vec2 uv, float speed, float freq, float time) {
    return sin(uv.x * freq + time * speed) * cos(uv.y * freq * 0.8 + time * speed * 0.7);
}

void vertex() {
    float t = TIME;
    float w1 = wave(VERTEX.xz, wave_speed,       wave_frequency,       t);
    float w2 = wave(VERTEX.xz, wave_speed * 0.7, wave_frequency * 1.6, t * 1.3);
    float w3 = wave(VERTEX.xz, wave_speed * 1.3, wave_frequency * 0.7, t * 0.9);
    VERTEX.y += (w1 * 0.6 + w2 * 0.3 + w3 * 0.1) * wave_height;

    // Recompute normal from wave slope
    float eps = 0.1;
    float wx = wave(VERTEX.xz + vec2(eps, 0.0), wave_speed, wave_frequency, t) * wave_height;
    float wz = wave(VERTEX.xz + vec2(0.0, eps), wave_speed, wave_frequency, t) * wave_height;
    NORMAL = normalize(vec3(-wx / eps, 1.0, -wz / eps));
}

void fragment() {
    // Foam at wave peaks
    float foam = smoothstep(0.4, 0.9, (VERTEX.y / wave_height + 1.0) * 0.5);
    vec3 col = mix(deep_color.rgb, shallow_color.rgb, 0.5 + 0.5 * foam);
    col = mix(col, foam_color.rgb, foam * 0.6);

    ALBEDO      = col;
    ALPHA       = transparency;
    METALLIC    = 0.0;
    ROUGHNESS   = 0.05;
    SPECULAR    = 0.9;
    EMISSION    = foam_color.rgb * foam * 0.15;
}