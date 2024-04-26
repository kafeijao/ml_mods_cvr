﻿using ABI.CCK.Components;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Systems.IK;
using ABI_RC.Systems.IK.SubSystems;
using ABI_RC.Systems.InputManagement;
using ABI_RC.Systems.Movement;
using RootMotion.Dynamics;
using RootMotion.FinalIK;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ml_prm
{
    [DisallowMultipleComponent]
    public class RagdollController : MonoBehaviour
    {
        const float c_defaultFriction = 0.6f;

        public static RagdollController Instance { get; private set; } = null;

        VRIK m_vrIK = null;
        bool m_applyHipsPosition = false;
        bool m_applyHipsRotation = false;

        bool m_enabled = false;
        bool m_forcedSwitch = false;

        readonly List<Rigidbody> m_rigidBodies = null;
        readonly List<Collider> m_colliders = null;
        Transform m_puppetRoot = null;
        Transform m_puppet = null;
        BipedRagdollReferences m_puppetReferences;
        readonly List<System.Tuple<Transform, Transform>> m_boneLinks = null;
        readonly List<System.Tuple<CharacterJoint, Vector3>> m_jointAnchors = null;
        readonly List<PhysicsInfluencer> m_physicsInfluencers = null;
        readonly List<GravityInfluencer> m_gravityInfluencers = null;

        bool m_avatarReady = false;
        Coroutine m_initCoroutine = null;
        Vector3 m_lastPosition = Vector3.zero;
        Vector3 m_velocity = Vector3.zero;
        Vector3 m_ragdollLastPos = Vector3.zero;
        bool m_wasSwimming = false;

        RagdollToggle m_avatarRagdollToggle = null;
        RagdollTrigger m_ragdollTrigger = null;
        AvatarBoolParameter m_ragdolledParameter = null;
        PhysicMaterial m_physicsMaterial = null;

        bool m_reachedGround = true;
        float m_groundedTime = 0f;
        float m_downTime = float.MinValue;

        bool m_inAir = false;
        float m_inAirDistance = 0f;

        internal RagdollController()
        {
            m_rigidBodies = new List<Rigidbody>();
            m_colliders = new List<Collider>();
            m_boneLinks = new List<System.Tuple<Transform, Transform>>();
            m_jointAnchors = new List<System.Tuple<CharacterJoint, Vector3>>();
            m_physicsInfluencers = new List<PhysicsInfluencer>();
            m_gravityInfluencers = new List<GravityInfluencer>();
        }

        // Unity events
        void Start()
        {
            if(Instance == null)
                Instance = this;

            m_physicsMaterial = new PhysicMaterial("Ragdoll");
            m_physicsMaterial.dynamicFriction = c_defaultFriction;
            m_physicsMaterial.staticFriction = c_defaultFriction;
            m_physicsMaterial.frictionCombine = PhysicMaterialCombine.Average;
            m_physicsMaterial.bounciness = 0f;
            m_physicsMaterial.bounceCombine = PhysicMaterialCombine.Average;

            m_puppetRoot = new GameObject("[PlayerAvatarPuppet]").transform;
            m_puppetRoot.parent = PlayerSetup.Instance.transform;
            m_puppetRoot.localPosition = Vector3.zero;
            m_puppetRoot.localRotation = Quaternion.identity;

            m_ragdollTrigger = BetterBetterCharacterController.Instance.NonKinematicProxy.gameObject.AddComponent<RagdollTrigger>();
            m_ragdollTrigger.enabled = false;

            Settings.OnMovementDragChanged.AddHandler(this.OnMovementDragChanged);
            Settings.OnAngularDragChanged.AddHandler(this.OnAngularDragChanged);
            Settings.OnGravityChanged.AddHandler(this.OnGravityChanged);
            Settings.OnSlipperinessChanged.AddHandler(this.OnPhysicsMaterialChanged);
            Settings.OnBouncinessChanged.AddHandler(this.OnPhysicsMaterialChanged);
            Settings.OnBuoyancyChanged.AddHandler(this.OnBuoyancyChanged);
            Settings.OnFallDamageChanged.AddHandler(this.OnFallDamageChanged);

            GameEvents.OnAvatarClear.AddHandler(this.OnAvatarClear);
            GameEvents.OnAvatarSetup.AddHandler(this.OnAvatarSetup);
            GameEvents.OnAvatarPreReuse.AddHandler(this.OnAvatarPreReuse);
            GameEvents.OnAvatarPostReuse.AddHandler(this.OnAvatarPostReuse);
            GameEvents.OnIKScaling.AddHandler(this.OnAvatarScaling);
            GameEvents.OnSeatPreSit.AddHandler(this.OnSeatPreSit);
            GameEvents.OnCalibrationStart.AddHandler(this.OnCalibrationStart);
            GameEvents.OnWorldPreSpawn.AddHandler(this.OnWorldPreSpawn);
            GameEvents.OnCombatPreDown.AddHandler(this.OnCombatPreDown);
            GameEvents.OnFlightChange.AddHandler(this.OnFlightChange);
            GameEvents.OnIKOffsetUpdate.AddHandler(this.OnIKOffsetUpdate);
            BetterBetterCharacterController.OnTeleport.AddListener(this.OnPlayerTeleport);

            ModUi.OnSwitchChanged.AddHandler(this.SwitchRagdoll);
        }

        void OnDestroy()
        {
            if(Instance == this)
                Instance = null;

            if(m_initCoroutine != null)
                StopCoroutine(m_initCoroutine);
            m_initCoroutine = null;

            if(m_puppetRoot != null)
                Object.Destroy(m_puppetRoot);
            m_puppetRoot = null;

            m_puppet = null;
            m_rigidBodies.Clear();
            m_colliders.Clear();
            m_boneLinks.Clear();
            m_jointAnchors.Clear();
            m_physicsInfluencers.Clear();
            m_avatarRagdollToggle = null;

            if(m_ragdollTrigger != null)
                Object.Destroy(m_ragdollTrigger);
            m_ragdollTrigger = null;

            if(m_physicsMaterial != null)
                Object.Destroy(m_physicsMaterial);
            m_physicsMaterial = null;

            Settings.OnMovementDragChanged.RemoveHandler(this.OnMovementDragChanged);
            Settings.OnAngularDragChanged.RemoveHandler(this.OnAngularDragChanged);
            Settings.OnGravityChanged.RemoveHandler(this.OnGravityChanged);
            Settings.OnSlipperinessChanged.RemoveHandler(this.OnPhysicsMaterialChanged);
            Settings.OnBouncinessChanged.RemoveHandler(this.OnPhysicsMaterialChanged);
            Settings.OnBuoyancyChanged.RemoveHandler(this.OnBuoyancyChanged);
            Settings.OnFallDamageChanged.RemoveHandler(this.OnFallDamageChanged);

            GameEvents.OnAvatarClear.RemoveHandler(this.OnAvatarClear);
            GameEvents.OnAvatarSetup.RemoveHandler(this.OnAvatarSetup);
            GameEvents.OnAvatarPreReuse.RemoveHandler(this.OnAvatarPreReuse);
            GameEvents.OnAvatarPostReuse.RemoveHandler(this.OnAvatarPostReuse);
            GameEvents.OnIKScaling.RemoveHandler(this.OnAvatarScaling);
            GameEvents.OnSeatPreSit.RemoveHandler(this.OnSeatPreSit);
            GameEvents.OnCalibrationStart.RemoveHandler(this.OnCalibrationStart);
            GameEvents.OnWorldPreSpawn.RemoveHandler(this.OnWorldPreSpawn);
            GameEvents.OnCombatPreDown.RemoveHandler(this.OnCombatPreDown);
            GameEvents.OnFlightChange.RemoveHandler(this.OnFlightChange);
            GameEvents.OnIKOffsetUpdate.RemoveHandler(this.OnIKOffsetUpdate);
            BetterBetterCharacterController.OnTeleport.RemoveListener(this.OnPlayerTeleport);

            ModUi.OnSwitchChanged.RemoveHandler(this.SwitchRagdoll);
        }

        void Update()
        {
            if(m_avatarReady && !m_enabled && Settings.FallDamage && !BetterBetterCharacterController.Instance.IsFlying())
            {
                bool l_grounded = BetterBetterCharacterController.Instance.IsGrounded();
                bool l_inWater = BetterBetterCharacterController.Instance.IsInWater();
                if(m_inAir && l_grounded && !l_inWater && (m_inAirDistance > Settings.FallLimit))
                {
                    m_inAirDistance = 0f;
                    SwitchRagdoll();
                }

                m_inAir = !(l_grounded || l_inWater);
                if(!m_inAir)
                    m_inAirDistance = 0f;
            }

            if(m_avatarReady && m_enabled)
            {
                m_inAirDistance = 0f;
                BodySystem.TrackingPositionWeight = 0f;
            }

            if(m_avatarReady && !m_enabled)
            {
                Vector3 l_pos = PlayerSetup.Instance.transform.position;
                m_velocity = (m_velocity + (l_pos - m_lastPosition) / Time.deltaTime) * 0.5f;
                if(m_inAir)
                {
                    m_inAirDistance += (Quaternion.Inverse(PlayerSetup.Instance.transform.rotation) * (m_lastPosition - l_pos)).y;
                    m_inAirDistance = Mathf.Clamp(m_inAirDistance, 0f, float.MaxValue);
                }

                m_lastPosition = l_pos;

                if(!m_reachedGround && BetterBetterCharacterController.Instance.IsOnGround())
                {
                    m_groundedTime += Time.deltaTime;
                    if(m_groundedTime >= 0.25f)
                        m_reachedGround = true;
                }
            }

            if(m_avatarReady && m_enabled && Settings.AutoRecover)
            {
                m_downTime += Time.deltaTime;
                if(m_downTime >= Settings.RecoverDelay)
                {
                    SwitchRagdoll();
                    m_downTime = float.MinValue; // One attepmt to recover
                }
            }

            if((m_avatarRagdollToggle != null) && m_avatarRagdollToggle.isActiveAndEnabled && m_avatarRagdollToggle.shouldOverride && (m_enabled != m_avatarRagdollToggle.isOn))
                SwitchRagdoll();

            if((m_ragdollTrigger != null) && m_ragdollTrigger.GetStateWithReset() && m_avatarReady && !m_enabled && Settings.PointersReaction)
                SwitchRagdoll();

            if(Settings.Hotkey && Input.GetKeyDown(Settings.HotkeyKey) && !ViewManager.Instance.IsAnyMenuOpen)
                SwitchRagdoll();

            if(m_avatarReady && m_enabled && CVRInputManager.Instance.jump && Settings.JumpRecover)
                SwitchRagdoll();
        }

        void FixedUpdate()
        {
            if(m_avatarReady && m_enabled)
            {
                Vector3 l_dif = m_puppetReferences.hips.position - m_ragdollLastPos;
                PlayerSetup.Instance.transform.position += l_dif;
                m_puppetReferences.hips.position -= l_dif;
                m_ragdollLastPos = m_puppetReferences.hips.position;
            }
        }

        void LateUpdate()
        {
            if(m_avatarReady)
            {
                if(m_enabled)
                {
                    foreach(var l_link in m_boneLinks)
                        l_link.Item1.CopyGlobal(l_link.Item2);
                }
                else
                {
                    foreach(var l_link in m_boneLinks)
                        l_link.Item2.CopyGlobal(l_link.Item1);
                }
            }
        }

        // Game events
        void OnAvatarClear()
        {
            if(m_initCoroutine != null)
            {
                StopCoroutine(m_initCoroutine);
                m_initCoroutine = null;
            }

            if(m_enabled)
            {
                TryRestoreMovement();
                BodySystem.TrackingPositionWeight = 1f;
            }

            if(m_puppet != null)
                Object.Destroy(m_puppet.gameObject);
            m_puppet = null;

            if(m_ragdollTrigger != null)
            {
                m_ragdollTrigger.GetStateWithReset();
                m_ragdollTrigger.enabled = false;
            }

            m_vrIK = null;
            m_applyHipsPosition = false;
            m_enabled = false;
            m_avatarReady = false;
            m_avatarRagdollToggle = null;
            m_ragdolledParameter = null;
            m_rigidBodies.Clear();
            m_colliders.Clear();
            m_puppetReferences = new BipedRagdollReferences();
            m_boneLinks.Clear();
            m_jointAnchors.Clear();
            m_physicsInfluencers.Clear();
            m_gravityInfluencers.Clear();
            m_reachedGround = true;
            m_groundedTime = 0f;
            m_downTime = float.MinValue;
            m_puppetRoot.localScale = Vector3.one;
            m_inAir = false;
            m_inAirDistance = 0f;
            m_wasSwimming = false;
        }

        void OnAvatarSetup()
        {
            if(PlayerSetup.Instance._animator.isHuman)
            {
                Utils.SetAvatarTPose();

                BipedRagdollReferences l_avatarReferences = BipedRagdollReferences.FromAvatar(PlayerSetup.Instance._animator);

                m_puppet = new GameObject("Root").transform;
                m_puppet.parent = m_puppetRoot;
                m_puppet.localPosition = Vector3.zero;
                m_puppet.localRotation = Quaternion.identity;

                m_puppetReferences.root = m_puppet;
                m_puppetReferences.hips = CloneTransform(l_avatarReferences.hips, m_puppetReferences.root, "Hips");
                m_puppetReferences.spine = CloneTransform(l_avatarReferences.spine, m_puppetReferences.hips, "Spine");

                if(l_avatarReferences.chest != null)
                    m_puppetReferences.chest = CloneTransform(l_avatarReferences.chest, m_puppetReferences.spine, "Chest");

                m_puppetReferences.head = CloneTransform(l_avatarReferences.head, (m_puppetReferences.chest != null) ? m_puppetReferences.chest : m_puppetReferences.spine, "Head");

                m_puppetReferences.leftUpperArm = CloneTransform(l_avatarReferences.leftUpperArm, (m_puppetReferences.chest != null) ? m_puppetReferences.chest : m_puppetReferences.spine, "LeftUpperArm");
                m_puppetReferences.leftLowerArm = CloneTransform(l_avatarReferences.leftLowerArm, m_puppetReferences.leftUpperArm, "LeftLowerArm");
                m_puppetReferences.leftHand = CloneTransform(l_avatarReferences.leftHand, m_puppetReferences.leftLowerArm, "LeftHand");

                m_puppetReferences.rightUpperArm = CloneTransform(l_avatarReferences.rightUpperArm, (m_puppetReferences.chest != null) ? m_puppetReferences.chest : m_puppetReferences.spine, "RightUpperArm");
                m_puppetReferences.rightLowerArm = CloneTransform(l_avatarReferences.rightLowerArm, m_puppetReferences.rightUpperArm, "RightLowerArm");
                m_puppetReferences.rightHand = CloneTransform(l_avatarReferences.rightHand, m_puppetReferences.rightLowerArm, "RightHand");

                m_puppetReferences.leftUpperLeg = CloneTransform(l_avatarReferences.leftUpperLeg, m_puppetReferences.hips, "LeftUpperLeg");
                m_puppetReferences.leftLowerLeg = CloneTransform(l_avatarReferences.leftLowerLeg, m_puppetReferences.leftUpperLeg, "LeftLowerLeg");
                m_puppetReferences.leftFoot = CloneTransform(l_avatarReferences.leftFoot, m_puppetReferences.leftLowerLeg, "LeftFoot");

                m_puppetReferences.rightUpperLeg = CloneTransform(l_avatarReferences.rightUpperLeg, m_puppetReferences.hips, "RightUpperLeg");
                m_puppetReferences.rightLowerLeg = CloneTransform(l_avatarReferences.rightLowerLeg, m_puppetReferences.rightUpperLeg, "RightLowerLeg");
                m_puppetReferences.rightFoot = CloneTransform(l_avatarReferences.rightFoot, m_puppetReferences.rightLowerLeg, "RightFoot");

                // Move to world origin to overcome possible issues, maybe?
                m_puppetRoot.position = Vector3.zero;
                m_puppetRoot.rotation = Quaternion.identity;

                BipedRagdollCreator.Options l_options = BipedRagdollCreator.AutodetectOptions(m_puppetReferences);
                l_options.joints = RagdollCreator.JointType.Character;
                BipedRagdollCreator.Create(m_puppetReferences, l_options);

                Transform[] l_puppetTransforms = m_puppetReferences.GetRagdollTransforms();
                Transform[] l_avatarTransforms = l_avatarReferences.GetRagdollTransforms();
                for(int i = 0; i < l_puppetTransforms.Length; i++)
                {
                    if(l_puppetTransforms[i] != null)
                    {
                        Rigidbody l_body = l_puppetTransforms[i].GetComponent<Rigidbody>();
                        if(l_body != null)
                        {
                            m_rigidBodies.Add(l_body);
                            l_body.isKinematic = true;
                            l_body.angularDrag = Settings.AngularDrag;
                            l_body.drag = (Utils.IsWorldSafe() ? Settings.MovementDrag : 1f);
                            l_body.useGravity = false;
                            l_body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                            l_body.gameObject.layer = LayerMask.NameToLayer("PlayerLocal");

                            GravityInfluencer l_gravInfluencer = l_body.gameObject.AddComponent<GravityInfluencer>();
                            l_gravInfluencer.SetActiveGravity((!Utils.IsWorldSafe() || Settings.Gravity));
                            m_gravityInfluencers.Add(l_gravInfluencer);
                        }

                        CharacterJoint l_joint = l_puppetTransforms[i].GetComponent<CharacterJoint>();
                        if(l_joint != null)
                        {
                            l_joint.enablePreprocessing = false;
                            l_joint.enableProjection = true;
                            m_jointAnchors.Add(System.Tuple.Create(l_joint, l_joint.connectedAnchor));
                        }

                        Collider l_collider = l_puppetTransforms[i].GetComponent<Collider>();
                        if(l_collider != null)
                        {
                            Physics.IgnoreCollision(l_collider, BetterBetterCharacterController.Instance.Collider, true);
                            Physics.IgnoreCollision(l_collider, BetterBetterCharacterController.Instance.KinematicTriggerProxy.Collider, true);
                            Physics.IgnoreCollision(l_collider, BetterBetterCharacterController.Instance.NonKinematicProxy.Collider, true);
                            Physics.IgnoreCollision(l_collider, BetterBetterCharacterController.Instance.SphereProxy.Collider, true);
                            BetterBetterCharacterController.Instance.IgnoreCollision(l_collider, true);

                            l_collider.sharedMaterial = m_physicsMaterial;
                            l_collider.material = m_physicsMaterial;
                            m_colliders.Add(l_collider);
                        }

                        if((l_body != null) && (l_collider != null) && (l_puppetTransforms[i] == m_puppetReferences.hips || l_puppetTransforms[i] == m_puppetReferences.spine || l_puppetTransforms[i] == m_puppetReferences.chest))
                        {
                            PhysicsInfluencer l_physicsInfluencer = l_puppetTransforms[i].gameObject.AddComponent<PhysicsInfluencer>();
                            l_physicsInfluencer.airDrag = (Utils.IsWorldSafe() ? Settings.MovementDrag : 1f);
                            l_physicsInfluencer.airAngularDrag = Settings.AngularDrag;
                            l_physicsInfluencer.fluidDrag = 3f;
                            l_physicsInfluencer.fluidAngularDrag = 1f;
                            l_physicsInfluencer.enableBuoyancy = true;
                            l_physicsInfluencer.enableInfluence = false;
                            l_physicsInfluencer.forceAlignUpright = false;
                            float mass = l_body.mass;
                            l_physicsInfluencer.UpdateDensity();
                            l_body.mass = mass;
                            l_physicsInfluencer.volume = mass * 0.005f;
                            l_physicsInfluencer.enableLocalGravity = true;

                            m_physicsInfluencers.Add(l_physicsInfluencer);
                        }

                        if(l_avatarTransforms[i] != null)
                            m_boneLinks.Add(System.Tuple.Create(l_puppetTransforms[i], l_avatarTransforms[i]));
                    }
                }

                // And return back
                m_puppetRoot.localPosition = Vector3.zero;
                m_puppetRoot.localRotation = Quaternion.identity;
                m_puppetRoot.gameObject.SetActive(true);

                m_vrIK = PlayerSetup.Instance._avatar.GetComponent<VRIK>();
                if(m_vrIK != null)
                    m_vrIK.onPostSolverUpdate.AddListener(this.OnIKPostSolverUpdate);

                m_avatarRagdollToggle = PlayerSetup.Instance._avatar.GetComponentInChildren<RagdollToggle>(true);
                m_ragdolledParameter = new AvatarBoolParameter("Ragdolled", PlayerSetup.Instance.animatorManager);

                m_initCoroutine = StartCoroutine(WaitForPhysicsInfluencers());
            }
        }

        IEnumerator WaitForPhysicsInfluencers()
        {
            while(!m_physicsInfluencers.TrueForAll(p => p.IsReady()))
                yield return null;

            m_puppetRoot.gameObject.SetActive(false);

            m_ragdollTrigger.enabled = true;
            m_avatarReady = true;
            m_initCoroutine = null;

            OnGravityChanged(Settings.Gravity);
            OnBuoyancyChanged(Settings.Buoyancy);
            OnMovementDragChanged(Settings.MovementDrag);
            OnAngularDragChanged(Settings.AngularDrag);
        }

        void OnAvatarPreReuse()
        {
            if(m_avatarReady && m_enabled)
            {
                m_forcedSwitch = true;
                SwitchRagdoll();
                m_forcedSwitch = false;
            }
        }
        void OnAvatarPostReuse()
        {
            m_vrIK = PlayerSetup.Instance._avatar.GetComponent<VRIK>();

            if(m_vrIK != null)
                m_vrIK.onPostSolverUpdate.AddListener(this.OnIKPostSolverUpdate);
        }

        void OnAvatarScaling(float p_scaleDifference)
        {
            if(m_puppetRoot != null)
                m_puppetRoot.localScale = Vector3.one * p_scaleDifference;

            foreach(var l_pair in m_jointAnchors)
                l_pair.Item1.connectedAnchor = l_pair.Item2 * p_scaleDifference;
        }

        void OnSeatPreSit(CVRSeat p_seat)
        {
            if(m_avatarReady && m_enabled && !p_seat.occupied)
            {
                m_forcedSwitch = true;
                SwitchRagdoll();
                m_forcedSwitch = false;
                ResetStates();
            }
        }

        void OnCalibrationStart()
        {
            if(m_avatarReady && m_enabled)
            {
                m_forcedSwitch = true;
                SwitchRagdoll();
                m_forcedSwitch = false;
                ResetStates();
            }
        }

        void OnWorldPreSpawn()
        {
            if(m_avatarReady && m_enabled)
                SwitchRagdoll();

            ResetStates();

            OnGravityChanged(Settings.Gravity);
            OnPhysicsMaterialChanged(true);
            OnMovementDragChanged(Settings.MovementDrag);
            OnBuoyancyChanged(Settings.Buoyancy);
        }

        void OnCombatPreDown()
        {
            if(m_avatarReady && !m_enabled && CombatSystem.Instance.isDown && Settings.CombatReaction)
            {
                m_reachedGround = true;
                m_forcedSwitch = true;
                SwitchRagdoll();
                m_forcedSwitch = false;
                ResetStates(false);
            }
        }

        void OnFlightChange()
        {
            if(m_avatarReady && m_enabled && BetterBetterCharacterController.Instance.IsFlying())
            {
                m_forcedSwitch = true;
                SwitchRagdoll();
                m_forcedSwitch = false;

                ResetStates(false);
            }
        }

        void OnPlayerTeleport(BetterBetterCharacterController.PlayerMoveOffset p_offset)
        {
            try
            {
                ResetStates();

                if(m_avatarReady && m_enabled)
                    m_ragdollLastPos = m_puppetReferences.hips.position;
            }
            catch(System.Exception e)
            {
                MelonLoader.MelonLogger.Error(e);
            }
        }

        void OnIKOffsetUpdate(GameEvents.EventResult p_result)
        {
            p_result.m_result |= (m_enabled && (m_vrIK != null));
        }

        // VRIK updates
        void OnIKPostSolverUpdate()
        {
            if(!m_enabled)
            {
                foreach(var l_link in m_boneLinks)
                    l_link.Item2.CopyGlobal(l_link.Item1);
            }
        }

        // Settings
        void OnMovementDragChanged(float p_value)
        {
            if(m_avatarReady)
            {
                float l_drag = (Utils.IsWorldSafe() ? p_value : 1f);
                foreach(Rigidbody l_body in m_rigidBodies)
                {
                    l_body.drag = l_drag;
                    if(m_enabled)
                        l_body.WakeUp();
                }
                foreach(PhysicsInfluencer l_influencer in m_physicsInfluencers)
                    l_influencer.airDrag = l_drag;
            }
        }
        void OnAngularDragChanged(float p_value)
        {
            if(m_avatarReady)
            {
                foreach(Rigidbody l_body in m_rigidBodies)
                {
                    l_body.angularDrag = p_value;
                    if(m_enabled)
                        l_body.WakeUp();
                }
                foreach(PhysicsInfluencer l_influencer in m_physicsInfluencers)
                    l_influencer.airAngularDrag = p_value;
            }
        }
        void OnGravityChanged(bool p_state)
        {
            if(m_avatarReady)
            {
                bool l_gravity = (!Utils.IsWorldSafe() || p_state);
                foreach(PhysicsInfluencer l_influencer in m_physicsInfluencers)
                    l_influencer.enabled = l_gravity;
                foreach(GravityInfluencer l_influencer in m_gravityInfluencers)
                    l_influencer.SetActiveGravity(l_gravity);

                if(!l_gravity)
                {
                    OnMovementDragChanged(Settings.MovementDrag);
                    OnAngularDragChanged(Settings.AngularDrag);
                }
            }
        }
        void OnPhysicsMaterialChanged(bool p_state)
        {
            if(m_physicsMaterial != null)
            {
                bool l_slipperiness = (Settings.Slipperiness && Utils.IsWorldSafe());
                bool l_bounciness = (Settings.Bounciness && Utils.IsWorldSafe());
                m_physicsMaterial.dynamicFriction = (l_slipperiness ? 0f : c_defaultFriction);
                m_physicsMaterial.staticFriction = (l_slipperiness ? 0f : c_defaultFriction);
                m_physicsMaterial.frictionCombine = (l_slipperiness ? PhysicMaterialCombine.Minimum : PhysicMaterialCombine.Average);
                m_physicsMaterial.bounciness = (l_bounciness ? 1f : 0f);
                m_physicsMaterial.bounceCombine = (l_bounciness ? PhysicMaterialCombine.Maximum : PhysicMaterialCombine.Average);
            }
        }
        void OnBuoyancyChanged(bool p_state)
        {
            if(m_avatarReady)
            {
                bool l_buoyancy = (!Utils.IsWorldSafe() || p_state);
                foreach(PhysicsInfluencer l_influencer in m_physicsInfluencers)
                    l_influencer.enableInfluence = l_buoyancy;

                if(!l_buoyancy)
                {
                    OnMovementDragChanged(Settings.MovementDrag);
                    OnAngularDragChanged(Settings.AngularDrag);
                }
            }
        }
        void OnFallDamageChanged(bool p_state)
        {
            m_inAir = false;
            m_inAirDistance = 0f;
        }

        // Arbitrary
        public void SwitchRagdoll()
        {
            if(m_avatarReady)
            {
                if(!m_enabled)
                {
                    if(CanRagdoll())
                    {
                        m_wasSwimming = BetterBetterCharacterController.Instance.IsSwimming();

                        if(BetterBetterCharacterController.Instance.IsFlying())
                            BetterBetterCharacterController.Instance.ChangeFlight(false, true);
                        BetterBetterCharacterController.Instance.SetImmobilized(true);
                        BetterBetterCharacterController.Instance.ClearFluidVolumes();
                        BodySystem.TrackingPositionWeight = 0f;
                        m_applyHipsPosition = IKSystem.Instance.applyOriginalHipPosition;
                        IKSystem.Instance.applyOriginalHipPosition = true;
                        m_applyHipsRotation = IKSystem.Instance.applyOriginalHipRotation;
                        IKSystem.Instance.applyOriginalHipRotation = true;

                        PlayerSetup.Instance.animatorManager.CancelEmote = true;
                        m_ragdolledParameter.SetValue(true);

                        if(!Utils.IsWorldSafe())
                        {
                            m_reachedGround = false; // Force player to unragdoll and reach ground first
                            m_groundedTime = 0f;
                        }

                        m_puppetRoot.gameObject.SetActive(true);

                        foreach(Rigidbody l_body in m_rigidBodies)
                            l_body.isKinematic = false;

                        Vector3 l_velocity = Vector3.ClampMagnitude(m_velocity * (Utils.IsWorldSafe() ? Settings.VelocityMultiplier : 1f), Utils.GetWorldMovementLimit());
                        if(Settings.ViewVelocity && Utils.IsWorldSafe())
                        {
                            float l_mag = l_velocity.magnitude;
                            l_velocity = PlayerSetup.Instance.GetActiveCamera().transform.forward * l_mag;
                        }

                        foreach(Rigidbody l_body in m_rigidBodies)
                        {
                            l_body.velocity = l_velocity;
                            l_body.angularVelocity = Vector3.zero;
                        }

                        m_ragdollLastPos = m_puppetReferences.hips.position;
                        m_downTime = 0f;

                        m_enabled = true;
                    }
                }
                else
                {
                    if(CanUnragdoll())
                    {
                        BetterBetterCharacterController.Instance.TeleportPlayerTo(m_puppetReferences.hips.position, PlayerSetup.Instance.GetPlayerRotation().eulerAngles, false, false);
                        TryRestoreMovement();
                        if(!Utils.IsWorldSafe())
                        {
                            Vector3 l_vec = BetterBetterCharacterController.Instance.GetVelocity();
                            l_vec.y = Mathf.Clamp(l_vec.y, float.MinValue, 0f);
                            BetterBetterCharacterController.Instance.SetVelocity(l_vec);
                        }
                        BodySystem.TrackingPositionWeight = 1f;
                        IKSystem.Instance.applyOriginalHipPosition = m_applyHipsPosition;
                        IKSystem.Instance.applyOriginalHipRotation = m_applyHipsRotation;

                        if(m_vrIK != null)
                            m_vrIK.solver.Reset();

                        m_ragdolledParameter.SetValue(false);

                        m_puppetRoot.gameObject.SetActive(false);
                        m_puppetRoot.localPosition = Vector3.zero;
                        m_puppetRoot.localRotation = Quaternion.identity;

                        foreach(Rigidbody l_body in m_rigidBodies)
                            l_body.isKinematic = true;

                        foreach(PhysicsInfluencer l_physicsInfluencer in m_physicsInfluencers)
                            l_physicsInfluencer.ClearFluidVolumes();

                        m_lastPosition = PlayerSetup.Instance.transform.position;
                        m_velocity = Vector3.zero;
                        m_downTime = float.MinValue;

                        // Restore rigidbody properties that could be affected by buoyancy
                        OnMovementDragChanged(Settings.MovementDrag);
                        OnAngularDragChanged(Settings.AngularDrag);

                        // Restore movement if was ragdolled in water and left it
                        if(m_wasSwimming)
                            BetterBetterCharacterController.Instance.SetMovementMode(ECM2.Character.MovementMode.Swimming);

                        m_enabled = false;
                    }
                }
            }
        }

        public bool IsRagdolled() => (m_avatarReady && m_enabled);

        void ResetStates(bool p_resetVelocity = true)
        {
            if(p_resetVelocity)
            {
                m_lastPosition = this.transform.position;
                m_velocity = Vector3.zero;
            }

            m_inAir = false;
            m_inAirDistance = 0f;
        }

        static Transform CloneTransform(Transform p_source, Transform p_parent, string p_name)
        {
            Transform l_target = new GameObject(p_name).transform;
            l_target.parent = p_parent;
            p_source.CopyGlobal(l_target);
            return l_target;
        }

        bool CanRagdoll()
        {
            bool l_result = m_reachedGround;
            l_result &= !BodySystem.isCalibrating;
            l_result &= !BetterBetterCharacterController.Instance.IsSitting();
            l_result &= ((CombatSystem.Instance == null) || !CombatSystem.Instance.isDown);
            return (l_result || m_forcedSwitch);
        }

        bool CanUnragdoll()
        {
            bool l_result = true;
            l_result &= ((CombatSystem.Instance == null) || !CombatSystem.Instance.isDown);
            return (l_result || m_forcedSwitch);
        }

        static void TryRestoreMovement()
        {
            bool l_state = true;
            l_state &= ((CombatSystem.Instance == null) || !CombatSystem.Instance.isDown);
            l_state &= !BetterBetterCharacterController.Instance.IsSitting();

            if(l_state)
                BetterBetterCharacterController.Instance.SetImmobilized(false);
        }
    }
}
