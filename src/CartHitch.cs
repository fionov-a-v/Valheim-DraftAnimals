using System.Collections.Generic;
using DraftAnimals.Logic;
using UnityEngine;

namespace DraftAnimals
{
    /// <summary>
    /// Сцепка тележки со зверем. Метка сцепки (случайное число) лежит в ZDO и тележки, и зверя: так пара находит друг
    /// друга и после перезагрузки мира (ZDOID при загрузке меняются). Физическая связь — такой же ConfigurableJoint,
    /// как у ванильной тележки с игроком, только к телу зверя; она есть только у владельца тележки, а владелец тележки
    /// следует за владельцем зверя (у возницы — оба у него). Ванильную «ручку» тележки мод не трогает: пока зверь
    /// запряжён, тележку нельзя взять за оглобли, зато сундук открывается как обычно.
    /// </summary>
    public sealed class CartHitch : MonoBehaviour
    {
        internal static readonly int s_hitchKey = "da_hitch".GetStableHashCode();
        internal static readonly int s_driverKey = "da_driver".GetStableHashCode();
        internal static readonly int s_driverWeightKey = "da_driver_w".GetStableHashCode();

        private static readonly List<CartHitch> s_all = new List<CartHitch>();

        /// <summary>Дальше этого от козел возница перестаёт считаться возницей, м.</summary>
        private const float DriverRange = 8f;

        private ZNetView m_nview;
        private Vagon m_vagon;
        private Container m_container;
        private Transform m_attachPoint;
        private Transform m_lineAttach0;
        private Transform m_lineAttach1;
        private LineRenderer m_line;
        private LineRenderer m_line2;
        private Collider[] m_colliders;
        private CartSeat m_seat;

        private ConfigurableJoint m_joint;
        private HarnessAnimal m_animal;
        private HarnessAnimal m_ignoredWith;
        private float m_searchTimer;
        private float m_missingTime;
        private float m_claimTimer;
        private float m_driverTimer;
        private bool m_validDriver;
        private float m_invalidDriverTime;
        private float m_hoverTimer;
        private HarnessAnimal m_hoverCandidate;

        public bool Valid => m_nview != null && m_nview.IsValid();
        public bool IsOwner => Valid && m_nview.IsOwner();
        public long HitchId => Valid ? m_nview.GetZDO().GetLong(s_hitchKey) : 0L;
        public bool IsHitched => HitchId != 0L;
        public long DriverId => Valid ? m_nview.GetZDO().GetLong(s_driverKey) : 0L;
        public bool HasValidDriver => IsHitched && m_validDriver;
        public HarnessAnimal Animal => m_animal;
        public ZNetView NView => m_nview;
        public Vagon Vagon => m_vagon;
        public CartSeat Seat => m_seat;

        public bool IsLocalDriver => m_seat != null && m_seat.IsLocalUser();

        private bool ContainerInUse => Valid && m_nview.GetZDO().GetInt(ZDOVars.s_inUse) == 1;

