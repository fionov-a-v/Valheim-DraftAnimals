using System.Collections.Generic;
using DraftAnimals.Logic;
using DraftAnimals.Visuals;
using UnityEngine;

namespace DraftAnimals
{
    /// <summary>
    /// Зверь, которого можно запрячь (кабан, олень, волк): упряжь и её модель, скорость от груза тележки, выносливость,
    /// свой ИИ в упряжи (следование за ведущим, вожжи возницы, паника оленя) и приручение дикого оленя на приманку.
    /// Состояние — в ZDO зверя: упряжь, метка сцепки с тележкой, выносливость. ИИ и запись в ZDO — только у владельца.
    /// </summary>
    public sealed class HarnessAnimal : MonoBehaviour, IFoodEater
    {
        public Species m_species;

        internal static readonly int s_harnessKey = "da_harness".GetStableHashCode();
        internal static readonly int s_hitchKey = "da_hitch".GetStableHashCode();
        internal static readonly int s_staminaKey = "da_stamina".GetStableHashCode();
        internal static readonly int s_tameFoodKey = "da_tamefood".GetStableHashCode();

        private static readonly List<HarnessAnimal> s_all = new List<HarnessAnimal>();
        private static readonly Dictionary<BaseAI, HarnessAnimal> s_byAI = new Dictionary<BaseAI, HarnessAnimal>();
        private static int s_itemMask;

        /// <summary>Как часто владелец пересчитывает скорость, сцепку и пишет выносливость в ZDO, с.</summary>
        private const float TickInterval = 0.25f;

        private ZNetView m_nview;
        private Character m_character;
        private BaseAI m_ai;
        private MonsterAI m_monsterAI;
        private AnimalAI m_animalAI;
        private Tameable m_tameable;
        private Procreation m_procreation;
        private Rigidbody m_body;
        private Collider[] m_colliders;
        private SpeciesInfo m_info;

        private GameObject m_visual;
        private HarnessKind m_visualKind;
        private float m_visualTimer;

        // Грудная кость и её поза в префабе относительно корня (снята в Awake, до первой анимации).
        private Transform m_bone;
        private Vector3 m_boneRestPos;
        private Quaternion m_boneRestRot = Quaternion.identity;
        private float m_boneRestScale = 1f;

        private float m_origSpeed;
        private float m_origWalk;
        private float m_origRun;
        private bool m_speedsSet;
        private float m_speedFactor = 1f;

        private float m_tick;
        private bool m_wasOwner;
        private float m_stamina;
        private bool m_exhausted;

        private CartHitch m_cart;
        private float m_cartMissing;

        // Вожжи (у владельца).
        private Vector3 m_driveDir;
        private DriveMode m_driveMode;
        private float m_driveSkill;

        // Следование.
        private GameObject m_follow;
        private Gait m_gait;

        // Паника оленя.
        private Character m_threat;
        private float m_threatTimer;
        private float m_lastHurtTime = -100f;
        private Vector3 m_lastAttackerPos;
        private bool m_panicNotified;

        // Еда на земле.
        private ItemDrop m_foodTarget;
        private float m_foodSearchTimer;

        // Приручение оленя.
        private float m_tameTimer = 3f;
        private bool m_wasAlerted;
        private float m_lastScare = -1000f;

        private bool m_vanillaRan;
        private TameAnimal m_tame;

        public static List<HarnessAnimal> All => s_all;

        public static bool TryGet(BaseAI ai, out HarnessAnimal animal)
        {
            return s_byAI.TryGetValue(ai, out animal);
        }

        public SpeciesInfo Info => m_info;
        public Species Species => m_species;
        public Character Character => m_character;
        public ZNetView NView => m_nview;
        public Rigidbody Body => m_body;
        public Collider[] Colliders => m_colliders;
        public Tameable Tameable => m_tameable;
        public SpeciesConfig Config => DraftConfig.For(m_species);

        public bool Valid => m_nview != null && m_nview.IsValid();
        public bool IsOwner => Valid && m_nview.IsOwner();
        public bool IsTamed => m_character != null && m_character.IsTamed();
        public bool IsDead => m_character == null || m_character.IsDead();
        public bool IsHungry => m_tameable != null && m_tameable.IsHungry();

        public HarnessKind Harness => Valid ? (HarnessKind)m_nview.GetZDO().GetInt(s_harnessKey) : HarnessKind.None;
        public bool IsHarnessed => Harness != HarnessKind.None;
        public long HitchId => Valid ? m_nview.GetZDO().GetLong(s_hitchKey) : 0L;
        public bool IsHitched => HitchId != 0L;

        /// <summary>Точка зверя, к которой пристёгнута поперечина оглобель (координаты зверя).</summary>
        public Vector3 HitchAnchor => Config.HitchAnchor;

        public float MaxStamina => Config.MaxStamina.Value;

        public float Stamina => IsOwner ? m_stamina : Valid ? m_nview.GetZDO().GetFloat(s_staminaKey, MaxStamina) : 0f;

