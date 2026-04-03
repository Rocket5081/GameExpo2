shader_type canvas_item;

// A horizontal dripping/bleeding separator line for eldritch horror UI.
// Apply to a ColorRect with minimum height ~20px.

uniform float speed       : hint_range(0.0, 3.0) = 0.6;
uniform float drip_count  : hint_range(2.0, 20.0) = 10.0;
uniform float drip_speed  : hint_range(0.1, 5.0)  = 0.9;
uniform vec4  line_color  : source_color = vec4(0.55, 0.0, 0.8,  1.0);
uniform vec4  drip_color  : source_color = vec4(0.35, 0.0, 0.6,  1.0);
uniform vec4  glow_color  : source_color = vec4(0.8,  0.3, 1.0,  1.0);

float hash(float n) { return fract(sin(n) * 43758.5453); }

void fragment() {
    vec2 uv = UV; // 0..1 across the rect

    vec4 col = vec4(0.0);

    // ── Main horizontal line with glow ────────────────────────────────────
    float line_y  = 0.25; // line sits near the top of the rect
    float line_d  = abs(uv.y - line_y);
    float line_w  = 0.06;

    // Soft glow behind the line
    float glow = smoothstep(0.35, 0.0, line_d);
    col = mix(col, glow_color * vec4(1,1,1,0.35), glow);

    // Sharp line stroke
    float stroke = smoothstep(line_w, 0.0, line_d);
    col = mix(col, line_color, stroke);

    // Bright core
    float core = smoothstep(line_w * 0.25, 0.0, line_d);
    col = mix(col, glow_color, core * 0.9);

    // ── Drips hanging down from the line ─────────────────────────────────
    for (float i = 0.0; i < 20.0; i++) {
        if (i >= drip_count) break;

        float seed = i * 13.73 + 2.17;

        // X position of this drip along the separator
        float drip_x = hash(seed);

        // Each drip has its own speed and length, cycling over time
        float cycle   = hash(seed + 1.3) * 2.0 + 1.0; // 1–3 sec cycle
        float phase   = hash(seed + 2.7) * 6.28318;
        // t goes 0→1 repeatedly, representing the drip travelling downward
        float t = fract((TIME * drip_speed / cycle) + phase);

        // Max drip length varies per drip
        float max_len = 0.35 + hash(seed + 3.1) * 0.55; // 0.35..0.9 of rect height
        float drip_tip = line_y + t * max_len * (1.0 - uv.x * 0.0); // tip Y position

        // Width of drip: thick at top, tapers to a point
        float drip_w_base = 0.018 + hash(seed + 4.5) * 0.022;

        // Distance from current pixel to drip center line
        float dx = abs(uv.x - drip_x);

        // Is this pixel inside the drip body (between line and tip)?
        float in_body = step(line_y, uv.y) * step(uv.y, drip_tip);

        // Taper: width shrinks as we approach the tip
        float taper = 1.0 - smoothstep(line_y, drip_tip, uv.y);
        float drip_w = drip_w_base * taper;

        float drip_mask = smoothstep(drip_w, 0.0, dx) * in_body;

        // Fade drip alpha: full at top, fades at tip
        float fade = taper * taper;

        col = mix(col, drip_color, drip_mask * fade * 0.7);

        // Bright drip core
        float drip_core = smoothstep(drip_w * 0.3, 0.0, dx) * in_body;
        col = mix(col, glow_color, drip_core * fade * 0.6);

        // Teardrop at tip: small glowing dot
        float tip_dist  = length(vec2((uv.x - drip_x) / 0.5, uv.y - drip_tip) * vec2(6.0, 1.0));
        float tip_drop  = smoothstep(0.025, 0.0, tip_dist) * (1.0 - step(drip_tip, 1.0) * 0.0);
        col = mix(col, glow_color, tip_drop * 0.85);
    }

    // ── Edge flicker: subtle pulse on the line ends ───────────────────────
    float edge_l = smoothstep(0.08, 0.0, uv.x);
    float edge_r = smoothstep(0.08, 0.0, 1.0 - uv.x);
    float flicker = 0.5 + 0.5 * sin(TIME * speed * 3.0);
    col.rgb += glow_color.rgb * (edge_l + edge_r) * flicker * 0.3 * stroke;

    COLOR = col;
}
