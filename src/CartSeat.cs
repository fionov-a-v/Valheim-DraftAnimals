using DraftAnimals.Logic;
using UnityEngine;

namespace DraftAnimals
{
    /// <summary>
    /// Козлы — доска на передней стенке тележки. Сев на них, возница правит запряжённым зверем, как наездник локсом:
    /// W — шагом в сторону взгляда, Shift — бегом, S — стоп, ПКМ — развернуться на месте; встать — прыжок или E.
    /// Управление уходит владельцу зверя (возница забирает себе и зверя, и тележку).
    /// </summary>
    public sealed class CartSeat : MonoBehaviour, Hoverable, Interactable, IDoodadController
    {
        public Transform m_attachPoint;
        public Vector3 m_detachOffset = new Vector3(1.1f, 0.4f, 0f);
        public float m_useDistance = 2.5f;

        /// <summary>Положение сидящего по умолчанию (относительно доски); подгоняется настройкой DriverSeatOffset.</summary>
        public static readonly Vector3 AttachLocal = new Vector3(0f, -0.4f, 0.05f);

        private CartHitch m_hitch;
        private DriveMode m_lastSent;
        private float m_skillTimer;
        private float m_weightTimer;
        private HarnessAnimal m_ignoredAnimal;
        private Collider m_ignoredPlayer;
        private float m_driverCheckTimer;
        private float m_otherDriverTime;

        public HarnessAnimal Animal => m_hitch != null ? m_hitch.Animal : null;
        public CartHitch Hitch => m_hitch;

        private void Awake()
        {
            m_hitch = GetComponentInParent<CartHitch>();
        }

        public bool IsLocalUser()
        {
            Player player = Player.m_localPlayer;
            return player != null && ReferenceEquals(player.GetDoodadController(), this);
        }

        private bool InUseDistance(Humanoid human)
        {
            return human != null && Vector3.Distance(human.transform.position, transform.position) < m_useDistance;
        }

        private void Update()
        {
            if (m_attachPoint != null)
            {
                m_attachPoint.localPosition = AttachLocal + DraftConfig.DriverSeatOffsetVector;
            }
            if (!IsLocalUser())
            {
                return;
            }
            Player player = Player.m_localPlayer;
            float dt = Time.deltaTime;
            if (!CheckDriverSlot(player, dt))
            {
                return;
            }
            m_skillTimer -= dt;
            if (m_skillTimer <= 0f)
            {
                m_skillTimer = 1f;
                if (m_lastSent == DriveMode.Run)
                {
                    player.RaiseSkill(Skills.SkillType.Ride);
                }
            }
            m_weightTimer -= dt;
            if (m_weightTimer <= 0f && m_hitch != null)
            {
                m_weightTimer = 1f;
                m_hitch.ReportDriverWeight(DraftConfig.DriverWeight.Value + player.GetInventory().GetTotalWeight());
            }
        }

        /// <summary>
        /// На козлах — один возница. Сидящий, у которого тележка, держит отметку на себе; если возницей записан другой
        /// игрок (ответы пришли одновременно), лишний встаёт. false — встал.
        /// </summary>
        private bool CheckDriverSlot(Player player, float dt)
        {
            if (m_hitch == null || !m_hitch.Valid)
            {
                return true;
            }
            m_driverCheckTimer -= dt;
            if (m_driverCheckTimer > 0f)
            {
                return true;
            }
            m_driverCheckTimer = 0.5f;
            long me = player.GetZDOID().UserID;
            long driver = m_hitch.DriverId;
            if (driver == me || driver == 0L)
            {
                m_otherDriverTime = 0f;
                m_hitch.ClaimDriver(me);
                return true;
            }
            m_otherDriverTime += 0.5f;
            if (m_otherDriverTime < 1.5f)
            {
                return true;
            }
            m_otherDriverTime = 0f;
            player.Message(MessageHud.MessageType.Center, "$msg_inuse");
            player.StopDoodadControl();
            return false;
        }

        /// <summary>Правит ли этот игрок: возницей записан он (или пока никто).</summary>
        private bool IsMyDrive(long me)
        {
            long driver = m_hitch != null ? m_hitch.DriverId : 0L;
            return driver == me || driver == 0L;
        }

        // ------------------------------------------------------------------ Hoverable / Interactable

        public string GetHoverText()
        {
            Player player = Player.m_localPlayer;
            if (!InUseDistance(player))
            {
                return Localization.instance.Localize("<color=#888888>$piece_toofar</color>");
            }
            string text = "$da_seat";
            if (m_hitch == null || !m_hitch.IsHitched)
            {
                text += "\n<color=#888888>$da_seat_no_animal</color>";
            }
            else if (m_hitch.HasValidDriver && !IsLocalUser())
            {
                text += "\n<color=#888888>$msg_inuse</color>";
            }
            else
            {
                text += "\n[<color=yellow><b>$KEY_Use</b></color>] $da_drive";
            }
            return Localization.instance.Localize(text);
        }

