using UnityEngine;

namespace AuroraBorealis;

// One ribbon of light hanging in the sky: a grid of vertices along an arc of the horizon (columns) and up the curtain (rows).
// The positions and colours are rewritten as time passes, so the curtain ripples and its rays brighten and fade.
internal class AuroraCurtain
{
	private const int Columns = 160;
	private const int Rows = 16;

	private readonly int m_index;
	private readonly float m_seed;
	private readonly GameObject m_object;
	private readonly Mesh m_mesh;
	private readonly Vector3[] m_vertices = new Vector3[(Columns + 1) * (Rows + 1)];
	private readonly Color32[] m_colours = new Color32[(Columns + 1) * (Rows + 1)];
	private readonly float[] m_rays = new float[Columns + 1];
	private readonly float[] m_rayHeights = new float[Columns + 1];

	internal AuroraCurtain(Transform parent, Material material, int index)
	{
		m_index = index;
		m_seed = index * 17.31f + 3.7f;

		m_mesh = new Mesh { name = "AuroraCurtain" };
		m_mesh.MarkDynamic();
		m_mesh.vertices = m_vertices;
		m_mesh.colors32 = m_colours;

		int[] triangles = new int[Columns * Rows * 6];
		int t = 0;
		for (int i = 0; i < Columns; i++)
		{
			for (int j = 0; j < Rows; j++)
			{
				int a = i * (Rows + 1) + j;
				int b = a + 1;
				int c = a + Rows + 1;
				int d = c + 1;
				triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
				triangles[t++] = c; triangles[t++] = b; triangles[t++] = d;
			}
		}
		m_mesh.triangles = triangles;

		m_object = new GameObject("AuroraCurtain" + index);
		m_object.transform.SetParent(parent, worldPositionStays: false);
		// Without a material the sky draws the mesh itself (see AuroraSky.OnPostRender), so there is no renderer
		if (material != null)
		{
			m_object.AddComponent<MeshFilter>().sharedMesh = m_mesh;
			MeshRenderer renderer = m_object.AddComponent<MeshRenderer>();
			renderer.sharedMaterial = material;
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			renderer.receiveShadows = false;
			renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
			renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
		}
	}

	internal Mesh Mesh => m_mesh;

	internal void SetActive(bool active)
	{
		if (m_object.activeSelf != active)
		{
			m_object.SetActive(active);
		}
	}

	internal void Destroy()
	{
		Object.Destroy(m_object);
		Object.Destroy(m_mesh);
	}

	/// <param name="count">How many curtains there are in all, so each takes its own distance and shade.</param>
	/// <param name="radius">Distance of the nearest curtain from the camera.</param>
	/// <param name="alpha">Overall brightness: 1 is the normal level, 2 is double.</param>
	internal void Update(float time, int count, float radius, float alpha, float heightScale, float centreDegrees, float arcDegrees, AuroraColours colours)
	{
		float layer = count > 1 ? m_index / (float)(count - 1) : 0f;
		float r = radius * (0.8f + 0.25f * layer);
		bool fullCircle = arcDegrees >= 359.5f;

		// Each curtain covers only part of the arc, at its own spot, so the sky shows between them instead of one solid sheet
		float span = fullCircle ? 1f : 0.5f + 0.3f * Mathf.Abs(Mathf.Sin(m_seed * 1.7f));
		float spread = fullCircle ? 0f : Mathf.Sin(m_seed * 2.9f) * (1f - span) * 0.5f * arcDegrees;
		float arc = arcDegrees * span * Mathf.Deg2Rad;
		float centre = (centreDegrees + spread + 8f * Mathf.Sin(time * 0.05f + m_seed)) * Mathf.Deg2Rad;

		// The curtains add their light together, so each gives less the more of them there are
		float layerScale = 1.4f / (count + 1);

		// Elevation above the horizon at the bottom and the top of the curtain
		float baseElevation = (6f + 4f * layer) * Mathf.Deg2Rad;
		float topElevation = baseElevation + (30f + 8f * Mathf.Sin(m_seed)) * heightScale * Mathf.Deg2Rad;

		// Rays: bright streaks that form, slide along and die away. Each has its own strength and its own height.
		for (int i = 0; i <= Columns; i++)
		{
			float u = i / (float)Columns;
			float slow = Mathf.PerlinNoise(u * 4f + m_seed, time * 0.1f + m_seed);
			float mid = Mathf.PerlinNoise(u * 14f - time * 0.25f + m_seed, m_seed * 2f + time * 0.15f);
			float fine = Mathf.PerlinNoise(u * 45f - time * 0.5f + m_seed * 3f, m_seed + time * 0.3f);
			float body = Mathf.Clamp01(slow * 1.7f - 0.35f);
			float streak = Mathf.Pow(Mathf.Clamp01(mid * 0.55f + fine * 0.6f), 2.2f);
			float edge = fullCircle ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Min(u, 1f - u) * 6f);
			m_rays[i] = body * (0.08f + 0.92f * streak) * edge;
			m_rayHeights[i] = Mathf.Lerp(0.35f, 1f, Mathf.Sqrt(streak));
		}

		int k = 0;
		for (int i = 0; i <= Columns; i++)
		{
			float u = i / (float)Columns;
			for (int j = 0; j <= Rows; j++, k++)
			{
				float v = j / (float)Rows;

				// The curtain folds sideways more towards its top, and the whole ribbon wanders in and out
				float fold = Mathf.Sin(u * 11f + time * 0.4f + m_seed) * 0.035f + Mathf.Sin(u * 27f - time * 0.55f) * 0.012f;
				float yaw = centre + (u - 0.5f) * arc + fold * v;
				float distance = r * (1f + 0.06f * Mathf.Sin(u * 6f + time * 0.2f + m_seed));
				float elevation = Mathf.Lerp(baseElevation, topElevation, v);

				// Bearing 0 is north, which is +Z; the compass runs clockwise
				float flat = distance * Mathf.Cos(elevation);
				m_vertices[k] = new Vector3(flat * Mathf.Sin(yaw), distance * Mathf.Sin(elevation), flat * Mathf.Cos(yaw));

				// A sharp bright lower edge, then a soft fade up the ray that ends at the ray's own height. The colour runs from the base of each
				// ray (t = 0) to its tip (t = 1), so the tall rays pick up their violet and pink tips.
				float t = Mathf.Clamp01(v / m_rayHeights[i]);
				float profile = Mathf.SmoothStep(0f, 1f, v * 10f) * Mathf.Pow(1f - t, 1.5f);
				float a = profile * m_rays[i] * alpha * layerScale;
				Color colour = AuroraPalette.Evaluate(colours, u, t, time);
				m_colours[k] = (Color32)new Color(colour.r, colour.g, colour.b, Mathf.Clamp01(a));
			}
		}

		m_mesh.vertices = m_vertices;
		m_mesh.colors32 = m_colours;
		// The mesh sits around the camera and is always on screen somewhere
		m_mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (r * 4f));
	}
}