        public bool IsExhausted => m_exhausted;

        /// <summary>Множитель скорости от груза и голода (у владельца; 1 — без замедления).</summary>
        public float SpeedFactor => m_speedFactor;

        public string DisplayName => m_tameable != null
            ? m_tameable.GetHoverName()
            : Localization.instance.Localize(m_character != null ? m_character.m_name : name);

        /// <summary>Тележка, в которую запряжён зверь (если она рядом и загружена).</summary>
        public CartHitch Cart
        {
            get
            {
                long id = HitchId;
                if (id == 0L)
                {
                    m_cart = null;
                    return null;
                }
                if (m_cart == null || m_cart.HitchId != id)
                {
                    m_cart = CartHitch.Find(id, transform.position, 40f);
                }
                return m_cart;
            }
        }

        public bool IsDriven
        {
            get
            {
                CartHitch cart = IsHitched ? Cart : null;
                return cart != null && cart.HasValidDriver;
            }
        }

        public GameObject FollowTarget => m_monsterAI != null ? m_monsterAI.GetFollowTarget() : m_follow;

        /// <summary>Привязь и кормушка (компонент на всех приручаемых).</summary>
        public TameAnimal Tame => m_tame != null ? m_tame : (m_tame = GetComponent<TameAnimal>());

        // ================================================================== жизненный цикл

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            m_character = GetComponent<Character>();
            m_ai = GetComponent<BaseAI>();
            m_monsterAI = m_ai as MonsterAI;
            m_animalAI = m_ai as AnimalAI;
            m_tameable = GetComponent<Tameable>();
            m_procreation = GetComponent<Procreation>();
            m_body = GetComponent<Rigidbody>();
            m_info = SpeciesInfo.Get(m_species);
            CaptureBone();
            var colliders = new List<Collider>();
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
            {
                if (!c.isTrigger)
                {
                    colliders.Add(c);
                }
            }
            m_colliders = colliders.ToArray();
            if (m_character != null)
            {
                m_origSpeed = m_character.m_speed;
                m_origWalk = m_character.m_walkSpeed;
                m_origRun = m_character.m_runSpeed;
                m_character.m_onDamaged += OnDamaged;
            }
            s_all.Add(this);
            if (m_ai != null)
            {
                s_byAI[m_ai] = this;
            }
            if (Valid)
            {
                m_nview.Register<int>("DA_SetHarness", RPC_SetHarness);
                m_nview.Register<Vector3>("DA_RemoveHarness", RPC_RemoveHarness);
                m_nview.Register<Vector3, int, float>("DA_Controls", RPC_Controls);
                m_nview.Register("DA_Unhitch", RPC_Unhitch);
                m_nview.Register<float>("DA_Feed", RPC_Feed);
            }
            ApplyTameableSettings();
            DraftConfig.VisualsChanged += OnVisualsChanged;
        }

        private void OnDestroy()
        {
            s_all.Remove(this);
            if (m_ai != null)
            {
                s_byAI.Remove(m_ai);
            }
            DraftConfig.VisualsChanged -= OnVisualsChanged;
        }

