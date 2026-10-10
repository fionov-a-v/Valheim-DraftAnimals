using System.Collections.Generic;
using DraftAnimals.Visuals;
using UnityEngine;

namespace DraftAnimals
{
    /// <summary>
    /// Силок — колышек с петлёй. Заправляется приманкой (корм зайцев с панели быстрого доступа); спокойный дикий заяц
    /// приходит к ней и попадается: петля держит его на месте (привязь с очень короткой верёвкой). Пойманного приручают,
    /// кормя с руки (<see cref="HareAnimal"/>); приручённого силок отпускает. Голодный (никто не покормил) за EscapeTime
    /// перегрызает петлю и убегает. Приманка съедается при поимке.
    /// Состояние в ZDO силка: приманка, метка пойманного зайца (как у привязи) и время поимки.
    /// </summary>
    public sealed class Snare : MonoBehaviour, Hoverable, Interactable
    {
        internal static readonly int s_baitKey = "da_snare_bait".GetStableHashCode();
        internal static readonly int s_caughtKey = "da_snare_t".GetStableHashCode();

        /// <summary>Ближе этого к петле дикий заяц попадается, м.</summary>
        private const float CatchRadius = 0.8f;

        /// <summary>Петля держит пойманного: длина «верёвки», м.</summary>
        private const float LoopRadius = 0.3f;

        private static readonly List<Snare> s_all = new List<Snare>();

        public GameObject m_baitVisual;

        private ZNetView m_nview;
        private LineRenderer m_cord;
        private TameAnimal m_hare;
        private HareAnimal m_hareAnimal;
        private float m_timer;
        private float m_missingTime;
        private readonly Vector3[] m_points = new Vector3[2];

        public bool Valid => m_nview != null && m_nview.IsValid();
        public string Bait => Valid ? m_nview.GetZDO().GetString(s_baitKey) : "";
        public bool IsBaited => !string.IsNullOrEmpty(Bait);
        public long TieId => Valid ? m_nview.GetZDO().GetLong(TameAnimal.s_tieKey) : 0L;
        public bool IsCaught => TieId != 0L;

        /// <summary>Петля — точка на земле у колышка.</summary>
        public Vector3 LoopPoint => transform.TransformPoint(new Vector3(0f, 0.02f, Art.SnareLoopOffset));

        public static Snare Find(long id, Vector3 near, float range)
        {
            if (id == 0L)
            {
                return null;
            }
            foreach (Snare snare in s_all)
            {
                if (snare != null && snare.TieId == id && Vector3.Distance(snare.LoopPoint, near) < range)
                {
                    return snare;
                }
            }
            return null;
        }

        /// <summary>Ближайший силок с приманкой и без зайца.</summary>
        public static Snare FindBaited(Vector3 near, float range)
        {
            Snare best = null;
            float bestDistance = range;
            foreach (Snare snare in s_all)
            {
                if (snare == null || !snare.IsBaited || snare.IsCaught)
                {
                    continue;
                }
                float d = Vector3.Distance(snare.LoopPoint, near);
                if (d < bestDistance)
                {
                    best = snare;
                    bestDistance = d;
                }
            }
            return best;
        }

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            m_cord = GetComponent<LineRenderer>();
            if (m_cord != null)
            {
                m_cord.positionCount = 0;
                m_cord.enabled = false;
            }
            s_all.Add(this);
            if (Valid)
            {
                m_nview.Register<string>("DA_SetBait", RPC_SetBait);
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
            if (m_baitVisual != null && m_baitVisual.activeSelf != IsBaited)
            {
                m_baitVisual.SetActive(IsBaited);
            }
            long id = TieId;
            if (id != 0L && (m_hare == null || m_hare.TieId != id))
            {
                m_hare = FindHare(id);
                m_hareAnimal = m_hare != null ? m_hare.GetComponent<HareAnimal>() : null;
            }
            else if (id == 0L)
            {
                m_hare = null;
                m_hareAnimal = null;
            }
            if (!m_nview.IsOwner())
            {
                return;
            }
            m_timer -= Time.deltaTime;
            if (m_timer > 0f)
            {
                return;
            }
            m_timer = 0.5f;
            if (id != 0L)
            {
                UpdateCaught();
            }
            else if (IsBaited)
            {
                TryCatch();
            }
        }

        private static TameAnimal FindHare(long id)
        {
            foreach (TameAnimal a in TameAnimal.All)
            {
                if (a != null && a.TieId == id)
                {
                    return a;
                }
            }
            return null;
        }

        /// <summary>
        /// Пойманный: пропал или убит — силок свободен; приручен — силок его отпустил; голодным просидел слишком долго —
        /// перегрыз петлю и убежал.
        /// </summary>
        private void UpdateCaught()
        {
            if (m_hare != null && !m_hare.IsDead && m_hare.IsTamed)
            {
                ClearCatch();
                return;
            }
            if (m_hare == null || m_hare.IsDead)
            {
                if (ZNetScene.instance != null && ZNetScene.instance.IsAreaReady(transform.position))
                {
                    m_missingTime += 0.5f;
                    if (m_missingTime > 5f || (m_hare != null && m_hare.IsDead))
                    {
                        ClearCatch();
                    }
                }
                return;
            }
            m_missingTime = 0f;
            long caught = m_nview.GetZDO().GetLong(s_caughtKey, 0L);
            double held = (ZNet.instance.GetTime() - new System.DateTime(caught)).TotalSeconds;
            if (caught != 0L && held > DraftConfig.HareEscapeTime.Value && m_hare.IsHungry)
            {
                m_hare.Untie();
                ClearCatch();
            }
        }

