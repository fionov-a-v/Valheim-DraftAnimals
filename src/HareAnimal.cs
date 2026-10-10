using System.Collections.Generic;
using DraftAnimals.Logic;
using UnityEngine;

namespace DraftAnimals
{
    /// <summary>
    /// Заяц (Туманные земли). Дикий ходит на ванильном ИИ животных, но спокойный замечает силок с приманкой и идёт к нему.
    /// Пойманного приручают в силке: кормят с руки, и пока он сыт и спокоен (рядом никто не стоит в полный рост — надо
    /// присесть), приручение идёт; ручного силок отпускает. Ручной пасётся, убегает от врагов, ест из кормушки и с руки,
    /// не слушается команд и не привязывается — переносят его в сумке (E — взять). Со временем грызёт деревянные
    /// постройки рядом: загоны приходится чинить или строить из камня.
    /// </summary>
    public sealed class HareAnimal : MonoBehaviour, IFoodEater
    {
        public const string PrefabName = "Hare";

        /// <summary>Сколько секунд заяц сыт после еды (как у кабана).</summary>
        public const float FedDuration = 600f;

        private static readonly List<HareAnimal> s_all = new List<HareAnimal>();
        private static readonly Dictionary<BaseAI, HareAnimal> s_byAI = new Dictionary<BaseAI, HareAnimal>();
        private static int s_itemMask;
        private static int s_pieceMask;

        private ZNetView m_nview;
        private Character m_character;
        private BaseAI m_ai;
        private AnimalAI m_animalAI;
        private Tameable m_tameable;
        private Procreation m_procreation;
        private TameAnimal m_tame;

        private bool m_vanillaRan;
        private Character m_threat;
        private float m_threatTimer;
        private float m_lastHurtTime = -100f;
        private Vector3 m_lastAttackerPos;
        private ItemDrop m_foodTarget;
        private float m_foodSearchTimer;
        private Snare m_lure;
        private float m_lureTimer;
        private float m_gnawTimer;
        private float m_settingsTimer;
        private float m_tameTimer;
        private Vector3 m_scarePos;
        private float m_scareTimer;
        private bool m_scared;

        public static List<HareAnimal> All => s_all;

        public static bool TryGet(BaseAI ai, out HareAnimal hare)
        {
            return s_byAI.TryGetValue(ai, out hare);
        }

        public ZNetView NView => m_nview;
        public TameAnimal Tame => m_tame != null ? m_tame : (m_tame = GetComponent<TameAnimal>());
        public bool Valid => m_nview != null && m_nview.IsValid();
        public bool IsOwner => Valid && m_nview.IsOwner();
        public bool IsTamed => m_character != null && m_character.IsTamed();
        public bool IsDead => m_character == null || m_character.IsDead();
        public bool IsHungry => m_tameable != null && m_tameable.IsHungry();
        public int Level => m_character != null ? m_character.GetLevel() : 1;

        public string DisplayName => m_tameable != null
            ? m_tameable.GetHoverName()
            : Localization.instance.Localize(m_character != null ? m_character.m_name : name);

        /// <summary>Дикий заяц, пойманный в силок (диких держит только силок).</summary>
        public bool InSnare => !IsTamed && Tame != null && Tame.IsTied;

        /// <summary>Приручение в силке, 0–100%.</summary>
        public int Tameness
        {
            get
            {
                float total = DraftConfig.HareTamingTime.Value;
                float left = Valid ? m_nview.GetZDO().GetFloat(ZDOVars.s_tameTimeLeft, total) : total;
                return Mathf.Clamp((int)((1f - Mathf.Clamp01(left / total)) * 100f), 0, 100);
            }
        }

        /// <summary>Имя, данное игроком (переносится вместе с зайцем в сумке).</summary>
        public string CustomName => Valid ? m_nview.GetZDO().GetString(ZDOVars.s_tamedName) : "";

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            m_character = GetComponent<Character>();
            m_ai = GetComponent<BaseAI>();
            m_animalAI = m_ai as AnimalAI;
            m_tameable = GetComponent<Tameable>();
            m_procreation = GetComponent<Procreation>();
            ApplySettings();
            if (m_character != null)
            {
                m_character.m_onDamaged += OnDamaged;
            }
            s_all.Add(this);
            if (m_ai != null)
            {
                s_byAI[m_ai] = this;
            }
            if (Valid)
            {
                m_nview.Register<long>("DA_Bag", RPC_Bag);
                m_nview.Register<float>("DA_Feed", RPC_Feed);
            }
            m_gnawTimer = Random.Range(0f, DraftConfig.HareGnawInterval.Value);
        }

