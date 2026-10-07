using System;
using System.Collections.Generic;
using DraftAnimals.Logic;

namespace DraftAnimals.Tests
{
    /// <summary>Проверки правил упряжи (dotnet run --project tests/LogicTests).</summary>
    internal static class Program
    {
        private static int s_failed;
        private static int s_passed;

        private static int Main()
        {
            Capacity();
            Load();
            Follow();
            Stamina();
            Driving();
            Taming();
            Tether();
            FeederPoint();
            Parsing();
            Console.WriteLine($"passed {s_passed}, failed {s_failed}");
            return s_failed == 0 ? 0 : 1;
        }

        private static void Capacity()
        {
            Check("без звёзд — базовая", Near(DraftRules.Capacity(900f, 1, 25f), 900f));
            Check("одна звезда +25%", Near(DraftRules.Capacity(900f, 2, 25f), 1125f));
            Check("две звезды +50%", Near(DraftRules.Capacity(900f, 3, 25f), 1350f));
            Check("уровень 0 считается как без звёзд", Near(DraftRules.Capacity(450f, 0, 25f), 450f));
        }

        private static void Load()
        {
            Check("пустая тележка — полная скорость", Near(DraftRules.LoadFactor(0f, 900f, 0.4f), 1f));
            Check("ровно грузоподъёмность — полная скорость", Near(DraftRules.LoadFactor(900f, 900f, 0.4f), 1f));
            Check("полуторный груз — середина между 1 и 0.4", Near(DraftRules.LoadFactor(1350f, 900f, 0.4f), 0.7f));
            Check("двойной груз — минимум", Near(DraftRules.LoadFactor(1800f, 900f, 0.4f), 0.4f));
            Check("тяжелее двойного — тот же минимум", Near(DraftRules.LoadFactor(5000f, 900f, 0.4f), 0.4f));
            Check("олень при двойном грузе встаёт", Near(DraftRules.LoadFactor(900f, 450f, 0f), 0f));
            Check("олень на полуторном — вполсилы", Near(DraftRules.LoadFactor(675f, 450f, 0f), 0.5f));
            Check("голод вдвое", Near(DraftRules.SpeedFactor(0f, 900f, 0.4f, true, 50f), 0.5f));
            Check("голод и перегруз перемножаются", Near(DraftRules.SpeedFactor(1350f, 900f, 0.4f, true, 50f), 0.35f));
            Check("нулевая грузоподъёмность — минимум", Near(DraftRules.LoadFactor(10f, 0f, 0.4f), 0.4f));
        }

        private static void Follow()
        {
            const float stop = 4f;
            const float run = 12f;
            Check("рядом — стоит", DraftRules.FollowGait(3f, Gait.Walk, true, stop, run) == Gait.Stop);
            Check("стоящий не трогается сразу за порогом", DraftRules.FollowGait(4.5f, Gait.Stop, true, stop, run) == Gait.Stop);
            Check("стоящий трогается, когда ведущий отошёл", DraftRules.FollowGait(5.5f, Gait.Stop, true, stop, run) == Gait.Walk);
            Check("идущий продолжает идти за порогом", DraftRules.FollowGait(4.5f, Gait.Walk, true, stop, run) == Gait.Walk);
            Check("далеко — бежит", DraftRules.FollowGait(15f, Gait.Walk, true, stop, run) == Gait.Run);
            Check("без сил не бежит", DraftRules.FollowGait(15f, Gait.Walk, false, stop, run) == Gait.Walk);
            Check("бегущий не переходит на шаг сразу", DraftRules.FollowGait(10f, Gait.Run, true, stop, run) == Gait.Run);
            Check("бегущий переходит на шаг, догнав", DraftRules.FollowGait(8f, Gait.Run, true, stop, run) == Gait.Walk);
            Check("порог бега не ближе порога остановки",
                DraftRules.FollowGait(6f, Gait.Walk, true, 4f, 5f) == Gait.Walk);
        }

        private static void Stamina()
        {
            Check("бег тратит", Near(DraftRules.StaminaStep(100f, 100f, true, 10f, 5f, 2f), 80f));
            Check("отдых восстанавливает", Near(DraftRules.StaminaStep(50f, 100f, false, 10f, 5f, 2f), 60f));
            Check("не ниже нуля", Near(DraftRules.StaminaStep(5f, 100f, true, 10f, 5f, 2f), 0f));
            Check("не выше максимума", Near(DraftRules.StaminaStep(99f, 100f, false, 10f, 5f, 2f), 100f));
            Check("на нуле — выдохся", DraftRules.Exhausted(false, 0f, 100f, 25f));
            Check("выдохшийся ещё не отдохнул", DraftRules.Exhausted(true, 20f, 100f, 25f));
            Check("выдохшийся отдохнул", !DraftRules.Exhausted(true, 30f, 100f, 25f));
            Check("бодрый с малым запасом бежит", !DraftRules.Exhausted(false, 5f, 100f, 25f));
            Check("новичок тратит полностью", Near(DraftRules.RideDrainFactor(0f), 1f));
            Check("мастер тратит вдвое меньше", Near(DraftRules.RideDrainFactor(1f), 0.5f));
        }