        public string GetHoverName()
        {
            return Localization.instance.Localize("$da_seat");
        }

        public float GetHoverOffset()
        {
            return 0f;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || m_hitch == null || !m_hitch.Valid)
            {
                return false;
            }
            Player player = user as Player;
            if (player == null || !InUseDistance(player))
            {
                return false;
            }
            if (IsLocalUser())
            {
                player.StopDoodadControl();
                return false;
            }
            if (!m_hitch.IsHitched)
            {
                player.Message(MessageHud.MessageType.Center, "$da_seat_no_animal");
                return false;
            }
            m_hitch.RequestDrive(player.GetZDOID().UserID);
            return false;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }

        /// <summary>Ответ владельца тележки на просьбу сесть на козлы.</summary>
        public void OnDriveResponse(bool granted)
        {
            Player player = Player.m_localPlayer;
            if (player == null || m_hitch == null)
            {
                return;
            }
            if (!granted || !m_hitch.IsHitched)
            {
                player.Message(MessageHud.MessageType.Center, "$msg_inuse");
                return;
            }
            m_hitch.NView.ClaimOwnership();
            HarnessAnimal animal = m_hitch.Animal;
            if (animal != null && animal.Valid)
            {
                animal.NView.ClaimOwnership();
            }
            m_hitch.ClaimDriver(player.GetZDOID().UserID);
            m_lastSent = DriveMode.Stop;
            m_otherDriverTime = 0f;
            m_driverCheckTimer = 0.5f;
            player.StartDoodadControl(this);
            player.AttachStart(m_attachPoint, m_hitch.gameObject, false, false, false, "attach_chair", m_detachOffset);
            IgnoreDriverAnimal(player, animal);
        }

        /// <summary>Сидящий на козлах не толкается со зверем перед собой.</summary>
        private void IgnoreDriverAnimal(Player player, HarnessAnimal animal)
        {
            RestoreDriverAnimal();
            Collider playerCollider = player != null ? player.GetComponent<CapsuleCollider>() : null;
            if (playerCollider == null || animal == null)
            {
                return;
            }
            foreach (Collider c in animal.Colliders)
            {
                if (c != null)
                {
                    Physics.IgnoreCollision(playerCollider, c, true);
                }
            }
            m_ignoredPlayer = playerCollider;
            m_ignoredAnimal = animal;
        }

        private void RestoreDriverAnimal()
        {
            if (m_ignoredPlayer != null && m_ignoredAnimal != null)
            {
                foreach (Collider c in m_ignoredAnimal.Colliders)
                {
                    if (c != null)
                    {
                        Physics.IgnoreCollision(m_ignoredPlayer, c, false);
                    }
                }
            }
            m_ignoredPlayer = null;
            m_ignoredAnimal = null;
        }

        // ------------------------------------------------------------------ IDoodadController

        public void OnUseStop(Player player)
        {
            if (m_hitch != null && m_hitch.Valid)
            {
                long me = player.GetZDOID().UserID;
                HarnessAnimal animal = m_hitch.Animal;
                // Встающий лишний не останавливает зверя настоящего возницы.
                if (animal != null && IsMyDrive(me))
                {
                    animal.SendControls(Vector3.zero, DriveMode.Stop, 0f);
                }
                m_hitch.ReleaseDrive(me);
            }
            m_lastSent = DriveMode.Stop;
            player.AttachStop();
            RestoreDriverAnimal();
        }

        public void ApplyControlls(Vector3 moveDir, Vector3 lookDir, bool run, bool autoRun, bool block)
        {
            HarnessAnimal animal = Animal;
            Player player = Player.m_localPlayer;
            if (animal == null || player == null || !IsMyDrive(player.GetZDOID().UserID))
            {
                return;
            }
            DriveMode requested = DraftRules.ControlsToMode(moveDir.z, run, block);
            Vector3 dir = Vector3.zero;
            if (block || moveDir.z > 0.5f || run)
            {
                dir = lookDir;
                dir.y = 0f;
                dir.Normalize();
            }
            float skill = player.GetSkills().GetSkillFactor(Skills.SkillType.Ride);
            animal.SendControls(dir, requested, skill);
            m_lastSent = DraftRules.NextDriveMode(m_lastSent, requested);
        }

        public Component GetControlledComponent()
        {
            return this;
        }

        public Vector3 GetPosition()
        {
            return transform.position;
        }

        public bool IsValid()
        {
            return this != null && m_hitch != null && m_hitch.IsHitched;
        }
    }
}
