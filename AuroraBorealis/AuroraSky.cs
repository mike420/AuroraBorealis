using System.Collections.Generic;
using UnityEngine;

namespace AuroraBorealis;

// Owns the curtains and keeps them centred on the camera, so they stay at the horizon however far you travel.
// Fades the aurora in at dusk and out at dawn, and when it is raining, snowing, or you are indoors.
internal class AuroraSky : MonoBehaviour
{
	private const float FadeSpeed = 0.25f;
	private const float UpdateInterval = 1f / 30f;

	private static readonly string[] ShaderNames =
	{
		"Legacy Shaders/Particles/Additive",
		"Mobile/Particles/Additive",
		"Particles/Standard Unlit",
		"Sprites/Default"
	};

	private readonly List<AuroraCurtain> m_curtains = new List<AuroraCurtain>();
	private Material m_material;
	private float m_intensity;
	private float m_nextUpdate;
	private float m_clock;

	internal static AuroraSky Instance { get; private set; }

	internal static void Create(GameObject host)
	{
		GameObject sky = new GameObject("AuroraBorealisSky");
		DontDestroyOnLoad(sky);
		Instance = sky.AddComponent<AuroraSky>();
	}

	private void Awake()
	{
		m_material = MakeMaterial();
	}

	private void OnDestroy()
	{
		DestroyCurtains();
		if (m_material != null)
		{
			Destroy(m_material);
		}
		if (Instance == this)
		{
			Instance = null;
		}
	}

	private static Material MakeMaterial()
	{
		Shader shader = null;
		foreach (string name in ShaderNames)
		{
			shader = Shader.Find(name);
			if (shader != null)
			{
				break;
			}
		}
		if (shader == null)
		{
			Debug.LogWarning("AuroraBorealis: no suitable shader found for the aurora");
			return null;
		}
		Material material = new Material(shader) { name = "AuroraBorealis", renderQueue = 2900 };
		// Neutral tint for the particle shaders, which double it
		if (material.HasProperty("_TintColor"))
		{
			material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
		}
		if (material.HasProperty("_Color"))
		{
			material.SetColor("_Color", Color.white);
		}
		return material;
	}

	private void DestroyCurtains()
	{
		foreach (AuroraCurtain curtain in m_curtains)
		{
			curtain.Destroy();
		}
		m_curtains.Clear();
	}

	private static float NightAmount()
	{
		if (MainConfig.alwaysVisible.Value)
		{
			return 1f;
		}
		EnvMan env = EnvMan.instance;
		if (env == null)
		{
			return 0f;
		}
		// 0 at midnight, 0.5 at noon: fully visible through the night, gone by full daylight
		float f = env.GetDayFraction();
		float fromMidnight = Mathf.Min(f, 1f - f);
		return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.2f, fromMidnight));
	}

	private static float TargetIntensity()
	{
		Player player = Player.m_localPlayer;
		if (player == null || MainConfig.brightness.Value <= 0 || player.InInterior())
		{
			return 0f;
		}
		float target = NightAmount() * MainConfig.brightness.Value / 100f;
		if (MainConfig.hideInBadWeather.Value && EnvMan.IsWet())
		{
			target = 0f;
		}
		return target;
	}

	private void LateUpdate()
	{
		if (m_material == null)
		{
			return;
		}

		m_intensity = Mathf.MoveTowards(m_intensity, TargetIntensity(), FadeSpeed * Time.deltaTime);
		Camera camera = Utils.GetMainCamera();
		bool visible = m_intensity > 0.002f && camera != null;

		int count = MainConfig.curtains.Value;
		if (m_curtains.Count != count)
		{
			DestroyCurtains();
			for (int i = 0; i < count; i++)
			{
				m_curtains.Add(new AuroraCurtain(transform, m_material, i));
			}
		}

		foreach (AuroraCurtain curtain in m_curtains)
		{
			curtain.SetActive(visible);
		}
		if (!visible)
		{
			return;
		}

		transform.position = camera.transform.position;
		m_clock += Time.deltaTime * MainConfig.speed.Value;

		if (Time.time < m_nextUpdate)
		{
			return;
		}
		m_nextUpdate = Time.time + UpdateInterval;

		// Keep the curtains inside the camera's far plane, with room for the layers further out
		float radius = Mathf.Clamp(camera.farClipPlane * 0.7f, 150f, 3500f) / 1.05f;
		float alpha = Mathf.Clamp01(m_intensity);
		float height = MainConfig.height.Value / 100f;
		foreach (AuroraCurtain curtain in m_curtains)
		{
			curtain.Update(m_clock, count, radius, alpha, height, MainConfig.direction.Value, MainConfig.arcWidth.Value, MainConfig.colours.Value);
		}
	}
}