        private static void Driving()
        {
            Check("Shift — бег", DraftRules.ControlsToMode(0f, true, false) == DriveMode.Run);
            Check("W — шагом", DraftRules.ControlsToMode(1f, false, false) == DriveMode.Walk);
            Check("S — стоп", DraftRules.ControlsToMode(-1f, false, false) == DriveMode.Stop);
            Check("ПКМ — разворот", DraftRules.ControlsToMode(0f, false, true) == DriveMode.Turn);
            Check("ничего — без изменений", DraftRules.ControlsToMode(0f, false, false) == DriveMode.NoChange);
            Check("отпущенная W — идёт дальше", DraftRules.NextDriveMode(DriveMode.Walk, DriveMode.NoChange) == DriveMode.Walk);
            Check("отпущенный ПКМ — стоп", DraftRules.NextDriveMode(DriveMode.Turn, DriveMode.NoChange) == DriveMode.Stop);
            Check("ПКМ на бегу не останавливает", DraftRules.NextDriveMode(DriveMode.Run, DriveMode.Turn) == DriveMode.Run);
            Check("ПКМ стоя — разворот", DraftRules.NextDriveMode(DriveMode.Stop, DriveMode.Turn) == DriveMode.Turn);
            Check("S на бегу — стоп", DraftRules.NextDriveMode(DriveMode.Run, DriveMode.Stop) == DriveMode.Stop);
        }

        private static void Taming()
        {
            Check("3 с спокойствия — минус 3 с", Near(DraftRules.TamingStep(100f, 3f, 1f), 97f));
            Check("морковь x1.5", Near(DraftRules.TamingStep(100f, 3f, 1.5f), 95.5f));
            Check("не ниже нуля", Near(DraftRules.TamingStep(2f, 3f, 1f), 0f));
            Check("испуг откатывает на 10%", Near(DraftRules.ScareRollback(1000f, 2700f, 10f), 1270f));
            Check("откат не дальше начала", Near(DraftRules.ScareRollback(2600f, 2700f, 10f), 2700f));
            Check("непочатого не трогает", Near(DraftRules.ScareRollback(2700f, 2700f, 10f), 2700f));
            Check("приручено 0%", DraftRules.Tameness(2700f, 2700f) == 0);
            Check("приручено 50%", DraftRules.Tameness(1350f, 2700f) == 50);
            Check("приручено 100%", DraftRules.Tameness(0f, 2700f) == 100);
            long now = 1000L * TimeSpan.TicksPerSecond;
            Check("обычный корм — отметка «сейчас»", DraftRules.FedTimestamp(now, 600f, 1f) == now);
            Check("корм x1.5 — отметка на 300 с позже",
                DraftRules.FedTimestamp(now, 600f, 1.5f) == now + 300L * TimeSpan.TicksPerSecond);
        }

