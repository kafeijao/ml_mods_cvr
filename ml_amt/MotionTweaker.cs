﻿using ABI_RC.Core.Player;
using ABI_RC.Core.Util.AnimatorManager;
using ABI_RC.Systems.IK.SubSystems;
using ABI_RC.Systems.Movement;
using RootMotion.FinalIK;
using System.Collections.Generic;
using UnityEngine;

namespace ml_amt
{
    [DisallowMultipleComponent]
    class MotionTweaker : MonoBehaviour
    {
        struct IKInfo
        {
            public float m_weight;
            public float m_locomotionWeight;
            public bool m_plantFeet;
            public bool m_bendNormalLeft;
            public bool m_bendNormalRight;
        }

        static MotionTweaker ms_instance = null;

        IKInfo m_ikInfo;
        VRIK m_vrIk = null;
        float m_avatarScale = 1f;
        Vector3 m_locomotionOffset = Vector3.zero; // Original locomotion offset

        bool m_avatarReady = false;
        Vector3 m_massCenter = Vector3.zero;
        Transform m_ikLimits = null;

        readonly List<AvatarParameter> m_parameters = null;

        internal MotionTweaker()
        {
            m_parameters = new List<AvatarParameter>();
        }

        // Unity events
        void Awake()
        {
            if(ms_instance != null)
            {
                DestroyImmediate(this);
                return;
            }

            ms_instance = this;
            DontDestroyOnLoad(this);
        }
        void Start()
        {
            OnCrouchLimitChanged(Settings.CrouchLimit);
            OnProneLimitChanged(Settings.ProneLimit);

            Settings.OnCrouchLimitChanged.AddListener(this.OnCrouchLimitChanged);
            Settings.OnProneLimitChanged.AddListener(this.OnProneLimitChanged);
            Settings.OnMassCenterChanged.AddListener(this.OnMassCenterChanged);

            GameEvents.OnAvatarSetup.AddListener(this.OnAvatarSetup);
            GameEvents.OnAvatarClear.AddListener(this.OnAvatarClear);
            GameEvents.OnAvatarReuse.AddListener(this.OnAvatarReuse);
            GameEvents.OnPlayspaceScale.AddListener(this.OnPlayspaceScale);
        }

        void OnDestroy()
        {
            if(ms_instance == this)
                ms_instance = null;

            m_vrIk = null;
            m_ikLimits = null;
            m_parameters.Clear();

            Settings.OnCrouchLimitChanged.RemoveListener(this.OnCrouchLimitChanged);
            Settings.OnProneLimitChanged.RemoveListener(this.OnProneLimitChanged);
            Settings.OnMassCenterChanged.RemoveListener(this.OnMassCenterChanged);

            GameEvents.OnAvatarSetup.RemoveListener(this.OnAvatarSetup);
            GameEvents.OnAvatarClear.RemoveListener(this.OnAvatarClear);
            GameEvents.OnAvatarReuse.RemoveListener(this.OnAvatarReuse);
            GameEvents.OnPlayspaceScale.RemoveListener(this.OnPlayspaceScale);
        }

        void Update()
        {
            if(m_avatarReady)
            {
                UpdateIKLimits();

                foreach(AvatarParameter l_param in m_parameters)
                    l_param.Update(this);
            }
        }

        // Game events
        void OnAvatarClear()
        {
            m_vrIk = null;
            m_avatarReady = false;
            m_avatarScale = 1f;
            m_locomotionOffset = Vector3.zero;
            m_massCenter = Vector3.zero;
            m_ikLimits = null;
            m_parameters.Clear();

            BetterBetterCharacterController.Instance.avatarCrouchLimit = Mathf.Clamp01(Settings.CrouchLimit);
            BetterBetterCharacterController.Instance.avatarProneLimit = Mathf.Clamp01(Settings.ProneLimit);
        }

        void OnAvatarSetup()
        {
            Utils.SetAvatarTPose();

            m_vrIk = PlayerSetup.Instance._avatar.GetComponent<VRIK>();
            m_avatarScale = Mathf.Abs(PlayerSetup.Instance._avatar.transform.localScale.y);

            // Parse animator parameters
            m_parameters.Add(new AvatarParameter(AvatarParameter.ParameterType.Moving, PlayerSetup.Instance.animatorManager));
            m_parameters.Add(new AvatarParameter(AvatarParameter.ParameterType.MovementSpeed, PlayerSetup.Instance.animatorManager));
            m_parameters.Add(new AvatarParameter(AvatarParameter.ParameterType.Velocity, PlayerSetup.Instance.animatorManager));
            m_parameters.RemoveAll(p => !p.IsValid());

            // Avatar custom IK limits
            m_ikLimits = PlayerSetup.Instance._avatar.transform.Find("[IKLimits]");
            UpdateIKLimits();

            // Apply VRIK tweaks
            if(m_vrIk != null)
            {
                m_locomotionOffset = m_vrIk.solver.locomotion.offset;
                m_massCenter = m_locomotionOffset;

                if(m_vrIk.solver.HasToes())
                {
                    Transform l_foot = PlayerSetup.Instance._animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                    if(l_foot == null)
                        l_foot = PlayerSetup.Instance._animator.GetBoneTransform(HumanBodyBones.RightFoot);

                    Transform l_toe = PlayerSetup.Instance._animator.GetBoneTransform(HumanBodyBones.LeftToes);
                    if(l_toe == null)
                        l_toe = PlayerSetup.Instance._animator.GetBoneTransform(HumanBodyBones.RightToes);

                    if((l_foot != null) && (l_toe != null))
                    {
                        Vector3 l_footPos = (PlayerSetup.Instance._avatar.transform.GetMatrix().inverse * l_foot.GetMatrix()).GetPosition();
                        Vector3 l_toePos = (PlayerSetup.Instance._avatar.transform.GetMatrix().inverse * l_toe.GetMatrix()).GetPosition();
                        m_massCenter = new Vector3(0f, 0f, l_toePos.z - l_footPos.z);
                    }
                }

                m_vrIk.solver.locomotion.offset = (Settings.MassCenter ? m_massCenter : m_locomotionOffset);

                m_vrIk.onPreSolverUpdate.AddListener(this.OnIKPreSolverUpdate);
                m_vrIk.onPostSolverUpdate.AddListener(this.OnIKPostSolverUpdate);
            }

            m_avatarReady = true;
        }