        /// <summary>Тележка с этой меткой сцепки ближе range.</summary>
        public static CartHitch Find(long id, Vector3 near, float range)
        {
            if (id == 0L)
            {
                return null;
            }
            foreach (CartHitch cart in s_all)
            {
                if (cart != null && cart.HitchId == id && Vector3.Distance(cart.transform.position, near) < range)
                {
                    return cart;
                }
            }
            return null;
        }

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            m_vagon = GetComponent<Vagon>();
            m_seat = GetComponentInChildren<CartSeat>(true);
            if (m_vagon != null)
            {
                m_container = m_vagon.m_container;
                m_attachPoint = m_vagon.m_attachPoint;
                m_lineAttach0 = m_vagon.m_lineAttachPoints0;
                m_lineAttach1 = m_vagon.m_lineAttachPoints1;
            }
            m_line = GetComponent<LineRenderer>();
            if (m_line != null)
            {
                // Вторая постромка: ванильная линия одна (V от двух концов поперечины к игроку), а нам нужны две — к бокам хомута.
                var go = new GameObject("da_trace");
                go.transform.SetParent(transform, false);
                m_line2 = go.AddComponent<LineRenderer>();
                m_line2.sharedMaterials = m_line.sharedMaterials;
                m_line2.widthMultiplier = m_line.widthMultiplier;
                m_line2.widthCurve = m_line.widthCurve;
                m_line2.colorGradient = m_line.colorGradient;
                m_line2.textureMode = m_line.textureMode;
                m_line2.alignment = m_line.alignment;
                m_line2.numCapVertices = m_line.numCapVertices;
                m_line2.numCornerVertices = m_line.numCornerVertices;
                m_line2.shadowCastingMode = m_line.shadowCastingMode;
                m_line2.useWorldSpace = true;
                m_line2.positionCount = 2;
                m_line2.enabled = false;
            }
            var colliders = new List<Collider>();
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
            {
                if (!c.isTrigger)
                {
                    colliders.Add(c);
                }
            }
            m_colliders = colliders.ToArray();
            s_all.Add(this);
            if (Valid)
            {
                m_nview.Register("DA_Unhitch", RPC_Unhitch);
                m_nview.Register<long>("DA_RequestDrive", RPC_RequestDrive);
                m_nview.Register<bool>("DA_DriveResponse", RPC_DriveResponse);
                m_nview.Register<long>("DA_ReleaseDrive", RPC_ReleaseDrive);
            }
        }

        private void OnDestroy()
        {
            s_all.Remove(this);
            Release();
        }

        private void Update()
        {
            if (!Valid || m_attachPoint == null)
            {
                return;
            }
            float dt = Time.deltaTime;
            long id = HitchId;
            if (id == 0L)
            {
                if (m_joint != null || m_animal != null)
                {
                    Release();
                }
                m_validDriver = false;
                return;
            }

            if (m_animal == null || m_animal.HitchId != id)
            {
                m_searchTimer -= dt;
                if (m_searchTimer <= 0f)
                {
                    m_searchTimer = 0.5f;
                    SetAnimal(FindAnimal(id));
                }
            }
            if (m_animal == null)
            {
                DestroyJoint();
                if (IsOwner && ZNetScene.instance != null && ZNetScene.instance.IsAreaReady(transform.position))
                {
                    m_missingTime += dt;
                    if (m_missingTime > 8f)
                    {
                        ClearHitch();
                    }
                }
                return;
            }
            m_missingTime = 0f;
            if (m_animal.IsDead)
            {
                if (IsOwner)
                {
                    ClearHitch();
                }
                return;
            }

            UpdateDriver(dt);

            if (!IsOwner)
            {
                DestroyJoint();
                // Тележку физически ведёт тот, у кого зверь: так связь считается на одной машине.
                m_claimTimer -= dt;
                if (m_claimTimer <= 0f && m_animal.IsOwner && !ContainerInUse)
                {
                    m_claimTimer = 1f;
                    m_nview.ClaimOwnership();
                }
                return;
            }

            if (transform.up.y < 0.1f)
            {
                // Тележка перевернулась — постромки отстёгиваются, как у игрока.
                ClearHitch();
                return;
            }
            Vector3 anchor = m_animal.transform.TransformPoint(m_animal.HitchAnchor);
            float distance = Vector3.Distance(anchor, m_attachPoint.position);
            if (m_joint == null)
            {
                if (distance > 4f)
                {
                    ClearHitch();
                    return;
                }
                CreateJoint();
            }
            else if (distance > 6f)
            {
                ClearHitch();
            }
            else if (m_joint.connectedAnchor != m_animal.HitchAnchor)
            {
                m_joint.connectedAnchor = m_animal.HitchAnchor;
            }
        }

        private HarnessAnimal FindAnimal(long id)
        {
            foreach (HarnessAnimal a in HarnessAnimal.All)
            {
                if (a != null && a.HitchId == id && Vector3.Distance(a.transform.position, transform.position) < 40f)
                {
                    return a;
                }
            }
            return null;
        }

