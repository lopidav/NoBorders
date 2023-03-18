using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using BepInEx.Configuration;

namespace NoBordersNS;

[BepInPlugin("NoBorders", "NoBorders", "0.1.0")]
public class NoBordersPlugin : BaseUnityPlugin
{
	public static ManualLogSource L;

	public static Harmony HarmonyInstance;
	public static float makeBoardsThatSlimmer = 0.01f;
	private static ConfigEntry<bool> ShowBorderVisuals;
	private static ConfigEntry<bool> BoardColorAsCenter;
	private void Awake()
	{
		L = ((NoBordersPlugin)this).Logger;
		
		try
		{
			HarmonyInstance = new Harmony("NoBordersPlugin");
			HarmonyInstance.PatchAll(typeof(NoBordersPlugin));
		}
		catch (Exception ex3)
		{
			Log("Patching failed: " + ex3.Message);
		}
		string description = "";
		description = $"Show lines of the border. You will still be able to go throught it tho.";
		ShowBorderVisuals = Config.Bind("--", "Show border visuals", false, description);
		ShowBorderVisuals.SettingChanged += (_1, _2) => SetCameraBackground(WorldManager.instance.CurrentBoard);

		description = $"Should the colour that usually within borders expend on everything? If no then the colour that yousually out of border will capture the center.";
		BoardColorAsCenter = Config.Bind("--", "Board color as center", true, description);
		BoardColorAsCenter.SettingChanged += (_1, _2) => SetCameraBackground(WorldManager.instance.CurrentBoard);
	}
	public static void Log(string s)
	{
		L.LogInfo((object)(DateTime.Now.ToString("HH:MM:ss") + ": " + s));
	}

	/*
	[HarmonyPatch(typeof(WorldManager), "DetermineTargetWorldSize")]
	[HarmonyPrefix]
	private static bool WorldManager__DetermineTargetWorldSize__Prefix(GameBoard board, ref float __result)
	{
		__result = 20f;
		return false;
	}*/
	/*
	[HarmonyPatch(typeof(WorldManager), "Awake")]
	[HarmonyPostfix]
	private static void WorldManager__Awake__Postfix()
	{
		WorldManager.instance.Boards.ForEach(board => {
			board.transform.localScale = new Vector3(board.transform.localScale.x, 0.01f, board.transform.localScale.z);
			board.transform.position = new Vector3(board.transform.position.x, -0.28f, board.transform.position.y);
			});
	}*/
	[HarmonyPatch(typeof(Draggable), "ClampPos")]
	[HarmonyPrefix]
	private static bool Draggable__ClampPos__Prefix()
	{
		return false;
	}
	[HarmonyPatch(typeof(GameCamera), "ClampPos")]
	[HarmonyPrefix]
	private static bool GameCamera__ClampPos__Prefix(Vector3 p, ref Vector3 __result)
	{
		__result = p;
		return false;
	}
	[HarmonyPatch(typeof(GameCard), "ClampPos2")]
	[HarmonyPrefix]
	private static bool GameCard__ClampPos2__Prefix(Vector3 p, ref Vector3 __result)
	{
		__result = p;
		return false;
	}
	[HarmonyPatch(typeof(WorldManager), "Awake")]
	[HarmonyPostfix]
	[HarmonyPriority(Priority.VeryLow)]
	public static void WorldManager__Awake__Prefix(WorldManager __instance)
	{
		foreach (GameBoard board in __instance.Boards)
		{
			Vector3 scale = board.transform.localScale;
			Vector3 position = board.transform.localPosition;
			Vector3 introPosition = board.CameraIntroPosition.transform.localPosition;
			position.y = position.y + scale.y / 2f + scale.y * makeBoardsThatSlimmer / 2f;
			scale.y *= makeBoardsThatSlimmer;
			introPosition.y *= 1 / makeBoardsThatSlimmer;
			board.transform.localScale = scale;
			board.transform.localPosition = position;
			board.CameraIntroPosition.transform.localPosition = introPosition;

		}
	}
	[HarmonyPatch(typeof(WorldManager), "GoToBoard")]
	[HarmonyPrefix]
	private static void WorldManager__GoToBoard__Prefix(GameBoard newBoard, ref Action onComplete, WorldManager __instance)
	{
		Action oldOnComplete = onComplete;
		onComplete = delegate
		{
			newBoard.gameObject.SetActive(true);
			__instance.GetBoardWithId(__instance.CurrentRunVariables.PreviouseBoard).gameObject.SetActive(false);

			foreach (Draggable drag in __instance.AllDraggables.ToList())
			{
				drag.gameObject.SetActive(drag.MyBoard == newBoard);
			}
			SetCameraBackground(newBoard);
			oldOnComplete?.Invoke();
		};
	}