        private void OnDestroy()
        {
            s_all.Remove(this);
            if (m_ai != null)
            {
                s_byAI.Remove(m_ai);
            }
        }

        /// <summary>Размножение из настроек (они могут прийти с сервера позже).</summary>
        private void ApplySettings()
        {
            if (m_tameable != null)
            {
                m_tameable.m_tamingTime = DraftConfig.HareTamingTime.Value;
            }
            if (m_procreation != null)
            {
                // Выключено — «в округе уже полно зайцев», ванильный Procreation дальше не идёт.
                m_procreation.m_maxCreatures = DraftConfig.HareBreeding.Value ? DraftConfig.MaxHaresNearby.Value : 0;
            }
        }

        private void Update()
        {
            m_settingsTimer -= Time.deltaTime;
            if (m_settingsTimer <= 0f)
            {
                m_settingsTimer = 5f;
                ApplySettings();
            }
            if (!IsOwner || IsDead)
            {
                return;
            }
            if (!IsTamed)
            {
                if (InSnare)
                {
                    UpdateTaming(Time.deltaTime);
                }
                return;
            }
            m_gnawTimer -= Time.deltaTime * (IsHungry ? 2f : 1f);
            if (m_gnawTimer <= 0f)
            {
                m_gnawTimer = DraftConfig.HareGnawInterval.Value;
                Gnaw();
            }
        }

        // ================================================================== еда

