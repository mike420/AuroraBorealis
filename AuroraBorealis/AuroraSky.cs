using System.Collections.Generic;
using UnityEngine;

namespace AuroraBorealis;

// Owns the curtains and keeps them centred on the camera, so they stay at the horizon however far you travel.
// Fades the aurora in at dusk and out at dawn, and when it is raining, snowing, or you are indoors.
internal class AuroraSky : MonoBehaviour
{
	private const float FadeSpeed = 0.25f;
	private const float UpdateInterval = 1f / 30f;
	// Valheim's cloud layer is a cylinder 500 m across from the camera that hides anything behind it, so the curtains hang inside it
	// (the outer layers sit a little further out, and they sway a little, so the nearest reaches about 445 m).
	private const float CurtainRadius = 400f;

	// Fallback only, for when the plain vertex-colour shader is missing
	private static readonly string[] ShaderNames =
	{
		"Sprites/Default",
		"Legacy Shaders/Particles/Additive",
		"Mobile/Particles/Additive",
		"Particles/Standard Unlit"
	};

	private readonly List<AuroraCurtain> m_curtains = new List<AuroraCurtain>();
	private Material m_material;
	private Material m_drawMaterial;   // Unity's plain vertex-colour shader, used to draw the meshes straight into the camera
	private bool m_visible;
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
		m_drawMaterial = MakeDrawMaterial();
		if (m_drawMaterial != null)
		{
			Camera.onPostRender += OnCameraPostRender;
		}
		else
		{
			m_material = MakeMaterial();
		}
	}

	// Additive, no depth write, drawn two-sided; the terrain still hides it because the depth test stays on
	private static Material MakeDrawMaterial()
	{
		Shader shader = Shader.Find("Hidden/Internal-Colored");
		if (shader == null)
		{
			return null;
		}
		Material material = new Material(shader) { name = "AuroraBorealisDraw", hideFlags = HideFlags.HideAndDontSave };
		material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
		material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
		material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
		material.SetInt("_ZWrite", 0);
		material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
		return material;
	}

	private void OnCameraPostRender(Camera camera)
	{
		if (!m_visible || m_curtains.Count == 0 || camera != Utils.GetMainCamera())
		{
			return;
		}
		m_drawMaterial.SetPass(0);
		Matrix4x4 matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, Vector3.one);
		foreach (AuroraCurtain curtain in m_curtains)
		{
			Graphics.DrawMeshNow(curtain.Mesh, matrix);
		}
	}

	private void OnDestroy()
	{
		Camera.onPostRender -= OnCameraPostRender;
		DestroyCurtains();
		if (m_drawMaterial != null)
		{
			Destroy(m_drawMaterial);
		}
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
		Material material = new Material(shader) { name = "AuroraBorealis", renderQueue = 3000 };
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
		if (!MainConfig.modEnabled.Value || player == null || MainConfig.brightness.Value <= 0 || player.InInterior())
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
		if (m_material == null && m_drawMaterial == null)
		{
			return;
		}

		m_intensity = Mathf.MoveTowards(m_intensity, TargetIntensity(), FadeSpeed * Time.deltaTime);
		Camera camera = Utils.GetMainCamera();
		bool visible = m_intensity > 0.002f && camera != null;
		m_visible = visible;

		int count = MainConfig.curtains.Value;
		if (m_curtains.Count != count)
		{
			DestroyCurtains();
			for (int i = 0; i < count; i++)
			{
				m_curtains.Add(new AuroraCurtain(transform, m_drawMaterial != null ? null : m_material, i));
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

		float radius = Mathf.Min(CurtainRadius, camera.farClipPlane * 0.7f / 1.05f);
		float alpha = Mathf.Clamp(m_intensity, 0f, 2f);
		float height = MainConfig.height.Value / 100f;
		foreach (AuroraCurtain curtain in m_curtains)
		{
			curtain.Update(m_clock, count, radius, alpha, height, MainConfig.direction.Value, MainConfig.arcWidth.Value, MainConfig.colours.Value);
		}
	}
}
