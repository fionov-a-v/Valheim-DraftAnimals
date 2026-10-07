using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using DraftAnimals.Logic;
using UnityEngine;

namespace DraftAnimals
{
    /// <summary>Настройки одного вида зверя в упряжи.</summary>
    public sealed class SpeciesConfig
    {
        public ConfigEntry<bool> Enabled;
        public ConfigEntry<float> Capacity;
        public ConfigEntry<float> OverloadMinPercent;
        public ConfigEntry<float> WalkSpeed;
        public ConfigEntry<float> RunSpeed;
        public ConfigEntry<float> MaxStamina;
        public ConfigEntry<float> StaminaDrain;
        public ConfigEntry<float> StaminaRegen;
        public ConfigEntry<float> HitchDistance;
        public ConfigEntry<float> HitchHeight;
        public ConfigEntry<string> VisualOffset;
        public ConfigEntry<float> VisualScale;

        /// <summary>Точка зверя (в его координатах), которая совпадает с поперечиной оглобель повозки.</summary>
        public Vector3 HitchAnchor => new Vector3(0f, HitchHeight.Value, -HitchDistance.Value);

        public Vector3 VisualOffsetVector => DraftConfig.ParseVector(VisualOffset.Value);
    }

    /// <summary>Настройки предмета-упряжи: станок и рецепт.</summary>
    public sealed class ItemRecipeConfig
    {
        public ConfigEntry<string> Station;
        public ConfigEntry<string> Recipe;
    }

    /// <summary>
    /// Все настройки мода. Игровые параметры синхронизируются с сервера (Jötunn, IsAdminOnly),
    /// поэтому у всех игроков звери и повозки ведут себя одинаково.
    /// </summary>
    public static class DraftConfig
    {
        public static ConfigEntry<float> HitchRange;
        public static ConfigEntry<float> LeaderRange;
        public static ConfigEntry<float> HungrySpeedPercent;
        public static ConfigEntry<float> StarCapacityBonusPercent;
        public static ConfigEntry<float> DriverWeight;
        public static ConfigEntry<float> ExhaustedRecoverPercent;

        public static ConfigEntry<float> DeerTamingTime;
        public static ConfigEntry<float> DeerFedDuration;
        public static ConfigEntry<string> DeerFood;
        public static ConfigEntry<float> DeerScareRollbackPercent;
        public static ConfigEntry<float> DeerScareCooldown;
        public static ConfigEntry<float> DeerLureRange;
        public static ConfigEntry<float> DeerPanicRange;
        public static ConfigEntry<float> DeerFleeRange;
        public static ConfigEntry<float> DeerBoltTime;
        public static ConfigEntry<bool> DeerBreeding;
        public static ConfigEntry<int> MaxDeerNearby;
        public static ConfigEntry<float> FawnGrowTime;

        public static ItemRecipeConfig Harness;
        public static ItemRecipeConfig HeavyHarness;

        public static ConfigEntry<float> TetherLength;
        public static ConfigEntry<float> TieRange;
        public static ConfigEntry<float> FeederRange;
        public static ConfigEntry<bool> FeedWildAnimals;
        public static ItemRecipeConfig PostRecipe;
        public static ItemRecipeConfig FeederRecipe;

        public static ConfigEntry<bool> ShowMountHud;
        public static ConfigEntry<bool> ShowBreedingInfo;
        public static ConfigEntry<string> DriverSeatOffset;

        /// <summary>Сдвиг сидящего на козлах (разобранный <see cref="DriverSeatOffset"/>).</summary>
        public static Vector3 DriverSeatOffsetVector { get; private set; }

        private static readonly SpeciesConfig[] s_species = new SpeciesConfig[3];

        private static List<KeyValuePair<string, float>> s_deerFood;

        /// <summary>Поменялись рецепты упряжи.</summary>
        public static event Action RecipesChanged;

        /// <summary>Поменялся вид модели упряжи (сдвиг или масштаб).</summary>
        public static event Action VisualsChanged;

        public static SpeciesConfig For(Species species) => s_species[(int)species];

        /// <summary>Чем приманивают и кормят оленя: имя префаба → множитель (скорость приручения и длительность сытости).</summary>
        public static List<KeyValuePair<string, float>> DeerFoodList =>
            s_deerFood ?? (s_deerFood = DraftRules.ParseFood(DeerFood.Value));