	[HarmonyPatch(typeof(WorldManager), "LoadSaveRound")]
	[HarmonyPostfix]
	private static void WorldManager__LoadSaveRound__Postfix(WorldManager __instance)
	{
		foreach (GameBoard allBoard in __instance.Boards)
		{
			allBoard.gameObject.SetActive(allBoard == __instance.CurrentBoard);
		}
		foreach (Draggable drag in __instance.AllDraggables)
		{
			drag.gameObject.SetActive(drag.MyBoard == __instance.CurrentBoard);
		}
		
		SetCameraBackground(__instance.CurrentBoard);
	}

	[HarmonyPatch(typeof(WorldManager), "StartNewRound")]
	[HarmonyPostfix]
	private static void WorldManager__StartNewRound__Postfix(WorldManager __instance)
	{
		foreach (GameBoard allBoard in __instance.Boards)
		{
			allBoard.gameObject.SetActive(allBoard == __instance.CurrentBoard);
		}
		foreach (Draggable drag in __instance.AllDraggables)
		{
			drag.gameObject.SetActive(drag.MyBoard == __instance.CurrentBoard);
		}
		SetCameraBackground(__instance.CurrentBoard);
	}
	public static void SetCameraBackground(GameBoard board)
	{
		Color backColor =
			board.Location == Location.Mainland
			? new Color32(180, 231, 180, 255)
			: board.Location == Location.Island
			 ?  new Color32(250, 245, 223, 255)
			 : board.Location == Location.Forest
			  ? new Color32(125, 140, 165, 255)
			  : (Color)board.gameObject.GetComponent<MeshRenderer>()?.material?.color;
		Camera mainCamera = GameCamera.instance.gameObject.GetComponent<Camera>();
		if (mainCamera != null && backColor != null)
		{
			mainCamera.backgroundColor = backColor;
		}
	}
	[HarmonyPatch(typeof(WorldManager), "SendToBoard")]
	[HarmonyPrefix]
	private static void WorldManager__SendToBoard__Prefix(GameCard rootCard, GameBoard newBoard, Vector2 normalizedPos, WorldManager __instance)
	{
		if (newBoard == __instance.CurrentBoard)
		{
			rootCard.gameObject.SetActive(true);
			foreach (GameCard childCard in rootCard.GetChildCards())
			{
				childCard.gameObject.SetActive(true);
			}
			foreach (GameCard item in rootCard.GetAllCardsInStack())
			{
				foreach (GameCard equipmentChild in item.EquipmentChildren)
				{
					equipmentChild.gameObject.SetActive(true);
				}
			}
		}
	}

	[HarmonyPatch(typeof(WorldManager), "SendToBoard")]
	[HarmonyPostfix]
	private static void WorldManager__SendToBoard__Postfix(GameCard rootCard, GameBoard newBoard, Vector2 normalizedPos, WorldManager __instance)
	{
		if (newBoard != __instance.CurrentBoard)
		{
			rootCard.gameObject.SetActive(false);
			foreach (GameCard childCard in rootCard.GetChildCards())
			{
				childCard.gameObject.SetActive(false);
			}
			foreach (GameCard item in rootCard.GetAllCardsInStack())
			{
				foreach (GameCard equipmentChild in item.EquipmentChildren)
				{
					equipmentChild.gameObject.SetActive(false);
				}
			}
		}
	}
	[HarmonyPatch(typeof(GameBoard), "Update")]
	[HarmonyPostfix]
	[HarmonyPriority(Priority.LowerThanNormal)]
	public static void GameBoard__Update__Postfix()
	{
		if (!ShowBorderVisuals.Value)
		{
			Shader.SetGlobalFloat("_WorldSizeIncrease", BoardColorAsCenter.Value ? 100f : -100f);
		}
	}

	
	
}
