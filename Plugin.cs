using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Photon.Pun;
using System.Collections.Generic;
using UnityEngine;

namespace RoundsRandom
{
    public enum SelectionMode
    {
        Random,
        Worst,
        Best
    }

    [BepInPlugin("com.dandol328.rounds.random", "RoundsRandom", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<SelectionMode> Mode;
        private Harmony harmony;

        private void Awake()
        {
            Enabled = Config.Bind("General", "Enabled", true, "Automatically choose cards instead of allowing manual picks.");
            Mode = Config.Bind("General", "Mode", SelectionMode.Random, "Random, Worst (lowest rarity), or Best (highest rarity).");
            harmony = new Harmony("com.dandol328.rounds.random");
            harmony.PatchAll();
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }

    [HarmonyPatch(typeof(CardChoice), "DoPlayerSelect")]
    internal static class AutomaticPick
    {
        private const float DisplaySeconds = 2f;
        private static GameObject firstCard;
        private static int pickerId = -1;
        private static float readyAt;

        private static bool Prefix(CardChoice __instance, List<GameObject> ___spawnedCards, bool ___isPlaying)
        {
            if (!Plugin.Enabled.Value)
            {
                firstCard = null;
                return true;
            }

            if (___isPlaying || ___spawnedCards.Count == 0 || !___spawnedCards[0])
            {
                firstCard = null;
                return false;
            }

            GameObject card = ___spawnedCards[0];
            PhotonView view = card.GetComponent<PhotonView>();
            if (!view || !view.IsMine)
            {
                firstCard = null;
                return false;
            }

            if (firstCard != card || pickerId != __instance.pickrID)
            {
                firstCard = card;
                pickerId = __instance.pickrID;
                readyAt = Time.realtimeSinceStartup + DisplaySeconds;
            }

            if (Time.realtimeSinceStartup < readyAt)
            {
                return false;
            }

            GameObject selected = null;
            int bestRarity = 0;
            int ties = 0;
            foreach (GameObject offered in ___spawnedCards)
            {
                if (!offered) continue;
                CardInfo info = offered.GetComponent<CardInfo>();
                if (!info) continue;

                int rarity = (int)info.rarity;
                if (selected == null ||
                    (Plugin.Mode.Value == SelectionMode.Worst && rarity < bestRarity) ||
                    (Plugin.Mode.Value == SelectionMode.Best && rarity > bestRarity))
                {
                    selected = offered;
                    bestRarity = rarity;
                    ties = 1;
                }
                else if (Plugin.Mode.Value == SelectionMode.Random || rarity == bestRarity)
                {
                    if (Random.Range(0, ++ties) == 0) selected = offered;
                }
            }

            if (selected)
            {
                __instance.Pick(selected, false);
                __instance.pickrID = -1;
                firstCard = null;
            }
            return false;
        }
    }
}
