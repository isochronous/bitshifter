using HarmonyLib;
using UnityEngine;

namespace Bitshifter
{
	// The bit selector side screen builds one row per GetBitDepth(), and both buildings
	// already shift by selectedBit with no upper bound, so widening the depth is the whole
	// change. The reader also passes the shifted value straight through to a ribbon output,
	// which is what makes shift chains work.
	[HarmonyPatch(typeof(LogicRibbonReader), nameof(LogicRibbonReader.GetBitDepth))]
	internal static class LogicRibbonReader_GetBitDepth_Patch
	{
		private static void Postfix(ref int __result)
		{
			__result = BitshifterMod.Bits;
		}
	}

	[HarmonyPatch(typeof(LogicRibbonWriter), nameof(LogicRibbonWriter.GetBitDepth))]
	internal static class LogicRibbonWriter_GetBitDepth_Patch
	{
		private static void Postfix(ref int __result)
		{
			__result = BitshifterMod.Bits;
		}
	}

	// Both buildings end UpdateVisuals with Play("<networks>_<bit+1>"), and the kanims only
	// have those states for bits 1-4. With a higher bit selected, play the plain idle pose
	// instead: the four bit lights and the port light are still tinted by the original
	// code; only the "which bit" highlight is absent. Without this the game would log a
	// missing-animation warning every 200 ms.
	internal static class HighBitVisuals
	{
		public static void Apply(KBatchedAnimController kbac, int selectedBit)
		{
			if (kbac != null && selectedBit >= 4)
				kbac.Play("idle");
		}
	}

	[HarmonyPatch(typeof(LogicRibbonReader), nameof(LogicRibbonReader.UpdateVisuals))]
	internal static class LogicRibbonReader_UpdateVisuals_Patch
	{
		private static void Postfix(LogicRibbonReader __instance, KBatchedAnimController ___kbac)
		{
			HighBitVisuals.Apply(___kbac, __instance.selectedBit);
		}
	}

	[HarmonyPatch(typeof(LogicRibbonWriter), nameof(LogicRibbonWriter.UpdateVisuals))]
	internal static class LogicRibbonWriter_UpdateVisuals_Patch
	{
		private static void Postfix(LogicRibbonWriter __instance, KBatchedAnimController ___kbac)
		{
			HighBitVisuals.Apply(___kbac, __instance.selectedBit);
		}
	}
}