        /// <summary>
        /// Запоминаем позу грудной кости в префабе (зверь стоит): модель упряжи строится в координатах корня под эту
        /// позу и вешается на кость — дальше двигается вместе с телом, когда бы её ни надели.
        /// </summary>
        private void CaptureBone()
        {
            m_bone = null;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == m_info.Bone)
                {
                    m_bone = t;
                    break;
                }
            }
            if (m_bone == null)
            {
                return;
            }
            m_boneRestPos = transform.InverseTransformPoint(m_bone.position);
            m_boneRestRot = Quaternion.Inverse(transform.rotation) * m_bone.rotation;
            float rootScale = Mathf.Max(1e-4f, transform.lossyScale.x);
            m_boneRestScale = Mathf.Max(1e-4f, m_bone.lossyScale.x / rootScale);
        }

        /// <summary>Сытость и время приручения оленя берутся из настроек (они могут прийти с сервера позже).</summary>
        private void ApplyTameableSettings()
        {
            if (m_species != Species.Deer)
            {
                return;
            }
            if (m_tameable != null)
            {
                m_tameable.m_fedDuration = DraftConfig.DeerFedDuration.Value;
                m_tameable.m_tamingTime = DraftConfig.DeerTamingTime.Value;
            }
            if (m_procreation != null)
            {
                // Размножение выключено — «в округе уже полно оленей», ванильный Procreation дальше не идёт.
                m_procreation.m_maxCreatures = DraftConfig.DeerBreeding.Value ? DraftConfig.MaxDeerNearby.Value : 0;
            }
        }

        private void Update()
        {
            if (!Valid)
            {
                return;
            }
            float dt = Time.deltaTime;
            m_visualTimer -= dt;
            if (m_visualTimer <= 0f)
            {
                m_visualTimer = 0.5f;
                UpdateVisual();
            }

            bool owner = m_nview.IsOwner();
            if (owner && !m_wasOwner)
            {
                m_stamina = m_nview.GetZDO().GetFloat(s_staminaKey, MaxStamina);
            }
            m_wasOwner = owner;
            if (!owner)
            {
                RestoreSpeeds();
                return;
            }

            UpdateStamina(dt);
            m_tick -= dt;
            if (m_tick <= 0f)
            {
                m_tick = TickInterval;
                ApplyTameableSettings();
                ValidateHitch();
                ApplySpeeds();
                RestoreDeerFollow();
                // Пишем, только если изменилась: у диких и незапряжённых зверей значение в ZDO так и не появится.
                if (Mathf.Abs(m_nview.GetZDO().GetFloat(s_staminaKey, MaxStamina) - m_stamina) > 0.05f)
                {
                    m_nview.GetZDO().Set(s_staminaKey, m_stamina);
                }
            }
            if (m_species == Species.Deer && !IsTamed)
            {
                UpdateTaming(dt);
            }
        }

        // ================================================================== модель упряжи

        private void OnVisualsChanged()
        {
            if (m_visual != null)
            {
                Destroy(m_visual);
                m_visual = null;
            }
            m_visualKind = HarnessKind.None;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            HarnessKind kind = IsTamed ? Harness : HarnessKind.None;
            if (kind == m_visualKind && (kind == HarnessKind.None) == (m_visual == null))
            {
                return;
            }
            m_visualKind = kind;
            if (m_visual != null)
            {
                Destroy(m_visual);
                m_visual = null;
            }
            if (kind == HarnessKind.None)
            {
                return;
            }
            float scale = Config.VisualScale.Value;
            Vector3 offset = Config.VisualOffsetVector;
            if (m_bone != null)
            {
                // Кадр модели совпадает с кадром корня в позе префаба (со сдвигом и масштабом из настроек).
                m_visual = Art.BuildHarness(m_bone, m_info, kind);
                Quaternion inverse = Quaternion.Inverse(m_boneRestRot);
                m_visual.transform.localRotation = inverse;
                m_visual.transform.localPosition = inverse * (offset - m_boneRestPos) / m_boneRestScale;
                m_visual.transform.localScale = Vector3.one * (scale / m_boneRestScale);
            }
            else
            {
                m_visual = Art.BuildHarness(transform, m_info, kind);
                m_visual.transform.localPosition = offset;
                m_visual.transform.localScale = Vector3.one * scale;
            }
        }

        /// <summary>Мировая точка, куда крепится постромка: side = +1 — справа, -1 — слева.</summary>
        public Vector3 TracePoint(int side)
        {
            Vector3 local = Art.TracePoint(m_info, side);
            if (m_visual != null)
            {
                return m_visual.transform.TransformPoint(local);
            }
            return transform.TransformPoint(Config.VisualOffsetVector + local * Config.VisualScale.Value);
        }

        // ================================================================== скорость и выносливость

        private void ApplySpeeds()
        {
            CartHitch cart = IsHitched ? Cart : null;
            if (cart == null || m_character == null)
            {
                RestoreSpeeds();
                m_speedFactor = 1f;
                return;
            }
            SpeciesConfig cfg = Config;
            float capacity = GetCapacity();
            m_speedFactor = DraftRules.SpeedFactor(cart.GetLoad(), capacity, cfg.OverloadMinPercent.Value / 100f, IsHungry,
                DraftConfig.HungrySpeedPercent.Value);
            float walk = cfg.WalkSpeed.Value * m_speedFactor;
            float run = m_exhausted ? walk : cfg.RunSpeed.Value * m_speedFactor;
            m_character.m_speed = walk;
            m_character.m_walkSpeed = walk;
            m_character.m_runSpeed = run;
            m_speedsSet = true;
        }

        private void RestoreSpeeds()
        {
            if (!m_speedsSet || m_character == null)
            {
                return;
            }
            m_character.m_speed = m_origSpeed;
            m_character.m_walkSpeed = m_origWalk;
            m_character.m_runSpeed = m_origRun;
            m_speedsSet = false;
        }

        /// <summary>Грузоподъёмность с учётом звёзд.</summary>
        public float GetCapacity()
        {
            int level = m_character != null ? m_character.GetLevel() : 1;
            return DraftRules.Capacity(Config.Capacity.Value, level, DraftConfig.StarCapacityBonusPercent.Value);
        }

        private void UpdateStamina(float dt)
        {
            float max = MaxStamina;
            if (!IsHitched)
            {
                m_stamina = max;
                m_exhausted = false;
                return;
            }
            SpeciesConfig cfg = Config;
            bool running = m_character != null && m_character.IsRunning() && m_body != null && m_body.linearVelocity.magnitude > 0.5f;
            float drain = cfg.StaminaDrain.Value * (IsDriven ? DraftRules.RideDrainFactor(m_driveSkill) : 1f);
            float regen = cfg.StaminaRegen.Value * (IsHungry ? 0.5f : 1f);
            m_stamina = DraftRules.StaminaStep(Mathf.Min(m_stamina, max), max, running, drain, regen, dt);
            bool wasExhausted = m_exhausted;
            m_exhausted = DraftRules.Exhausted(m_exhausted, m_stamina, max, DraftConfig.ExhaustedRecoverPercent.Value);
            if (m_exhausted != wasExhausted)
            {
                ApplySpeeds();
            }
        }

        // ================================================================== сцепка

        /// <summary>Тележки с нашей меткой рядом нет (сломали, распрягли) — снимаем метку и у себя.</summary>
        private void ValidateHitch()
        {
            if (!IsHitched)
            {
                m_cartMissing = 0f;
                return;
            }
            if (IsDead)
            {
                SetHitch(0L);
                return;
            }
            if (Cart != null)
            {
                m_cartMissing = 0f;
                return;
            }
            if (ZNetScene.instance != null && ZNetScene.instance.IsAreaReady(transform.position))
            {
                m_cartMissing += TickInterval;
                if (m_cartMissing > 8f)
                {
                    SetHitch(0L);
                }
            }
        }

        private void SetHitch(long id)
        {
            if (!IsOwner)
            {
                return;
            }
            m_nview.GetZDO().Set(s_hitchKey, id);
            if (id == 0L)
            {
                m_cart = null;
                m_driveMode = DriveMode.Stop;
                RestoreSpeeds();
            }
        }

        /// <summary>
        /// Начало сцепки на клиенте того, кто запрягает: забираем зверя себе, ставим метку и переносим к оглоблям
        /// мордой по ходу тележки.
        /// </summary>
        public void BeginHitch(long id, Vector3 position, Quaternion rotation)
        {
            if (!Valid)
            {
                return;
            }
            m_nview.ClaimOwnership();
            m_nview.GetZDO().Set(s_hitchKey, id);
            m_cart = null;
            m_driveMode = DriveMode.Stop;
            m_gait = Gait.Stop;
            transform.SetPositionAndRotation(position, rotation);
            if (m_body != null)
            {
                m_body.position = position;
                m_body.rotation = rotation;
                m_body.linearVelocity = Vector3.zero;
            }
            // Иначе новый владелец в первом кадре вернул бы зверя на позицию из ZDO.
            m_nview.GetZDO().SetPosition(position);
            m_nview.GetZDO().SetRotation(rotation);
            m_character?.SetLookDir(rotation * Vector3.forward);
            m_ai?.StopMoving();
            Physics.SyncTransforms();
        }

        private void RPC_Unhitch(long sender)
        {
            SetHitch(0L);
        }

        // ================================================================== упряжь

        /// <summary>Надеть упряжь (из инвентаря игрока). Проверки — здесь, запись — у владельца.</summary>
        public bool TryPutHarness(Humanoid user, ItemDrop.ItemData item, HarnessKind kind)
        {
            if (!Valid || user == null)
            {
                return false;
            }
            if (!IsTamed)
            {
                user.Message(MessageHud.MessageType.Center, "$da_msg_not_tamed");
                return true;
            }
            if (!Config.Enabled.Value)
            {
                user.Message(MessageHud.MessageType.Center, "$da_msg_disabled");
                return true;
            }
            if (IsHarnessed)
            {
                user.Message(MessageHud.MessageType.Center, DisplayName + " $da_msg_already");
                return true;
            }
            if (kind != m_info.Harness)
            {
                user.Message(MessageHud.MessageType.Center, m_info.Harness == HarnessKind.Heavy ? "$da_msg_need_heavy" : "$da_msg_need_light");
                return true;
            }
            m_nview.InvokeRPC("DA_SetHarness", (int)kind);
            user.GetInventory().RemoveOneItem(item);
            user.Message(MessageHud.MessageType.Center, DisplayName + " $da_msg_harnessed");
            return true;
        }

        /// <summary>Снять упряжь (Shift+E): только с незапряжённого зверя; предмет падает рядом.</summary>
        public bool TryRemoveHarness(Humanoid user)
        {
            if (!Valid || !IsHarnessed)
            {
                return false;
            }
            if (IsHitched)
            {
                user?.Message(MessageHud.MessageType.Center, "$da_msg_unhitch_first");
                return true;
            }
            m_nview.InvokeRPC("DA_RemoveHarness", user != null ? user.transform.position : transform.position);
            return true;
        }

        private void RPC_SetHarness(long sender, int kind)
        {
            if (IsOwner && !IsHarnessed)
            {
                m_nview.GetZDO().Set(s_harnessKey, kind);
                UpdateVisual();
            }
        }

        private void RPC_RemoveHarness(long sender, Vector3 userPoint)
        {
            if (!IsOwner || !IsHarnessed || IsHitched)
            {
                return;
            }
            HarnessKind kind = Harness;
            m_nview.GetZDO().Set(s_harnessKey, 0);
            if (m_species != Species.Wolf)
            {
                // Кабан и олень слушаются команд только в упряжи.
                ClearFollow();
            }
            UpdateVisual();
            GameObject prefab = Items.Prefab(kind);
            if (prefab == null)
            {
                return;
            }
            GameObject drop = Instantiate(prefab, transform.position + Vector3.up, Quaternion.identity);
            Rigidbody rb = drop.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 dir = userPoint - transform.position;
                dir.y = 0f;
                rb.AddForce((Vector3.up + (dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.zero)) * 4f, ForceMode.VelocityChange);
            }
        }

        // ================================================================== корм

        /// <summary>Множитель корма (для оленя — из настроек, у остальных — 1) или 0, если зверь это не ест.</summary>
        public float FoodMultiplier(ItemDrop.ItemData item)
        {
            return item != null && item.m_shared != null ? FoodMultiplier(item.m_shared.m_name) : 0f;
        }

        /// <summary>То же по имени предмета (m_shared.m_name).</summary>
        public float FoodMultiplier(string sharedName)
        {
            if (string.IsNullOrEmpty(sharedName))
            {
                return 0f;
            }
            if (m_species == Species.Deer)
            {
                foreach (KeyValuePair<string, float> food in DraftConfig.DeerFoodList)
                {
                    GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(food.Key) : null;
                    ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                    if (drop != null && drop.m_itemData.m_shared.m_name == sharedName)
                    {
                        return food.Value;
                    }
                }
                return 0f;
            }
            if (m_monsterAI != null && m_monsterAI.m_consumeItems != null)
            {
                foreach (ItemDrop drop in m_monsterAI.m_consumeItems)
                {
                    if (drop != null && drop.m_itemData.m_shared.m_name == sharedName)
                    {
                        return 1f;
                    }
                }
            }
            return 0f;
        }

        /// <summary>Покормить с руки (предмет с панели быстрого доступа, глядя на зверя). Только ручного.</summary>
        public bool TryFeed(Humanoid user, ItemDrop.ItemData item, float multiplier)
        {
            if (!IsTamed)
            {
                if (m_species == Species.Deer)
                {
                    user.Message(MessageHud.MessageType.Center, "$da_msg_wild_food");
                    return true;
                }
                return false;
            }
            if (!IsHungry)
            {
                user.Message(MessageHud.MessageType.Center, DisplayName + " $da_msg_not_hungry");
                return true;
            }
            m_nview.InvokeRPC("DA_Feed", multiplier);
            user.GetInventory().RemoveOneItem(item);
            user.Message(MessageHud.MessageType.Center, DisplayName + " $da_msg_eats");
            return true;
        }

        private void RPC_Feed(long sender, float multiplier)
        {
            if (IsOwner)
            {
                Feed(multiplier);
            }
        }

        /// <summary>Отметка сытости (у владельца): корм с множителем держит сытость дольше и ускоряет приручение оленя.</summary>
        public void Feed(float multiplier)
        {
            if (m_tameable == null)
            {
                return;
            }
            bool wasHungry = IsHungry;
            long ticks = DraftRules.FedTimestamp(ZNet.instance.GetTime().Ticks, m_tameable.m_fedDuration, multiplier);
            m_nview.GetZDO().Set(ZDOVars.s_tameLastFeeding, ticks);
            m_nview.GetZDO().Set(s_tameFoodKey, multiplier);
            if (wasHungry)
            {
                m_tameable.m_sootheEffect.Create(m_character != null ? m_character.GetCenterPoint() : transform.position, Quaternion.identity);
            }
            if (m_character is Humanoid humanoid)
            {
                humanoid.m_consumeItemEffects.Create(transform.position, Quaternion.identity);
            }
        }

        private ItemDrop FindFood(float range)
        {
            if (s_itemMask == 0)
            {
                s_itemMask = LayerMask.GetMask("item");
            }
            ItemDrop best = null;
            float bestDistance = float.MaxValue;
            foreach (Collider c in Physics.OverlapSphere(transform.position, range, s_itemMask))
            {
                Rigidbody rb = c.attachedRigidbody;
                ItemDrop drop = rb != null ? rb.GetComponent<ItemDrop>() : null;
                if (drop == null)
                {
                    continue;
                }
                ZNetView view = drop.GetComponent<ZNetView>();
                if (view == null || !view.IsValid() || FoodMultiplier(drop.m_itemData) <= 0f)
                {
                    continue;
                }
                if (Tame != null && !Tame.CanReach(drop.transform.position))
                {
                    continue;
                }
                float d = Vector3.Distance(drop.transform.position, transform.position);
                if (d < bestDistance)
                {
                    best = drop;
                    bestDistance = d;
                }
            }
            return best;
        }

        /// <summary>Подойти к корму на земле и съесть его. true — зверь занят едой (ИИ больше ничего не делает).</summary>
        private bool UpdateConsume(float dt, float range)
        {
            if (Tame != null && Tame.UpdateFeeder(dt))
            {
                m_foodTarget = null;
                return true;
            }
            m_foodSearchTimer -= dt;
            if (m_foodSearchTimer <= 0f)
            {
                m_foodSearchTimer = 2f;
                m_foodTarget = FindFood(range);
            }
            if (m_foodTarget == null)
            {
                return false;
            }
            ZNetView view = m_foodTarget.GetComponent<ZNetView>();
            if (view == null || !view.IsValid())
            {
                m_foodTarget = null;
                return false;
            }
            Vector3 point = m_foodTarget.transform.position;
            if (AiAccess.MoveTo(m_ai, dt, point, 1.2f, false))
            {
                AiAccess.LookAt(m_ai, point);
                if (m_ai.IsLookingAt(point, 30f))
                {
                    float mult = FoodMultiplier(m_foodTarget.m_itemData);
                    if (m_foodTarget.RemoveOne())
                    {
                        Feed(mult);
                    }
                    m_foodTarget = null;
                }
            }
            return true;
        }

        // ================================================================== приручение оленя

        /// <summary>
        /// Дикий олень приручается, пока сыт и спокоен. Каждый испуг откатывает приручение (не чаще раза за ScareCooldown).
        /// </summary>
        private void UpdateTaming(float dt)
        {
            if (m_ai == null || m_tameable == null)
            {
                return;
            }
            bool alerted = m_ai.IsAlerted();
            if (alerted && !m_wasAlerted)
            {
                Scare();
            }
            m_wasAlerted = alerted;

            m_tameTimer -= dt;
            if (m_tameTimer > 0f)
            {
                return;
            }
            m_tameTimer = 3f;
            if (alerted || IsHungry)
            {
                return;
            }
            float tamingTime = DraftConfig.DeerTamingTime.Value;
            float left = m_nview.GetZDO().GetFloat(ZDOVars.s_tameTimeLeft, tamingTime);
            float speed = m_nview.GetZDO().GetFloat(s_tameFoodKey, 1f) * TamingBoost();
            left = DraftRules.TamingStep(left, 3f, speed);
            m_nview.GetZDO().Set(ZDOVars.s_tameTimeLeft, left);
            if (left <= 0f)
            {
                TameNow();
            }
            else
            {
                m_tameable.m_sootheEffect.Create(transform.position, transform.rotation);
            }
        }

        /// <summary>Ванильный ускоритель приручения: вдвое быстрее за каждого рядом стоящего игрока с таким эффектом.</summary>
        private float TamingBoost()
        {
            var players = new List<Player>();
            Player.GetPlayersInRange(transform.position, m_tameable.m_tamingSpeedMultiplierRange, players);
            float boost = 1f;
            foreach (Player p in players)
            {
                if (p.GetSEMan().HaveStatusAttribute(StatusEffect.StatusAttribute.TamingBoost))
                {
                    boost *= m_tameable.m_tamingBoostMultiplier;
                }
            }
            return boost;
        }

        private void Scare()
        {
            if (Time.time - m_lastScare < DraftConfig.DeerScareCooldown.Value)
            {
                return;
            }
            float tamingTime = DraftConfig.DeerTamingTime.Value;
            float left = m_nview.GetZDO().GetFloat(ZDOVars.s_tameTimeLeft, tamingTime);
            if (left >= tamingTime)
            {
                return;
            }
            m_lastScare = Time.time;
            m_nview.GetZDO().Set(ZDOVars.s_tameTimeLeft,
                DraftRules.ScareRollback(left, tamingTime, DraftConfig.DeerScareRollbackPercent.Value));
        }

        /// <summary>Консольная команда «tame» (ванильный Tameable.Tame требует MonsterAI, а у оленя его нет).</summary>
        public void TameByCommand()
        {
            if (m_species == Species.Deer && IsOwner && !IsTamed && m_character != null && m_tameable != null)
            {
                TameNow();
            }
        }

        private void TameNow()
        {
            m_character.SetTamed(true);
            AiAccess.SetAlerted(m_ai, false);
            // Ручной олень пасётся там, где его приручили, а не уходит блуждать.
            m_ai.SetPatrolPoint();
            m_foodTarget = null;
            m_tameable.m_tamedEffect.Create(transform.position, transform.rotation);
            Player closest = Player.GetClosestPlayer(transform.position, 30f);
            if (closest != null)
            {
                closest.Message(MessageHud.MessageType.Center, m_character.m_name + " $hud_tamedone");
            }
        }

        // ================================================================== команды «за мной / стой»

        /// <summary>Команда ручному оленю (у него нет MonsterAI, поэтому ванильная команда до него не доходит).</summary>
        public void DeerCommand(ZDOID characterID, bool message)
        {
            GameObject go = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(characterID) : null;
            Player player = go != null ? go.GetComponent<Player>() : null;
            if (player == null)
            {
                return;
            }
            if (m_follow != null)
            {
                m_follow = null;
                m_ai.SetPatrolPoint();
                if (IsOwner)
                {
                    m_nview.GetZDO().Set(ZDOVars.s_follow, "");
                }
                if (message)
                {
                    player.Message(MessageHud.MessageType.Center, DisplayName + " $hud_tamestay");
                }
                return;
            }
            m_follow = player.gameObject;
            m_ai.ResetPatrolPoint();
            if (IsOwner)
            {
                m_nview.GetZDO().Set(ZDOVars.s_follow, player.GetPlayerName());
            }
            if (message)
            {
                player.Message(MessageHud.MessageType.Center, DisplayName + " $hud_tamefollow");
            }
        }

        /// <summary>Перестать идти за ведущим (зверя привязали к колу).</summary>
        public void StopFollowing()
        {
            if (FollowTarget != null || (Valid && !string.IsNullOrEmpty(m_nview.GetZDO().GetString(ZDOVars.s_follow))))
            {
                ClearFollow();
            }
        }

        private void ClearFollow()
        {
            if (m_monsterAI != null)
            {
                m_monsterAI.SetFollowTarget(null);
                m_monsterAI.SetPatrolPoint();
            }
            else
            {
                m_ai?.SetPatrolPoint();
            }
            m_follow = null;
            if (IsOwner)
            {
                m_nview.GetZDO().Set(ZDOVars.s_follow, "");
            }
        }

        /// <summary>После загрузки или смены владельца ручной олень в упряжи снова находит, за кем шёл.</summary>
        private void RestoreDeerFollow()
        {
            if (m_species != Species.Deer || !IsTamed || m_follow != null || !IsHarnessed)
            {
                return;
            }
            string leader = m_nview.GetZDO().GetString(ZDOVars.s_follow);
            if (string.IsNullOrEmpty(leader))
            {
                return;
            }
            foreach (Player p in Player.GetAllPlayers())
            {
                if (p.GetPlayerName() == leader)
                {
                    m_follow = p.gameObject;
                    return;
                }
            }
        }

        // ================================================================== вожжи

        /// <summary>Возница передаёт управление (с каждого кадра, как седло локса).</summary>
        public void SendControls(Vector3 dir, DriveMode mode, float skill)
        {
            if (Valid)
            {
                m_nview.InvokeRPC("DA_Controls", dir, (int)mode, skill);
            }
        }

        private void RPC_Controls(long sender, Vector3 dir, int mode, float skill)
        {
            if (!IsOwner)
            {
                return;
            }
            m_driveSkill = skill;
            if (dir.sqrMagnitude > 0.01f)
            {
                m_driveDir = dir;
            }
            m_driveMode = DraftRules.NextDriveMode(m_driveMode, (DriveMode)mode);
        }

        // ================================================================== ИИ

        /// <summary>
        /// Перед ванильным ИИ. Дикие и звери без упряжи (кроме ручного оленя) — ванильный ИИ. Волк в упряжи без возницы —
        /// тоже ванильный (бросается в драку, волоча тележку). Остальное — свой ИИ: true — пустить ванильный.
        /// </summary>
        public bool PreUpdateAI(float dt, ref bool result)
        {
            m_vanillaRan = false;
            if (!IsTamed)
            {
                m_vanillaRan = true;
                return true;
            }
            if (m_species != Species.Deer)
            {
                bool harnessed = IsHarnessed && Config.Enabled.Value;
                if (!harnessed || (m_species == Species.Wolf && !IsDriven))
                {
                    m_vanillaRan = true;
                    return true;
                }
            }
            result = AiAccess.BaseUpdateAI(m_ai, dt);
            if (result)
            {
                Brain(dt);
            }
            return false;
        }

        /// <summary>После ванильного ИИ: дикий олень идёт на приманку, запряжённый волк без дела не бродит с тележкой.</summary>
        public void PostUpdateAI(float dt)
        {
            if (!m_vanillaRan || !IsOwner)
            {
                return;
            }
            if (m_species == Species.Deer && !IsTamed)
            {
                if (m_ai.IsAlerted() || AiAccess.AnimalTarget(m_animalAI) != null)
                {
                    m_foodTarget = null;
                    return;
                }
                if (IsHungry)
                {
                    UpdateConsume(dt, DraftConfig.DeerLureRange.Value);
                }
                return;
            }
            if (m_species == Species.Wolf && IsHitched && m_monsterAI != null && m_monsterAI.GetFollowTarget() == null &&
                m_monsterAI.GetTargetCreature() == null)
            {
                m_ai.StopMoving();
            }
        }

        private void Brain(float dt)
        {
            bool hitched = IsHitched;
            CartHitch cart = hitched ? Cart : null;
            if (m_species == Species.Deer && UpdatePanic(dt, hitched, cart))
            {
                return;
            }
            if (cart != null && cart.HasValidDriver)
            {
                Drive();
                return;
            }
            m_driveMode = DriveMode.Stop;
            GameObject follow = FollowTarget;
            if (follow != null && IsHarnessed)
            {
                HarnessFollow(follow, dt, hitched);
                return;
            }
            m_gait = Gait.Stop;
            if (hitched)
            {
                Stand();
                return;
            }
            if (IsHungry && UpdateConsume(dt, 8f))
            {
                return;
            }
            AiAccess.IdleMovement(m_ai, dt);
        }

        private void Stand()
        {
            m_ai.StopMoving();
            m_character.SetRun(false);
        }

        /// <summary>
        /// Пугливость ручного оленя: в упряжи при враге рядом — упирается (стоит), от удара — рвётся прочь, волоча тележку;
        /// без упряжи — убегает. true — олень занят паникой.
        /// </summary>
        private bool UpdatePanic(float dt, bool hitched, CartHitch cart)
        {
            m_threatTimer -= dt;
            if (m_threatTimer <= 0f)
            {
                m_threatTimer = 0.5f;
                m_threat = FindThreat(hitched ? DraftConfig.DeerPanicRange.Value : DraftConfig.DeerFleeRange.Value);
            }
            if (m_threat != null && m_threat.IsDead())
            {
                m_threat = null;
            }
            if (Time.time - m_lastHurtTime < DraftConfig.DeerBoltTime.Value)
            {
                AiAccess.SetAlerted(m_ai, true);
                AiAccess.Flee(m_ai, dt, m_lastAttackerPos);
                NotifyPanic(cart);
                return true;
            }
            if (m_threat != null)
            {
                if (hitched)
                {
                    Stand();
                }
                else
                {
                    AiAccess.SetAlerted(m_ai, true);
                    AiAccess.Flee(m_ai, dt, m_threat.transform.position);
                }
                NotifyPanic(cart);
                return true;
            }
            m_panicNotified = false;
            if (m_ai.IsAlerted())
            {
                AiAccess.SetAlerted(m_ai, false);
            }
            return false;
        }

        /// <summary>Ближайший враг (не игрок и не травоядное) ближе range.</summary>
        private Character FindThreat(float range)
        {
            if (range <= 0f)
            {
                return null;
            }
            Character best = null;
            float bestDistance = range;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == m_character || c.IsDead() || c.IsPlayer() || c.GetFaction() == Character.Faction.AnimalsVeg)
                {
                    continue;
                }
                if (!BaseAI.IsEnemy(m_character, c))
                {
                    continue;
                }
                float d = Vector3.Distance(c.transform.position, transform.position);
                if (d < bestDistance)
                {
                    best = c;
                    bestDistance = d;
                }
            }
            return best;
        }

        private void NotifyPanic(CartHitch cart)
        {
            if (m_panicNotified)
            {
                return;
            }
            m_panicNotified = true;
            if (cart != null && cart.IsLocalDriver && Player.m_localPlayer != null)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, DisplayName + " $da_msg_balks");
            }
        }

        private void OnDamaged(float damage, Character attacker)
        {
            if (m_species != Species.Deer || !IsTamed)
            {
                return;
            }
            m_lastHurtTime = Time.time;
            m_lastAttackerPos = attacker != null ? attacker.transform.position : transform.position - transform.forward * 3f;
        }

        /// <summary>Вожжи: идти в сторону взгляда возницы шагом или бегом, стоять или развернуться на месте.</summary>
        private void Drive()
        {
            if (m_driveMode == DriveMode.Run && m_exhausted)
            {
                m_driveMode = DriveMode.Walk;
            }
            Vector3 dir = m_driveDir.sqrMagnitude > 0.01f ? m_driveDir : transform.forward;
            switch (m_driveMode)
            {
                case DriveMode.Walk:
                case DriveMode.Run:
                    m_ai.MoveTowards(dir, m_driveMode == DriveMode.Run);
                    break;
                case DriveMode.Turn:
                    Stand();
                    m_ai.LookTowards(dir);
                    break;
                default:
                    Stand();
                    break;
            }
            m_ai.ResetRandomMovement();
        }

        /// <summary>Следование за ведущим в упряжи: без драк и блужданий, бегом — только пока есть силы.</summary>
        private void HarnessFollow(GameObject leader, float dt, bool hitched)
        {
            float distance = Utils.DistanceXZ(leader.transform.position, transform.position);
            if (distance > DraftConfig.LeaderRange.Value)
            {
                m_gait = Gait.Stop;
                Stand();
                return;
            }
            float stop = hitched ? 4f : 3f;
            m_gait = DraftRules.FollowGait(distance, m_gait, !m_exhausted, stop, 12f);
            if (m_gait == Gait.Stop)
            {
                Stand();
                return;
            }
            AiAccess.MoveTo(m_ai, dt, leader.transform.position, stop, m_gait == Gait.Run);
        }
    }
}
