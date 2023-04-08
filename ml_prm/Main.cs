﻿using ABI_RC.Core;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Systems.IK.SubSystems;
using System;
using System.Collections.Generic;
using System.Reflection;
using ABI_RC.Core.Util.AssetFiltering;

namespace ml_prm
{
    public class PlayerRagdollMod : MelonLoader.MelonMod
    {
        static PlayerRagdollMod ms_instance = null;

        RagdollController m_localController = null;

        public override void OnInitializeMelon()
        {
            if(ms_instance == null)
                ms_instance = this;

            Settings.Init();

            HarmonyInstance.Patch(
                typeof(PlayerSetup).GetMethod(nameof(PlayerSetup.ClearAvatar)),
                null,
                new HarmonyLib.HarmonyMethod(typeof(PlayerRagdollMod).GetMethod(nameof(OnAvatarClear_Postfix), BindingFlags.NonPublic | BindingFlags.Static))
            );
            HarmonyInstance.Patch(
                typeof(PlayerSetup).GetMethod(nameof(PlayerSetup.SetupAvatar)),
                null,
                new HarmonyLib.HarmonyMethod(typeof(PlayerRagdollMod).GetMethod(nameof(OnSetupAvatar_Postfix), BindingFlags.Static | BindingFlags.NonPublic))
            );
            HarmonyInstance.Patch(
                typeof(CVRSeat).GetMethod(nameof(CVRSeat.SitDown)),
                new HarmonyLib.HarmonyMethod(typeof(PlayerRagdollMod).GetMethod(nameof(OnCVRSeatSitDown_Prefix), BindingFlags.Static | BindingFlags.NonPublic)),
                null
            );
            HarmonyInstance.Patch(
                typeof(BodySystem).GetMethod(nameof(BodySystem.StartCalibration)),
                new HarmonyLib.HarmonyMethod(typeof(PlayerRagdollMod).GetMethod(nameof(OnStartCalibration_Prefix), BindingFlags.Static | BindingFlags.NonPublic)),
                null
            );
            HarmonyInstance.Patch(
                typeof(RootLogic).GetMethod(nameof(RootLogic.SpawnOnWorldInstance)),
                new HarmonyLib.HarmonyMethod(typeof(PlayerRagdollMod).GetMethod(nameof(OnWorldSpawn_Prefix), BindingFlags.Static | BindingFlags.NonPublic)),
                null
            );

            // Whitelist the toggle script
            var l_localComponentWhitelist = typeof(SharedFilter).GetField("_localComponentWhitelist", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null) as HashSet<Type>;
            l_localComponentWhitelist!.Add(typeof(RagdollToggle));

            MelonLoader.MelonCoroutines.Start(WaitForLocalPlayer());
        }

        public override void OnDeinitializeMelon()
        {
            if(ms_instance == this)
                ms_instance = null;

            m_localController = null;
        }

        System.Collections.IEnumerator WaitForLocalPlayer()
        {
            while(PlayerSetup.Instance == null)
                yield return null;

            m_localController = PlayerSetup.Instance.gameObject.AddComponent<RagdollController>();
        }

        static void OnAvatarClear_Postfix() => ms_instance?.OnAvatarClear();
        void OnAvatarClear()
        {
            try
            {
                if(m_localController != null)
                    m_localController.OnAvatarClear();
            }
            catch(Exception e)
            {
                MelonLoader.MelonLogger.Error(e);
            }
        }

        static void OnSetupAvatar_Postfix() => ms_instance?.OnSetupAvatar();
        void OnSetupAvatar()
        {
            try
            {
                if(m_localController != null)
                    m_localController.OnAvatarSetup();
            }
            catch(Exception e)
            {
                MelonLoader.MelonLogger.Error(e);
            }
        }

        static void OnCVRSeatSitDown_Prefix(ref CVRSeat __instance) => ms_instance?.OnCVRSeatSitDown(__instance);
        void OnCVRSeatSitDown(CVRSeat p_seat)
        {
            try
            {
                if(m_localController != null)
                    m_localController.OnSeatSitDown(p_seat);
            }
            catch(Exception e)
            {
                MelonLoader.MelonLogger.Error(e);
            }
        }

        static void OnStartCalibration_Prefix() => ms_instance?.OnStartCalibration();
        void OnStartCalibration()
        {
            try
            {
                if(m_localController != null)
                    m_localController.OnStartCalibration();
            }
            catch(Exception e)
            {
                MelonLoader.MelonLogger.Error(e);
            }
        }

        static void OnWorldSpawn_Prefix() => ms_instance?.OnWorldSpawn();
        void OnWorldSpawn()
        {
            try
            {
                if(m_localController != null)
                    m_localController.OnWorldSpawn();
            }
            catch(Exception e)
            {
                MelonLoader.MelonLogger.Error(e);
            }
        }

    }
}