        public float FoodMultiplier(string sharedName)
        {
            if (string.IsNullOrEmpty(sharedName) || ObjectDB.instance == null)
            {
                return 0f;
            }
            foreach (KeyValuePair<string, float> food in DraftConfig.HareFoodList)
            {
                GameObject prefab = ObjectDB.instance.GetItemPrefab(food.Key);
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop != null && drop.m_itemData.m_shared.m_name == sharedName)
                {
                    return food.Value;
                }
            }
            return 0f;
        }

        /// <summary>Приманка ли это для силка (или корм для ручного зайца).</summary>
        public static bool IsFood(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null || ObjectDB.instance == null)
            {
                return false;
            }
            foreach (KeyValuePair<string, float> food in DraftConfig.HareFoodList)
            {
                GameObject prefab = ObjectDB.instance.GetItemPrefab(food.Key);
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop != null && drop.m_itemData.m_shared.m_name == item.m_shared.m_name)
                {
                    return true;
                }
            }
            return false;
        }

        public void Feed(float multiplier)
        {
            if (m_tameable == null || !IsOwner)
            {
                return;
            }
            bool wasHungry = IsHungry;
            long ticks = DraftRules.FedTimestamp(ZNet.instance.GetTime().Ticks, m_tameable.m_fedDuration, multiplier);
            m_nview.GetZDO().Set(ZDOVars.s_tameLastFeeding, ticks);
            if (wasHungry)
            {
                m_tameable.m_sootheEffect.Create(m_character.GetCenterPoint(), Quaternion.identity);
            }
        }

        /// <summary>Покормить с руки ручного зайца или пойманного в силок (так его приручают).</summary>
        public bool TryFeed(Humanoid user, ItemDrop.ItemData item)
        {
            if (!IsTamed && !InSnare)
            {
                user.Message(MessageHud.MessageType.Center, "$da_msg_hare_wild");
                return true;
            }
            if (!IsHungry)
            {
                user.Message(MessageHud.MessageType.Center, DisplayName + " $da_msg_not_hungry");
                return true;
            }
            m_nview.InvokeRPC("DA_Feed", FoodMultiplier(item.m_shared.m_name));
            user.GetInventory().RemoveOneItem(item);
            user.Message(MessageHud.MessageType.Center, DisplayName + " $da_msg_eats");
            return true;
        }

        private void RPC_Feed(long sender, float multiplier)
        {
            Feed(multiplier);
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
                ZNetView view = drop != null ? drop.GetComponent<ZNetView>() : null;
                if (view == null || !view.IsValid() || !IsFood(drop.m_itemData))
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

        /// <summary>Голодный ручной заяц ест из кормушки, а нет её — с земли. true — занят едой.</summary>
        private bool UpdateConsume(float dt)
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
                m_foodTarget = FindFood(8f);
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
            if (AiAccess.MoveTo(m_ai, dt, point, 0.8f, false))
            {
                AiAccess.LookAt(m_ai, point);
                if (m_ai.IsLookingAt(point, 40f))
                {
                    float mult = FoodMultiplier(m_foodTarget.m_itemData.m_shared.m_name);
                    if (m_foodTarget.RemoveOne())
                    {
                        Feed(mult);
                    }
                    m_foodTarget = null;
                }
            }
            return true;
        }

        // ================================================================== ИИ

        /// <summary>Дикого ведёт ванильный ИИ (и после него — силок), пойманного и ручного — свой.</summary>
        public bool PreUpdateAI(float dt, ref bool result)
        {
            m_vanillaRan = false;
            bool snared = InSnare;
            if (!IsTamed && !snared)
            {
                m_vanillaRan = true;
                return true;
            }
            result = AiAccess.BaseUpdateAI(m_ai, dt);
            if (result)
            {
                if (snared)
                {
                    SnareBrain(dt);
                }
                else
                {
                    Brain(dt);
                }
            }
            return false;
        }

        // ================================================================== приручение в силке

        /// <summary>
        /// Что пугает пойманного зайца: игрок в полный рост ближе ScareRange (присевший — нет) или враг ближе FleeRange.
        /// Возвращает, есть ли такой, и откуда он.
        /// </summary>
        public bool FindScare(out Vector3 from)
        {
            from = transform.position;
            float range = DraftConfig.HareScareRange.Value;
            float best = float.MaxValue;
            bool found = false;
            foreach (Player p in Player.GetAllPlayers())
            {
                if (p == null || p.IsDead() || p.IsCrouching())
                {
                    continue;
                }
                float d = Vector3.Distance(p.transform.position, transform.position);
                if (d < range && d < best)
                {
                    best = d;
                    from = p.transform.position;
                    found = true;
                }
            }
            Character enemy = FindThreat(DraftConfig.HareFleeRange.Value);
            if (enemy != null && Vector3.Distance(enemy.transform.position, transform.position) < best)
            {
                from = enemy.transform.position;
                found = true;
            }
            return found;
        }

        /// <summary>Пойманный: напуган — бьётся в петле, рвётся прочь; спокоен — топчется у приманки.</summary>
        private void SnareBrain(float dt)
        {
            m_scareTimer -= dt;
            if (m_scareTimer <= 0f)
            {
                m_scareTimer = 0.5f;
                m_scared = FindScare(out m_scarePos);
            }
            if (m_scared || Time.time - m_lastHurtTime < 3f)
            {
                AiAccess.SetAlerted(m_ai, true);
                AiAccess.Flee(m_ai, dt, Time.time - m_lastHurtTime < 3f ? m_lastAttackerPos : m_scarePos);
                return;
            }
            if (m_ai.IsAlerted())
            {
                AiAccess.SetAlerted(m_ai, false);
            }
            AiAccess.IdleMovement(m_ai, dt);
        }

        /// <summary>Пока пойманный сыт и спокоен, приручение идёт; приручен — силок его отпускает.</summary>
        private void UpdateTaming(float dt)
        {
            m_tameTimer -= dt;
            if (m_tameTimer > 0f || m_tameable == null)
            {
                return;
            }
            m_tameTimer = 3f;
            if (IsHungry || FindScare(out _))
            {
                return;
            }
            float total = DraftConfig.HareTamingTime.Value;
            float left = m_nview.GetZDO().GetFloat(ZDOVars.s_tameTimeLeft, total);
            left = DraftRules.TamingStep(left, 3f, TamingBoost());
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

        private void TameNow()
        {
            m_character.SetTamed(true);
            AiAccess.SetAlerted(m_ai, false);
            Tame?.Untie();
            m_ai.SetPatrolPoint();
            m_tameable?.m_tamedEffect.Create(transform.position, transform.rotation);
            Player closest = Player.GetClosestPlayer(transform.position, 30f);
            if (closest != null)
            {
                closest.Message(MessageHud.MessageType.Center, m_character.m_name + " $hud_tamedone");
            }
        }

        /// <summary>После ванильного ИИ: спокойный дикий заяц идёт к ближайшему силку с приманкой.</summary>
        public void PostUpdateAI(float dt)
        {
            if (!m_vanillaRan || !IsOwner || IsTamed)
            {
                return;
            }
            if (m_ai.IsAlerted() || AiAccess.AnimalTarget(m_animalAI) != null || (Tame != null && Tame.IsTied))
            {
                m_lure = null;
                return;
            }
            m_lureTimer -= dt;
            if (m_lureTimer <= 0f)
            {
                m_lureTimer = 1f;
                m_lure = Snare.FindBaited(transform.position, DraftConfig.HareLureRange.Value);
            }
            if (m_lure != null && m_lure.IsBaited)
            {
                AiAccess.MoveTo(m_ai, dt, m_lure.LoopPoint, 0.2f, false);
            }
        }

        private void Brain(float dt)
        {
            m_threatTimer -= dt;
            if (m_threatTimer <= 0f)
            {
                m_threatTimer = 0.5f;
                m_threat = FindThreat(DraftConfig.HareFleeRange.Value);
            }
            if (m_threat != null && m_threat.IsDead())
            {
                m_threat = null;
            }
            if (Time.time - m_lastHurtTime < 3f)
            {
                AiAccess.SetAlerted(m_ai, true);
                AiAccess.Flee(m_ai, dt, m_lastAttackerPos);
                return;
            }
            if (m_threat != null)
            {
                AiAccess.SetAlerted(m_ai, true);
                AiAccess.Flee(m_ai, dt, m_threat.transform.position);
                return;
            }
            if (m_ai.IsAlerted())
            {
                AiAccess.SetAlerted(m_ai, false);
            }
            if (IsHungry && UpdateConsume(dt))
            {
                return;
            }
            AiAccess.IdleMovement(m_ai, dt);
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
                if (c == m_character || c.IsDead() || c.IsPlayer() || c.GetFaction() == Character.Faction.AnimalsVeg ||
                    !BaseAI.IsEnemy(m_character, c))
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

        private void OnDamaged(float damage, Character attacker)
        {
            if (!IsTamed && !InSnare)
            {
                return;
            }
            m_lastHurtTime = Time.time;
            m_lastAttackerPos = attacker != null ? attacker.transform.position : transform.position - transform.forward * 3f;
        }

        /// <summary>Консольная «tame»: зайца без силка тоже можно приручить для проверки.</summary>
        public void TameByCommand()
        {
            if (IsOwner && !IsTamed && m_character != null)
            {
                TameNow();
            }
        }

        // ================================================================== грызёт постройки

        /// <summary>
        /// Укус ближайшей деревянной постройки в пределах досягаемости: урон — доля её полной прочности (прочная древесина —
        /// вдвое меньше, камень и металл не грызутся). Сама постройка показывает щепки и цифру урона.
        /// </summary>
        private void Gnaw()
        {
            float percent = DraftConfig.HareGnawPercent.Value;
            if (percent <= 0f)
            {
                return;
            }
            if (s_pieceMask == 0)
            {
                s_pieceMask = LayerMask.GetMask("piece", "piece_nonsolid", "vehicle", "Default");
            }
            Vector3 center = transform.position + Vector3.up * 0.3f;
            float reach = (m_character != null ? m_character.GetRadius() : 0.4f) + 0.6f;
            WearNTear best = null;
            Vector3 bestPoint = center;
            float bestDistance = float.MaxValue;
            foreach (Collider c in Physics.OverlapSphere(center, reach, s_pieceMask))
            {
                WearNTear wear = c.GetComponentInParent<WearNTear>();
                if (wear == null || GnawFactor(wear.m_materialType) <= 0f)
                {
                    continue;
                }
                Vector3 p = c.ClosestPoint(center);
                float d = Vector3.Distance(p, center);
                if (d < bestDistance)
                {
                    best = wear;
                    bestPoint = p;
                    bestDistance = d;
                }
            }
            if (best == null)
            {
                return;
            }
            var hit = new HitData();
            hit.m_damage.m_damage = best.m_health * percent / 100f * GnawFactor(best.m_materialType);
            hit.m_point = bestPoint;
            hit.m_dir = (bestPoint - center).normalized;
            best.Damage(hit);
            AiAccess.LookAt(m_ai, bestPoint);
        }

        private static float GnawFactor(WearNTear.MaterialType material)
        {
            switch (material)
            {
                case WearNTear.MaterialType.Wood:
                case WearNTear.MaterialType.Timberwood:
                    return 1f;
                case WearNTear.MaterialType.HardWood:
                    return 0.5f;
                default:
                    return 0f;
            }
        }

        // ================================================================== в сумку

        /// <summary>Взять зайца в сумку: владелец зайца убирает его из мира и отдаёт предмет «Живой заяц» игроку peer.</summary>
        public void RequestBag(long peer)
        {
            if (Valid)
            {
                m_nview.InvokeRPC("DA_Bag", peer);
            }
        }

        private void RPC_Bag(long sender, long peer)
        {
            if (!IsOwner || IsDead)
            {
                return;
            }
            LiveHare.Give(peer, Level, CustomName);
            ZNetScene.instance.Destroy(gameObject);
        }
    }
}
