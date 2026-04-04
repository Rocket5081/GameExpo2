shader_type sky;
render_mode use_half_res_pass;

uniform float nebula_speed    : hint_range(0.0, 1.0)  = 0.12;
uniform float lightning_speed : hint_range(0.0, 5.0)  = 1.8;
uniform float cloud_coverage  : hint_range(0.0, 1.0)  = 0.55;
uniform float cloud_sharpness : hint_range(1.0, 8.0)  = 3.5;

uniform vec4 sky_top         : source_color = vec4(0.04, 0.0,  0.12, 1.0);
uniform vec4 sky_mid         : source_color = vec4(0.25, 0.0,  0.55, 1.0);
uniform vec4 sky_horizon     : source_color = vec4(0.6,  0.05, 0.88, 1.0);
uniform vec4 cloud_dark      : source_color = vec4(0.02, 0.0,  0.05, 1.0);
uniform vec4 cloud_mid       : source_color = vec4(0.22, 0.0,  0.5,  1.0);
uniform vec4 cloud_bright    : source_color = vec4(0.55, 0.05, 0.92, 1.0);
uniform vec4 cloud_lit       : source_color = vec4(0.8,  0.3,  1.0,  1.0);
uniform vec4 lightning_color : source_color = vec4(1.0,  0.9,  1.0,  1.0);
uniform vec4 star_color      : source_color = vec4(1.0,  0.9,  1.0,  1.0);

float hash(float n)  { return fract(sin(n) * 43758.5453); }
float hash2(vec2 p)  { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

float noise(vec2 p) {
    vec2 i = floor(p); vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash2(i),           hash2(i+vec2(1,0)), f.x),
               mix(hash2(i+vec2(0,1)), hash2(i+vec2(1,1)), f.x), f.y);
}

// Domain-warped FBM — this is what makes clouds look realistic instead of flat
float fbm(vec2 p, int octaves) {
    float v = 0.0; float a = 0.5;
    vec2 shift = vec2(100.0);
    mat2 rot = mat2(vec2(cos(0.5), sin(0.5)), vec2(-sin(0.5), cos(0.5)));
    for (int i = 0; i < 8; i++) {
        if (i >= octaves) break;
        v += a * noise(p);
        p  = rot * p * 2.1 + shift;
        a *= 0.48;
    }
    return v;
}

float domain_warp_fbm(vec2 p) {
    // First pass: compute warp offset
    vec2 q = vec2(fbm(p + vec2(0.0, 0.0), 5),
                  fbm(p + vec2(5.2, 1.3), 5));
    // Second pass: warp the domain using q
    vec2 r = vec2(fbm(p + 3.5*q + vec2(1.7, 9.2), 5),
                  fbm(p + 3.5*q + vec2(8.3, 2.8), 5));
    // Final pass: sample with doubly-warped domain
    return fbm(p + 3.5*r, 6);
}

float stars(vec2 uv) {
    vec2 cell  = floor(uv * 220.0);
    vec2 local = fract(uv * 220.0);
    float rng  = hash2(cell);
    if (rng > 0.07) return 0.0;
    vec2 pos   = vec2(hash(rng), hash(rng+0.3)) * 0.6 + 0.2;
    float twinkle = 0.55 + 0.45 * sin(TIME * (1.5 + rng*5.0) + rng*6.28);
    return smoothstep(0.06, 0.0, length(local-pos)) * twinkle;
}

float lightning_bolt(vec2 uv, float seed) {
    float dist = 1e9;
    vec2 cur = vec2(hash(seed)*0.6 + 0.2, 0.95);
    for (int i = 0; i < 10; i++) {
        vec2 next = cur + vec2(
            (hash(seed + float(i)*3.7) - 0.5) * 0.07,
            -0.08 - hash(seed + float(i)*1.3) * 0.07
        );
        vec2 pa = uv-cur, ba = next-cur;
        float h = clamp(dot(pa,ba)/dot(ba,ba), 0.0, 1.0);
        dist = min(dist, length(pa - ba*h));
        if (mod(float(i), 3.0) < 1.0) {
            vec2 tip = cur + vec2(
                (hash(seed+float(i)*7.1)-0.5)*0.05,
                -hash(seed+float(i)*2.9)*0.06
            );
            vec2 pb = uv-cur, bb = tip-cur;
            float hb = clamp(dot(pb,bb)/dot(bb,bb), 0.0, 1.0);
            dist = min(dist, length(pb - bb*hb) * 1.5);
        }
        cur = next;
    }
    return dist;
}

