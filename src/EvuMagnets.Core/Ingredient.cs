namespace EvuMagnets.Core;

public readonly struct Ingredient
{
    public Ingredient(string item, int amount)
    {
        Item = item;
        Amount = amount;
    }

    public string Item { get; }

    public int Amount { get; }
}
