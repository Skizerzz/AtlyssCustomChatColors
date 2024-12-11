using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Net;
using System.Runtime.Remoting.Messaging;
using System.Text.RegularExpressions;
using UnityEngine;
using Newtonsoft.Json;
using System.Reflection;
using System.IO;
using System;
using Mirror;
using UnityEngine.UIElements;


namespace CustomChatColors {
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin {
        internal static new ManualLogSource Logger;
        private static string DatabaseFilename { get; } = "/chatColorDatabase";

        /// <summary>
        /// steamID, colorHex.  Can be null if failed to retrieve database.
        /// </summary>
        private static Dictionary<string, string> _customChatColors;
        private static HashSet<string> _noChatColors;

        /// <summary>
        /// steamID, colorHex
        /// </summary>
        //private static Dictionary<string, string> _customNameColors;

        private static ChatColorDatabase chatColorDatabase;

        private void Awake() {
            // Plugin startup logic
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

            _customChatColors = new Dictionary<string, string>();
            _noChatColors = new HashSet<string>();
            //_customNameColors = new Dictionary<string, string>();

            string assemblyDirectoryPath = GetAssemblyDirectoryPath();
            if(assemblyDirectoryPath != null) {
                chatColorDatabase = new ChatColorDatabase(assemblyDirectoryPath + DatabaseFilename);
            } else {
                Logger.LogError("The database could not be created. Path was invalid");
            }

            Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
        }

        private string GetAssemblyDirectoryPath() {
            string path = null;
            try {
                string assemblyLoc = Assembly.GetExecutingAssembly().Location;
                string assemblyDir = Path.GetDirectoryName(assemblyLoc);
                path = assemblyDir;
            } catch (Exception e) {
                Logger.LogWarning("Failed to retrieve executing assembly directory.");
            }

            return path;
        }

        /// <summary>
        /// This is called server-side.
        /// </summary>
        [HarmonyPatch(typeof(ChatBehaviour), "Rpc_RecieveChatMessage")]
        public static class ChatBehaviour_Rpc_RecieveChatMessage {
            public static bool Prefix(ref ChatBehaviour __instance, ref string message) {
                bool updatedColor = false;

                // check to see if the player is wanting to change their chatcolor or namecolor
                if (message != null) {
                    string colorHex;
                    Player player = __instance.GetComponent<Player>();
                    if (player == null) {
                        Logger.LogWarning("ChatBehaviour instance had no Player component.");
                        return true;
                    }

                    if (message.Contains("/chatcolor") && message.Length > 11 && message.Substring(0, 10).Equals("/chatcolor")) {
                        string hex = message.Substring(11); // chop off /chatcolor and the space after
                        hex = hex.Replace(" ", ""); // get rid of whitespace
                        // check if valid hex
                        string pattern = @"^#([0-9A-Fa-f]{6})$";
                        Regex regex = new Regex(pattern);
                        bool isMatch = regex.IsMatch(hex);

                        bool isClearCmd = hex.ToLower().Equals("clear");
                        if (isClearCmd) {
                            _customChatColors.Remove(player._steamID);
                            _noChatColors.Add(player._steamID);
                            return false;
                        }

                        if (!isMatch) return true;

                        _customChatColors[player._steamID] = hex;
                        _noChatColors.Remove(player._steamID);
                        //if (chatColorDatabase != null) chatColorDatabase.SetUserColor(player._steamID, hex);
                        updatedColor = true;
                    // disabling name colors, causes issues when they leave the server by overriding their name in their savefile
                    // this is a bad problem
                    }// else if(message.Contains("/namecolor") && message.Length > 11) {
                     //    string hex = message.Substring(11); // chop off /chatcolor and the space after
                     //    hex = hex.Replace(" ", ""); // get rid of whitespace
                     //    // check if valid hex
                     //    string pattern = @"^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$";
                     //    Regex regex = new Regex(pattern);
                     //    bool isMatch = regex.IsMatch(hex);
                     //    if (!isMatch) return true;

                    //    _customNameColors[player._steamID] = hex;
                    //    updatedColor = true;

                    //    if (_customNameColors.TryGetValue(player.Network_steamID, out hex)) {
                    //        player.Network_nickname = $"<color={hex}>{player.Network_nickname}</color>";
                    //    }

                    //    Logger.LogWarning($"Saved {hex} name color for steamID {player._steamID}");
                    //}

                    bool usingChatColors = !_noChatColors.Contains(player._steamID);
                    
                    if (!usingChatColors) return true;
                    
                    bool inDictionary = _customChatColors.TryGetValue(player._steamID, out colorHex);
                    
                    if(!inDictionary) {
                        // if they aren't in either datastructure, this is their first message since entering the server
                        // check DB
                        string color = chatColorDatabase.GetUserColor(player._steamID);
                        if (color == null) {
                            _noChatColors.Add(player._steamID);
                            return true;
                        } else { // pull from db and cache in memory
                            _customChatColors.Add(player._steamID, color);
                            colorHex = color;
                        }
                    }

                    if(colorHex != null) {
                        message = $"<color={colorHex}>{message}</color>";
                    }
                }

                return !updatedColor;
            }
        }

        /// <summary>
        /// Called on the server to initialize player information. Removes tags from nickname.
        /// Applying nickname change in Postfix.
        /// </summary>
        //[HarmonyPatch(typeof(ProfileDataSender), "Assign_PlayerStats")]
        //public static class Player_Assign_PlayerStats {
        //    public static void Postfix(ref Player _player, ref PlayerProfileDataMessage _message) {
        //        Logger.LogWarning("Set player name color");

        //        string colorHex;
        //        if(_customNameColors.TryGetValue(_player.Network_steamID, out colorHex)) {
        //            _player.Network_nickname = $"<color={colorHex}>{_message._nickName}</color>";
        //        }
        //    }
        //}

        [HarmonyPatch(typeof(AtlyssNetworkManager), nameof(AtlyssNetworkManager.OnServerDisconnect))]
        public static class AtlyssNetworkManager_OnServerDisconnect {
            public static void Prefix(ref NetworkConnectionToClient _conn) {

                if (chatColorDatabase == null) return;

                // I don't like getting the player information this way, but I don't see where this information is cached anywhere.
                // The game code itself iterates lists like this to find player data, so it's possible it isn't cached anywhere specific
                List<HC_PeerListEntry> playerEntries = HostConsole._current?._peerListEntries;
                int connectionID = _conn.connectionId;
                Player player = null;
                if (playerEntries == null) return;
                for(int i = 0; i < playerEntries.Count; i++) {
                    if (playerEntries[i]._dataID == connectionID) {
                        player = playerEntries[i]._peerPlayer;
                        break;
                    }
                }

                if (player == null) return;

                string steamID = player.Network_steamID;
                if (steamID == null) return;

                string chatColor;
                if(_customChatColors.TryGetValue(steamID, out chatColor)) {
                    chatColorDatabase.SetUserColor(steamID, chatColor);
                }

                if(_noChatColors.Contains(steamID)) {
                    chatColorDatabase.RemoveUserColor(steamID);
                }

                _customChatColors.Remove(steamID);
                _noChatColors.Remove(steamID);
            }
        }

        /// <summary>
        /// This function checks for color tags every frame and removes them, short-circuiting to prevent this.
        /// It does nothing else of pertinence.
        /// </summary>
        [HarmonyPatch(typeof(Player), "<Handle_ServerConditions>g__Handle_NicknameParams|77_0")]
        public static class Player_Handle_ServerConditions {
            public static bool Prefix() {
                return false;
            }
        }
    }
}
