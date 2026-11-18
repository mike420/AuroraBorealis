using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using Jotunn.Extensions;

namespace AuroraBorealis;

internal enum AuroraColours
{
	Classic,
	Violet,
	Ice,
	Crimson,
	Rainbow
}

internal static class MainConfig
{
	public static ConfigEntry<bool> modEnabled;

	// Aurora
	public static ConfigEntry<bool> alwaysVisible;
	public static ConfigEntry<int> brightness;
	public static ConfigEntry<AuroraColours> colours;
	public static ConfigEntry<bool> hideInBadWeather;

	// Shape
	public static ConfigEntry<int> curtains;
	public static ConfigEntry<int> direction;
	public static ConfigEntry<int> arcWidth;
	public static ConfigEntry<int> height;
	public static ConfigEntry<float> speed;

	public static void InitConfigs(ConfigFile config)
	{
		modEnabled = config.BindConfigInOrder("General Settings", "Enable Plugin", defaultValue: true, "Enable plugin.\n(Restart required. It is read before connecting to a server, so it is a local setting).\n", synced: false, sectionOrder: true, settingOrder: true);

		alwaysVisible = config.BindConfigInOrder("Aurora", "Always Visible", defaultValue: false, "On: the aurora shows all day as well as at night. Off: it fades in at dusk and out at dawn.", synced: false, sectionOrder: true, settingOrder: true);
		brightness = config.BindConfigInOrder("Aurora", "Brightness", defaultValue: 100, "How bright the aurora is, as a percentage. 0 hides it.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<int>(0, 200));
		colours = config.BindConfigInOrder("Aurora", "Colours", defaultValue: AuroraColours.Classic, "Classic: green with a violet fringe. Violet: purple and pink. Ice: cyan and blue. Crimson: rare deep red. Rainbow: colours drift along the curtains.", synced: false, sectionOrder: true, settingOrder: true);
		hideInBadWeather = config.BindConfigInOrder("Aurora", "Hide In Bad Weather", defaultValue: true, "On: the aurora fades away while it is raining or snowing.", synced: false, sectionOrder: true, settingOrder: true);

		curtains = config.BindConfigInOrder("Shape", "Curtains", defaultValue: 4, "How many layered curtains of light make up the aurora.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<int>(1, 8));
		direction = config.BindConfigInOrder("Shape", "Direction", defaultValue: 0, "Where in the sky the aurora hangs, as a compass bearing in degrees. 0 is north, 90 east, 180 south, 270 west.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<int>(0, 359));
		arcWidth = config.BindConfigInOrder("Shape", "Arc Width", defaultValue: 140, "How wide the aurora stretches across the horizon, in degrees. 360 wraps all the way around you.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<int>(30, 360));
		height = config.BindConfigInOrder("Shape", "Height", defaultValue: 100, "How tall the curtains rise above the horizon, as a percentage.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<int>(25, 200));
		speed = config.BindConfigInOrder("Shape", "Speed", defaultValue: 1f, "How fast the curtains ripple and dance. 0 freezes them.", synced: false, sectionOrder: true, settingOrder: true, acceptableValues: new AcceptableValueRange<float>(0f, 5f));

		// Decimal settings rounded (and kept in range) whichever editor sets them
		MagicMike.ModSettings.ConfigRounding.RoundAll(config);

		CarryOverOldSections(config);
	}

	// Sections that were renamed: the new name, and the names it had before
	private static readonly Dictionary<string, string[]> OldSections = new Dictionary<string, string[]>
	{
	};

	/// <summary>
	/// Sections are numbered in the config file ("2 - Shape"), so adding or removing one renumbers those after it, and a setting
	/// saved under the old number would be left behind (orphaned) and go back to its default. A setting still at its default
	/// takes the value it had under its section's old number (or old name, from <see cref="OldSections"/>), and the old entry
	/// is removed from the file.
	/// </summary>
	private static void CarryOverOldSections(ConfigFile config)
	{
		Dictionary<ConfigDefinition, string> orphans;
		try
		{
			PropertyInfo property = typeof(ConfigFile).GetProperty("OrphanedEntries", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			orphans = property?.GetValue(config) as Dictionary<ConfigDefinition, string>;
		}
		catch (System.Exception e)
		{
			Jotunn.Logger.LogWarning("[AuroraBorealis] Could not look for settings under old section names: " + e.Message);
			return;
		}
		if (orphans == null || orphans.Count == 0)
		{
			return;
		}

		int carried = 0, dropped = 0;
		HashSet<ConfigEntryBase> handled = new HashSet<ConfigEntryBase>();
		bool saveOnSet = config.SaveOnConfigSet;
		config.SaveOnConfigSet = false;
		try
		{
			// An earlier renumbering can leave the same setting under several old numbers; the highest is the latest
			foreach (ConfigDefinition old in orphans.Keys.OrderByDescending(d => SectionNumber(d.Section)).ToList())
			{
				string oldName = SectionName(old.Section);
				ConfigEntryBase entry = ((IDictionary<ConfigDefinition, ConfigEntryBase>)config).Values.FirstOrDefault(e => e.Definition.Key == old.Key && e.Definition.Section != old.Section
					&& (SectionName(e.Definition.Section) == oldName || OldSections.TryGetValue(SectionName(e.Definition.Section), out string[] before) && before.Contains(oldName)));
				if (entry == null)
				{
					continue;   // (not one of ours any more: left as it is)
				}
				if (handled.Add(entry) && Equals(entry.BoxedValue, entry.DefaultValue))
				{
					try
					{
						entry.SetSerializedValue(orphans[old]);
						carried++;
					}
					catch (System.Exception e)
					{
						Jotunn.Logger.LogWarning("[AuroraBorealis] Could not carry over " + old.Section + " / " + old.Key + ": " + e.Message);
					}
				}
				orphans.Remove(old);
				dropped++;
			}
		}
		finally
		{
			config.SaveOnConfigSet = saveOnSet;
		}
		if (dropped > 0)
		{
			config.Save();
			Jotunn.Logger.LogInfo("[AuroraBorealis] Carried " + carried + " setting(s) over from old section names in the config file.");
		}
	}

	// A section's name without the number in front ("2 - Shape": Shape)
	private static string SectionName(string section) => Regex.Replace(section ?? "", @"^\d+\s*-\s*", "");

	private static int SectionNumber(string section)
	{
		Match match = Regex.Match(section ?? "", @"^(\d+)\s*-");
		return match.Success ? int.Parse(match.Groups[1].Value) : -1;
	}
}