        public static void Bind(ConfigFile cfg)
        {
            const string general = "1 - General";
            HitchRange = Synced(cfg, general, "HitchRange", 8f,
                Text("How far from the cart shafts a harnessed animal may stand to be hitched with Shift+E, m.",
                    "На каком расстоянии от оглобель может стоять зверь в упряжи, чтобы запрячь его (Shift+E), м."),
                new AcceptableValueRange<float>(2f, 30f));
            LeaderRange = Synced(cfg, general, "LeaderRange", 40f,
                Text("A hitched animal follows its leader only while the leader is closer than this, m; farther — it stops and waits.",
                    "Запряжённый зверь идёт за ведущим, только пока тот ближе этого расстояния, м; дальше — стоит и ждёт."),
                new AcceptableValueRange<float>(10f, 200f));
            HungrySpeedPercent = Synced(cfg, general, "HungrySpeedPercent", 50f,
                Text("Speed of a hungry hitched animal, % of normal.",
                    "Скорость голодного запряжённого зверя, % от обычной."),
                new AcceptableValueRange<float>(0f, 100f));
            StarCapacityBonusPercent = Synced(cfg, general, "StarCapacityBonusPercent", 25f,
                Text("Extra load capacity for each star of the animal, %.",
                    "Прибавка к грузоподъёмности за каждую звезду зверя, %."),
                new AcceptableValueRange<float>(0f, 200f));
            DriverWeight = Synced(cfg, general, "DriverWeight", 80f,
                Text("Weight of the driver on the cart bench; the driver's inventory weight is added to it.",
                    "Вес возницы на козлах; к нему прибавляется вес его инвентаря."),
                new AcceptableValueRange<float>(0f, 500f));
            ExhaustedRecoverPercent = Synced(cfg, general, "ExhaustedRecoverPercent", 25f,
                Text("An exhausted animal (stamina at zero) can run again only after recovering this much stamina, % of maximum.",
                    "Выдохшийся зверь (выносливость на нуле) снова побежит, только восстановив столько выносливости, % от максимума."),
                new AcceptableValueRange<float>(0f, 100f));

            const string deer = "2 - Deer taming";
            DeerTamingTime = Synced(cfg, deer, "TamingTime", 900f,
                Text("Seconds of calm, well-fed time needed to tame a deer (a boar needs 1800).",
                    "Сколько секунд спокойного сытого времени нужно, чтобы приручить оленя (кабану нужно 1800)."),
                new AcceptableValueRange<float>(10f, 36000f));
            DeerFedDuration = Synced(cfg, deer, "FedDuration", 600f,
                Text("How long a deer stays fed after eating, seconds (multiplied by the food multiplier).",
                    "Сколько секунд олень сыт после еды (умножается на множитель корма)."),
                new AcceptableValueRange<float>(10f, 36000f));
            DeerFood = Synced(cfg, deer, "Food", "Dandelion:1,Carrot:1.5",
                Text("What lures and feeds deer: Item:multiplier, comma-separated. The multiplier speeds up taming and makes the deer stay fed longer.",
                    "Чем приманивают и кормят оленей: Предмет:множитель через запятую. Множитель ускоряет приручение и продлевает сытость."),
                null);
            DeerScareRollbackPercent = Synced(cfg, deer, "ScareRollbackPercent", 10f,
                Text("Each time a deer being tamed gets frightened, its taming goes back by this share of the full taming time, %.",
                    "Каждый раз, когда приручаемый олень пугается, приручение откатывается на эту долю полного времени, %."),
                new AcceptableValueRange<float>(0f, 100f));
            DeerScareCooldown = Synced(cfg, deer, "ScareCooldown", 30f,
                Text("After a rollback, new scares do not roll taming back for this many seconds (one chase — one rollback).",
                    "После отката новые испуги не откатывают приручение столько секунд (одна погоня — один откат)."),
                new AcceptableValueRange<float>(0f, 600f));
            DeerLureRange = Synced(cfg, deer, "LureRange", 10f,
                Text("A calm hungry deer notices food lying on the ground from this far, m.",
                    "С какого расстояния спокойный голодный олень замечает корм на земле, м."),
                new AcceptableValueRange<float>(1f, 50f));
            DeerPanicRange = Synced(cfg, deer, "PanicRange", 10f,
                Text("A hitched deer balks (stops and refuses to go) while an enemy is closer than this, m.",
                    "Запряжённый олень упирается (встаёт и не идёт), пока враг ближе этого расстояния, м."),
                new AcceptableValueRange<float>(0f, 50f));
            DeerFleeRange = Synced(cfg, deer, "FleeRange", 15f,
                Text("A tame deer without a cart runs away from enemies closer than this, m.",
                    "Ручной олень без повозки убегает от врагов ближе этого расстояния, м."),
                new AcceptableValueRange<float>(0f, 50f));
            DeerBoltTime = Synced(cfg, deer, "BoltTime", 3f,
                Text("When hit, a hitched deer bolts away from the attacker for this many seconds, dragging the cart.",
                    "Получив удар, запряжённый олень столько секунд рвётся прочь от обидчика, волоча повозку."),
                new AcceptableValueRange<float>(0f, 20f));
            DeerBreeding = Synced(cfg, deer, "Breeding", true,
                Text("Tame deer have fawns, like boars have piglets: a fed, calm deer with a fed partner within 4 m.",
                    "Ручные олени приносят оленят, как кабаны поросят: сытый спокойный олень с сытой парой ближе 4 м."),
                null);
            MaxDeerNearby = Synced(cfg, deer, "MaxDeerNearby", 5,
                Text("Deer stop breeding when this many deer and fawns are within 10 m (boars have the same limit of 5).",
                    "Олени перестают размножаться, когда в радиусе 10 м столько оленей с оленятами (у кабанов тот же предел — 5)."),
                new AcceptableValueRange<int>(2, 50));
            FawnGrowTime = Synced(cfg, deer, "FawnGrowTime", 3000f,
                Text("Seconds until a fawn grows into a deer (a piglet takes 3000). Applies to fawns born after the change.",
                    "Через сколько секунд оленёнок вырастает в оленя (поросёнку нужно 3000). Действует на оленят, родившихся после изменения."),
                new AcceptableValueRange<float>(30f, 36000f));
            DeerFood.SettingChanged += (_, _) => s_deerFood = null;

            s_species[(int)Species.Boar] = BindSpecies(cfg, "3 - Boar", capacity: 900f, overloadMin: 40f, walk: 3f, run: 5.5f,
                stamina: 100f, drain: 5f, regen: 5f, hitchDistance: 0.85f, hitchHeight: 0.6f);
            s_species[(int)Species.Deer] = BindSpecies(cfg, "4 - Deer", capacity: 450f, overloadMin: 0f, walk: 3.5f, run: 8.5f,
                stamina: 100f, drain: 10f, regen: 8f, hitchDistance: 1.1f, hitchHeight: 0.8f);
            s_species[(int)Species.Wolf] = BindSpecies(cfg, "5 - Wolf", capacity: 1200f, overloadMin: 40f, walk: 3.5f, run: 7.5f,
                stamina: 100f, drain: 4f, regen: 6f, hitchDistance: 1f, hitchHeight: 0.7f);

            Harness = BindItem(cfg, "6 - Harness", "", "piece_workbench", "LeatherScraps:10,DeerHide:4,BronzeNails:5",
                Text("Harness (boar and deer)", "Упряжь (кабан и олень)"));
            HeavyHarness = BindItem(cfg, "7 - Heavy harness", "", "forge", "WolfPelt:4,LeatherScraps:10,Silver:2",
                Text("Heavy harness (wolf)", "Тяжёлая упряжь (волк)"));

            const string farm = "8 - Tether post and feeder";
            TetherLength = Synced(cfg, farm, "TetherLength", 3f,
                Text("Rope length of the tether post: a tied animal stays within this distance of the post, m.",
                    "Длина верёвки привязи: привязанный зверь ходит не дальше этого от кола, м."),
                new AcceptableValueRange<float>(1f, 15f));
            TieRange = Synced(cfg, farm, "TieRange", 6f,
                Text("How far from the post a tame animal may stand to be tied (E on the post), m.",
                    "На каком расстоянии от кола может стоять ручной зверь, чтобы его привязать (E на колу), м."),
                new AcceptableValueRange<float>(1f, 30f));
            FeederRange = Synced(cfg, farm, "FeederRange", 10f,
                Text("Hungry animals notice a feeder with their food from this far, m (a tied animal — only if it can reach the feeder on its rope).",
                    "С какого расстояния голодные звери замечают кормушку со своей едой, м (привязанный — если дотянется на верёвке)."),
                new AcceptableValueRange<float>(1f, 50f));
            FeedWildAnimals = Synced(cfg, farm, "FeedWildAnimals", false,
                Text("Wild animals eat from feeders too — a way to tame them without dropping food on the ground.",
                    "Дикие звери тоже едят из кормушек — так их можно приручать, не бросая еду на землю."),
                null);
            PostRecipe = BindItem(cfg, farm, "Post", "piece_workbench", "Wood:3,LeatherScraps:2", Text("Tether post", "Привязь"));
            FeederRecipe = BindItem(cfg, farm, "Feeder", "piece_workbench", "Wood:10", Text("Feeder", "Кормушка"));

            ShowMountHud = cfg.Bind("9 - Client", "ShowMountHud", true,
                Text("While driving, show the animal's health and stamina in the mount panel — for this client only.",
                    "Когда правите повозкой, показывать здоровье и выносливость зверя в панели скакуна — только для этого клиента."));
            ShowBreedingInfo = cfg.Bind("9 - Client", "ShowBreedingInfo", true,
                Text("Show a breeding line over tame animals that can have young: what stops them (hunger, no partner close enough, too crowded) or how close they are — this client only.",
                    "Показывать над ручными зверями, которые размножаются, строку о потомстве: что мешает (голод, нет пары рядом, тесно) или сколько осталось — только для этого клиента."));
            DriverSeatOffset = cfg.Bind("9 - Client", "DriverSeatOffset", "0,0,0",
                Text("Shift of the seated driver on the cart bench: x,y,z in metres (+y — up, +z — forward). For fitting the pose, this client only.",
                    "Сдвиг сидящего на козлах: x,y,z в метрах (+y — вверх, +z — вперёд). Для подгонки позы, только на этом клиенте."));
            DriverSeatOffsetVector = ParseVector(DriverSeatOffset.Value);
            DriverSeatOffset.SettingChanged += (_, _) => DriverSeatOffsetVector = ParseVector(DriverSeatOffset.Value);
        }

