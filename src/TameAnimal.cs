using System.Collections.Generic;
using DraftAnimals.Logic;
using UnityEngine;

namespace DraftAnimals
{
    /// <summary>
    /// Общее для всех приручаемых зверей (кабан, волк, олень, локс, курица, асквин…): привязь к колу и еда из кормушки.
    /// Привязь хранится в ZDO зверя (метка и место кола), поэтому зверя держит его владелец, не ища кол: каждый шаг
    /// физики зверь, ушедший дальше длины верёвки, подтягивается обратно, а блуждает он вокруг кола.
    /// </summary>
    public sealed class TameAnimal : MonoBehaviour
    {
        internal static readonly int s_tieKey = "da_tie".GetStableHashCode();
        internal static readonly int s_tiePosKey = "da_tie_pos".GetStableHashCode();

        private static readonly List<TameAnimal> s_all = new List<TameAnimal>();
        private static readonly Dictionary<BaseAI, TameAnimal> s_byAI = new Dictionary<BaseAI, TameAnimal>();

        /// <summary>С какой скоростью верёвка подтягивает зверя, привязанного издалека, м/с.</summary>
        private const float PullSpeed = 4f;

        private ZNetView m_nview;
        private Character m_character;
        private BaseAI m_ai;
        private MonsterAI m_monsterAI;
        private Tameable m_tameable;
        private Rigidbody m_body;
        private ZSyncAnimation m_zanim;
        private HarnessAnimal m_harness;
        private Procreation m_procreation;

        private Transform m_head;
        private Vector3 m_headLocal = new Vector3(0f, 1f, 0.5f);
        private bool m_tied;
        private Vector3 m_tiePos;
        private int m_turnSign;

        private float m_origRandomRange;
        private bool m_rangeReduced;
        private float m_checkTimer;
        private float m_postMissing;

        private Feeder m_feeder;
        private string m_feederFood;
        private float m_feederSearch;
        private float m_eatRequestTime = -100f;

        public static List<TameAnimal> All => s_all;

        public static bool TryGet(BaseAI ai, out TameAnimal animal)
        {
            return s_byAI.TryGetValue(ai, out animal);
        }

        public Character Character => m_character;
        public ZNetView NView => m_nview;
        public HarnessAnimal Harness => m_harness;

        public bool Valid => m_nview != null && m_nview.IsValid();
        public bool IsOwner => Valid && m_nview.IsOwner();
        public bool IsTamed => m_character != null && m_character.IsTamed();
        public bool IsDead => m_character == null || m_character.IsDead();
        public bool IsHungry => m_tameable != null && m_tameable.IsHungry();

        public long TieId => Valid ? m_nview.GetZDO().GetLong(s_tieKey) : 0L;
        public bool IsTied => TieId != 0L;
        public Vector3 TiePos => Valid ? m_nview.GetZDO().GetVec3(s_tiePosKey, transform.position) : transform.position;

        /// <summary>Зверь может бегать только в этом радиусе от кола.</summary>
        public static float TetherRadius => DraftConfig.TetherLength.Value;

        public float Radius => m_character != null ? m_character.GetRadius() : 0.5f;

        public string DisplayName => m_tameable != null
            ? m_tameable.GetHoverName()
            : Localization.instance.Localize(m_character != null ? m_character.m_name : name);

        /// <summary>
        /// Куда привязана верёвка: недоуздок — кость головы (двигается с анимацией: олень пасётся, голова опущена —
        /// и верёвка за ней). Нет такой кости — точка глаз из префаба.
        /// </summary>
        public Vector3 RopePoint
        {
            get
            {
                if (m_head != null)
                {
                    return m_head.position;
                }
                if (m_character != null && m_character.m_eye != null)
                {
                    return m_character.m_eye.position - Vector3.up * 0.15f;
                }
                return m_character != null ? m_character.GetCenterPoint() : transform.position;
            }
        }

        /// <summary>
        /// Длина верёвки до головы: натянута, когда зверь стоит у края круга мордой наружу
        /// (голова впереди корпуса и на своей высоте, кольцо кола — на высоте ringHeight).
        /// </summary>
        public float RopeLength(float radius, float ringHeight)
        {
            float horizontal = radius + Mathf.Max(0f, m_headLocal.z);
            float vertical = ringHeight - m_headLocal.y;
            return Mathf.Sqrt(horizontal * horizontal + vertical * vertical) + 0.1f;
        }

