shader_type sky;
render_mode use_half_res_pass;

uniform float star_density  : hint_range(0.0, 1.0)  = 0.6;
uniform float nebula_speed  : hint_range(0.0, 1.0)  = 0.04;
uniform float fog_horizon   : hint_range(-1.0, 1.0) = -0.1;

uniform vec4 sky_top        : source_color = vec4(0.01, 0.0,  0.06, 1.0);
uniform vec4 sky_horizon    : source_color = vec4(0.08, 0.0,  0.22, 1.0);
uniform vec4 nebula_color_a : source_color = vec4(0.3,  0.0,  0.7,  1.0);
uniform vec4 nebula_color_b : source_color = vec4(0.05, 0.0,  0.18, 1.0);
uniform vec4 star_color     : source_color = vec4(0.85, 0.65, 1.0,  1.0);

float hash(float n)  { return fract(sin(n) * 43758.5453); }
float hash2(vec2 p)  { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

float noise(vec2 p) {
    vec2 i = floor(p); vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash2(i), hash2(i+vec2(1,0)), f.x),
               mix(hash2(i+vec2(0,1)), hash2(i+vec2(1,1)), f.x), f.y);
}

float fbm(vec2 p) {
    float v = 0.0; float a = 0.5;
    for (int i = 0; i < 5; i++) {
        v += a * noise(p);
        p  = p * 2.1 + vec2(1.7, 9.2);
        a *= 0.5;
    }
    return v;
}

float stars(vec2 uv, float threshold) {
    vec2 cell = floor(uv * 180.0);
    vec2 local = fract(uv * 180.0);
    float rng = hash2(cell);
    if (rng > threshold) return 0.0;
    vec2 pos = vec2(hash(rng), hash(rng + 0.3)) * 0.6 + 0.2;
    float dist = length(local - pos);
    float twinkle = 0.7 + 0.3 * sin(TIME * (2.0 + rng * 4.0) + rng * 6.28);
    return smoothstep(0.07, 0.0, dist) * twinkle;
}

void sky() {
    // Direction-based UV for the sky dome
    vec3 dir = EYEDIR;
    float up  = clamp(dir.y, -1.0, 1.0);

    // Base gradient: horizon (purple) to top (near black)
    float t = smoothstep(fog_horizon, 0.6, up);
    vec4 base = mix(sky_horizon, sky_top, t);

    // Nebula clouds using FBM noise
    vec2 nuv = vec2(atan(dir.z, dir.x) / 6.28318 + 0.5,
                    dir.y * 0.5 + 0.5);
    nuv += vec2(TIME * nebula_speed * 0.01, 0.0);

    float n1 = fbm(nuv * 2.5);
    float n2 = fbm(nuv * 4.0 + vec2(5.2, 1.3));
    float nebula = smoothstep(0.3, 0.75, n1 * 0.6 + n2 * 0.4);

    base = mix(base, nebula_color_a, nebula * 0.55 * clamp(up + 0.3, 0.0, 1.0));
    base = mix(base, nebula_color_b, (1.0 - nebula) * 0.2);

    // Stars — only above horizon
    float star_uv_x = atan(dir.z, dir.x) / 6.28318 + 0.5;
    float star_uv_y = acos(clamp(dir.y, -1.0, 1.0)) / 3.14159;
    float s = stars(vec2(star_uv_x, star_uv_y), star_density * 0.15);
    s *= smoothstep(0.0, 0.15, up); // fade stars near horizon

    base.rgb += star_color.rgb * s;

    // Subtle pulsing void glow at horizon
    float glow = smoothstep(0.25, -0.1, abs(up - fog_horizon));
    base.rgb += vec3(0.15, 0.0, 0.35) * glow * 0.4;

    COLOR = base.rgb;
}
