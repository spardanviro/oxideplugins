/*
TODO:
- Add automatic admin exclusion
*/

using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("AFK", "Wulf/lukespragg", "1.0.0")]
    [Description("Kicks players that are AFK (away from keyboard) for too long.")]

    class AFK : CovalencePlugin
    {
        // Do NOT edit this file, instead edit AFK.json in oxide/config and AFK.en.json in the oxide/lang directory,
        // or create a new language file for another language using the 'en' file as a default.

        #region Configuration

        int AfkLimitMinutes => GetConfig("AfkLimitMinutes", 10);
        bool KickAfkPlayers => GetConfig("KickAfkPlayers", true);
        //bool WarnBeforeKick => GetConfig("WarnBeforeKick", true);

        protected override void LoadDefaultConfig()
        {
            Config["AfkLimitMinutes"] = AfkLimitMinutes;
            Config["KickAfkPlayers"] = KickAfkPlayers;
            //Config["WarnBeforeKick"] = WarnBeforeKick;
            SaveConfig();
        }

        #endregion

        #region Localization

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"KickedForAfk", "You were kicked for being AFK for {0} minutes"},
                //{"NoLongerAfk", "You are no longer AFK"},
                //{"YouWentAfk", "You went AFK"}
            }, this);
        }

        #endregion

        #region Initialization

        void Loaded()
        {
            #if !HURTWORLD && !REIGNOFKINGS && !RUST && !RUSTLEGACY
            throw new NotSupportedException("This plugin does not support this game");
            #endif

            LoadDefaultConfig();
            LoadDefaultMessages();
            permission.RegisterPermission("afk.excluded", this);
        }

        void OnServerInitialized()
        {
            foreach (var player in players.Online)
            {
                lastPosition[player.BasePlayer.UniqueID] = player.Character.GetPosition();
                AfkCheck(player.BasePlayer.UniqueID);
            }
        }

        #endregion

        #region AFK Checking

        readonly Hash<string, GenericPosition> lastPosition = new Hash<string, GenericPosition>();
        readonly Dictionary<string, Timer> afkTimer = new Dictionary<string, Timer>();

        void AfkCheck(string userId)
        {
            if (HasPermission(userId, "afk.excluded")) return;

            afkTimer.Add(userId, timer.Repeat(AfkLimitMinutes*60, 0, () =>
            {
                var player = players.GetOnlinePlayer(userId);
                if (!IsPlayerAfk(player)) return;

                //player.Message(GetMessage("YouWentAfk", userId));

                if (KickAfkPlayers)
                {
                    // TODO: Send timed message/warning to player before kick

                    player.Kick(string.Format(GetMessage("KickedForAfk", userId), AfkLimitMinutes));
                }
            }));
        }

        void ResetPlayer(string userId)
        {
            if (afkTimer.ContainsKey(userId))
            {
                afkTimer[userId].Destroy();
                afkTimer.Remove(userId);
            }
            if (lastPosition.ContainsKey(userId)) lastPosition.Remove(userId);
        }

        bool IsPlayerAfk(ILivePlayer player)
        {
            var position = player.Character.GetPosition();
            if (lastPosition[player.BasePlayer.UniqueID].Equals(position)) return true;
            lastPosition[player.BasePlayer.UniqueID] = position;
            return false;
        }

        void Unload()
        {
            foreach (var player in players.Online) ResetPlayer(player.BasePlayer.UniqueID);
        }

        #endregion

        #region Game Hooks

        #if HURTWORLD
        void OnPlayerInit(PlayerSession session) => AfkCheck(session.SteamId.ToString());
        void OnPlayerDisconnected(PlayerSession session) => ResetPlayer(session.SteamId.ToString());
        bool IsPlayerAfk(PlayerSession session) => IsPlayerAfk(players.GetOnlinePlayer(session.SteamId.ToString()));
        #endif

        #if REIGNOFKINGS
        void OnPlayerSpawn(CodeHatch.Networking.Events.Players.PlayerFirstSpawnEvent evt) => AfkCheck(evt.PlayerId.ToString());
        void OnPlayerDisconnected(CodeHatch.Engine.Networking.Player player) => ResetPlayer(player.Id.ToString());
        bool IsPlayerAfk(CodeHatch.Engine.Networking.Player player) => IsPlayerAfk(players.GetOnlinePlayer(player.Id.ToString()));
        #endif

        #if RUST
        void OnPlayerInit(BasePlayer player) => AfkCheck(player.UserIDString());
        void OnPlayerDisconnected(BasePlayer player) => ResetPlayer(player.UserIDString);
        bool IsPlayerAfk(BasePlayer player) => IsPlayerAfk(players.GetOnlinePlayer(player.UserIDString));
        #endif

        #if RUSTLEGACY
        void OnPlayerSpawn(PlayerClient client) => AfkCheck(client.netUser.userID.ToString());
        void OnPlayerDisconnected(uLink.NetworkPlayer player) => ResetPlayer(player.GetLocalData<NetUser>()?.userID.ToString());
        bool IsPlayerAfk(NetUser netUser) => IsPlayerAfk(players.GetOnlinePlayer(netUser.userID.ToString()));
        #endif

        #endregion

        #region Helper Methods

        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);

        bool HasPermission(string userId, string perm) => permission.UserHasPermission(userId, perm);

        #endregion
    }
}
