namespace PreludeLib.Compat;

public static class CompatExtensions
{
#if !NET5_0_OR_GREATER
    public static void Deconstruct<K, V>(this KeyValuePair<K, V> pair, out K key, out V value)
    {
        key = pair.Key;
        value = pair.Value;
    }

    public static IEnumerable<T> SkipLast<T>(this IEnumerable<T> source, int count)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

        if (count == 0)
            return source;
        else
            return SkipLastIterator(source, count);
    }

    private static IEnumerable<T> SkipLastIterator<T>(IEnumerable<T> source, int count)
    {
        var queue = new Queue<T>(count + 1);

        foreach (var item in source)
        {
            queue.Enqueue(item);
            if (queue.Count > count)
                yield return queue.Dequeue();
        }
    }
#endif
}