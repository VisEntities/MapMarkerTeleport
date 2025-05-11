/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using ProtoBuf;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Map Marker Teleport", "VisEntities", "1.0.0")]
    [Description("Place a map marker and instantly teleport there.")]
    public class MapMarkerTeleport : RustPlugin
    {
        #region Fields

        private static MapMarkerTeleport _plugin;

        #endregion Fields

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            PermissionUtil.RegisterPermissions();
        }

        private void Unload()
        {
            _plugin = null;
        }

        private void OnMapMarkerAdded(BasePlayer player, MapNote note)
        {
            if (player == null || note == null)
                return;

            if (!PermissionUtil.HasPermission(player, PermissionUtil.USE))
                return;

            Teleport(player, note.worldPosition);
        }

        #endregion Oxide Hooks

        #region Teleportation

        public static void Teleport(BasePlayer player, Vector3 destination, bool putToSleep = false, bool wakeUp = false)
        {
            if (player.isMounted)
                player.GetMounted().DismountPlayer(player, true);

            if (player.GetParentEntity())
                player.SetParent(null, true, true);

            player.RemoveFromTriggers();

            try
            {
                player.DisablePlayerCollider();

                if (putToSleep && player.IsConnected)
                {
                    player.StartSleeping();
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
                    player.SetServerFall(true);
                }

                player.MovePosition(destination);
                player.ClientRPC(RpcTarget.Player("ForcePositionTo", player), destination);

                if (player.IsConnected)
                {
                    player.UpdateNetworkGroup();
                    player.SendNetworkUpdateImmediate(false);

                    if (putToSleep)
                    {
                        player.ClearEntityQueue(null);
                        player.SendFullSnapshot();
                    }
                }
            }
            finally
            {
                player.EnablePlayerCollider();
                player.SetServerFall(false);
            }

            if (putToSleep && wakeUp)
            {
                player.EndSleeping();
                player.SendNetworkUpdateImmediate(false);
            }
        }

        #endregion Teleportation

        #region Permissions

        private static class PermissionUtil
        {
            public const string USE = "mapmarkerteleport.use";
            private static readonly List<string> _permissions = new List<string>
            {
                USE,
            };

            public static void RegisterPermissions()
            {
                foreach (var permission in _permissions)
                {
                    _plugin.permission.RegisterPermission(permission, _plugin);
                }
            }

            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                return _plugin.permission.UserHasPermission(player.UserIDString, permissionName);
            }
        }

        #endregion Permissions
    }
}