void sky() {
    vec3 dir = normalize(EYEDIR);
    float up = clamp(dir.y, -1.0, 1.0);

    // ── Base gradient ─────────────────────────────────────────────────────
    float t1 = smoothstep(-0.15, 0.5, up);
    float t2 = smoothstep(0.3,   1.0, up);
    vec4 base = mix(sky_horizon, sky_mid, t1);
    base = mix(base, sky_top, t2);

    // ── Cloud UV with slow drift ──────────────────────────────────────────
    vec2 nuv = vec2(
        atan(dir.z, dir.x) / 6.28318 + 0.5,
        dir.y * 0.5 + 0.5
    );
    // Perspective stretch — clouds flatten toward horizon naturally
    nuv.y = pow(max(nuv.y, 0.0), 0.6);
    nuv.x += TIME * nebula_speed * 0.006;
    nuv.y += TIME * nebula_speed * 0.002;

    // ── Domain-warped cloud density ───────────────────────────────────────
    float density = domain_warp_fbm(nuv * 2.2);

    // Remap: cloud_coverage controls how much sky is covered
    float cloud = smoothstep(cloud_coverage - 0.1, cloud_coverage + 0.25, density);
    // Sharp inner core vs soft edges
    float cloud_core  = smoothstep(cloud_coverage + 0.1, cloud_coverage + 0.4, density);
    float cloud_edge  = cloud - cloud_core;
    float cloud_wisp  = smoothstep(cloud_coverage - 0.2, cloud_coverage, density) * (1.0 - cloud);

    // Only show clouds above horizon
    float above = smoothstep(-0.05, 0.15, up);
    cloud      *= above;
    cloud_core *= above;
    cloud_edge *= above;
    cloud_wisp *= above;

    // ── Layer the cloud colors for depth ──────────────────────────────────
    // Dark mass (shadowed interior)
    base = mix(base, cloud_dark,   cloud_core * 0.92);
    // Mid tone body
    base = mix(base, cloud_mid,    cloud * 0.5);
    // Bright lit edges (lightning/ambient scattering)
    base = mix(base, cloud_bright, cloud_edge * 0.8);
    // Ultra-bright specular rim — lit directly like in reference image
    float rim_density = domain_warp_fbm(nuv * 2.2 + vec2(0.3, 0.7));
    float rim = smoothstep(cloud_coverage + 0.28, cloud_coverage + 0.35, rim_density) * above;
    base = mix(base, cloud_lit, rim * 0.9);
    // Wispy transparent edges
    base = mix(base, cloud_mid * vec4(1,1,1,0.4), cloud_wisp * 0.35);

    // ── Secondary smaller cloud layer for depth ───────────────────────────
    float density2  = fbm(nuv * 4.5 + vec2(3.1, 7.4), 5);
    float cloud2    = smoothstep(0.58, 0.72, density2) * above * 0.6;
    base = mix(base, cloud_dark * vec4(0.5,0.5,0.5,1), cloud2 * 0.4);
    base = mix(base, cloud_bright, cloud2 * 0.25);

    // ── Stars in clear gaps ───────────────────────────────────────────────
    float star_uv_x = atan(dir.z, dir.x) / 6.28318 + 0.5;
    float star_uv_y = acos(clamp(dir.y, -1.0, 1.0)) / 3.14159;
    float s = stars(vec2(star_uv_x, star_uv_y));
    s *= (1.0 - cloud) * (1.0 - cloud2) * smoothstep(0.0, 0.2, up);
    base.rgb += star_color.rgb * s;

    // ── Lightning ─────────────────────────────────────────────────────────
    for (float i = 0.0; i < 3.0; i++) {
        float seed   = i * 47.3 + 12.1;
        float period = 4.0 + hash(seed) * 5.0;
        float phase  = hash(seed + 1.7) * period;
        float t = mod(TIME * lightning_speed * 0.35 + phase, period);
        float flash = smoothstep(0.0, 0.04, t) * smoothstep(0.2, 0.08, t);
        if (flash < 0.001) continue;

        vec2 luv = vec2(atan(dir.z, dir.x) / 6.28318 + 0.5,
                        1.0 - acos(clamp(dir.y,-1.0,1.0)) / 3.14159);
        float d    = lightning_bolt(luv, seed + floor(TIME*0.2)*137.0);
        float glow = smoothstep(0.05, 0.0, d) * flash;
        float core = smoothstep(0.005, 0.0, d) * flash;

        // Clouds light up around lightning
        base.rgb += lightning_color.rgb * glow * 1.5 * (0.5 + cloud * 0.8);
        base.rgb += vec3(1.0) * core * 3.0;
        // Full sky ambient flash
        base.rgb += lightning_color.rgb * flash * 0.08;
    }

    // ── Horizon glow ──────────────────────────────────────────────────────
    float hglow = smoothstep(0.4, -0.1, abs(up + 0.05));
    base.rgb += vec3(0.45, 0.02, 0.85) * hglow * 0.55;

    COLOR = base.rgb;
}
