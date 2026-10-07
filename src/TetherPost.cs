using System.Collections.Generic;
using DraftAnimals.Logic;
using DraftAnimals.Visuals;
using UnityEngine;

namespace DraftAnimals
{
    /// <summary>
    /// Привязь — кол с железным кольцом. E: привязать ближайшего ручного зверя (он ходит не дальше длины верёвки от кола)
    /// или отвязать. На кол — один зверь. Метка привязи лежит в ZDO кола и зверя; верёвку рисуют все клиенты.
    /// </summary>
    public sealed class TetherPost : MonoBehaviour, Hoverable, Interactable
    {
        private static readonly List<TetherPost> s_all = new List<TetherPost>();

        private ZNetView m_nview;
        private LineRenderer m_rope;
        private TameAnimal m_animal;
        private float m_searchTimer;
        private float m_missingTime;
        private float m_hoverTimer;
        private TameAnimal m_hoverCandidate;
        private readonly Vector3[] m_points = new Vector3[RopePoints];

        private const int RopePoints = 16;

        public bool Valid => m_nview != null && m_nview.IsValid();
        public long TieId => Valid ? m_nview.GetZDO().GetLong(TameAnimal.s_tieKey) : 0L;
        public bool IsTied => TieId != 0L;

        /// <summary>Кол с этой меткой привязи рядом с точкой.</summary>
        public static TetherPost Find(long id, Vector3 near, float range)
        {
            if (id == 0L)
            {
                return null;
            }
            foreach (TetherPost post in s_all)
            {
                if (post != null && post.TieId == id && Vector3.Distance(post.transform.position, near) < range)
                {
                    return post;
                }
            }
            return null;
        }

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            m_rope = GetComponent<LineRenderer>();
            if (m_rope != null)
            {
                m_rope.positionCount = 0;
                m_rope.enabled = false;
            }
            s_all.Add(this);
            if (Valid)
            {
                m_nview.Register<long>("DA_SetTie", RPC_SetTie);
            }
        }

        private void OnDestroy()
        {
            s_all.Remove(this);
        }

        private void Update()
        {
            if (!Valid)
            {
                return;
            }
            long id = TieId;
            if (id == 0L)
            {
                m_animal = null;
                m_missingTime = 0f;
                return;
            }
            if (m_animal == null || m_animal.TieId != id)
            {
                m_animal = null;
                m_searchTimer -= Time.deltaTime;
                if (m_searchTimer <= 0f)
                {
                    m_searchTimer = 0.5f;
                    m_animal = FindAnimal(id);
                }
            }
            if (m_animal != null && !m_animal.IsDead)
            {
                m_missingTime = 0f;
                return;
            }
            // Зверь погиб или его уже нет рядом — освобождаем кол (у владельца кола).
            if (m_nview.IsOwner() && ZNetScene.instance != null && ZNetScene.instance.IsAreaReady(transform.position))
            {
                m_missingTime += Time.deltaTime;
                if (m_missingTime > 10f || (m_animal != null && m_animal.IsDead))
                {
                    m_nview.GetZDO().Set(TameAnimal.s_tieKey, 0L);
                    m_animal = null;
                }
            }
        }

        private TameAnimal FindAnimal(long id)
        {
            float range = TameAnimal.TetherRadius + 15f;
            foreach (TameAnimal a in TameAnimal.All)
            {
                if (a != null && a.TieId == id && Vector3.Distance(a.transform.position, transform.position) < range)
                {
                    return a;
                }
            }
            return null;
        }

