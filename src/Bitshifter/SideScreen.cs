using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Bitshifter
{
	/// <summary>
	/// The bit selector side screen is a fixed-height panel whose rows sit in a vertical
	/// layout; four rows fit, thirty-one do not. The first time the screen is shown, the rows'
	/// container is moved into a scroll view one and a half times the height the four rows
	/// had (tall enough to make it obvious there is more below), with a permanent scrollbar
	/// on the right. The scroll view is the game's own KScrollRect and the bar is cloned from
	/// a vanilla side screen, so both look and feel like the rest of the details panel.
	/// </summary>
	[HarmonyPatch(typeof(LogicBitSelectorSideScreen), nameof(LogicBitSelectorSideScreen.SetTarget))]
	internal static class LogicBitSelectorSideScreen_SetTarget_Patch
	{
		private const int VisibleRows = 4;
		private const float HeightScale = 1.5f;
		private const float FallbackRowHeight = 32f;
		private const float FallbackHeaderWidth = 280f;
		private const float BarWidth = 12f;
		private const float BarGap = 2f;
		private static readonly Color TrackColor = new Color(0.10f, 0.10f, 0.12f, 0.85f);
		private static readonly Color HandleColor = new Color(0.62f, 0.64f, 0.68f, 1f);

		private static void Postfix(LogicBitSelectorSideScreen __instance)
		{
			if (__instance.rowPrefab == null)
				return;
			RectTransform rows = __instance.rowPrefab.transform.parent as RectTransform;
			if (rows == null || rows.parent == null || IsWrapped(rows))
				return;
			Wrap(rows, RowListHeight(__instance.rowPrefab, rows) * HeightScale);
			FitUnderHeader(__instance);
		}

		/// <summary>
		/// True once the row list sits in the scroll view. The details screen calls SetTarget
		/// before it re-shows a hidden side screen, so this checks the direct parent rather
		/// than GetComponentInParent, which skips inactive objects and would wrap the list
		/// again (a scroll view inside a scroll view) every time the screen is reopened.
		/// </summary>
		private static bool IsWrapped(RectTransform rows)
		{
			return rows.parent.GetComponent<KScrollRect>() != null;
		}

		/// <summary>Height the row list has with the vanilla four rows.</summary>
		private static float RowListHeight(GameObject rowPrefab, RectTransform rows)
		{
			RectTransform row = rowPrefab.GetComponent<RectTransform>();
			float rowHeight = row != null && row.rect.height > 0f ? row.rect.height : FallbackRowHeight;
			VerticalLayoutGroup layout = rows.GetComponent<VerticalLayoutGroup>();
			float spacing = layout != null ? layout.spacing : 0f;
			float padding = layout != null ? layout.padding.vertical : 0f;
			return rowHeight * VisibleRows + spacing * (VisibleRows - 1) + padding;
		}

		/// <summary>Puts the row list inside a new scroll view of the given height, in its old place.</summary>
		private static void Wrap(RectTransform rows, float height)
		{
			GameObject scroll = new GameObject("BitshifterScroll", typeof(RectTransform), typeof(RectMask2D), typeof(KScrollRect), typeof(LayoutElement));
			RectTransform viewport = scroll.GetComponent<RectTransform>();
			viewport.SetParent(rows.parent, false);
			viewport.SetSiblingIndex(rows.GetSiblingIndex());
			LayoutElement size = scroll.GetComponent<LayoutElement>();
			size.minHeight = height;
			size.preferredHeight = height;
			size.flexibleHeight = 0f;
			size.flexibleWidth = 1f;

			// The list hangs from the viewport's top edge and grows with its rows.
			rows.SetParent(viewport, false);
			rows.anchorMin = new Vector2(0f, 1f);
			rows.anchorMax = new Vector2(1f, 1f);
			rows.pivot = new Vector2(0.5f, 1f);
			rows.anchoredPosition = Vector2.zero;
			rows.sizeDelta = new Vector2(0f, rows.sizeDelta.y);
			rows.gameObject.AddOrGet<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			Scrollbar bar = CloneVanillaScrollbar(viewport) ?? MakeScrollbar(viewport);
			rows.offsetMax = new Vector2(-(bar.GetComponent<RectTransform>().sizeDelta.x + BarGap), rows.offsetMax.y);

			KScrollRect scrollRect = scroll.GetComponent<KScrollRect>();
			scrollRect.content = rows;
			scrollRect.viewport = viewport;
			scrollRect.horizontal = false;
			scrollRect.vertical = true;
			scrollRect.movementType = ScrollRect.MovementType.Clamped;
			scrollRect.allowVerticalScrollWheel = true;
			scrollRect.verticalScrollbar = bar;
			scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
			LayoutRebuilder.MarkLayoutForRebuild(viewport);
		}

		/// <summary>
		/// The screen's "Contents" object declares a minimum width of 316 while the details
		/// panel's Options header is 280 wide. The panel root takes its children's minimum
		/// width, so the body would grow past the header and leave a blank strip at the top
		/// left. Clamp every width to the header's, and have the screen's layout groups size
		/// their children to it (they only expand them, so the children would keep the widths
		/// they were built with). The rows need 250.
		/// </summary>
		private static void FitUnderHeader(LogicBitSelectorSideScreen screen)
		{
			float width = HeaderWidth();
			foreach (LayoutElement element in screen.GetComponentsInChildren<LayoutElement>(true))
			{
				element.minWidth = Mathf.Min(element.minWidth, width);
				element.preferredWidth = Mathf.Min(element.preferredWidth, width);
			}
			foreach (VerticalLayoutGroup group in screen.GetComponentsInChildren<VerticalLayoutGroup>(true))
			{
				group.childControlWidth = true;
				group.childForceExpandWidth = true;
			}
		}

		private static float HeaderWidth()
		{
			DetailsScreen details = DetailsScreen.Instance;
			GameObject header = details != null ? AccessTools.Field(typeof(DetailsScreen), "sidescreenTabHeader")?.GetValue(details) as GameObject : null;
			RectTransform rect = header != null ? header.GetComponent<RectTransform>() : null;
			return rect != null && rect.rect.width > 0f ? rect.rect.width : FallbackHeaderWidth;
		}

		/// <summary>
		/// A copy of the vertical scrollbar of the first vanilla side screen that has one,
		/// pinned to the viewport's right edge at its own width; null when none can be found.
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
			float width = template.GetComponent<RectTransform>().rect.width;
			PinToRightEdge(barObject.GetComponent<RectTransform>(), width > 0f ? width : BarWidth);
			Scrollbar scrollbar = barObject.GetComponent<Scrollbar>();
			scrollbar.onValueChanged.RemoveAllListeners();
			scrollbar.direction = Scrollbar.Direction.BottomToTop;
			return scrollbar;
		}

		private static Scrollbar FindVanillaScrollbar()
		{
			DetailsScreen details = DetailsScreen.Instance;
			var screens = details != null ? AccessTools.Field(typeof(DetailsScreen), "sideScreens")?.GetValue(details) as List<DetailsScreen.SideScreenRef> : null;
			if (screens == null)
				return null;
			foreach (DetailsScreen.SideScreenRef screen in screens)
			{
				if (screen.screenPrefab == null)
					continue;
				foreach (ScrollRect rect in screen.screenPrefab.GetComponentsInChildren<ScrollRect>(true))
				{
					if (rect.vertical && rect.verticalScrollbar != null)
						return rect.verticalScrollbar;
				}
			}
			return null;
		}

		/// <summary>Fallback: a plain track-and-handle scrollbar in flat colours down the viewport's right edge.</summary>
		private static Scrollbar MakeScrollbar(RectTransform viewport)
		{
			GameObject barObject = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
			RectTransform bar = barObject.GetComponent<RectTransform>();
			bar.SetParent(viewport, false);
			PinToRightEdge(bar, BarWidth);
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

		private static void PinToRightEdge(RectTransform bar, float width)
		{
			bar.anchorMin = new Vector2(1f, 0f);
			bar.anchorMax = new Vector2(1f, 1f);
			bar.pivot = new Vector2(1f, 0.5f);
			bar.anchoredPosition = Vector2.zero;
			bar.sizeDelta = new Vector2(width, 0f);
		}
	}
}
