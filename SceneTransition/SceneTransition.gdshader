// SceneTransition.gdshader
// Organic void-dissolve: animated noise eats the screen from everywhere at once.
// Controlled by the "progress" uniform (0 = clear, 1 = fully covered).

shader_type canvas_item;

uniform float progress : hint_range(0.0, 1.0) = 0.0;

// ── Noise helpers ──────────────────────────────────────────────────────────
float hash21(vec2 p) {
    p = fract(p * vec2(234.34, 435.345));
    p += dot(p, p + 34.23);
    return fract(p.x * p.y);
}

float valueNoise(vec2 uv) {
    vec2 i = floor(uv);
    vec2 f = fract(uv);
    f = f * f * (3.0 - 2.0 * f);
    return mix(
        mix(hash21(i),                    hash21(i + vec2(1.0, 0.0)), f.x),
        mix(hash21(i + vec2(0.0, 1.0)),   hash21(i + vec2(1.0, 1.0)), f.x),
        f.y
    );
}

void fragment() {
    // Three octaves — different scales, speeds, and drift directions
    // so the dissolve writhes and crawls rather than looking static.
    float n = valueNoise(UV * 6.0  + vec2( TIME * 0.22,  TIME * 0.18)) * 0.50
            + valueNoise(UV * 15.0 + vec2(-TIME * 0.38,  TIME * 0.29)) * 0.32
            + valueNoise(UV * 32.0 + vec2( TIME * 0.55, -TIME * 0.44)) * 0.18;
    // n range ≈ [0, 1]

    // Remap progress so we start fully transparent and end fully opaque
    float threshold = progress * 1.10 - 0.05;
    float edge      = 0.08;

    // alpha = 1 (void) where noise < threshold, 0 (clear) where noise > threshold
    float alpha = 1.0 - smoothstep(threshold - edge, threshold + edge, n);

    // Deep void: nearly black with a faint purple tint
    COLOR = vec4(0.008, 0.003, 0.022, alpha);
}
