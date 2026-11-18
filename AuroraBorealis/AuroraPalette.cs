using UnityEngine;

namespace AuroraBorealis;

// Colour of the aurora at a point on a curtain: v is 0 at the bright lower edge and 1 at the faded top
internal static class AuroraPalette
{
	private static readonly Color[] Classic = { C(0.25f, 1f, 0.45f), C(0.1f, 0.95f, 0.55f), C(0.2f, 0.6f, 0.85f), C(0.65f, 0.25f, 0.9f) };
	private static readonly Color[] Violet = { C(0.9f, 0.3f, 0.85f), C(0.65f, 0.25f, 0.95f), C(0.4f, 0.3f, 0.95f), C(0.25f, 0.4f, 0.85f) };
	private static readonly Color[] Ice = { C(0.3f, 1f, 0.9f), C(0.15f, 0.85f, 1f), C(0.2f, 0.55f, 1f), C(0.45f, 0.35f, 0.95f) };
	private static readonly Color[] Crimson = { C(1f, 0.25f, 0.3f), C(0.95f, 0.15f, 0.35f), C(0.75f, 0.15f, 0.5f), C(0.5f, 0.15f, 0.6f) };

	private static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

	internal static Color Evaluate(AuroraColours scheme, float u, float v, float time)
	{
		if (scheme == AuroraColours.Rainbow)
		{
			float hue = Mathf.Repeat(u * 0.6f + v * 0.25f + time * 0.02f, 1f);
			return Color.HSVToRGB(hue, 0.75f, 1f);
		}
		Color[] stops = scheme switch
		{
			AuroraColours.Violet => Violet,
			AuroraColours.Ice => Ice,
			AuroraColours.Crimson => Crimson,
			_ => Classic
		};
		float f = Mathf.Clamp01(v) * (stops.Length - 1);
		int i = Mathf.Min((int)f, stops.Length - 2);
		return Color.Lerp(stops[i], stops[i + 1], f - i);
	}
}
