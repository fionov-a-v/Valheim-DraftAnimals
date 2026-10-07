using System;
using System.Collections.Generic;
using System.Globalization;

namespace DraftAnimals.Logic
{
    /// <summary>Шаг зверя, идущего за ведущим.</summary>
    public enum Gait
    {
        Stop = 0,
        Walk = 1,
        Run = 2,
    }

    /// <summary>
    /// Режим, заданный возницей (как у седла локса): стоять, шагом, бегом, развернуться на месте.
    /// NoChange — «ничего не нажато»: режим остаётся прежним (зверь продолжает идти без удержания W).
    /// </summary>
    public enum DriveMode
    {
        Stop = 0,
        Walk = 1,
        Run = 2,
        Turn = 3,
        NoChange = 4,
    }

    /// <summary>
    /// Правила мода без Unity: грузоподъёмность, замедление от груза, выносливость, шаг при следовании,
    /// управление возницы, приручение оленя, разбор строк настроек. Проверяются тестами в tests/LogicTests.
    /// </summary>
    public static class DraftRules
    {
        /// <summary>Грузоподъёмность зверя: базовая плюс бонус за каждую звезду (уровень 1 — без звёзд).</summary>
        public static float Capacity(float baseCapacity, int level, float starBonusPercent)
        {
            int stars = Math.Max(0, level - 1);
            return Math.Max(0f, baseCapacity) * (1f + Math.Max(0f, starBonusPercent) / 100f * stars);
        }

        /// <summary>
        /// Множитель скорости от груза: до грузоподъёмности — 1, дальше линейно падает до minFactor при двойном грузе
        /// (minFactor = 0 — зверь встаёт совсем), ещё тяжелее — minFactor.
        /// </summary>
        public static float LoadFactor(float load, float capacity, float minFactor)
        {
            minFactor = Clamp01(minFactor);
            if (capacity <= 0f)
            {
                return minFactor;
            }
            if (load <= capacity)
            {
                return 1f;
            }
            float over = Clamp01((load - capacity) / capacity);
            return 1f - (1f - minFactor) * over;
        }

        /// <summary>Итоговый множитель скорости запряжённого зверя: груз и голод.</summary>
        public static float SpeedFactor(float load, float capacity, float minFactor, bool hungry, float hungryPercent)
        {
            float f = LoadFactor(load, capacity, minFactor);
            if (hungry)
            {
                f *= Clamp01(hungryPercent / 100f);
            }
            return f;
        }

        /// <summary>
        /// Шаг при следовании за ведущим с запасом (чтобы не дёргаться на границах): ближе stopDistance — стоять,
        /// дальше runDistance — бежать (если есть силы), между ними — шагом. Остановившийся трогается, только когда
        /// ведущий отошёл ещё на метр; бегущий переходит на шаг, только подойдя на 3 м ближе порога бега.
        /// </summary>
        public static Gait FollowGait(float distance, Gait current, bool canRun, float stopDistance, float runDistance)
        {
            const float startMargin = 1f;
            const float runMargin = 3f;
            runDistance = Math.Max(runDistance, stopDistance + startMargin + runMargin);
            if (distance < stopDistance || (current == Gait.Stop && distance < stopDistance + startMargin))
            {
                return Gait.Stop;
            }
            bool run = distance > runDistance || (current == Gait.Run && distance > runDistance - runMargin);
            return run && canRun ? Gait.Run : Gait.Walk;
        }

        /// <summary>Выносливость через dt секунд: бег тратит drain в секунду, иначе восстанавливается regen в секунду.</summary>
        public static float StaminaStep(float stamina, float max, bool running, float drain, float regen, float dt)
        {
            float next = running ? stamina - Math.Max(0f, drain) * dt : stamina + Math.Max(0f, regen) * dt;
            return Math.Max(0f, Math.Min(max, next));
        }

        /// <summary>
        /// Выдохся ли зверь: на нуле выносливости — да, и бежать снова сможет, лишь восстановив recoverPercent от максимума.
        /// </summary>
        public static bool Exhausted(bool wasExhausted, float stamina, float max, float recoverPercent)
        {
            if (stamina <= 0.001f)
            {
                return true;
            }
            if (!wasExhausted)
            {
                return false;
            }
            return stamina < max * Clamp01(recoverPercent / 100f);
        }

        /// <summary>Расход выносливости с учётом навыка верховой езды возницы (0..1): мастер тратит вдвое меньше.</summary>
        public static float RideDrainFactor(float rideSkill)
        {
            return 1f - 0.5f * Clamp01(rideSkill);
        }

        /// <summary>Что нажал возница (как у седла): бег — Run, W — Walk, S — Stop, ПКМ — Turn, иначе NoChange.</summary>
        public static DriveMode ControlsToMode(float moveZ, bool run, bool block)
        {
            if (run)
            {
                return DriveMode.Run;
            }
            if (moveZ > 0.5f)
            {
                return DriveMode.Walk;
            }
            if (moveZ < -0.5f)
            {
                return DriveMode.Stop;
            }
            return block ? DriveMode.Turn : DriveMode.NoChange;
        }

