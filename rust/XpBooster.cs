/*
TODO:
- Add console commands to change config
*/

using System.Collections.Generic;
using Rust.Xp;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("XpBooster", "Wulf/lukespragg", "0.5.1", ResourceId = 2001)]
    [Description("Multiplies the base XP players earn per source")]

    class XpBooster : RustPlugin
    {
        // Do NOT edit this file, instead edit XpBooster.json in oxide/config and XpBooster.en.json in the oxide/lang directory,
        // or create a new language file for another language using the 'en' file as a default

        #region Initialization
        
        [PluginReference] Plugin Godmode;
        
        void Init()
        {
            LoadDefaultConfig();
            lang.RegisterMessages(new Dictionary<string, string> { ["WelcomeBonus"] = "You've received an XP welcome bonus of {0}!" }, this);

            foreach (var def in Definitions.All)
                if (!def.Name.Contains("Cheat")) permission.RegisterPermission($"{Title}.{def.Name}".ToLower(), this);
        }

        #endregion

        #region Configuration

        bool usePermissions;
        double welcomeBonus;

        protected override void LoadDefaultConfig()
        {
            foreach (var def in Definitions.All) if (!def.Name.Contains("Cheat")) Config[def.Name] = GetConfig(def.Name, 1.0);
            Config["UsePermissions"] = usePermissions = GetConfig("UsePermissions", false);
            Config["WelcomeBonus"] = welcomeBonus = GetConfig("WelcomeBonus", 0.0);
            SaveConfig();
        }

        #endregion
        
        #region XP Boosting

        void OnPlayerInit(BasePlayer player)
        {
            if (!(welcomeBonus > 0) || !player.xp.CurrentLevel.Equals(1) || !player.xp.UnspentXp.Equals(0)) return;

            player.xp.Add(Definitions.Cheat, (float)welcomeBonus);
            PrintToChat(player, Lang("WelcomeBonus", player.UserIDString, welcomeBonus));
        }

        object OnXpEarn(ulong steamId, double amount, string source)
        {
            if (string.IsNullOrEmpty(source) || source.Contains("Cheat")) return null;

            var id = steamId.ToString();
            if (Godmode && (bool)Godmode.Call("IsGod", id)) return null;
            if (usePermissions && !IsAllowed(id, $"{Title}.{source}".ToLower())) return null;

            #if DEBUG
            PrintWarning($"Original amount: {amount}, Boosted amount: {amount * (double)Config[source]}");
            #endif

            return (float)(amount * (double)Config[source]);
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T) System.Convert.ChangeType(Config[name], typeof (T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        bool IsAllowed(string id, string perm) => permission.UserHasPermission(id, perm);

        #endregion
    }
}
