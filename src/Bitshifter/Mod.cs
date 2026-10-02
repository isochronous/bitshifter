using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using KMod;
using UnityEngine;

namespace Bitshifter
{
	public sealed class BitshifterMod : UserMod2
	{
		/// <summary>Bits 1-31. Bit 32 is the int's sign bit; the game tests bits with "&gt; 0", so it never reads as on.</summary>
		public const int Bits = 31;

		/// <summary>
		/// Workshop items that may already widen the ribbon reader and writer. When one of them is
		/// enabled this mod does nothing, so the two never fight over the same methods.
		/// </summary>
		private static readonly string[] YieldToWorkshopIds =
		{
			"2014558219", // Automation Expanded
			"2661900022", // Automation Plus
			"2777145427", // Edge Detectors and Diode (Automation)
		};

		private static readonly MethodBase[] Contested =
		{
			AccessTools.Method(typeof(LogicRibbonReader), nameof(LogicRibbonReader.GetBitDepth)),
			AccessTools.Method(typeof(LogicRibbonWriter), nameof(LogicRibbonWriter.GetBitDepth)),
		};

		// Patches are applied in OnAllModsLoaded, after every other mod had its turn, so
		// deliberately not base.OnLoad (which would PatchAll right away).
		public override void OnLoad(Harmony harmony)
		{
			Debug.Log("[Bitshifter] Loaded version " + typeof(BitshifterMod).Assembly.GetName().Version);
		}

		public override void OnAllModsLoaded(Harmony harmony, IReadOnlyList<Mod> mods)
		{
			string reason = WhyYield(mods);
			if (reason != null)
			{
				Debug.Log("[Bitshifter] Disabled: " + reason);
				return;
			}
			harmony.PatchAll(typeof(BitshifterMod).Assembly);
			Debug.Log("[Bitshifter] Ribbon Reader and Writer can select bits 1-" + Bits);
		}

		private static string WhyYield(IReadOnlyList<Mod> mods)
		{
			foreach (Mod mod in mods)
			{
				if (!mod.IsEnabledForActiveDlc())
					continue;
				foreach (string id in YieldToWorkshopIds)
				{
					if (mod.label.id == id)
						return "'" + mod.title + "' (Workshop " + id + ") is enabled";
				}
			}
			foreach (MethodBase method in Contested)
			{
				Patches patches = Harmony.GetPatchInfo(method);
				if (patches != null && patches.Owners.Count > 0)
					return method.DeclaringType.Name + "." + method.Name + " is already patched by " + string.Join(", ", patches.Owners);
			}
			return null;
		}
	}
}
