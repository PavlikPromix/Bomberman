namespace Bomberman.Server.Infrastructure;
public static class Guard
{
    public static void NotEmpty(string? v, string name)
    {
        if (string.IsNullOrWhiteSpace(v)) throw new ArgumentException($"{name} must be a non-empty string.");
    }
    public static void Positive(int v, string name)
    {
        if (v <= 0) throw new ArgumentException($"{name} must be a positive integer.");
    }
    public static void AtLeast(int v, int min, string name)
    {
        if (v < min) throw new ArgumentException($"{name} must be >= {min}.");
    }
}
