using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnboundLib;
using UnboundLib.Networking;
using UnboundLib.Utils.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RoundsRandom
{
    public enum SelectionMode
    {
        Random,
        Worst,
        Best
    }

    [BepInPlugin("com.dandol328.rounds.random", "RoundsRandom", "1.0.0")]
    [BepInDependency("com.willis.rounds.unbound", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<SelectionMode> Mode;
        internal static ConfigEntry<int> CardsPerRound;
        internal static SelectionMode EffectiveMode { get; private set; }
        internal static int EffectiveCardsPerRound { get; private set; }
        internal static event Action SettingsChanged;
        private Harmony harmony;

        private void Awake()
        {
            Enabled = Config.Bind("General", "Enabled", true, "Automatically choose cards instead of allowing manual picks.");
            Mode = Config.Bind("General", "Mode", SelectionMode.Random, "Random, Worst (lowest rarity), or Best (highest rarity).");
            CardsPerRound = Config.Bind("General", "CardsPerRound", 1, "Number of cards to automatically choose each round, from 1 to 5.");
            EffectiveMode = Mode.Value;
            EffectiveCardsPerRound = Mathf.Clamp(CardsPerRound.Value, 1, 5);
            Unbound.RegisterMenu("Rounds Random", null, CreateModMenu, null, false);
            var networkCallbacks = gameObject.AddComponent<NetworkEventCallbacks>();
            networkCallbacks.OnJoinedRoomEvent += OnJoinedRoom;
            harmony = new Harmony("com.dandol328.rounds.random");
            harmony.PatchAll();
        }

        private static void OnJoinedRoom()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                EffectiveMode = Mode.Value;
                EffectiveCardsPerRound = Mathf.Clamp(CardsPerRound.Value, 1, 5);
                BroadcastSettings();
            }
            else
            {
                NetworkingManager.RPC(typeof(HostModeSync), nameof(HostModeSync.RequestSettings));
            }
        }

        private static void SetMode(SelectionMode mode)
        {
            if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
            {
                return;
            }

            Mode.Value = mode;
            EffectiveMode = mode;
            if (PhotonNetwork.InRoom)
            {
                BroadcastSettings();
            }
            SettingsChanged?.Invoke();
        }

        private static void SetCardsPerRound(int cardsPerRound)
        {
            if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
            {
                return;
            }

            CardsPerRound.Value = cardsPerRound;
            EffectiveCardsPerRound = Mathf.Clamp(cardsPerRound, 1, 5);
            if (PhotonNetwork.InRoom)
            {
                BroadcastSettings();
            }
            SettingsChanged?.Invoke();
        }

        private static void BroadcastSettings()
        {
            NetworkingManager.RPC_Others(
                typeof(HostModeSync),
                nameof(HostModeSync.ReceiveSettings),
                (int)EffectiveMode,
                EffectiveCardsPerRound);
        }

        internal static void ApplyHostSettings(int mode, int cardsPerRound)
        {
            if (!Enum.IsDefined(typeof(SelectionMode), mode))
            {
                Debug.LogError($"RoundsRandom received invalid host mode value: {mode}.");
                return;
            }

            EffectiveMode = (SelectionMode)mode;
            EffectiveCardsPerRound = Mathf.Clamp(cardsPerRound, 1, 5);
            SettingsChanged?.Invoke();
        }

        private static void CreateModMenu(GameObject menu)
        {
            MenuHandler.CreateToggle(
                Enabled.Value,
                "Automatically choose cards",
                menu,
                value => Enabled.Value = value);

            GameObject modeButton = null;
            modeButton = MenuHandler.CreateButton(
                GetModeLabel(),
                menu,
                () =>
                {
                    SetMode((SelectionMode)(((int)EffectiveMode + 1) % 3));
                    modeButton.GetComponentInChildren<TextMeshProUGUI>().text = GetModeLabel().ToUpperInvariant();
                });

            GameObject cardsButton = null;
            cardsButton = MenuHandler.CreateButton(
                GetCardsPerRoundLabel(),
                menu,
                () =>
                {
                    SetCardsPerRound(EffectiveCardsPerRound % 5 + 1);
                    cardsButton.GetComponentInChildren<TextMeshProUGUI>().text = GetCardsPerRoundLabel().ToUpperInvariant();
                });

            MenuHandler.CreateText(
                "Mode cycles through random, worst, and best.",
                menu,
                out _,
                35,
                false);

            SettingsChanged += () =>
            {
                modeButton.GetComponentInChildren<TextMeshProUGUI>().text = GetModeLabel().ToUpperInvariant();
                cardsButton.GetComponentInChildren<TextMeshProUGUI>().text = GetCardsPerRoundLabel().ToUpperInvariant();
            };
        }

        private static string GetModeLabel()
        {
            return $"Mode: {EffectiveMode}";
        }

        private static string GetCardsPerRoundLabel()
        {
            return $"Cards per round: {EffectiveCardsPerRound}";
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

            if (___spawnedCards.Count == 0 || !___spawnedCards[0])
            {
                firstCard = null;
                return false;
            }

            GameObject card = ___spawnedCards[0];
            PhotonView view = card.GetComponent<PhotonView>();
            if (!view)
            {
                firstCard = null;
                return false;
            }

            if (___isPlaying)
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

            if (!view.IsMine) return false;

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
                    (Plugin.EffectiveMode == SelectionMode.Worst && rarity < bestRarity) ||
                    (Plugin.EffectiveMode == SelectionMode.Best && rarity > bestRarity))
                {
                    selected = offered;
                    bestRarity = rarity;
                    ties = 1;
                }
                else if (Plugin.EffectiveMode == SelectionMode.Random || rarity == bestRarity)
                {
                    if (UnityEngine.Random.Range(0, ++ties) == 0) selected = offered;
                }
            }

            if (selected)
            {
                __instance.Pick(selected, false);
                firstCard = null;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(CardChoice), "DoPick")]
    internal static class PickCountPatch
    {
        private static void Prefix(ref int picksToSet)
        {
            picksToSet = Plugin.EffectiveCardsPerRound;
        }
    }

    [HarmonyPatch(typeof(CardChoice), "Update")]
    internal static class CardRevealPatch
    {
        private const float RevealInterval = 0.25f;
        private static readonly Dictionary<CardChoice, RevealState> States = new Dictionary<CardChoice, RevealState>();

        private sealed class RevealState
        {
            internal readonly int PickerId;
            internal readonly Queue<GameObject> Cards = new Queue<GameObject>();
            internal readonly HashSet<GameObject> QueuedCards = new HashSet<GameObject>();
            internal bool Running;
            internal bool PickerShown;

            internal RevealState(int pickerId)
            {
                PickerId = pickerId;
            }
        }

        private static void Postfix(CardChoice __instance, List<GameObject> ___spawnedCards, int ___pickrID)
        {
            if (___pickrID < 0) return;

            if (!States.TryGetValue(__instance, out RevealState state) ||
                state.PickerId != ___pickrID)
            {
                state = new RevealState(___pickrID);
                States[__instance] = state;
            }

            if (!state.PickerShown && CardChoiceVisuals.instance)
            {
                CardChoiceVisuals.instance.Show(___pickrID, true);
                state.PickerShown = true;
            }

            foreach (GameObject card in ___spawnedCards)
            {
                if (!card || !state.QueuedCards.Add(card)) continue;
                state.Cards.Enqueue(card);
            }

            if (!state.Running)
            {
                state.Running = true;
                __instance.StartCoroutine(RevealQueuedCards(state));
            }
        }

        private static IEnumerator RevealQueuedCards(RevealState state)
        {
            yield return new WaitForSecondsRealtime(RevealInterval);

            while (state.Cards.Count > 0)
            {
                GameObject card = state.Cards.Dequeue();
                if (card)
                {
                    yield return null;
                    CardVisuals visuals = card.GetComponentInChildren<CardVisuals>();
                    if (visuals)
                    {
                        visuals.ChangeSelected(true);

                        PhotonView view = card.GetComponent<PhotonView>();
                        if (view && view.IsMine)
                        {
                            CardRevealSync.BroadcastCard(view.ViewID);
                        }
                    }
                }

                yield return new WaitForSecondsRealtime(RevealInterval);
            }

            state.Running = false;
        }
    }

    internal static class HostModeSync
    {
        [UnboundRPC]
        internal static void RequestSettings()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                NetworkingManager.RPC_Others(
                    typeof(HostModeSync),
                    nameof(ReceiveSettings),
                    (int)Plugin.EffectiveMode,
                    Plugin.EffectiveCardsPerRound);
            }
        }

        [UnboundRPC]
        internal static void ReceiveSettings(int mode, int cardsPerRound)
        {
            Plugin.ApplyHostSettings(mode, cardsPerRound);
        }
    }

    internal static class CardRevealSync
    {
        internal static void BroadcastCard(int viewId)
        {
            NetworkingManager.RPC(
                typeof(CardRevealSync),
                nameof(RevealCard),
                viewId);
        }

        [UnboundRPC]
        internal static void RevealCard(int viewId)
        {
            PhotonView view = PhotonNetwork.GetPhotonView(viewId);
            if (!view)
            {
                return;
            }

            CardChoice.instance.StartCoroutine(RevealWhenReady(view.gameObject));
        }

        private static IEnumerator RevealWhenReady(GameObject card)
        {
            for (int frame = 0; frame < 60 && card; frame++)
            {
                CardVisuals visuals = card.GetComponentInChildren<CardVisuals>();
                if (visuals)
                {
                    visuals.ChangeSelected(true);
                    yield break;
                }

                yield return null;
            }
        }
    }
}