        /// <summary>
        /// Верёвка от кольца к голове зверя (кость головы — двигается с анимацией). Натянута, когда зверь у края круга
        /// мордой наружу; со слабиной провисает, а лишнее ложится на землю.
        /// </summary>
        private void LateUpdate()
        {
            if (m_rope == null)
            {
                return;
            }
            if (m_animal == null || !IsTied)
            {
                if (m_rope.enabled)
                {
                    m_rope.enabled = false;
                }
                return;
            }
            Vector3 end = m_animal.RopePoint;
            Vector3 ring = transform.position + Vector3.up * Art.PostRingHeight;
            Vector3 toAnimal = end - ring;
            toAnimal.y = 0f;
            Vector3 start = ring + (toAnimal.sqrMagnitude > 0.0001f ? toAnimal.normalized : transform.forward) * (Art.PostRadius + 0.01f);
            float distance = Vector3.Distance(start, end);
            float sag = DraftRules.RopeSag(m_animal.RopeLength(TameAnimal.TetherRadius, Art.PostRingHeight), distance);
            ZoneSystem zones = ZoneSystem.instance;
            for (int i = 0; i < RopePoints; i++)
            {
                float t = (float)i / (RopePoints - 1);
                Vector3 p = Vector3.Lerp(start, end, t) + Vector3.down * (sag * 4f * t * (1f - t));
                if (sag > 0f && zones != null && i > 0 && i < RopePoints - 1)
                {
                    // Слабина ложится на землю, а не уходит под неё.
                    p.y = Mathf.Max(p.y, zones.GetGroundHeight(p) + 0.03f);
                }
                m_points[i] = p;
            }
            m_rope.positionCount = RopePoints;
            m_rope.SetPositions(m_points);
            m_rope.enabled = true;
        }

        private void RPC_SetTie(long sender, long id)
        {
            if (m_nview.IsOwner())
            {
                m_nview.GetZDO().Set(TameAnimal.s_tieKey, id);
                m_missingTime = 0f;
            }
        }

        // ------------------------------------------------------------------ Hoverable / Interactable

        public string GetHoverText()
        {
            if (!Valid)
            {
                return "";
            }
            string text = "$da_post";
            if (IsTied)
            {
                if (m_animal != null)
                {
                    text += " ( " + m_animal.DisplayName + " )";
                }
                text += "\n[<color=yellow><b>$KEY_Use</b></color>] $da_untie";
                return Localization.instance.Localize(text);
            }
            m_hoverTimer -= Time.deltaTime;
            if (m_hoverTimer <= 0f)
            {
                m_hoverTimer = 0.5f;
                m_hoverCandidate = TameAnimal.FindCandidate(transform.position, DraftConfig.TieRange.Value);
            }
            text += m_hoverCandidate != null
                ? "\n[<color=yellow><b>$KEY_Use</b></color>] $da_tie: " + m_hoverCandidate.DisplayName
                : "\n<color=#888888>$da_post_no_animal</color>";
            return Localization.instance.Localize(text);
        }

        public string GetHoverName()
        {
            return Localization.instance.Localize("$da_post");
        }

        public float GetHoverOffset()
        {
            return 0f;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || !Valid || user == null)
            {
                return false;
            }
            if (IsTied)
            {
                TameAnimal tied = m_animal != null ? m_animal : FindAnimal(TieId);
                m_nview.InvokeRPC("DA_SetTie", 0L);
                if (tied != null)
                {
                    tied.Untie();
                }
                user.Message(MessageHud.MessageType.Center, (tied != null ? tied.DisplayName : "$da_post") + " $da_msg_untied");
                return true;
            }
            TameAnimal animal = TameAnimal.FindCandidate(transform.position, DraftConfig.TieRange.Value);
            if (animal == null)
            {
                user.Message(MessageHud.MessageType.Center, "$da_post_no_animal");
                return false;
            }
            long id = 0L;
            while (id == 0L)
            {
                id = ((long)Random.Range(int.MinValue, int.MaxValue) << 32) ^ (uint)Random.Range(int.MinValue, int.MaxValue);
            }
            m_nview.InvokeRPC("DA_SetTie", id);
            animal.Tie(id, transform.position);
            m_animal = animal;
            user.Message(MessageHud.MessageType.Center, animal.DisplayName + " $da_msg_tied");
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }
    }
}