        private void SetAnimal(HarnessAnimal animal)
        {
            if (animal == m_animal)
            {
                return;
            }
            DestroyJoint();
            if (m_ignoredWith != null)
            {
                IgnoreCollisions(m_ignoredWith, false);
            }
            m_animal = animal;
            if (animal != null)
            {
                IgnoreCollisions(animal, true);
            }
        }

        /// <summary>Тележка и запряжённый зверь не толкают друг друга (зверь стоит между оглоблями).</summary>
        private void IgnoreCollisions(HarnessAnimal animal, bool ignore)
        {
            m_ignoredWith = ignore ? animal : null;
            if (animal == null)
            {
                return;
            }
            foreach (Collider a in m_colliders)
            {
                if (a == null)
                {
                    continue;
                }
                foreach (Collider b in animal.Colliders)
                {
                    if (b != null)
                    {
                        Physics.IgnoreCollision(a, b, ignore);
                    }
                }
            }
        }

        /// <summary>Та же связь, что у ванильной тележки с игроком (Vagon.AttachTo), но к выбранной точке зверя.</summary>
        private void CreateJoint()
        {
            Rigidbody body = m_animal.Body;
            if (body == null)
            {
                return;
            }
            m_joint = gameObject.AddComponent<ConfigurableJoint>();
            m_joint.autoConfigureConnectedAnchor = false;
            m_joint.anchor = transform.InverseTransformPoint(m_attachPoint.position);
            m_joint.connectedAnchor = m_animal.HitchAnchor;
            m_joint.breakForce = m_vagon.m_breakForce;
            m_joint.xMotion = ConfigurableJointMotion.Limited;
            m_joint.yMotion = ConfigurableJointMotion.Limited;
            m_joint.zMotion = ConfigurableJointMotion.Locked;
            m_joint.linearLimit = new SoftJointLimit { limit = 0.001f };
            m_joint.linearLimitSpring = new SoftJointLimitSpring { spring = m_vagon.m_spring, damper = m_vagon.m_springDamping };
            m_joint.connectedBody = body;
        }

        private void DestroyJoint()
        {
            if (m_joint != null)
            {
                Destroy(m_joint);
            }
            m_joint = null;
        }

        private void Release()
        {
            DestroyJoint();
            if (m_ignoredWith != null)
            {
                IgnoreCollisions(m_ignoredWith, false);
            }
            m_animal = null;
            m_missingTime = 0f;
            RestoreLines();
        }

        /// <summary>Снять сцепку (у владельца тележки) и сообщить зверю.</summary>
        private void ClearHitch()
        {
            HarnessAnimal animal = m_animal;
            if (IsOwner)
            {
                m_nview.GetZDO().Set(s_hitchKey, 0L);
                m_nview.GetZDO().Set(s_driverKey, 0L);
            }
            if (animal != null && animal.Valid)
            {
                animal.NView.InvokeRPC("DA_Unhitch");
            }
            m_validDriver = false;
            Release();
        }

        // ================================================================== возница

        /// <summary>Возница действителен, пока его персонаж рядом с козлами (как наездник у седла локса).</summary>
        private void UpdateDriver(float dt)
        {
            m_driverTimer -= dt;
            if (m_driverTimer > 0f)
            {
                return;
            }
            m_driverTimer = 0.5f;
            m_validDriver = CheckDriver(DriverId);
            if (IsOwner && DriverId != 0L && !m_validDriver)
            {
                m_invalidDriverTime += 0.5f;
                if (m_invalidDriverTime > 3f)
                {
                    m_nview.GetZDO().Set(s_driverKey, 0L);
                }
            }
            else
            {
                m_invalidDriverTime = 0f;
            }
        }

        private bool CheckDriver(long driver)
        {
            if (driver == 0L || ZNet.instance == null)
            {
                return false;
            }
            Vector3 seat = m_seat != null ? m_seat.transform.position : transform.position;
            foreach (ZDO zdo in ZNet.instance.GetAllCharacterZDOS())
            {
                if (zdo.m_uid.UserID == driver)
                {
                    return Vector3.Distance(zdo.GetPosition(), seat) < DriverRange;
                }
            }
            return false;
        }

