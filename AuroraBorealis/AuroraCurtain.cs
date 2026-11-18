using UnityEngine;

namespace AuroraBorealis;

// One ribbon of light hanging in the sky: a grid of vertices along an arc of the horizon (columns) and up the curtain (rows).
// The positions and colours are rewritten as time passes, so the curtain ripples and its rays brighten and fade.
internal class AuroraCurtain
{
	private const int Columns = 90;
	private const int Rows = 10;

	private readonly int m_index;
	private readonly float m_seed;
	private readonly GameObject m_object;
	private readonly Mesh m_mesh;
	private readonly Vector3[] m_vertices = new Vector3[(Columns + 1) * (Rows + 1)];
	private readonly Color32[] m_colours = new Color32[(Columns + 1) * (Rows + 1)];
	private readonly float[] m_rays = new float[Columns + 1];

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
		m_object.AddComponent<MeshFilter>().sharedMesh = m_mesh;
		MeshRenderer renderer = m_object.AddComponent<MeshRenderer>();
		renderer.sharedMaterial = material;
		renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		renderer.receiveShadows = false;
		renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
		renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
	}

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
	/// <param name="alpha">Overall brightness, 0 to 1.</param>
	internal void Update(float time, int count, float radius, float alpha, float heightScale, float centreDegrees, float arcDegrees, AuroraColours colours)
	{
		float layer = count > 1 ? m_index / (float)(count - 1) : 0f;
		float r = radius * (0.8f + 0.25f * layer);

		// Each curtain is a little narrower than the arc and drifts, so they overlap without lining up
		float arc = arcDegrees * Mathf.Deg2Rad * (1f - 0.18f * layer);
		float centre = (centreDegrees + 12f * Mathf.Sin(time * 0.05f + m_seed)) * Mathf.Deg2Rad;
		bool fullCircle = arcDegrees >= 359.5f;

		// Elevation above the horizon at the bottom and the top of the curtain
		float baseElevation = (6f + 4f * layer) * Mathf.Deg2Rad;
		float topElevation = baseElevation + (28f + 8f * Mathf.Sin(m_seed)) * heightScale * Mathf.Deg2Rad;

		// Rays: brightness along the curtain from drifting noise, so bright streaks form, slide along and die away
		for (int i = 0; i <= Columns; i++)
		{
			float u = i / (float)Columns;
			float slow = Mathf.PerlinNoise(u * 5f + m_seed, time * 0.12f + m_seed);
			float fast = Mathf.PerlinNoise(u * 23f - time * 0.35f + m_seed, m_seed * 2f + time * 0.2f);
			float ray = Mathf.Clamp01(slow * 1.5f - 0.2f) * (0.45f + 0.55f * fast);
			float edge = fullCircle ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Min(u, 1f - u) * 6f);
			m_rays[i] = ray * edge;
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

				// Bright just above the lower edge, which is sharp, then a long soft fade up the rays
				float profile = Mathf.SmoothStep(0f, 1f, v * 9f) * Mathf.Pow(1f - v, 1.6f);
				float a = profile * m_rays[i] * alpha;
				Color colour = AuroraPalette.Evaluate(colours, u, v, time);
				m_colours[k] = new Color(colour.r, colour.g, colour.b, Mathf.Clamp01(a));
			}
		}

		m_mesh.vertices = m_vertices;
		m_mesh.colors32 = m_colours;
		// The mesh sits around the camera and is always on screen somewhere
		m_mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (r * 4f));
	}
}
