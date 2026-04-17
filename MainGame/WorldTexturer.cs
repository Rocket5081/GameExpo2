using Godot;

/// <summary>
/// Runs once at game start. Applies procedural FastNoiseLite-based textures to the
/// world geometry (terrain mounds, cover platforms, ground, mountain range) while
/// preserving the existing dark-purple eldritch colour palette and emission glow.
/// </summary>
public partial class WorldTexturer : Node
{
	public override void _Ready()
	{
		ApplyTerrainTexture();
		ApplyPlatformTexture();
		ApplyGroundTexture();
		ApplyMountainTexture();
	}

	// ── Terrain mounds ────────────────────────────────────────────────────────
	// Ridged simplex noise → sharp rocky ridges on the cylindrical mounds.
	private void ApplyTerrainTexture()
	{
		var terrainParent = GetNodeOrNull<Node>("../NavigationRegion3D/Terrain");
		if (terrainParent == null) return;

		// Sample the first mound to clone its existing material
		var sample = GetMeshInstance(terrainParent.GetNodeOrNull("Mound_1"));
		if (sample == null) return;

		var mat = CloneMaterial(sample);
		if (mat == null) return;

		// Albedo – ridged simplex gives craggy rock-like patterns
		mat.AlbedoTexture = MakeNoiseTex(
			noiseType:    FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
			seed:         1337,
			frequency:    0.06f,
			fractal:      FastNoiseLite.FractalTypeEnum.Ridged,
			octaves:      4,
			domainWarp:   false,
			size:         512
		);

		// Normal map – softer FBm with domain warp for organic bumps
		mat.NormalEnabled  = true;
		mat.NormalScale    = 2.5f;
		mat.NormalTexture  = MakeNormalTex(
			seed:         4242,
			frequency:    0.09f,
			octaves:      5,
			domainWarpAmt: 25f,
			bumpStrength:  7f,
			size:         512
		);

		mat.Uv1Scale = new Vector3(3f, 1.5f, 3f);

		ApplyToChildren(terrainParent, mat);
	}

	// ── Cover platforms / obstacles ───────────────────────────────────────────
	// Cellular noise → cracked stone tile look on the box obstacles.
	private void ApplyPlatformTexture()
	{
		var platformParent = GetNodeOrNull<Node>("../NavigationRegion3D/CoverPlatforms");
		if (platformParent == null) return;

		var sample = GetMeshInstance(platformParent.GetNodeOrNull("CoverPlatform_1"));
		if (sample == null) return;

		var mat = CloneMaterial(sample);
		if (mat == null) return;

		// Albedo – cellular noise looks like stone brick / cracked slate
		mat.AlbedoTexture = MakeNoiseTex(
			noiseType:  FastNoiseLite.NoiseTypeEnum.Cellular,
			seed:       555,
			frequency:  0.07f,
			fractal:    FastNoiseLite.FractalTypeEnum.Fbm,
			octaves:    3,
			domainWarp: false,
			size:       512
		);

		// Normal map – smooth FBm for subtle carved-stone surface detail
		mat.NormalEnabled  = true;
		mat.NormalScale    = 1.5f;
		mat.NormalTexture  = MakeNormalTex(
			seed:         9999,
			frequency:    0.05f,
			octaves:      4,
			domainWarpAmt: 0f,
			bumpStrength:  4f,
			size:         512
		);

		mat.Uv1Scale = new Vector3(4f, 0.6f, 2f);

		ApplyToChildren(platformParent, mat);
	}

	// ── Ground plane ──────────────────────────────────────────────────────────
	// Large-scale domain-warped noise → subtle dark earth variation.
	private void ApplyGroundTexture()
	{
		var groundMesh = GetNodeOrNull<MeshInstance3D>("../NavigationRegion3D/Ground/MeshInstance3D");
		if (groundMesh == null) return;

		var mat = CloneMaterial(groundMesh);
		if (mat == null) return;

		mat.AlbedoTexture = MakeNoiseTex(
			noiseType:  FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
			seed:       777,
			frequency:  0.015f,
			fractal:    FastNoiseLite.FractalTypeEnum.Fbm,
			octaves:    4,
			domainWarp: true,
			size:       1024
		);

		// Ground is scaled 260× in the transform, so UV needs many repeats
		mat.Uv1Scale = new Vector3(12f, 12f, 12f);

		groundMesh.MaterialOverride = mat;
	}