        private static void Tether()
        {
            float x;
            float z;
            Check("в пределах верёвки — не трогаем",
                !DraftRules.TetherPull(2f, 0f, 0f, 0f, 3f, 0.1f, out x, out z) && Near(x, 2f) && Near(z, 0f));
            Check("чуть за пределом — ровно на край",
                DraftRules.TetherPull(3.05f, 0f, 0f, 0f, 3f, 0.1f, out x, out z) && Near(x, 3f) && Near(z, 0f));
            Check("далеко — подтягиваем на шаг",
                DraftRules.TetherPull(0f, 6f, 0f, 0f, 3f, 0.1f, out x, out z) && Near(x, 0f) && Near(z, 5.9f));
            Check("кол не в нуле, по диагонали",
                DraftRules.TetherPull(14f, 14f, 10f, 10f, 3f, 10f, out x, out z) && Near(x, 10f + 3f / (float)Math.Sqrt(2)) &&
                Near(z, 10f + 3f / (float)Math.Sqrt(2)));
            Check("натянутая верёвка не провисает", Near(DraftRules.RopeSag(3.5f, 3.6f), 0f));
            Check("слабина — провис по длине дуги", Near(DraftRules.RopeSag(4f, 2f), (float)Math.Sqrt(1.5)));
            Check("концы вместе — провис в полверёвки", Near(DraftRules.RopeSag(4f, 0f), 2f));
            Check("провис не больше полверёвки", DraftRules.RopeSag(4f, 0.3f) <= 2f + 0.001f);

            Check("цель внутри верёвки не трогаем",
                !DraftRules.ClampToCircle(1f, 1f, 0f, 0f, 3f, 0.3f, out x, out z) && Near(x, 1f) && Near(z, 1f));
            Check("цель снаружи — на край с запасом",
                DraftRules.ClampToCircle(10f, 0f, 0f, 0f, 3f, 0.3f, out x, out z) && Near(x, 2.7f) && Near(z, 0f));

            int turn = 0;
            Check("в середине круга направление не меняется",
                !DraftRules.SteerAlongRope(1f, 0f, 1f, 0f, 1f, 3f, 0.6f, ref turn, out x, out z) && Near(x, 1f) && Near(z, 0f));
            Check("у края к колу — не меняется",
                !DraftRules.SteerAlongRope(-1f, 0f, 1f, 0f, 3f, 3f, 0.6f, ref turn, out x, out z) && Near(x, -1f));
            turn = 0;
            bool steered = DraftRules.SteerAlongRope(1f, 0f, 1f, 0f, 3f, 3f, 0.6f, ref turn, out x, out z);
            Check("у края прямо наружу — вдоль круга и чуть внутрь", steered && x < 0f && Math.Abs(z) > 0.9f && turn != 0);
            turn = 0;
            DraftRules.SteerAlongRope(0.7f, 0.7f, 1f, 0f, 3f, 3f, 0.6f, ref turn, out x, out z);
            Check("наружу и вбок — продолжает вбок", z > 0.9f && turn == -1);
            DraftRules.SteerAlongRope(0.7f, -0.7f, 1f, 0f, 3f, 3f, 0.6f, ref turn, out x, out z);
            Check("сторона меняется, только если зверь явно повернул", z < -0.9f && turn == 1);
            DraftRules.SteerAlongRope(2f, 0f, 1f, 0f, 3f, 3f, 0.6f, ref turn, out x, out z);
            Check("скорость (длина направления) сохраняется", Near((float)Math.Sqrt(x * x + z * z), 2f));
            DraftRules.SteerAlongRope(1f, 0f, 1f, 0f, 2.7f, 3f, 0.6f, ref turn, out x, out z);
            Check("на подходе к краю — частичный поворот", x > 0f && Math.Abs(z) > 0.1f);
        }

        private static void FeederPoint()
        {
            float x;
            float z;
            DraftRules.FeederEatPoint(0.2f, 3f, 0.7f, 0.3f, 0.5f, out x, out z);
            Check("спереди — встать перед длинной стороной", Near(x, 0.2f) && Near(z, 0.8f));
            DraftRules.FeederEatPoint(3f, 0f, 0.7f, 0.3f, 0.5f, out x, out z);
            Check("с торца — у торца", Near(x, 1.2f) && Near(z, 0f));
            DraftRules.FeederEatPoint(3.7f, 4.3f, 0.7f, 0.3f, 0.5f, out x, out z);
            Check("с угла — от угла по диагонали", Near(x, 0.7f + 0.3f) && Near(z, 0.3f + 0.4f));
            DraftRules.FeederEatPoint(0.1f, -0.2f, 0.7f, 0.3f, 0.5f, out x, out z);
            Check("изнутри — через ближайший край", Near(x, 0.1f) && Near(z, -0.8f));
        }

        private static void Parsing()
        {
            List<KeyValuePair<string, float>> food = DraftRules.ParseFood("Dandelion, Carrot:1.5 ,  ,Bad:x");
            Check("три вида корма", food.Count == 3);
            Check("без множителя — 1", food.Count > 0 && food[0].Key == "Dandelion" && Near(food[0].Value, 1f));
            Check("множитель читается", food.Count > 1 && food[1].Key == "Carrot" && Near(food[1].Value, 1.5f));
            Check("испорченный множитель — 1", food.Count > 2 && food[2].Key == "Bad" && Near(food[2].Value, 1f));
            Check("пустая строка — пусто", DraftRules.ParseFood("  ").Count == 0);

            List<KeyValuePair<string, int>> recipe = DraftRules.ParseRecipe("LeatherScraps:10, DeerHide:4,BronzeNails");
            Check("три ингредиента", recipe.Count == 3);
            Check("количество читается", recipe.Count > 1 && recipe[1].Key == "DeerHide" && recipe[1].Value == 4);
            Check("без количества — 1", recipe.Count > 2 && recipe[2].Value == 1);
            Check("нулевое количество отбрасывается", DraftRules.ParseRecipe("Wood:0").Count == 0);
        }

        private static bool Near(float a, float b)
        {
            return Math.Abs(a - b) < 0.001f;
        }

        private static void Check(string name, bool ok)
        {
            if (ok)
            {
                s_passed++;
                return;
            }
            s_failed++;
            Console.WriteLine("FAIL " + name);
        }
    }
}