        private static SpeciesConfig BindSpecies(ConfigFile cfg, string section, float capacity, float overloadMin, float walk,
            float run, float stamina, float drain, float regen, float hitchDistance, float hitchHeight)
        {
            var s = new SpeciesConfig
            {
                Enabled = Synced(cfg, section, "Enabled", true,
                    Text("Whether this animal can wear a harness and pull a cart.", "Можно ли надеть на этого зверя упряжь и запрячь в повозку."),
                    null),
                Capacity = Synced(cfg, section, "Capacity", capacity,
                    Text("Load the animal pulls at full speed: cart contents plus the driver (an ore stack weighs about 300).",
                        "Груз, который зверь тянет без замедления: содержимое повозки плюс возница (стопка руды весит около 300)."),
                    new AcceptableValueRange<float>(10f, 100000f)),
                OverloadMinPercent = Synced(cfg, section, "OverloadMinPercent", overloadMin,
                    Text("Speed at double the capacity, % (it falls linearly from 100% at the capacity). 0 — the animal cannot move a double load at all.",
                        "Скорость при двойном грузе, % (падает линейно от 100% при грузоподъёмности). 0 — двойной груз зверь не сдвинет вовсе."),
                    new AcceptableValueRange<float>(0f, 100f)),
                WalkSpeed = Synced(cfg, section, "WalkSpeed", walk,
                    Text("Walking speed of the hitched animal, m/s (the player jogs at about 4, runs at about 7).",
                        "Скорость запряжённого зверя шагом, м/с (игрок бежит трусцой примерно 4, бегом — 7)."),
                    new AcceptableValueRange<float>(0.5f, 20f)),
                RunSpeed = Synced(cfg, section, "RunSpeed", run,
                    Text("Running speed of the hitched animal, m/s.", "Скорость запряжённого зверя бегом, м/с."),
                    new AcceptableValueRange<float>(0.5f, 30f)),
                MaxStamina = Synced(cfg, section, "MaxStamina", stamina,
                    Text("Stamina of the hitched animal.", "Выносливость запряжённого зверя."),
                    new AcceptableValueRange<float>(1f, 10000f)),
                StaminaDrain = Synced(cfg, section, "StaminaDrain", drain,
                    Text("Stamina spent per second of running (a driver with high Riding skill spends up to half as much).",
                        "Расход выносливости за секунду бега (у возницы с высоким навыком верховой езды — до вдвое меньше)."),
                    new AcceptableValueRange<float>(0f, 1000f)),
                StaminaRegen = Synced(cfg, section, "StaminaRegen", regen,
                    Text("Stamina recovered per second when not running (half as much when hungry).",
                        "Восстановление выносливости за секунду, когда зверь не бежит (голодный — вдвое медленнее)."),
                    new AcceptableValueRange<float>(0f, 1000f)),
                HitchDistance = Synced(cfg, section, "HitchDistance", hitchDistance,
                    Text("How far behind the animal's centre the cart crossbar is, m.",
                        "Насколько позади центра зверя находится поперечина оглобель повозки, м."),
                    new AcceptableValueRange<float>(0f, 5f)),
                HitchHeight = Synced(cfg, section, "HitchHeight", hitchHeight,
                    Text("Height of the cart crossbar above the animal's feet, m (a player holds it at 0.8).",
                        "Высота поперечины оглобель над землёй под зверем, м (игрок держит её на 0.8)."),
                    new AcceptableValueRange<float>(0f, 3f)),
                VisualOffset = cfg.Bind(section, "HarnessVisualOffset", "0,0,0",
                    Text("Shift of the harness model on the animal: x,y,z in metres (+y — up, +z — forward). Client-side, for fitting the model.",
                        "Сдвиг модели упряжи на звере: x,y,z в метрах (+y — вверх, +z — вперёд). Только внешний вид на этом клиенте.")),
                VisualScale = cfg.Bind(section, "HarnessVisualScale", 1f,
                    new ConfigDescription(Text("Scale of the harness model on the animal. Client-side.",
                        "Масштаб модели упряжи на звере. Только внешний вид на этом клиенте."), new AcceptableValueRange<float>(0.3f, 3f))),
            };
            s.Enabled.SettingChanged += (_, _) => RecipesChanged?.Invoke();
            s.VisualOffset.SettingChanged += (_, _) => VisualsChanged?.Invoke();
            s.VisualScale.SettingChanged += (_, _) => VisualsChanged?.Invoke();
            return s;
        }

