using System.Collections.Generic;
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
	// into a scroll view one and a half times the height the four rows had (tall enough to
	// make it obvious there is more below), with a permanent scrollbar on the right. The
	// scroll view is the game's own KScrollRect and the bar is cloned from a vanilla side
	// screen, so both look and feel like the rest of the details panel.
	[HarmonyPatch(typeof(LogicBitSelectorSideScreen), nameof(LogicBitSelectorSideScreen.SetTarget))]
	internal static class LogicBitSelectorSideScreen_SetTarget_Patch
	{
		private const string ScrollName = "BitshifterScroll";
		private const int VisibleRows = 4;
		private const float HeightScale = 1.5f;
		private const float BarWidth = 12f;
		private const float BarGap = 4f;
		private static readonly Color TrackColor = new Color(0.10f, 0.10f, 0.12f, 0.85f);
		private static readonly Color HandleColor = new Color(0.62f, 0.64f, 0.68f, 1f);

		private static void Prefix(LogicBitSelectorSideScreen __instance, out float __state)
		{
			// Height of the row list as laid out for the vanilla depth, before more rows exist.
			RectTransform rows = __instance.rowPrefab != null ? __instance.rowPrefab.transform.parent as RectTransform : null;
			__state = rows != null && !IsWrapped(rows) ? rows.rect.height : 0f;
		}

		/// <summary>
		/// True once the row list sits in the scroll view. The details screen calls SetTarget
		/// before it re-shows a hidden side screen, so this must not rely on
		/// GetComponentInParent, which skips inactive objects and would wrap the list again
		/// (a scroll view inside a scroll view) every time the screen is reopened.
		/// </summary>
		private static bool IsWrapped(RectTransform rows)
		{
			return rows.parent != null && rows.parent.GetComponent<KScrollRect>() != null;
		}

		private static void Postfix(LogicBitSelectorSideScreen __instance, float __state)
		{
			if (__instance.rowPrefab == null)
				return;
			RectTransform rows = __instance.rowPrefab.transform.parent as RectTransform;
			if (rows == null || rows.parent == null || IsWrapped(rows))
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

			GameObject scroll = new GameObject(ScrollName, typeof(RectTransform), typeof(RectMask2D), typeof(KScrollRect), typeof(LayoutElement));
			RectTransform viewport = scroll.GetComponent<RectTransform>();
			viewport.SetParent(rows.parent, false);
			viewport.SetSiblingIndex(rows.GetSiblingIndex());
			height *= HeightScale;
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

			KScrollRect scrollRect = scroll.GetComponent<KScrollRect>();
			scrollRect.content = rows;
			scrollRect.viewport = viewport;
			scrollRect.horizontal = false;
			scrollRect.vertical = true;
			scrollRect.movementType = ScrollRect.MovementType.Clamped;
			scrollRect.allowVerticalScrollWheel = true;
			Scrollbar bar = CloneVanillaScrollbar(viewport) ?? MakeScrollbar(viewport);
			float barWidth = bar.GetComponent<RectTransform>().sizeDelta.x;
			rows.offsetMax = new Vector2(-(barWidth + BarGap), rows.offsetMax.y);
			scrollRect.verticalScrollbar = bar;
			scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
			LayoutRebuilder.MarkLayoutForRebuild(viewport);
			FitUnderHeader(__instance);
		}

		/// <summary>
		/// The screen's "Contents" object declares a minimum width of 316 while the details
		/// panel's Options header is 280 wide. The panel root takes its children's minimum
		/// width, so the body grows past the header and a blank strip shows at the top left.
		/// Clamp the minimum to the header's width; the rows need 266.
		/// </summary>
		private static void FitUnderHeader(LogicBitSelectorSideScreen screen)
		{
			float width = HeaderWidth();
			foreach (LayoutElement element in screen.GetComponentsInChildren<LayoutElement>(true))
			{
				if (element.minWidth > width)
					element.minWidth = width;
				if (element.preferredWidth > width)
					element.preferredWidth = width;
			}
		}

		private static float HeaderWidth()
		{
			DetailsScreen details = DetailsScreen.Instance;
			GameObject header = details != null ? AccessTools.Field(typeof(DetailsScreen), "sidescreenTabHeader")?.GetValue(details) as GameObject : null;
			RectTransform rect = header != null ? header.GetComponent<RectTransform>() : null;
			return rect != null && rect.rect.width > 0f ? rect.rect.width : 280f;
		}

		/// <summary>
		/// A copy of the vertical scrollbar of the first vanilla side screen that has one (the
		/// receptacle side screen, usually), pinned to the viewport's right edge at its own
		/// width. Null when none can be found.
		/// </summary>
		private static Scrollbar CloneVanillaScrollbar(RectTransform viewport)
		{
			Scrollbar template = FindVanillaScrollbar();
			if (template == null)
			{
				Debug.LogWarning("[Bitshifter] No vanilla scrollbar to copy; using a plain one");
				return null;
			}
			GameObject barObject = Object.Instantiate(template.gameObject, viewport, false);
			barObject.name = "Scrollbar";
			barObject.SetActive(true);
			RectTransform bar = barObject.GetComponent<RectTransform>();
			float width = template.GetComponent<RectTransform>().rect.width;
			if (width <= 0f)
				width = BarWidth;
			bar.anchorMin = new Vector2(1f, 0f);
			bar.anchorMax = new Vector2(1f, 1f);
			bar.pivot = new Vector2(1f, 0.5f);
			bar.anchoredPosition = Vector2.zero;
			bar.sizeDelta = new Vector2(width, 0f);
			Scrollbar scrollbar = barObject.GetComponent<Scrollbar>();
			scrollbar.onValueChanged.RemoveAllListeners();
			scrollbar.direction = Scrollbar.Direction.BottomToTop;
			return scrollbar;
		}

		private static Scrollbar FindVanillaScrollbar()
		{
			DetailsScreen details = DetailsScreen.Instance;
			if (details != null)
			{
				var refs = AccessTools.Field(typeof(DetailsScreen), "sideScreens")?.GetValue(details) as List<DetailsScreen.SideScreenRef>;
				if (refs != null)
				{
					foreach (DetailsScreen.SideScreenRef screen in refs)
					{
						if (screen.screenPrefab == null)
							continue;
						foreach (ScrollRect rect in screen.screenPrefab.GetComponentsInChildren<ScrollRect>(true))
						{
							if (rect.vertical && rect.verticalScrollbar != null)
								return rect.verticalScrollbar;
						}
					}
				}
			}
			foreach (Scrollbar candidate in Resources.FindObjectsOfTypeAll<Scrollbar>())
			{
				if (candidate.direction == Scrollbar.Direction.BottomToTop && candidate.handleRect != null
					&& candidate.handleRect.GetComponent<Image>()?.sprite != null)
					return candidate;
			}
			return null;
		}

		/// <summary>Fallback: a plain track-and-handle scrollbar (flat colours, no sprites) down the viewport's right edge.</summary>
		private static Scrollbar MakeScrollbar(RectTransform viewport)
		{
			GameObject barObject = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
			RectTransform bar = barObject.GetComponent<RectTransform>();
			bar.SetParent(viewport, false);
			bar.anchorMin = new Vector2(1f, 0f);
			bar.anchorMax = new Vector2(1f, 1f);
			bar.pivot = new Vector2(1f, 0.5f);
			bar.anchoredPosition = Vector2.zero;
			bar.sizeDelta = new Vector2(BarWidth, 0f);
			barObject.GetComponent<Image>().color = TrackColor;

			GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
			RectTransform handle = handleObject.GetComponent<RectTransform>();
			handle.SetParent(bar, false);
			handle.anchorMin = Vector2.zero;
			handle.anchorMax = Vector2.one;
			handle.offsetMin = new Vector2(2f, 2f);
			handle.offsetMax = new Vector2(-2f, -2f);
			Image handleImage = handleObject.GetComponent<Image>();
			handleImage.color = HandleColor;

			Scrollbar scrollbar = barObject.GetComponent<Scrollbar>();
			scrollbar.handleRect = handle;
			scrollbar.targetGraphic = handleImage;
			scrollbar.direction = Scrollbar.Direction.BottomToTop;
			return scrollbar;
		}
	}
}
