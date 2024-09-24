﻿using ABI_RC.Core.Player;
using ABI_RC.Core.Util.AssetFiltering;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ml_prm
{
    public class PlayerRagdollMod : MelonLoader.MelonMod
    {
        RagdollController m_controller = null;

        public override void OnInitializeMelon()
        {
            Settings.Init();
            ModUi.Init();
            GameEvents.Init(HarmonyInstance);
            WorldManager.Init();

            MelonLoader.MelonCoroutines.Start(WaitForRootLogic());
            MelonLoader.MelonCoroutines.Start(WaitForWhitelist());
        }

        public override void OnDeinitializeMelon()
        {
            WorldManager.DeInit();

            if(m_controller != null)
                UnityEngine.Object.Destroy(m_controller.gameObject);
            m_controller = null;
        }

        System.Collections.IEnumerator WaitForRootLogic()
        {
            while(ABI_RC.Core.RootLogic.Instance == null)
                yield return null;

            m_controller = new UnityEngine.GameObject("[PlayerRagdollMod]").AddComponent<RagdollController>();
            m_controller.gameObject.AddComponent<RemoteGesturesManager>();
        }

        System.Collections.IEnumerator WaitForWhitelist()
        {
            // Whitelist the toggle script
            FieldInfo l_field = typeof(SharedFilter).GetField("_localComponentWhitelist", BindingFlags.NonPublic | BindingFlags.Static);
            HashSet<Type> l_hashSet = l_field?.GetValue(null) as HashSet<Type>;
            while(l_hashSet == null)
            {
                l_hashSet = l_field?.GetValue(null) as HashSet<Type>;
                yield return null;
            }
            l_hashSet.Add(typeof(RagdollToggle));
        }
    }
}
