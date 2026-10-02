using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

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

	// The bit selector side screen is a fixed-height panel whose rows sit in a vertical
	// layout; four rows fit, thirty-one do not. On first use the rows' container is moved
	// into a scroll view that keeps the height the four rows had, so the panel's layout is
	// untouched and the list scrolls with the mouse wheel.
	[HarmonyPatch(typeof(LogicBitSelectorSideScreen), nameof(LogicBitSelectorSideScreen.SetTarget))]
	internal static class LogicBitSelectorSideScreen_SetTarget_Patch
	{
		private const string ScrollName = "BitshifterScroll";
		private const int VisibleRows = 4;

		private static void Prefix(LogicBitSelectorSideScreen __instance, out float __state)
		{
			// Height of the row list as laid out for the vanilla depth, before more rows exist.
			RectTransform rows = __instance.rowPrefab != null ? __instance.rowPrefab.transform.parent as RectTransform : null;
			__state = rows != null && rows.GetComponentInParent<ScrollRect>() == null ? rows.rect.height : 0f;
		}

		private static void Postfix(LogicBitSelectorSideScreen __instance, float __state)
		{
			if (__instance.rowPrefab == null)
				return;
			RectTransform rows = __instance.rowPrefab.transform.parent as RectTransform;
			if (rows == null || rows.parent == null || rows.GetComponentInParent<ScrollRect>() != null)
				return;

			float height = __state;
			if (height <= 0f)
			{
				RectTransform row = __instance.rowPrefab.GetComponent<RectTransform>();
				float rowHeight = row != null && row.rect.height > 0f ? row.rect.height : 32f;
				VerticalLayoutGroup layout = rows.GetComponent<VerticalLayoutGroup>();
				float spacing = layout != null ? layout.spacing : 0f;
				float padding = layout != null ? layout.padding.vertical : 0;
				height = rowHeight * VisibleRows + spacing * (VisibleRows - 1) + padding;
			}

			GameObject scroll = new GameObject(ScrollName, typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect), typeof(LayoutElement));
			RectTransform viewport = scroll.GetComponent<RectTransform>();
			viewport.SetParent(rows.parent, false);
			viewport.SetSiblingIndex(rows.GetSiblingIndex());
			LayoutElement size = scroll.GetComponent<LayoutElement>();
			size.preferredHeight = height;
			size.minHeight = height;
			size.flexibleHeight = 0f;
			size.flexibleWidth = 1f;

			rows.SetParent(viewport, false);
			rows.anchorMin = new Vector2(0f, 1f);
			rows.anchorMax = new Vector2(1f, 1f);
			rows.pivot = new Vector2(0.5f, 1f);
			rows.anchoredPosition = Vector2.zero;
			rows.sizeDelta = new Vector2(0f, rows.sizeDelta.y);
			ContentSizeFitter fitter = rows.gameObject.AddOrGet<ContentSizeFitter>();
			fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			ScrollRect scrollRect = scroll.GetComponent<ScrollRect>();
			scrollRect.content = rows;
			scrollRect.viewport = viewport;
			scrollRect.horizontal = false;
			scrollRect.vertical = true;
			scrollRect.movementType = ScrollRect.MovementType.Clamped;
			scrollRect.inertia = false;
			scrollRect.scrollSensitivity = 24f;
			LayoutRebuilder.MarkLayoutForRebuild(viewport);
		}
	}
}