	// ── Mountain range ────────────────────────────────────────────────────────
	// Ridged simplex → craggy distant peaks.
	private void ApplyMountainTexture()
	{
		var mountainParent = GetNodeOrNull<Node>("../MountainRange");
		if (mountainParent == null) return;

		var sample = mountainParent.GetNodeOrNull<MeshInstance3D>("Peak_1");
		if (sample == null) return;

		var mat = CloneMaterial(sample);
		if (mat == null) return;

		mat.AlbedoTexture = MakeNoiseTex(
			noiseType:  FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
			seed:       3333,
			frequency:  0.04f,
			fractal:    FastNoiseLite.FractalTypeEnum.Ridged,
			octaves:    5,
			domainWarp: false,
			size:       512
		);

		mat.NormalEnabled  = true;
		mat.NormalScale    = 1.5f;
		mat.NormalTexture  = MakeNormalTex(
			seed:         8765,
			frequency:    0.06f,
			octaves:      4,
			domainWarpAmt: 15f,
			bumpStrength:  5f,
			size:         512
		);

		mat.Uv1Scale = new Vector3(2f, 2f, 2f);

		ApplyToChildren(mountainParent, mat);
	}

	// ── Helpers ───────────────────────────────────────────────────────────────

	/// Finds the MeshInstance3D child of a StaticBody3D parent node.
	private static MeshInstance3D GetMeshInstance(Node parent)
		=> parent?.GetNodeOrNull<MeshInstance3D>("MeshInstance3D");

	/// Duplicates the StandardMaterial3D from a MeshInstance3D so we don't
	/// modify the shared original sub-resource.
	private static StandardMaterial3D CloneMaterial(MeshInstance3D mi)
	{
		var src = mi?.Mesh?.SurfaceGetMaterial(0) as StandardMaterial3D;
		return src != null ? (StandardMaterial3D)src.Duplicate() : null;
	}

	/// Applies a material override to every MeshInstance3D child of a node.
	private static void ApplyToChildren(Node parent, StandardMaterial3D mat)
	{
		foreach (var child in parent.GetChildren())
		{
			// Each child is a StaticBody3D (mound / platform) — go one level deeper
			var mi = child.GetNodeOrNull<MeshInstance3D>("MeshInstance3D")
			      ?? child as MeshInstance3D;   // mountains are direct MeshInstance3D
			if (mi != null)
				mi.MaterialOverride = mat;
		}
	}

	/// Creates a seamless albedo NoiseTexture2D.
	private static NoiseTexture2D MakeNoiseTex(
		FastNoiseLite.NoiseTypeEnum noiseType,
		int    seed,
		float  frequency,
		FastNoiseLite.FractalTypeEnum fractal,
		int    octaves,
		bool   domainWarp,
		int    size)
	{
		var n = new FastNoiseLite();
		n.NoiseType      = noiseType;
		n.Seed           = seed;
		n.Frequency      = frequency;
		n.FractalType    = fractal;
		n.FractalOctaves = octaves;
		if (domainWarp)
		{
			n.DomainWarpEnabled   = true;
			n.DomainWarpAmplitude = 50f;
			n.DomainWarpFrequency = 0.02f;
		}

		return new NoiseTexture2D
		{
			Width    = size,
			Height   = size,
			Seamless = true,
			Noise    = n
		};
	}

	/// Creates a seamless normal-map NoiseTexture2D.
	private static NoiseTexture2D MakeNormalTex(
		int   seed,
		float frequency,
		int   octaves,
		float domainWarpAmt,
		float bumpStrength,
		int   size)
	{
		var n = new FastNoiseLite();
		n.NoiseType      = FastNoiseLite.NoiseTypeEnum.SimplexSmooth;
		n.Seed           = seed;
		n.Frequency      = frequency;
		n.FractalType    = FastNoiseLite.FractalTypeEnum.Fbm;
		n.FractalOctaves = octaves;
		if (domainWarpAmt > 0f)
		{
			n.DomainWarpEnabled   = true;
			n.DomainWarpAmplitude = domainWarpAmt;
		}

		return new NoiseTexture2D
		{
			Width        = size,
			Height       = size,
			Seamless     = true,
			AsNormalMap  = true,
			BumpStrength = bumpStrength,
			Noise        = n
		};
	}
}