        /// <summary>Станок и рецепт; prefix — приставка к именам ключей, если в разделе несколько предметов.</summary>
        private static ItemRecipeConfig BindItem(ConfigFile cfg, string section, string prefix, string station, string recipe, string name)
        {
            var item = new ItemRecipeConfig
            {
                Station = Synced(cfg, section, prefix + "CraftingStation", station,
                    Text(name + ": crafting station (piece_workbench, forge, piece_artisanstation, blackforge...; empty — crafted anywhere).",
                        name + ": станок для изготовления (piece_workbench, forge, piece_artisanstation, blackforge...; пусто — где угодно)."),
                    null),
                Recipe = Synced(cfg, section, prefix + "Recipe", recipe,
                    Text(name + ": ingredients, Item:Amount separated by commas.", name + ": ресурсы, Предмет:Кол-во через запятую."),
                    null),
            };
            item.Station.SettingChanged += (_, _) => RecipesChanged?.Invoke();
            item.Recipe.SettingChanged += (_, _) => RecipesChanged?.Invoke();
            return item;
        }

        /// <summary>«x,y,z» → вектор (ошибки — нули).</summary>
        public static Vector3 ParseVector(string text)
        {
            var v = Vector3.zero;
            if (string.IsNullOrWhiteSpace(text))
            {
                return v;
            }
            string[] parts = text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length && i < 3; i++)
            {
                if (float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                {
                    v[i] = f;
                }
            }
            return v;
        }

        /// <summary>Описание настройки на двух языках: в .cfg — строка по-английски и под ней по-русски.</summary>
        private static string Text(string en, string ru) => en + "\n" + ru;

        private static ConfigEntry<T> Synced<T>(ConfigFile cfg, string section, string key, T value, string description,
            AcceptableValueBase range)
        {
            var attributes = new ConfigurationManagerAttributes { IsAdminOnly = true };
            return cfg.Bind(section, key, value, new ConfigDescription(description, range, attributes));
        }
    }
}
