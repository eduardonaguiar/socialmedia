namespace PostService.Models;

public static class PaginationOptions
{
    public const int DefaultLimit = 25;
    public const int MaxLimit = 100;

    public static int NormalizeLimit(int requested)
    {
        if (requested <= 0)
        {
            return DefaultLimit;
        }

        return Math.Min(requested, MaxLimit);
    }
}