        private void ClearCatch()
        {
            m_nview.GetZDO().Set(TameAnimal.s_tieKey, 0L);
            m_nview.GetZDO().Set(s_caughtKey, 0L);
            m_hare = null;
            m_hareAnimal = null;
            m_missingTime = 0f;
        }

        /// <summary>Дикий заяц у петли попадается: приманка съедена, петля держит его.</summary>
        private void TryCatch()
        {
            Vector3 loop = LoopPoint;
            foreach (HareAnimal hare in HareAnimal.All)
            {
                if (hare == null || !hare.Valid || hare.IsTamed || hare.IsDead || hare.Tame == null || hare.Tame.IsTied)
                {
                    continue;
                }
                if (Utils.DistanceXZ(hare.transform.position, loop) > CatchRadius)
                {
                    continue;
                }
                long id = 0L;
                while (id == 0L)
                {
                    id = ((long)Random.Range(int.MinValue, int.MaxValue) << 32) ^ (uint)Random.Range(int.MinValue, int.MaxValue);
                }
                m_nview.GetZDO().Set(TameAnimal.s_tieKey, id);
                m_nview.GetZDO().Set(s_caughtKey, ZNet.instance.GetTime().Ticks);
                m_nview.GetZDO().Set(s_baitKey, "");
                hare.Tame.Tie(id, loop, LoopRadius);
                m_hare = hare.Tame;
                return;
            }
        }

        /// <summary>Шнур от колышка к пойманному зайцу.</summary>
        private void LateUpdate()
        {
            if (m_cord == null)
            {
                return;
            }
            if (m_hare == null || !IsCaught)
            {
                if (m_cord.enabled)
                {
                    m_cord.enabled = false;
                }
                return;
            }
            m_points[0] = transform.TransformPoint(new Vector3(0f, Art.SnarePegHeight * 0.4f, 0f));
            m_points[1] = m_hare.transform.position + Vector3.up * 0.12f;
            m_cord.positionCount = 2;
            m_cord.SetPositions(m_points);
            m_cord.enabled = true;
        }

        private void RPC_SetBait(long sender, string bait)
        {
            if (m_nview.IsOwner() && !IsBaited && !IsCaught)
            {
                m_nview.GetZDO().Set(s_baitKey, bait);
            }
        }

        // ------------------------------------------------------------------ Hoverable / Interactable

        public string GetHoverText()
        {
            if (!Valid)
            {
                return "";
            }
            string text = "$da_snare";
            if (IsCaught)
            {
                text += " ( " + (m_hare != null ? m_hare.DisplayName : "$enemy_hare");
                if (m_hareAnimal != null)
                {
                    text += ", $hud_tameness " + m_hareAnimal.Tameness + "%";
                }
                text += " )\n<color=#888888>$da_hover_hare_feed</color>";
            }
            else if (IsBaited)
            {
                GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(Bait) : null;
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                text += "\n$da_snare_bait: " + (drop != null ? drop.m_itemData.m_shared.m_name : Bait);
            }
            else
            {
                text += "\n<color=#888888>$da_snare_empty</color>";
            }
            return Localization.instance.Localize(text);
        }

        public string GetHoverName()
        {
            return Localization.instance.Localize("$da_snare");
        }

        public float GetHoverOffset()
        {
            return 0f;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || !Valid || !(user is Player player))
            {
                return false;
            }
            player.Message(MessageHud.MessageType.Center,
                IsCaught ? "$da_msg_tame_first" : IsBaited ? "$da_msg_snare_waiting" : "$da_msg_snare_need_bait");
            return false;
        }

        /// <summary>Приманка — корм зайцев с панели быстрого доступа, глядя на силок; с зайцем в силке — корм ему.</summary>
        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            if (!Valid || item == null || !HareAnimal.IsFood(item))
            {
                return false;
            }
            if (IsCaught)
            {
                // Корм на силке с зайцем — зайцу с руки (в маленького зайца трудно навестись).
                if (m_hareAnimal != null)
                {
                    return m_hareAnimal.TryFeed(user, item);
                }
                user.Message(MessageHud.MessageType.Center, "$da_msg_snare_full");
                return true;
            }
            if (IsBaited)
            {
                user.Message(MessageHud.MessageType.Center, "$da_msg_snare_has_bait");
                return true;
            }
            string prefabName = item.m_dropPrefab != null ? Utils.GetPrefabName(item.m_dropPrefab) : "";
            if (string.IsNullOrEmpty(prefabName))
            {
                return false;
            }
            m_nview.InvokeRPC("DA_SetBait", prefabName);
            user.GetInventory().RemoveOneItem(item);
            user.Message(MessageHud.MessageType.Center, "$da_msg_snare_baited");
            return true;
        }
    }
}