        /// <summary>Новый режим зверя после команды возницы (логика ванильного седла).</summary>
        public static DriveMode NextDriveMode(DriveMode current, DriveMode requested)
        {
            switch (requested)
            {
                case DriveMode.NoChange:
                    // Отпустили ПКМ после разворота на месте — стоим; в движении ничего не меняется.
                    return current == DriveMode.Turn ? DriveMode.Stop : current;
                case DriveMode.Turn:
                    // На ходу ПКМ только поворачивает, не останавливая.
                    return current == DriveMode.Walk || current == DriveMode.Run ? current : DriveMode.Turn;
                default:
                    return requested;
            }
        }

        /// <summary>Сколько секунд приручения осталось после seconds спокойного сытого времени (speed — множитель корма).</summary>
        public static float TamingStep(float timeLeft, float seconds, float speed)
        {
            return Math.Max(0f, timeLeft - Math.Max(0f, seconds) * Math.Max(0f, speed));
        }

        /// <summary>
        /// Испуг откатывает приручение на rollbackPercent от полного времени (не дальше начала).
        /// Дикого, ещё не начатого — не трогает.
        /// </summary>
        public static float ScareRollback(float timeLeft, float tamingTime, float rollbackPercent)
        {
            if (timeLeft >= tamingTime)
            {
                return timeLeft;
            }
            return Math.Min(tamingTime, timeLeft + tamingTime * Math.Max(0f, rollbackPercent) / 100f);
        }

        /// <summary>Приручено, %: 0 — дикий, 100 — готово.</summary>
        public static int Tameness(float timeLeft, float tamingTime)
        {
            if (tamingTime <= 0f)
            {
                return 100;
            }
            return (int)((1f - Clamp01(timeLeft / tamingTime)) * 100f);
        }

        /// <summary>
        /// Отметка «последнее кормление» для корма с множителем: сытость считается как (сейчас − отметка) &lt; fedDuration,
        /// поэтому для корма x1,5 отметка сдвигается в будущее на половину fedDuration — сытости хватает в полтора раза дольше.
        /// </summary>
        public static long FedTimestamp(long nowTicks, float fedDurationSeconds, float multiplier)
        {
            double extra = Math.Max(0.0, multiplier - 1.0) * Math.Max(0.0, fedDurationSeconds);
            return nowTicks + (long)(extra * TimeSpan.TicksPerSecond);
        }

        /// <summary>
        /// Привязь: зверь ушёл от кола (cx, cz) дальше длины верёвки radius (по горизонтали) — тянем его обратно,
        /// но не больше чем на maxStep за шаг (привязанного издалека подтягивает плавно, а не телепортирует).
        /// false — зверь в пределах верёвки, (nx, nz) = (x, z).
        /// </summary>
        public static bool TetherPull(float x, float z, float cx, float cz, float radius, float maxStep, out float nx, out float nz)
        {
            float dx = x - cx;
            float dz = z - cz;
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            if (d <= radius || d < 1e-5f)
            {
                nx = x;
                nz = z;
                return false;
            }
            float target = Math.Max(radius, d - Math.Max(0f, maxStep));
            nx = cx + dx / d * target;
            nz = cz + dz / d * target;
            return true;
        }

        /// <summary>
        /// Провис верёвки длиной ropeLength между точками на расстоянии distance, м: прогиб параболы, у которой длина дуги
        /// равна длине верёвки (L ≈ d + 8s²/3d). Натянутая (d ≥ L) не провисает; лишнее у земли обрезает вызывающий.
        /// </summary>
        public static float RopeSag(float ropeLength, float distance)
        {
            if (distance >= ropeLength)
            {
                return 0f;
            }
            if (distance < 0.05f)
            {
                return ropeLength * 0.5f;
            }
            float sag = (float)Math.Sqrt(3.0 * distance * (ropeLength - distance) / 8.0);
            return Math.Min(sag, ropeLength * 0.5f);
        }

        /// <summary>
        /// Цель привязанного зверя за пределами верёвки переносится на её край (с запасом margin внутрь),
        /// чтобы зверь дошёл до края и остановился, а не шагал на месте, упираясь в верёвку.
        /// </summary>
        public static bool ClampToCircle(float x, float z, float cx, float cz, float radius, float margin, out float nx, out float nz)
        {
            float r = Math.Max(0f, radius - margin);
            float dx = x - cx;
            float dz = z - cz;
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            if (d <= r || d < 1e-5f)
            {
                nx = x;
                nz = z;
                return false;
            }
            nx = cx + dx / d * r;
            nz = cz + dz / d * r;
            return true;
        }

