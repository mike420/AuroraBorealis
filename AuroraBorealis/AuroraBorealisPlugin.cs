using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Utils;
using UnityEngine;

namespace AuroraBorealis;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency("com.jotunn.jotunn", BepInDependency.DependencyFlags.HardDependency)]
[NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
public class AuroraBorealisPlugin : BaseUnityPlugin
{
	public const string PluginName = "AuroraBorealis";
	public const string PluginVersion = "1.0.4";
	public const string PluginAuthor = "MagicMike";
	internal const string PluginGUID = "MagicMike.AuroraBorealis";
	internal const string PluginCopy = "Copyright © MagicMike 2026";
	public const string PluginLink = "https://discord.gg/mnr36G6J4x";

	public static AuroraBorealisPlugin plugininstance;

	internal static ConfigFile ConfigFile;
	internal static ConfigFileWatcher ConfigFileWatcher;

	private Harmony harmony;

	public void Awake()
	{
		plugininstance = this;
		ConfigFile = Config;
		ConfigFileWatcher = new ConfigFileWatcher(Config, 1000L);
		Config.SaveOnConfigSet = false;
		MainConfig.InitConfigs(Config);

		MagicMike.ModSettings.ModSettings.Init(this);

		Config.Save();

		// Nothing to draw on a dedicated server. The sky is always created, so Enable Plugin can be switched in game: off just fades it out.
		if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
		{
			Translations.Load(this);
			harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGUID);
			AuroraSky.Create(gameObject);
		}
	}

	private void Update()
	{
		Translations.Tick();
	}

	private void OnDestroy()
	{
		Translations.Unload();
		harmony?.UnpatchSelf();
	}
}
