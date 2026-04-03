shader_type canvas_item;

uniform float speed        : hint_range(0.05, 2.0) = 0.18;
uniform float rune_count   : hint_range(3.0, 16.0) = 8.0;
uniform float rune_size    : hint_range(0.02, 0.2)  = 0.07;
uniform float glow_radius  : hint_range(1.0, 8.0)   = 3.5;

uniform vec4 void_dark     : source_color = vec4(0.01, 0.0,  0.04, 1.0);
uniform vec4 void_mid      : source_color = vec4(0.03, 0.01, 0.10, 1.0);
uniform vec4 rune_glow     : source_color = vec4(0.4,  0.0,  0.9,  1.0);
uniform vec4 rune_core     : source_color = vec4(0.85, 0.6,  1.0,  1.0);
uniform vec4 rune_dim      : source_color = vec4(0.15, 0.0,  0.35, 1.0);

// ── Helpers ───────────────────────────────────────────────────────────────────
float hash(float n)  { return fract(sin(n) * 43758.5453123); }
float hash2f(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

float noise(vec2 p) {
    vec2 i = floor(p); vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash2f(i), hash2f(i+vec2(1,0)), f.x),
               mix(hash2f(i+vec2(0,1)), hash2f(i+vec2(1,1)), f.x), f.y);
}

// ── SDF primitives for building rune strokes ──────────────────────────────────
float sd_line(vec2 p, vec2 a, vec2 b) {
    vec2 pa = p-a, ba = b-a;
    return length(pa - ba * clamp(dot(pa,ba)/dot(ba,ba), 0.0, 1.0));
}

float sd_circle(vec2 p, vec2 c, float r) {
    return abs(length(p-c) - r);
}

float sd_arc(vec2 p, vec2 c, float r, float a0, float a1) {
    // distance to arc segment (not full circle)
    vec2 d = p - c;
    float angle = atan(d.y, d.x);
    // clamp angle to arc range
    float mid = (a0 + a1) * 0.5;
    float half = (a1 - a0) * 0.5;
    float da = angle - mid;
    da = da - 6.28318 * floor((da + 3.14159) / 6.28318); // wrap
    float clamped = mid + clamp(da, -half, half);
    vec2 closest = c + r * vec2(cos(clamped), sin(clamped));
    return length(p - closest);
}

// ── Rune definitions — 6 distinct symbols, chosen by index ────────────────────
float rune_sdf(vec2 p, int type, float sz) {
    float d = 1e9;
    float s = sz;

    if (type == 0) {
        // Elder Eye: outer circle + inner triangle
        d = min(d, sd_circle(p, vec2(0.0), s));
        d = min(d, sd_line(p, vec2(0.0, s*0.55), vec2(-s*0.48, -s*0.28)));
        d = min(d, sd_line(p, vec2(-s*0.48, -s*0.28), vec2(s*0.48, -s*0.28)));
        d = min(d, sd_line(p, vec2(s*0.48, -s*0.28), vec2(0.0, s*0.55)));
        // pupil dot approximated as tiny circle
        d = min(d, sd_circle(p, vec2(0.0), s * 0.15));
    }
    else if (type == 1) {
        // Void Cross: two crossed lines + outer diamond
        d = min(d, sd_line(p, vec2(0.0, s), vec2(0.0, -s)));
        d = min(d, sd_line(p, vec2(-s, 0.0), vec2(s, 0.0)));
        d = min(d, sd_line(p, vec2(0.0, s), vec2(s, 0.0)));
        d = min(d, sd_line(p, vec2(s, 0.0), vec2(0.0, -s)));
        d = min(d, sd_line(p, vec2(0.0, -s), vec2(-s, 0.0)));
        d = min(d, sd_line(p, vec2(-s, 0.0), vec2(0.0, s)));
    }
    else if (type == 2) {
        // Sigil of Binding: circle + 3 radiating spokes + small circles at tips
        d = min(d, sd_circle(p, vec2(0.0), s * 0.5));
        for (int k = 0; k < 3; k++) {
            float a = float(k) * 2.09440; // 120 deg apart
            vec2 tip = vec2(cos(a), sin(a)) * s;
            vec2 base = vec2(cos(a), sin(a)) * s * 0.5;
            d = min(d, sd_line(p, base, tip));
            d = min(d, sd_circle(p, tip, s * 0.12));
        }
    }
    else if (type == 3) {
        // Star of Madness: 6-pointed star (two triangles)
        float r1 = s; float r2 = s * 0.5;
        for (int k = 0; k < 3; k++) {
            float a  = float(k) * 2.09440;
            float a2 = a + 1.04720;
            vec2 pa = vec2(cos(a),  sin(a))  * r1;
            vec2 pb = vec2(cos(a2), sin(a2)) * r1;
            // triangle 1
            d = min(d, sd_line(p, pa, pb));
            // triangle 2 (rotated 60)
            vec2 qa = vec2(cos(a + 1.0472),  sin(a + 1.0472))  * r1;
            vec2 qb = vec2(cos(a2 + 1.0472), sin(a2 + 1.0472)) * r1;
            d = min(d, sd_line(p, qa, qb));
        }
        d = min(d, sd_circle(p, vec2(0.0), s * 0.18));
    }
    else if (type == 4) {
        // Spiral Rune: two concentric partial arcs + a stem
        d = min(d, sd_arc(p, vec2(0.0), s * 0.7, 0.0, 4.71239));       // 270-deg arc
        d = min(d, sd_arc(p, vec2(0.0), s * 0.4, 0.5, 3.64159));       // smaller arc
        d = min(d, sd_line(p, vec2(s*0.7, 0.0), vec2(s*0.7, -s*0.4))); // stem
    }
    else {
        // Abyss Gate: rectangle frame + X inside
        d = min(d, sd_line(p, vec2(-s, -s*0.6), vec2(s, -s*0.6)));
        d = min(d, sd_line(p, vec2(s, -s*0.6), vec2(s, s*0.6)));
        d = min(d, sd_line(p, vec2(s, s*0.6), vec2(-s, s*0.6)));
        d = min(d, sd_line(p, vec2(-s, s*0.6), vec2(-s, -s*0.6)));
        d = min(d, sd_line(p, vec2(-s, -s*0.6), vec2(s, s*0.6)));
        d = min(d, sd_line(p, vec2(s, -s*0.6), vec2(-s, s*0.6)));
    }
    return d;
}

