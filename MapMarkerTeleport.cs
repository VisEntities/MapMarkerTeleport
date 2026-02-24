/*
 * Copyright (C) 2026 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Oxide.Core;
using ProtoBuf;
using Rust;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Map Marker Teleport", "VisEntities", "1.1.0")]
    [Description("Teleport to any location by placing a map marker.")]
    public class MapMarkerTeleport : RustPlugin
    {
        #region Fields

        private static MapMarkerTeleport _plugin;
        public const int GROUND_LAYERS = Layers.Mask.Terrain | Layers.Mask.World | Layers.Mask.Default | Layers.Mask.Construction;

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

            Vector3 dest = note.worldPosition;
            RaycastHit hit;
            const float range = 100f;

            if (GetGroundInfo(dest, out hit, range, GROUND_LAYERS))
                dest = hit.point + Vector3.up * 0.25f;

            float waterLevel = WaterLevel.GetWaterLevel(dest, waves: true);
            if (waterLevel > dest.y)
                dest.y = waterLevel;

            object hookResult = Interface.CallHook("OnMapMarkerTeleport", player, dest);
            if (hookResult != null)
                return;

            Teleport(player, dest);
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
                    player.SendNetworkUpdateImmediate();

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
                player.SendNetworkUpdateImmediate();
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

        #region Helpers
           
        public static bool GetGroundInfo(Vector3 startPosition, out RaycastHit raycastHit, float range, LayerMask mask)
            {
                return Physics.Linecast(startPosition + new Vector3(0.0f, range, 0.0f), startPosition - new Vector3(0.0f, range, 0.0f), out raycastHit, mask);
            }
        
        #endregion Helper Helpers
    }
}