        void OnPlayspaceScale()
        {
            if((m_vrIk != null) && Settings.MassCenter)
                m_vrIk.solver.locomotion.offset = m_massCenter * GetRelativeScale();
        }

        void OnAvatarReuse()
        {
            // Old VRIK is destroyed by game
            Utils.SetAvatarTPose();

            m_vrIk = PlayerSetup.Instance._animator.GetComponent<VRIK>();
            if(m_vrIk != null)
            {
                m_vrIk.solver.locomotion.offset = (Settings.MassCenter ? m_massCenter : m_locomotionOffset);

                m_vrIk.onPreSolverUpdate.AddListener(this.OnIKPreSolverUpdate);
                m_vrIk.onPostSolverUpdate.AddListener(this.OnIKPostSolverUpdate);
            }
        }

        // IK events
        void OnIKPreSolverUpdate()
        {
            m_ikInfo.m_weight = m_vrIk.solver.IKPositionWeight;
            m_ikInfo.m_locomotionWeight = m_vrIk.solver.locomotion.weight;
            m_ikInfo.m_plantFeet = m_vrIk.solver.plantFeet;
            m_ikInfo.m_bendNormalLeft = m_vrIk.solver.leftLeg.useAnimatedBendNormal;
            m_ikInfo.m_bendNormalRight = m_vrIk.solver.rightLeg.useAnimatedBendNormal;

            if(!BodySystem.isCalibratedAsFullBody)
            {
                if(BetterBetterCharacterController.Instance.AvatarUpright <= BetterBetterCharacterController.Instance.avatarCrouchLimit)
                {
                    m_vrIk.solver.leftLeg.useAnimatedBendNormal = true;
                    m_vrIk.solver.rightLeg.useAnimatedBendNormal = true;
                }
                if(Settings.IKOverrideFly && BetterBetterCharacterController.Instance.IsFlying())
                {
                    m_vrIk.solver.locomotion.weight = 0f;
                    m_vrIk.solver.leftLeg.useAnimatedBendNormal = true;
                    m_vrIk.solver.rightLeg.useAnimatedBendNormal = true;
                }
                if(Settings.IKOverrideJump && !BetterBetterCharacterController.Instance.IsGrounded() && !BetterBetterCharacterController.Instance.IsFlying())
                {
                    m_vrIk.solver.locomotion.weight = 0f;
                    m_vrIk.solver.leftLeg.useAnimatedBendNormal = true;
                    m_vrIk.solver.rightLeg.useAnimatedBendNormal = true;
                }
            }
        }

        void OnIKPostSolverUpdate()
        {
            m_vrIk.solver.IKPositionWeight = m_ikInfo.m_weight;
            m_vrIk.solver.locomotion.weight = m_ikInfo.m_locomotionWeight;
            m_vrIk.solver.plantFeet = m_ikInfo.m_plantFeet;
            m_vrIk.solver.leftLeg.useAnimatedBendNormal = m_ikInfo.m_bendNormalLeft;
            m_vrIk.solver.rightLeg.useAnimatedBendNormal = m_ikInfo.m_bendNormalRight;
        }

        // Settings
        void OnCrouchLimitChanged(float p_value)
        {
            if(m_ikLimits == null)
                BetterBetterCharacterController.Instance.avatarCrouchLimit = Mathf.Clamp01(p_value);
        }
        void OnProneLimitChanged(float p_value)
        {
            if(m_ikLimits == null)
                BetterBetterCharacterController.Instance.avatarProneLimit = Mathf.Clamp01(p_value);
        }
        void OnMassCenterChanged(bool p_state)
        {
            if(m_vrIk != null)
                m_vrIk.solver.locomotion.offset = (Settings.MassCenter ? (m_massCenter * GetRelativeScale()) : m_locomotionOffset);
        }

        // Arbitrary
        float GetRelativeScale()
        {
            return ((m_avatarScale > 0f) ? (PlayerSetup.Instance._avatar.transform.localScale.y / m_avatarScale) : 0f);
        }

        void UpdateIKLimits()
        {
            if(m_ikLimits != null)
            {
                Vector3 l_values = m_ikLimits.localPosition;
                BetterBetterCharacterController.Instance.avatarCrouchLimit = Mathf.Clamp01(l_values.x);
                BetterBetterCharacterController.Instance.avatarProneLimit = Mathf.Clamp01(l_values.y);
            }
        }

        // Parameters access
        public bool IsMoving() => BetterBetterCharacterController.Instance.IsMoving();
        public float GetMovementSpeed()
        {
            AvatarAnimatorManager l_animatorManager = PlayerSetup.Instance.animatorManager;
            return Mathf.Sqrt(l_animatorManager.MovementX * l_animatorManager.MovementX + l_animatorManager.MovementY * l_animatorManager.MovementY);
        }
        public float GetVelocity() => BetterBetterCharacterController.Instance.velocity.magnitude;
    }
}
