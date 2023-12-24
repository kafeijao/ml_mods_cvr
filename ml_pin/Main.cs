﻿using ABI_RC.Core.AudioEffects;
using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.GameEventSystem;
using System;
using System.Collections;

namespace ml_pin
{
    public class PlayersInstanceNotifier : MelonLoader.MelonMod
    {
        SoundManager m_soundManager = null;

        public override void OnInitializeMelon()
        {
            Settings.Init();
            ResourcesHandler.ExtractAudioResources();

            MelonLoader.MelonCoroutines.Start(WaitForInstances());
        }

        public override void OnDeinitializeMelon()
        {
            m_soundManager = null;
        }

        IEnumerator WaitForInstances()
        {
            if(InterfaceAudio.Instance == null)
                yield return null;

            m_soundManager = new SoundManager();
            m_soundManager.LoadSounds();

            CVRGameEventSystem.Player.OnJoin.AddListener(OnPlayerJoin);
            CVRGameEventSystem.Player.OnLeave.AddListener(OnPlayerLeave);
        }

        void OnPlayerJoin(PlayerDescriptor p_player)
        {
            try
            {
                bool l_isFriend = Friends.FriendsWith(p_player.ownerId);
                bool l_notify = true;

                switch(Settings.NotifyType)
                {
                    case Settings.NotificationType.None:
                        l_notify = false;
                        break;
                    case Settings.NotificationType.Friends:
                        l_notify = (ShouldNotifyInCurrentInstance() && l_isFriend);
                        break;
                    case Settings.NotificationType.All:
                        l_notify = ShouldNotifyInCurrentInstance();
                        break;
                }
                l_notify |= (Settings.FriendsAlways && l_isFriend);

                if(l_notify)
                    m_soundManager?.PlaySound(l_isFriend ? SoundManager.SoundType.FriendJoin : SoundManager.SoundType.PlayerJoin);
            }
            catch(Exception e)
            {
                MelonLoader.MelonLogger.Warning(e);
            }
        }
        void OnPlayerLeave(PlayerDescriptor p_player)
        {
            try
            {
                bool l_isFriend = Friends.FriendsWith(p_player.ownerId);
                bool l_notify = true;

                switch(Settings.NotifyType)
                {
                    case Settings.NotificationType.None:
                        l_notify = false;
                        break;
                    case Settings.NotificationType.Friends:
                        l_notify = (ShouldNotifyInCurrentInstance() && l_isFriend);
                        break;
                    case Settings.NotificationType.All:
                        l_notify = ShouldNotifyInCurrentInstance();
                        break;
                }
                l_notify |= (Settings.FriendsAlways && l_isFriend);

                if(l_notify)
                    m_soundManager?.PlaySound(l_isFriend ? SoundManager.SoundType.FriendLeave : SoundManager.SoundType.PlayerLeave);
            }
            catch(Exception e)
            {
                MelonLoader.MelonLogger.Warning(e);
            }
        }

        bool ShouldNotifyInCurrentInstance()
        {
            bool l_isInPublic = (Settings.NotifyInPublic && MetaPort.Instance.CurrentInstancePrivacy.Contains("Public"));
            bool l_isInFriends = (Settings.NotifyInFriends && MetaPort.Instance.CurrentInstancePrivacy.Contains("Friends"));
            bool l_isInPrivate = (Settings.NotifyInPrivate && MetaPort.Instance.CurrentInstancePrivacy.Contains("invite"));
            return (l_isInPublic || l_isInFriends || l_isInPrivate);
        }
    }
}
