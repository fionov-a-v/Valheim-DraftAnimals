using UnityEngine;

namespace DraftAnimals
{
    /// <summary>Звери, которых можно запрячь.</summary>
    public enum Species
    {
        Boar = 0,
        Deer = 1,
        Wolf = 2,
    }

    /// <summary>Какая упряжь на звере (хранится в его ZDO).</summary>
    public enum HarnessKind
    {
        None = 0,
        Light = 1,
        Heavy = 2,
    }

    /// <summary>
    /// Неизменные данные о виде: префаб, нужная упряжь и размеры тела для модели упряжи (в координатах корня зверя:
    /// +Z — вперёд, начало — между ног на земле). Размеры сняты с костей префабов (позвоночник, лопатки, плечи);
    /// точнее подгоняются настройками HarnessVisualOffset / HarnessVisualScale. Модель крепится к грудной кости
    /// (<see cref="Bone"/>) и двигается вместе с телом.
    /// </summary>
    public sealed class SpeciesInfo
    {
        public Species Species;
        public string Prefab;
        public HarnessKind Harness;

        /// <summary>Кость, к которой крепится упряжь (грудной отдел позвоночника).</summary>
        public string Bone = "Spine2";

        /// <summary>
        /// Дуга над холкой (как в русской упряжи): стоит поперёк тела на ArchZ, концы — по бокам на высоте ArchBottom
        /// и на ArchHalfWidth от середины, верх — на ArchTop. К кольцам на концах идут постромки от тележки.
        /// </summary>
        public float ArchZ;
        public float ArchBottom;
        public float ArchTop;
        public float ArchHalfWidth;

        /// <summary>Подпруга: положение по Z, центр туловища по высоте и полуразмеры сечения (ширина, высота).</summary>
        public float GirthZ;
        public float BodyCenterY;
        public float BodyHalfWidth;
        public float BodyHalfHeight;

        /// <summary>Верх спины над подпругой.</summary>
        public float BackTop => BodyCenterY + BodyHalfHeight;

        private static readonly SpeciesInfo[] s_all =
        {
            new SpeciesInfo
            {
                Species = Species.Boar, Prefab = "Boar", Harness = HarnessKind.Light,
                ArchZ = 0.3f, ArchBottom = 0.62f, ArchTop = 1.12f, ArchHalfWidth = 0.34f,
                GirthZ = 0.02f, BodyCenterY = 0.63f, BodyHalfWidth = 0.25f, BodyHalfHeight = 0.29f,
            },
            new SpeciesInfo
            {
                Species = Species.Deer, Prefab = "Deer", Harness = HarnessKind.Light,
                ArchZ = 0.34f, ArchBottom = 1.0f, ArchTop = 1.68f, ArchHalfWidth = 0.3f,
                GirthZ = 0.05f, BodyCenterY = 1.08f, BodyHalfWidth = 0.19f, BodyHalfHeight = 0.25f,
            },
            new SpeciesInfo
            {
                Species = Species.Wolf, Prefab = "Wolf", Harness = HarnessKind.Heavy,
                ArchZ = 0.28f, ArchBottom = 0.85f, ArchTop = 1.38f, ArchHalfWidth = 0.28f,
                GirthZ = 0f, BodyCenterY = 0.88f, BodyHalfWidth = 0.18f, BodyHalfHeight = 0.23f,
            },
        };

        public static SpeciesInfo Get(Species species)
        {
            return s_all[(int)species];
        }

        public static SpeciesInfo[] All => s_all;
    }
}