// ── Main ──────────────────────────────────────────────────────────────────────
void fragment() {
    vec2 uv = UV;

    // --- Void background nebula ---
    float n1 = noise(uv * 2.5 + vec2(TIME * speed * 0.04, 0.0));
    float n2 = noise(uv * 5.0 - vec2(0.0, TIME * speed * 0.06));
    float n3 = noise(uv * 10.0 + TIME * speed * 0.02);
    float nebula = n1 * 0.5 + n2 * 0.3 + n3 * 0.2;

    vec4 bg = mix(void_dark, void_mid, nebula);

    // Deep purple bloom blobs
    float blob1 = smoothstep(0.6, 0.8, noise(uv * 1.8 + TIME * speed * 0.03));
    float blob2 = smoothstep(0.6, 0.8, noise(uv * 3.2 - TIME * speed * 0.025));
    bg = mix(bg, vec4(0.07, 0.0, 0.18, 1.0), blob1 * 0.5);
    bg = mix(bg, vec4(0.02, 0.0, 0.12, 1.0), blob2 * 0.4);

    // --- Runes ---
    for (float i = 0.0; i < 16.0; i++) {
        if (i >= rune_count) break;

        float seed = i * 31.17 + 5.3;

        // Each rune has a fixed position, slow drift, and its own fade phase
        vec2 pos = vec2(hash(seed), hash(seed + 1.7));

        // Slow wandering drift
        float drift_speed = speed * 0.06;
        pos.x += sin(TIME * drift_speed + hash(seed + 2.3) * 6.28) * 0.04;
        pos.y += cos(TIME * drift_speed + hash(seed + 3.1) * 6.28) * 0.04;
        pos = fract(pos); // wrap at edges

        // Fade in/out — each rune has its own slow phase
        float phase  = hash(seed + 4.5) * 6.28318;
        float period = 4.0 + hash(seed + 5.9) * 6.0;  // 4–10 second cycle
        float fade   = 0.5 + 0.5 * sin(TIME * (6.28318 / period) + phase);
        fade = smoothstep(0.15, 0.7, fade); // sharpen the fade curve

        // Pick rune type
        int rtype = int(mod(i, 6.0));

        // Slow rotation per rune
        float rot_speed = (hash(seed + 6.2) - 0.5) * speed * 0.3;
        float rot = TIME * rot_speed + hash(seed + 7.1) * 6.28318;
        float cr = cos(rot); float sr = sin(rot);

        // Transform UV into rune local space
        vec2 local = uv - pos;
        // Aspect correction (assume ~16:9 screen)
        local.x *= 1.777;
        // Rotate
        local = vec2(cr * local.x - sr * local.y,
                     sr * local.x + cr * local.y);

        // SDF distance for this rune
        float sz  = rune_size * (0.8 + hash(seed + 8.3) * 0.4);
        float dist = rune_sdf(local, rtype, sz);

        float stroke = 0.004;

        // Outer glow
        float glow  = smoothstep(stroke * glow_radius, 0.0, dist) * fade;
        // Stroke line
        float line  = smoothstep(stroke * 1.5, 0.0, abs(dist - stroke * 0.3)) * fade;
        // Bright core
        float core  = smoothstep(stroke * 0.4, 0.0, dist) * fade;

        // Dim always-visible ghost (so runes aren't completely invisible when faded)
        float ghost = smoothstep(stroke * 2.0, 0.0, dist) * 0.08;

        bg = mix(bg, rune_dim,  ghost);
        bg = mix(bg, rune_glow, glow  * 0.55);
        bg = mix(bg, rune_glow, line  * 0.85);
        bg = mix(bg, rune_core, core  * 0.95);
    }

    // --- Vignette ---
    float vig = 1.0 - smoothstep(0.3, 1.05, length(uv - 0.5) * 1.8);
    bg.rgb *= vig;

    // Extra darkness at very center to keep UI readable
    float center_clear = smoothstep(0.0, 0.25, length(uv - 0.5));
    bg.rgb *= mix(0.6, 1.0, center_clear);

    COLOR = bg;
}
