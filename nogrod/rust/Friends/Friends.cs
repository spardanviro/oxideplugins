using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

using Oxide.Core;

using ProtoBuf;

using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Friends", "Nogrod", "2.0.0", ResourceId = 686)]
    class Friends : RustPlugin
    {
        private readonly FieldInfo whitelistPlayersField = typeof(CodeLock).GetField("whitelistPlayers", BindingFlags.Instance | BindingFlags.NonPublic);
        private ConfigData configData;
        private Dictionary<ulong, PlayerData> FriendsData;
        private readonly Dictionary<ulong, HashSet<ulong>> ReverseData = new Dictionary<ulong, HashSet<ulong>>();

        class ConfigData
        {
            public int MaxFriends { get; set; }
            public bool ShareCodeLocks { get; set; }
            public bool ShareAutoTurrets { get; set; }
        }

        class PlayerData
        {
            public string Name { get; set; } = string.Empty;
            public HashSet<ulong> Friends { get; set; } = new HashSet<ulong>();
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                MaxFriends = 30,
                ShareCodeLocks = false,
                ShareAutoTurrets = false
            };
            Config.WriteObject(config, true);
        }

        private void Init()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"List", "Friends {0}:\n{1}"},
                {"NoFriends", "You don't have friends."},
                {"NotOnFriendlist", "{0} not found on your friendlist."},
                {"FriendRemoved", "{0} was removed from your friendlist."},
                {"PlayerNotFound", "Player '{0}' not found."},
                {"CantAddSelf", "You cant add yourself."},
                {"AlreadyOnList", "{0} is already your friend."},
                {"FriendAdded", "{0} is now your friend."},
                {"FriendlistFull", "Your friendlist is full."},
                {"HelpText", "Use /friend <add|+|remove|-|list> <name/steamID> to add/remove/list friends"},
                {"Syntax", "Syntax: /friend <add/+/remove/-> <name/steamID> or /friend list"}
            }, this);
            configData = Config.ReadObject<ConfigData>();
            try
            {
                FriendsData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>("Friends");
            }
            catch
            {
                FriendsData = new Dictionary<ulong, PlayerData>();
            }
            foreach (var data in FriendsData)
            {
                foreach (var friend in data.Value.Friends)
                {
                    AddFriendReverse(data.Key, friend);
                }
            }
        }

        private object OnTurretSetTarget(AutoTurret turret, BaseCombatEntity targ)
        {
            if (!configData.ShareAutoTurrets || !(targ is BasePlayer) || turret.OwnerID <= 0) return null;
            var player = (BasePlayer) targ;
            if (turret.IsAuthed(player) || !HasFriend(turret.OwnerID, player.userID)) return null;
            turret.authorizedPlayers.Add(new PlayerNameID
            {
                userid = player.userID,
                username = player.displayName
            });
            return false;
        }

        private object CanUseDoor(BasePlayer player, BaseLock @lock)
        {
            if (!configData.ShareCodeLocks || !(@lock is CodeLock) || @lock.GetParentEntity().OwnerID <= 0) return null;
            if (HasFriend(@lock.GetParentEntity().OwnerID, player.userID))
            {
                var whitelistPlayers = (List<ulong>)whitelistPlayersField.GetValue(@lock);
                if (!whitelistPlayers.Contains(player.userID)) whitelistPlayers.Add(player.userID);
            }
            return null;
        }

        private void SaveFriends()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Friends", FriendsData);
        }

        private bool AddFriendS(string playerS, string friendS)
        {
            if (string.IsNullOrEmpty(playerS) || string.IsNullOrEmpty(friendS)) return false;
            var playerId = Convert.ToUInt64(playerS);
            var friendId = Convert.ToUInt64(friendS);
            return AddFriend(playerId, friendId);
        }

        private bool AddFriend(ulong playerId, ulong friendId)
        {
            var playerData = GetPlayerData(playerId);
            if (playerData.Friends.Count >= configData.MaxFriends || !playerData.Friends.Add(friendId)) return false;
            AddFriendReverse(playerId, friendId);
            SaveFriends();
            return true;
        }

        private bool RemoveFriendS(string playerS, string friendS)
        {
            if (string.IsNullOrEmpty(playerS) || string.IsNullOrEmpty(friendS)) return false;
            var playerId = Convert.ToUInt64(playerS);
            var friendId = Convert.ToUInt64(friendS);
            return RemoveFriend(playerId, friendId);
        }

        private bool RemoveFriend(ulong playerId, ulong friendId)
        {
            if (!GetPlayerData(playerId).Friends.Remove(friendId)) return false;
            HashSet<ulong> friends;
            if (ReverseData.TryGetValue(friendId, out friends))
                friends.Remove(playerId);
            var turrets = UnityEngine.Object.FindObjectsOfType<AutoTurret>();
            foreach (var turret in turrets)
            {
                if (turret.OwnerID != playerId) continue;
                turret.authorizedPlayers.RemoveAll(a => a.userid == friendId);
            }
            var codeLocks = UnityEngine.Object.FindObjectsOfType<CodeLock>();
            foreach (var codeLock in codeLocks)
            {
                var entity = codeLock.GetParentEntity();
                if (entity == null || entity.OwnerID != playerId) continue;
                var whitelistPlayers = (List<ulong>)whitelistPlayersField.GetValue(codeLock);
                whitelistPlayers.RemoveAll(a => a == friendId);
            }
            SaveFriends();
            return true;
        }

        private bool HasFriendS(string playerS, string friendS)
        {
            if (string.IsNullOrEmpty(playerS) || string.IsNullOrEmpty(friendS)) return false;
            var playerId = Convert.ToUInt64(playerS);
            var friendId = Convert.ToUInt64(friendS);
            return HasFriend(playerId, friendId);
        }

        private bool HasFriend(ulong playerId, ulong friendId)
        {
            return GetPlayerData(playerId).Friends.Contains(friendId);
        }

        private bool AreFriendsS(string playerS, string friendS)
        {
            if (string.IsNullOrEmpty(playerS) || string.IsNullOrEmpty(friendS)) return false;
            var playerId = Convert.ToUInt64(playerS);
            var friendId = Convert.ToUInt64(friendS);
            return AreFriends(playerId, friendId);
        }

        private bool AreFriends(ulong playerId, ulong friendId)
        {
            return GetPlayerData(playerId).Friends.Contains(friendId) && GetPlayerData(friendId).Friends.Contains(playerId);
        }

        private bool IsFriendS(string playerS, string friendS)
        {
            if (string.IsNullOrEmpty(playerS) || string.IsNullOrEmpty(friendS)) return false;
            var playerId = Convert.ToUInt64(playerS);
            var friendId = Convert.ToUInt64(friendS);
            return IsFriend(playerId, friendId);
        }

        private bool IsFriend(ulong playerId, ulong friendId)
        {
            return GetPlayerData(friendId).Friends.Contains(playerId);
        }

        private string[] GetFriendListS(string playerS)
        {
            var playerId = Convert.ToUInt64(playerS);
            return GetFriendList(playerId);
        }

        private string[] GetFriendList(ulong playerId)
        {
            var playerData = GetPlayerData(playerId);
            var players = new List<string>();
            foreach (var friend in playerData.Friends)
                players.Add(GetPlayerData(friend).Name);
            return players.ToArray();
        }

        private string[] IsFriendOfS(string playerS)
        {
            var playerId = Convert.ToUInt64(playerS);
            var friends = IsFriendOf(playerId);
            return friends.ToList().ConvertAll(f => f.ToString()).ToArray();
        }

        private ulong[] IsFriendOf(ulong playerId)
        {
            HashSet<ulong> friends;
            return ReverseData.TryGetValue(playerId, out friends) ? friends.ToArray() : new ulong[0];
        }

        private PlayerData GetPlayerData(ulong playerId)
        {
            var player = FindPlayer(playerId);
            PlayerData playerData;
            if (!FriendsData.TryGetValue(playerId, out playerData))
                FriendsData[playerId] = playerData = new PlayerData();
            if (player != null) playerData.Name = player.displayName;
            return playerData;
        }

        [ChatCommand("friend")]
        private void cmdFriend(BasePlayer player, string command, string[] args)
        {
            if (args == null || args.Length <= 0 || args.Length == 1 && !args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                PrintMessage(player, "Syntax");
                return;
            }
            switch (args[0].ToLower())
            {
                case "list":
                    var friendList = GetFriendList(player.userID);
                    if (friendList.Length > 0)
                        PrintMessage(player, "List", $"{friendList.Length}/{configData.MaxFriends}", string.Join(", ", friendList));
                    else
                        PrintMessage(player, "NoFriends");
                    return;
                case "add":
                case "+":
                    var friendPlayer = FindPlayer(args[1]);
                    if (friendPlayer == null)
                    {
                        PrintMessage(player, "PlayerNotFound", args[1]);
                        return;
                    }
                    if (player == friendPlayer)
                    {
                        PrintMessage(player, "CantAddSelf");
                        return;
                    }
                    var playerData = GetPlayerData(player.userID);
                    if (playerData.Friends.Count >= configData.MaxFriends)
                    {
                        PrintMessage(player, "FriendlistFull");
                        return;
                    }
                    if (playerData.Friends.Contains(friendPlayer.userID))
                    {
                        PrintMessage(player, "AlreadyOnList", friendPlayer.displayName);
                        return;
                    }
                    AddFriend(player.userID, friendPlayer.userID);
                    PrintMessage(player, "FriendAdded", friendPlayer.displayName);
                    return;
                case "remove":
                case "-":
                    var friend = FindFriend(args[1]);
                    if (friend <= 0)
                    {
                        PrintMessage(player, "NotOnFriendlist", args[1]);
                        return;
                    }
                    var removed = RemoveFriend(player.userID, friend);
                    PrintMessage(player, removed ? "FriendRemoved" : "NotOnFriendlist", args[1]);
                    return;
            }
        }

        private void SendHelpText(BasePlayer player)
        {
            PrintMessage(player, "HelpText");
        }

        private void AddFriendReverse(ulong playerId, ulong friendId)
        {
            HashSet<ulong> friends;
            if (!ReverseData.TryGetValue(friendId, out friends))
                ReverseData[friendId] = friends = new HashSet<ulong>();
            friends.Add(playerId);
        }

        private void PrintMessage(BasePlayer player, string msgId, params object[] args)
        {
            PrintToChat(player, lang.GetMessage(msgId, this, player.UserIDString), args);
        }

        private ulong FindFriend(string friend)
        {
            if (string.IsNullOrEmpty(friend)) return 0;
            foreach (var playerData in FriendsData)
            {
                if (playerData.Key.ToString().Equals(friend) || playerData.Value.Name.Contains(friend, CompareOptions.IgnoreCase))
                    return playerData.Key;
            }
            return 0;
        }

        private static BasePlayer FindPlayer(string nameOrIdOrIp)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrIdOrIp)
                    return activePlayer;
                if (activePlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase))
                    return activePlayer;
                if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress == nameOrIdOrIp)
                    return activePlayer;
            }
            foreach (var sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (sleepingPlayer.UserIDString == nameOrIdOrIp)
                    return sleepingPlayer;
                if (sleepingPlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase))
                    return sleepingPlayer;
            }
            return null;
        }

        private static BasePlayer FindPlayer(ulong id)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.userID == id)
                    return activePlayer;
            }
            foreach (var sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (sleepingPlayer.userID == id)
                    return sleepingPlayer;
            }
            return null;
        }
    }
}
