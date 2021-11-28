namespace IORing.Samples.Library;

public static class Extensions
{
    public static bool TryDispose<T>(this T disposable) where T : IDisposable
    {
        if (disposable == null) return false;
        try
        {
            disposable.Dispose();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void AddRange<T>(this IList<T> list, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            list.Add(item);
        }
    }
}
