using System;

namespace Oxide.Plugins
{
    [Info("SleeperGuard", "Wulf/lukespragg", 0.1, ResourceId = 1454)]
    [Description("Protects sleeping players from being killed, with optional loot protection.")]

    class SleeperGuard : RustPlugin
    {
        // Do NOT edit this file, instead edit SleeperGuard.json in server/<identity>/oxide/config

        #region Configuration

        bool BlockLooting => GetConfig("BlockLooting", true);

        protected override void LoadDefaultConfig()
        {
            Config["BlockLooting"] = BlockLooting;
            SaveConfig();
        }

        void Loaded() => LoadDefaultConfig();

        #endregion

        #region Damage Blocking

        object OnEntityTakeDamage(BaseCombatEntity entity)
        {
            var player = entity as BasePlayer;
            if (player != null && player.IsSleeping()) return true;
            return null;
        }

        #endregion

        #region Loot Blocking

        bool CanLootPlayer(BasePlayer target) => (!BlockLooting || !target.IsSleeping());

        #endregion

        #region Helper Methods

        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        #endregion
    }
}
