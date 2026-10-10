using DraftAnimals.Visuals;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace DraftAnimals
{
    /// <summary>
    /// Дополняет ванильные префабы (при каждом ZNetScene.Awake, до появления объектов в мире; повторно — без изменений):
    /// кабану, оленю и волку — <see cref="HarnessAnimal"/>, оленю ещё и Tameable (у кабана берутся эффекты сердечек),
    /// зайцу — Tameable, размножение и <see cref="HareAnimal"/>, всем приручаемым — <see cref="TameAnimal"/>
    /// (привязь и кормушка; зайцу — только кормушка), тележке — <see cref="CartHitch"/> и козлы.
    /// </summary>
    internal static class PrefabSetup
    {
        private const string CartPrefab = "Cart";

        /// <summary>Козлы на передней стенке тележки (координаты корня тележки).</summary>
        private static readonly Vector3 SeatPosition = new Vector3(0f, 0.98f, 0.93f);

        public static void Apply(ZNetScene scene)
        {
            if (scene == null)
            {
                return;
            }
            GameObject boar = scene.GetPrefab("Boar");
            Tameable boarTameable = boar != null ? boar.GetComponent<Tameable>() : null;
            foreach (SpeciesInfo info in SpeciesInfo.All)
            {
                GameObject prefab = scene.GetPrefab(info.Prefab);
                if (prefab == null)
                {
                    Logger.LogWarning($"Draft Animals: префаб '{info.Prefab}' не найден");
                    continue;
                }
                if (info.Species == Species.Deer && prefab.GetComponent<Tameable>() == null)
                {
                    AddDeerTameable(prefab, boarTameable);
                }
                if (info.Species == Species.Deer && prefab.GetComponent<Procreation>() == null)
                {
                    AddDeerProcreation(prefab, boar != null ? boar.GetComponent<Procreation>() : null);
                }
                HarnessAnimal animal = prefab.GetComponent<HarnessAnimal>();
                if (animal == null)
                {
                    animal = prefab.AddComponent<HarnessAnimal>();
                }
                animal.m_species = info.Species;
            }
            Procreation boarProcreation = boar != null ? boar.GetComponent<Procreation>() : null;
            GameObject hare = scene.GetPrefab(HareAnimal.PrefabName);
            if (hare == null || hare.GetComponent<Character>() == null)
            {
                Logger.LogWarning($"Draft Animals: префаб '{HareAnimal.PrefabName}' не найден — зайцы не приручаются");
            }
            else if (hare.GetComponent<HareAnimal>() == null)
            {
                SetupHare(hare, boarTameable, boarProcreation);
            }

            // Привязь и кормушка — для всех приручаемых зверей игры.
            foreach (GameObject prefab in scene.m_prefabs)
            {
                if (prefab != null && prefab.GetComponent<TameAnimal>() == null && prefab.GetComponent<Tameable>() != null &&
                    prefab.GetComponent<Character>() != null && prefab.GetComponent<BaseAI>() != null)
                {
                    prefab.AddComponent<TameAnimal>();
                }
            }
            TameAnimal hareTame = hare != null ? hare.GetComponent<TameAnimal>() : null;
            if (hareTame != null)
            {
                hareTame.m_tetherable = false;
            }

            GameObject cart = scene.GetPrefab(CartPrefab);
            if (cart == null || cart.GetComponent<Vagon>() == null)
            {
                Logger.LogWarning("Draft Animals: тележка не найдена — запрягать будет не во что");
                return;
            }
            if (cart.GetComponent<CartHitch>() == null)
            {
                AddSeat(cart);
                cart.AddComponent<CartHitch>();
            }
        }

        /// <summary>
        /// Олень в игре не приручается: у него нет Tameable. Добавляем его ради сытости, имени, подсказки «приручение N%»
        /// и команд; само приручение ведёт <see cref="HarnessAnimal"/> (ванильное требует MonsterAI, а у оленя AnimalAI).
        /// </summary>
        private static void AddDeerTameable(GameObject deer, Tameable sample)
        {
            Tameable tameable = deer.AddComponent<Tameable>();
            tameable.m_fedDuration = DraftConfig.DeerFedDuration.Value;
            tameable.m_tamingTime = DraftConfig.DeerTamingTime.Value;
            tameable.m_commandable = false;
            if (sample != null)
            {
                tameable.m_tamedEffect = sample.m_tamedEffect;
                tameable.m_sootheEffect = sample.m_sootheEffect;
                tameable.m_petEffect = sample.m_petEffect;
            }
        }

        /// <summary>
        /// Размножение оленей — ванильный Procreation с числами кабана (пара ближе 4 м: олени крупнее), детёныш —
        /// оленёнок (<see cref="Young"/>). Сердечки и рождение — те же эффекты, что у кабана. Выключенное в настройках —
        /// m_maxCreatures = 0 (ставит <see cref="HarnessAnimal"/>).
        /// </summary>
        private static void AddDeerProcreation(GameObject deer, Procreation sample)
        {
            GameObject fawn = Young.Fawn != null ? Young.Fawn : Jotunn.Managers.PrefabManager.Instance.GetPrefab(Young.FawnPrefab);
            if (fawn == null)
            {
                Logger.LogWarning("Draft Animals: оленёнка нет — олени размножаться не будут");
                return;
            }
            Procreation procreation = deer.AddComponent<Procreation>();
            procreation.m_updateInterval = 30f;
            procreation.m_totalCheckRange = 10f;
            procreation.m_maxCreatures = DraftConfig.DeerBreeding.Value ? DraftConfig.MaxDeerNearby.Value : 0;
            procreation.m_partnerCheckRange = 4f;
            procreation.m_pregnancyChance = 0.33f;
            procreation.m_pregnancyDuration = 60f;
            procreation.m_requiredLovePoints = 3;
            procreation.m_offspring = fawn;
            procreation.m_spawnOffset = 1.5f;
            if (sample != null)
            {
                procreation.m_birthEffects = sample.m_birthEffects;
                procreation.m_loveEffects = sample.m_loveEffects;
            }
        }

        /// <summary>
        /// Заяц: Tameable ради сытости, имени и сердечек (эффекты кабана; само приручение — силком), размножение быстрее
        /// кабаньего (симпатии нужно меньше, пара ближе 3 м, тесно — от MaxHaresNearby в 10 м), детёныш — зайчонок
        /// (<see cref="Young"/>). Числа из настроек обновляет <see cref="HareAnimal"/>.
        /// </summary>
        private static void SetupHare(GameObject hare, Tameable tameableSample, Procreation procreationSample)
        {
            Tameable tameable = hare.GetComponent<Tameable>();
            if (tameable == null)
            {
                tameable = hare.AddComponent<Tameable>();
                tameable.m_fedDuration = HareAnimal.FedDuration;
                tameable.m_tamingTime = 600f;
                tameable.m_commandable = false;
                if (tameableSample != null)
                {
                    tameable.m_tamedEffect = tameableSample.m_tamedEffect;
                    tameable.m_sootheEffect = tameableSample.m_sootheEffect;
                    tameable.m_petEffect = tameableSample.m_petEffect;
                }
            }
            GameObject kit = Young.HareKit != null ? Young.HareKit : Jotunn.Managers.PrefabManager.Instance.GetPrefab(Young.HareKitPrefab);
            if (kit == null)
            {
                Logger.LogWarning("Draft Animals: зайчонка нет — зайцы размножаться не будут");
            }
            else if (hare.GetComponent<Procreation>() == null)
            {
                Procreation procreation = hare.AddComponent<Procreation>();
                procreation.m_updateInterval = 30f;
                procreation.m_totalCheckRange = 10f;
                procreation.m_maxCreatures = DraftConfig.HareBreeding.Value ? DraftConfig.MaxHaresNearby.Value : 0;
                procreation.m_partnerCheckRange = 3f;
                procreation.m_pregnancyChance = 0.33f;
                procreation.m_pregnancyDuration = 60f;
                procreation.m_requiredLovePoints = 2;
                procreation.m_offspring = kit;
                procreation.m_spawnOffset = 0.6f;
                if (procreationSample != null)
                {
                    procreation.m_birthEffects = procreationSample.m_birthEffects;
                    procreation.m_loveEffects = procreationSample.m_loveEffects;
                }
            }
            hare.AddComponent<HareAnimal>();
        }

        private static void AddSeat(GameObject cart)
        {
            Vagon vagon = cart.GetComponent<Vagon>();
            int layer = LayerMask.NameToLayer("vehicle");
            if (layer < 0)
            {
                layer = vagon.m_container != null ? vagon.m_container.gameObject.layer : cart.layer;
            }
            var seat = new GameObject("da_seat");
            seat.layer = layer;
            seat.transform.SetParent(cart.transform, false);
            seat.transform.localPosition = SeatPosition;
            seat.AddComponent<MeshFilter>().sharedMesh = Art.BenchMesh();
            seat.AddComponent<MeshRenderer>().sharedMaterial = Art.Wood != null ? Art.Wood : CartMaterial(cart);
            BoxCollider box = seat.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.05f, 0f);
            box.size = new Vector3(1.32f, 0.2f, 0.32f);
            var attach = new GameObject("attach");
            attach.transform.SetParent(seat.transform, false);
            attach.transform.localPosition = CartSeat.AttachLocal;
            seat.AddComponent<CartSeat>().m_attachPoint = attach.transform;
        }

        /// <summary>Дерево самой тележки — для доски козел.</summary>
        private static Material CartMaterial(GameObject cart)
        {
            foreach (MeshRenderer r in cart.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.sharedMaterial != null)
                {
                    return r.sharedMaterial;
                }
            }
            return null;
        }
    }
}