        public void RequestDrive(long playerId)
        {
            if (Valid)
            {
                m_nview.InvokeRPC("DA_RequestDrive", playerId);
            }
        }

        public void ReleaseDrive(long playerId)
        {
            if (Valid)
            {
                m_nview.InvokeRPC("DA_ReleaseDrive", playerId);
            }
        }

        private void RPC_RequestDrive(long sender, long playerId)
        {
            if (!IsOwner)
            {
                return;
            }
            if (!IsHitched)
            {
                m_nview.InvokeRPC(sender, "DA_DriveResponse", false);
                return;
            }
            long current = DriverId;
            if (current == 0L || current == playerId || !CheckDriver(current))
            {
                m_nview.GetZDO().Set(s_driverKey, playerId);
                m_validDriver = true;
                m_invalidDriverTime = 0f;
                m_nview.InvokeRPC(sender, "DA_DriveResponse", true);
                // Тележку ведёт возница: и её, и зверя он заберёт себе.
                m_nview.GetZDO().SetOwner(sender);
            }
            else
            {
                m_nview.InvokeRPC(sender, "DA_DriveResponse", false);
            }
        }

        private void RPC_DriveResponse(long sender, bool granted)
        {
            if (m_seat != null)
            {
                m_seat.OnDriveResponse(granted);
            }
        }

        private void RPC_ReleaseDrive(long sender, long playerId)
        {
            if (IsOwner && DriverId == playerId)
            {
                m_nview.GetZDO().Set(s_driverKey, 0L);
            }
        }

        /// <summary>Вес возницы с его инвентарём (пишет сам возница, когда тележка у него).</summary>
        public void ReportDriverWeight(float weight)
        {
            if (IsOwner && Mathf.Abs(m_nview.GetZDO().GetFloat(s_driverWeightKey) - weight) > 0.5f)
            {
                m_nview.GetZDO().Set(s_driverWeightKey, weight);
            }
        }

        // ================================================================== груз

        /// <summary>Груз для зверя: содержимое тележки и возница.</summary>
        public float GetLoad()
        {
            float load = m_container != null && m_container.GetInventory() != null ? m_container.GetInventory().GetTotalWeight() : 0f;
            if (HasValidDriver)
            {
                load += m_nview.GetZDO().GetFloat(s_driverWeightKey, DraftConfig.DriverWeight.Value);
            }
            return load;
        }

        // ================================================================== запрячь / распрячь

        /// <summary>Ближайший ручной зверь в подходящей упряжи, ещё не запряжённый.</summary>
        public static HarnessAnimal FindCandidate(Vector3 point, float range)
        {
            HarnessAnimal best = null;
            float bestDistance = range;
            foreach (HarnessAnimal a in HarnessAnimal.All)
            {
                if (a == null || !a.Valid || a.IsDead || !a.IsTamed || a.IsHitched || !a.Config.Enabled.Value)
                {
                    continue;
                }
                if (a.Harness == HarnessKind.None || a.Harness != a.Info.Harness)
                {
                    continue;
                }
                if (a.Tame != null && a.Tame.IsTied)
                {
                    continue;
                }
                float d = Vector3.Distance(a.transform.position, point);
                if (d < bestDistance)
                {
                    best = a;
                    bestDistance = d;
                }
            }
            return best;
        }