        /// <summary>
        /// Привязанный зверь у края верёвки идёт не наружу, а вдоль круга: направление (dx, dz) плавно (в полосе zone
        /// у края) поворачивается к касательной, чуть внутрь. (ox, oz) — единичный вектор от кола к зверю, distance —
        /// расстояние до кола. turnSign помнит, в какую сторону зверь пошёл вдоль круга (чтобы не дёргался туда-сюда).
        /// false — направление не менялось.
        /// </summary>
        public static bool SteerAlongRope(float dx, float dz, float ox, float oz, float distance, float radius, float zone,
            ref int turnSign, out float sx, out float sz)
        {
            sx = dx;
            sz = dz;
            float start = radius - zone;
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (distance <= start || len < 1e-4f)
            {
                return false;
            }
            float fx = dx / len;
            float fz = dz / len;
            if (fx * ox + fz * oz <= 0f)
            {
                return false;
            }
            // Касательная против часовой стрелки — Cross(up, out) = (oz, -ox).
            float tx = oz;
            float tz = -ox;
            float side = fx * tx + fz * tz;
            if (Math.Abs(side) > 0.15f)
            {
                turnSign = side > 0f ? 1 : -1;
            }
            else if (turnSign == 0)
            {
                turnSign = 1;
            }
            tx = tx * turnSign - ox * 0.15f;
            tz = tz * turnSign - oz * 0.15f;
            float tl = (float)Math.Sqrt(tx * tx + tz * tz);
            tx /= tl;
            tz /= tl;
            float w = Math.Min(1f, (distance - start) / Math.Max(zone, 1e-4f));
            float rx = fx + (tx - fx) * w;
            float rz = fz + (tz - fz) * w;
            float rl = (float)Math.Sqrt(rx * rx + rz * rz);
            if (rl < 1e-4f)
            {
                rx = tx;
                rz = tz;
                rl = 1f;
            }
            sx = rx / rl * len;
            sz = rz / rl * len;
            return true;
        }

        /// <summary>
        /// Куда встать зверю, чтобы есть из кормушки: ближайшая к (x, z) точка края прямоугольника |x| ≤ hx, |z| ≤ hz
        /// (координаты кормушки) и от неё наружу на standOff. Точка внутри кормушки выводится через ближайший край.
        /// </summary>
        public static void FeederEatPoint(float x, float z, float hx, float hz, float standOff, out float ex, out float ez)
        {
            float cx = Math.Max(-hx, Math.Min(hx, x));
            float cz = Math.Max(-hz, Math.Min(hz, z));
            float dx = x - cx;
            float dz = z - cz;
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            if (d < 1e-4f)
            {
                if (hz - Math.Abs(z) <= hx - Math.Abs(x))
                {
                    cz = z >= 0f ? hz : -hz;
                    dx = 0f;
                    dz = z >= 0f ? 1f : -1f;
                }
                else
                {
                    cx = x >= 0f ? hx : -hx;
                    dx = x >= 0f ? 1f : -1f;
                    dz = 0f;
                }
                d = 1f;
            }
            ex = cx + dx / d * standOff;
            ez = cz + dz / d * standOff;
        }

        /// <summary>«Предмет:множитель, …» (множитель можно опустить — тогда defaultMultiplier).</summary>
        public static List<KeyValuePair<string, float>> ParseFood(string text, float defaultMultiplier = 1f)
        {
            var result = new List<KeyValuePair<string, float>>();
            foreach (string part in Split(text))
            {
                string name = part;
                float mult = defaultMultiplier;
                int colon = part.IndexOf(':');
                if (colon >= 0)
                {
                    name = part.Substring(0, colon).Trim();
                    if (!float.TryParse(part.Substring(colon + 1).Trim().Replace(',', '.'), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out mult) || mult <= 0f)
                    {
                        mult = defaultMultiplier;
                    }
                }
                if (name.Length > 0)
                {
                    result.Add(new KeyValuePair<string, float>(name, mult));
                }
            }
            return result;
        }

        /// <summary>«Предмет:количество, …» (количество можно опустить — 1).</summary>
        public static List<KeyValuePair<string, int>> ParseRecipe(string text)
        {
            var result = new List<KeyValuePair<string, int>>();
            foreach (string part in Split(text))
            {
                string name = part;
                int amount = 1;
                int colon = part.IndexOf(':');
                if (colon >= 0)
                {
                    name = part.Substring(0, colon).Trim();
                    if (!int.TryParse(part.Substring(colon + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out amount))
                    {
                        amount = 1;
                    }
                }
                if (name.Length > 0 && amount > 0)
                {
                    result.Add(new KeyValuePair<string, int>(name, amount));
                }
            }
            return result;
        }

        private static IEnumerable<string> Split(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }
            foreach (string raw in text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string part = raw.Trim();
                if (part.Length > 0)
                {
                    yield return part;
                }
            }
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }
    }
}