        /// <summary>Кость головы: «Head», иначе что-нибудь с head или neck в имени.</summary>
        private static Transform FindHead(Transform root)
        {
            Transform best = null;
            int bestRank = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                int rank = n == "head" ? 3 : n.Contains("head") && !n.Contains("end") ? 2 : n.Contains("neck") && !n.Contains("end") ? 1 : 0;
                if (rank > bestRank)
                {
                    best = t;
                    bestRank = rank;
                }
            }
            return best;
        }

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            m_character = GetComponent<Character>();
            m_ai = GetComponent<BaseAI>();
            m_monsterAI = m_ai as MonsterAI;
            m_tameable = GetComponent<Tameable>();
            m_body = GetComponent<Rigidbody>();
            m_zanim = GetComponent<ZSyncAnimation>();
            m_harness = GetComponent<HarnessAnimal>();
            m_procreation = GetComponent<Procreation>();
            m_head = FindHead(transform);
            if (m_head != null)
            {
                m_headLocal = transform.InverseTransformPoint(m_head.position);
            }
            if (m_ai != null)
            {
                m_origRandomRange = m_ai.m_randomMoveRange;
                s_byAI[m_ai] = this;
            }
            s_all.Add(this);
            if (Valid)
            {
                m_nview.Register<long, Vector3>("DA_Tie", RPC_Tie);
                m_nview.Register("DA_Untie", RPC_Untie);
                m_nview.Register<string>("DA_FeederFed", RPC_FeederFed);
            }
        }

        private void OnDestroy()
        {
            s_all.Remove(this);
            if (m_ai != null)
            {
                s_byAI.Remove(m_ai);
            }
        }

        // ================================================================== привязь

        private void FixedUpdate()
        {
            if (m_body == null || !IsOwner || !IsTied)
            {
                return;
            }
            Vector3 post = TiePos;
            Vector3 pos = m_body.position;
            if (!DraftRules.TetherPull(pos.x, pos.z, post.x, post.z, TetherRadius, PullSpeed * Time.fixedDeltaTime, out float x, out float z))
            {
                return;
            }
            var outward = new Vector3(pos.x - post.x, 0f, pos.z - post.z);
            outward.Normalize();
            m_body.position = new Vector3(x, pos.y, z);
            Vector3 v = m_body.linearVelocity;
            float away = Vector3.Dot(v, outward);
            if (away > 0f)
            {
                m_body.linearVelocity = v - outward * away;
            }
        }

        private void Update()
        {
            if (!IsOwner)
            {
                m_tied = false;
                return;
            }
            // Привязь читается в ИИ каждый кадр (MoveTo, MoveTowards) — держим её под рукой.
            m_tied = IsTied;
            if (m_tied)
            {
                m_tiePos = TiePos;
            }
            m_checkTimer -= Time.deltaTime;
            if (m_checkTimer > 0f)
            {
                return;
            }
            m_checkTimer = 1f;
            bool tied = IsTied;
            SetReducedRange(tied);
            if (!tied)
            {
                m_postMissing = 0f;
                return;
            }
            // Кол сломали — через несколько секунд зверь свободен (если кол просто не загружен — ждём).
            if (TetherPost.Find(TieId, TiePos, 2f) != null)
            {
                m_postMissing = 0f;
            }
            else if (ZNetScene.instance != null && ZNetScene.instance.IsAreaReady(TiePos))
            {
                m_postMissing += 1f;
                if (m_postMissing > 5f)
                {
                    ClearTie();
                }
            }
        }

        /// <summary>Привязанный блуждает не дальше верёвки, а не упирается в неё.</summary>
        private void SetReducedRange(bool reduced)
        {
            if (m_ai == null || reduced == m_rangeReduced)
            {
                return;
            }
            m_rangeReduced = reduced;
            m_ai.m_randomMoveRange = reduced ? Mathf.Min(m_origRandomRange, TetherRadius * 0.8f) : m_origRandomRange;
        }

        /// <summary>
        /// Цель ИИ (куда идти) за пределами верёвки — на её край: зверь дойдёт до края и остановится или повернёт,
        /// а не будет шагать на месте. Касается только привязанных (у владельца).
        /// </summary>
        public void ClampTarget(ref Vector3 point)
        {
            if (!m_tied)
            {
                return;
            }
            if (DraftRules.ClampToCircle(point.x, point.z, m_tiePos.x, m_tiePos.z, TetherRadius, 0.3f, out float x, out float z))
            {
                point.x = x;
                point.z = z;
            }
        }

        /// <summary>Направление шага у края верёвки — вдоль круга, а не наружу.</summary>
        public void SteerDirection(ref Vector3 dir)
        {
            if (!m_tied)
            {
                return;
            }
            Vector3 offset = transform.position - m_tiePos;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance < 1e-3f)
            {
                return;
            }
            Vector3 outward = offset / distance;
            if (DraftRules.SteerAlongRope(dir.x, dir.z, outward.x, outward.z, distance, TetherRadius, 0.6f, ref m_turnSign,
                    out float x, out float z))
            {
                dir.x = x;
                dir.z = z;
            }
        }

        /// <summary>Дотянется ли привязанный до точки (для еды на земле); свободный — всегда.</summary>
        public bool CanReach(Vector3 point)
        {
            return !m_tied || Utils.DistanceXZ(point, m_tiePos) <= TetherRadius + Radius + 0.5f;
        }

        /// <summary>
        /// Что сейчас с потомством (для подсказки): те же условия, что проверяет ванильный Procreation раз в
        /// m_updateInterval — сыт, не напуган, рядом (m_partnerCheckRange) есть готовая пара, в округе
        /// (m_totalCheckRange) своих с детёнышами меньше m_maxCreatures; тогда копится симпатия до m_requiredLovePoints,
        /// потом беременность. null — зверь не размножается (дикий или нет Procreation).
        /// </summary>
        public string BreedingStatus()
        {
            Procreation procreation = m_procreation;
            if (procreation == null || procreation.m_maxCreatures <= 0 || !Valid || !IsTamed || ZNetScene.instance == null)
            {
                return null;
            }
            ZDO zdo = m_nview.GetZDO();
            if (zdo.GetLong(ZDOVars.s_pregnant, 0L) != 0L)
            {
                return "$da_breed_pregnant";
            }
            if (IsHungry)
            {
                return "$da_breed_hungry";
            }
            if (m_ai != null && m_ai.IsAlerted())
            {
                return "$da_breed_scared";
            }
            GameObject self = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
            if (self == null)
            {
                return null;
            }
            GameObject young = procreation.m_offspring != null ? ZNetScene.instance.GetPrefab(Utils.GetPrefabName(procreation.m_offspring)) : null;
            Vector3 p = transform.position;
            int crowd = SpawnSystem.GetNrOfInstances(self, p, procreation.m_totalCheckRange) +
                        (young != null ? SpawnSystem.GetNrOfInstances(young, p, procreation.m_totalCheckRange) : 0);
            if (crowd >= procreation.m_maxCreatures)
            {
                return "$da_breed_crowded (" + crowd + "/" + procreation.m_maxCreatures + ", " +
                       Mathf.RoundToInt(procreation.m_totalCheckRange) + " $da_unit_m)";
            }
            GameObject partner = procreation.m_seperatePartner != null ? procreation.m_seperatePartner : self;
            int partners = SpawnSystem.GetNrOfInstances(partner, p, procreation.m_partnerCheckRange, false, true);
            bool havePartner = procreation.m_noPartnerOffspring != null ||
                               (procreation.m_seperatePartner != null ? partners >= 1 : partners >= 2);
            if (!havePartner)
            {
                return "$da_breed_nopartner " + procreation.m_partnerCheckRange.ToString("0.#") + " $da_unit_m";
            }
            return "$da_breed_love " + procreation.GetLovePoints() + "/" + procreation.m_requiredLovePoints;
        }

        /// <summary>Ближайший ручной зверь, которого можно привязать: не привязан, не в тележке, без наездника.</summary>
        public static TameAnimal FindCandidate(Vector3 point, float range)
        {
            TameAnimal best = null;
            float bestDistance = range;
            foreach (TameAnimal a in s_all)
            {
                if (a == null || !a.Valid || a.IsDead || !a.IsTamed || a.IsTied)
                {
                    continue;
                }
                if (a.m_harness != null && a.m_harness.IsHitched)
                {
                    continue;
                }
                if (a.m_tameable != null && a.m_tameable.HaveRider())
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

        /// <summary>Привязать (метку и место кола записывает владелец зверя).</summary>
        public void Tie(long id, Vector3 postPosition)
        {
            if (Valid)
            {
                m_nview.InvokeRPC("DA_Tie", id, postPosition);
            }
        }

        public void Untie()
        {
            if (Valid)
            {
                m_nview.InvokeRPC("DA_Untie");
            }
        }

        private void RPC_Tie(long sender, long id, Vector3 postPosition)
        {
            if (!IsOwner)
            {
                return;
            }
            m_nview.GetZDO().Set(s_tieKey, id);
            m_nview.GetZDO().Set(s_tiePosKey, postPosition);
            m_tied = true;
            m_tiePos = postPosition;
            // Привязанный никуда не идёт за хозяином и пасётся вокруг кола.
            StopFollowing();
            if (m_ai != null)
            {
                AiAccess.SetPatrolPoint(m_ai, postPosition);
            }
            SetReducedRange(true);
            m_postMissing = 0f;
        }

        private void RPC_Untie(long sender)
        {
            ClearTie();
        }

        private void ClearTie()
        {
            if (!IsOwner || !IsTied)
            {
                return;
            }
            m_nview.GetZDO().Set(s_tieKey, 0L);
            m_tied = false;
            SetReducedRange(false);
            m_ai?.SetPatrolPoint();
        }

        private void StopFollowing()
        {
            if (m_harness != null)
            {
                m_harness.StopFollowing();
                return;
            }
            if (m_monsterAI != null && m_monsterAI.GetFollowTarget() != null)
            {
                m_monsterAI.SetFollowTarget(null);
                m_nview.GetZDO().Set(ZDOVars.s_follow, "");
            }
        }

        // ================================================================== кормушка

        /// <summary>Ест ли зверь этот предмет (для оленя — корм из настроек, для остальных — ванильный список еды).</summary>
        public bool CanEat(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null)
            {
                return false;
            }
            if (m_harness != null)
            {
                return m_harness.FoodMultiplier(item.m_shared.m_name) > 0f;
            }
            if (m_monsterAI != null && m_monsterAI.m_consumeItems != null)
            {
                foreach (ItemDrop drop in m_monsterAI.m_consumeItems)
                {
                    if (drop != null && drop.m_itemData.m_shared.m_name == item.m_shared.m_name)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Голодный зверь идёт к кормушке со своей едой и ест (вызывается из ИИ владельца вместо поиска еды на земле).
        /// true — зверь занят кормушкой.
        /// </summary>
        public bool UpdateFeeder(float dt)
        {
            if (m_ai == null || !IsHungry || (!IsTamed && !DraftConfig.FeedWildAnimals.Value) ||
                (m_harness != null && m_harness.IsHitched))
            {
                m_feeder = null;
                return false;
            }
            m_feederSearch -= dt;
            if (m_feederSearch <= 0f)
            {
                m_feederSearch = 3f;
                m_feeder = Feeder.FindFor(this, out m_feederFood);
            }
            if (m_feeder == null || !m_feeder.Contains(m_feederFood))
            {
                m_feeder = null;
                return false;
            }
            // Привязанный подходит к кормушке со стороны кола, иначе упирался бы в верёвку.
            Vector3 from = IsTied ? TiePos : transform.position;
            Vector3 eatPoint = m_feeder.EatPoint(from, Radius + 0.15f);
            if (AiAccess.MoveTo(m_ai, dt, eatPoint, 0.4f, false))
            {
                Vector3 center = m_feeder.Center;
                AiAccess.LookAt(m_ai, center);
                if (m_ai.IsLookingAt(center, 60f) && Time.time - m_eatRequestTime > 4f)
                {
                    m_eatRequestTime = Time.time;
                    m_feeder.RequestEat(m_nview.GetZDO().m_uid, m_feederFood);
                }
            }
            return true;
        }

        /// <summary>Владелец кормушки убрал из неё еду — зверь сыт (у владельца зверя).</summary>
        private void RPC_FeederFed(long sender, string sharedName)
        {
            if (!IsOwner)
            {
                return;
            }
            m_feeder = null;
            if (m_harness != null)
            {
                m_harness.Feed(m_harness.FoodMultiplier(sharedName));
            }
            else
            {
                bool wasHungry = IsHungry;
                m_nview.GetZDO().Set(ZDOVars.s_tameLastFeeding, ZNet.instance.GetTime().Ticks);
                if (wasHungry && m_tameable != null)
                {
                    m_tameable.m_sootheEffect.Create(m_character.GetCenterPoint(), Quaternion.identity);
                }
                if (m_character is Humanoid humanoid)
                {
                    humanoid.m_consumeItemEffects.Create(transform.position, Quaternion.identity);
                }
            }
            if (m_monsterAI != null && m_zanim != null)
            {
                m_zanim.SetTrigger("consume");
            }
        }
    }
}