        /// <summary>Shift+E у оглобель: запрячь ближайшего зверя в упряжи или распрячь.</summary>
        public void ToggleHitch(Humanoid user)
        {
            if (!Valid || user == null)
            {
                return;
            }
            if (IsHitched)
            {
                Unhitch(user);
                return;
            }
            if (m_vagon.IsAttached())
            {
                user.Message(MessageHud.MessageType.Center, "$da_msg_cart_busy");
                return;
            }
            if (ContainerInUse)
            {
                user.Message(MessageHud.MessageType.Center, "$da_msg_cart_open");
                return;
            }
            if (transform.up.y < 0.5f)
            {
                user.Message(MessageHud.MessageType.Center, "$msg_cart_incorrectposition");
                return;
            }
            HarnessAnimal animal = FindCandidate(m_attachPoint.position, DraftConfig.HitchRange.Value);
            if (animal == null)
            {
                user.Message(MessageHud.MessageType.Center, "$da_msg_no_animal");
                return;
            }

            long id = 0L;
            while (id == 0L)
            {
                id = ((long)Random.Range(int.MinValue, int.MaxValue) << 32) ^ (uint)Random.Range(int.MinValue, int.MaxValue);
            }
            m_nview.ClaimOwnership();
            m_nview.GetZDO().Set(s_hitchKey, id);
            m_nview.GetZDO().Set(s_driverKey, 0L);

            // Зверь встаёт перед оглоблями мордой по ходу тележки, поперечина — у него за спиной.
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            Vector3 position = m_attachPoint.position + forward * animal.Config.HitchDistance.Value;
            position.y = ZoneSystem.instance != null ? ZoneSystem.instance.GetSolidHeight(position) : position.y;
            animal.BeginHitch(id, position, Quaternion.LookRotation(forward));

            m_searchTimer = 0f;
            m_missingTime = 0f;
            SetAnimal(animal);
            user.Message(MessageHud.MessageType.Center, animal.DisplayName + " $da_msg_hitched");
        }

        public void Unhitch(Humanoid user)
        {
            HarnessAnimal animal = m_animal;
            m_nview.InvokeRPC("DA_Unhitch");
            if (animal != null && animal.Valid)
            {
                animal.NView.InvokeRPC("DA_Unhitch");
            }
            user?.Message(MessageHud.MessageType.Center, "$da_msg_unhitched");
        }

        private void RPC_Unhitch(long sender)
        {
            if (IsOwner)
            {
                ClearHitch();
            }
        }

        // ================================================================== вид

        /// <summary>Постромки: от концов поперечины к кольцам по бокам хомута (после ванильного LateUpdate).</summary>
        public void DrawTraces()
        {
            if (m_line == null || m_lineAttach0 == null || m_lineAttach1 == null)
            {
                return;
            }
            if (m_animal == null || !IsHitched)
            {
                RestoreLines();
                return;
            }
            m_line.enabled = true;
            m_line.positionCount = 2;
            m_line.SetPosition(0, m_lineAttach0.position);
            m_line.SetPosition(1, m_animal.TracePoint(1));
            if (m_line2 != null)
            {
                m_line2.enabled = true;
                m_line2.SetPosition(0, m_lineAttach1.position);
                m_line2.SetPosition(1, m_animal.TracePoint(-1));
            }
        }

        private void RestoreLines()
        {
            if (m_line != null && m_line.positionCount != 3)
            {
                m_line.positionCount = 3;
            }
            if (m_line2 != null)
            {
                m_line2.enabled = false;
            }
        }

        /// <summary>Подсказка над оглоблями: кто запряжён и сколько везёт, или кого можно запрячь.</summary>
        public string GetHoverText()
        {
            string name = Localization.instance.Localize(m_vagon.m_name);
            if (IsHitched)
            {
                string text = name;
                if (m_animal != null)
                {
                    float load = GetLoad();
                    float capacity = m_animal.GetCapacity();
                    text += " ( " + m_animal.DisplayName + " )\n$da_hover_load: " + Mathf.RoundToInt(load) + " / " + Mathf.RoundToInt(capacity);
                    if (load > capacity)
                    {
                        text += " <color=#ffa040>$da_hover_overload</color>";
                    }
                }
                text += "\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] $da_unhitch";
                return Localization.instance.Localize(text);
            }
            m_hoverTimer -= Time.deltaTime;
            if (m_hoverTimer <= 0f)
            {
                m_hoverTimer = 0.5f;
                m_hoverCandidate = m_attachPoint != null ? FindCandidate(m_attachPoint.position, DraftConfig.HitchRange.Value) : null;
            }
            string result = name + "\n[<color=yellow><b>$KEY_Use</b></color>] $piece_use";
            if (m_hoverCandidate != null)
            {
                result += "\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] $da_hitch: " + m_hoverCandidate.DisplayName;
            }
            return Localization.instance.Localize(result);
        }
    }
}
