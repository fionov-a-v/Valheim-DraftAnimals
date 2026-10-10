namespace DraftAnimals
{
    /// <summary>Зверь со своим списком еды (олень, заяц): что он ест и что делать, когда поел (кормушка, корм с руки).</summary>
    public interface IFoodEater
    {
        /// <summary>Множитель корма по имени предмета (m_shared.m_name) или 0, если зверь это не ест.</summary>
        float FoodMultiplier(string sharedName);

        /// <summary>Поел (у владельца зверя): сытость и эффекты.</summary>
        void Feed(float multiplier);
    }
}
