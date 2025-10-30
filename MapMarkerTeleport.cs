/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using ProtoBuf;
using Rust;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Map Marker Teleport", "VisEntities", "1.0.2")]
    [Description("Place a map marker and instantly teleport there.")]
    public class MapMarkerTeleport : RustPlugin
    {
        #region Fields

        private static MapMarkerTeleport _plugin;
        public const int LAYER_GROUND = Layers.Mask.Terrain | Layers.Mask.World | Layers.Mask.Default | Layers.Mask.Construction;

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

            if (TerrainUtil.GetGroundInfo(dest, out hit, range, LAYER_GROUND))
                dest = hit.point + Vector3.up * 0.25f;

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

        #region Helper Classes

        public static class TerrainUtil
        {
            public static bool GetGroundInfo(Vector3 startPosition, out RaycastHit raycastHit, float range, LayerMask mask)
            {
                return Physics.Linecast(startPosition + new Vector3(0.0f, range, 0.0f), startPosition - new Vector3(0.0f, range, 0.0f), out raycastHit, mask);
            }

            public static bool GetGroundInfo(Vector3 startPosition, out RaycastHit raycastHit, float range, LayerMask mask, Transform ignoreTransform = null)
            {
                startPosition.y += 0.25f;
                range += 0.25f;
                raycastHit = default;

                RaycastHit hit;
                if (!GamePhysics.Trace(new Ray(startPosition, Vector3.down), 0f, out hit, range, mask, QueryTriggerInteraction.UseGlobal, null))
                    return false;

                if (ignoreTransform != null && hit.collider != null
                    && (hit.collider.transform == ignoreTransform || hit.collider.transform.IsChildOf(ignoreTransform)))
                {
                    return GetGroundInfo(startPosition - new Vector3(0f, 0.01f, 0f), out raycastHit, range, mask, ignoreTransform);
                }

                raycastHit = hit;
                return true;
            }
        }

        #endregion Helper Classes
    }
}