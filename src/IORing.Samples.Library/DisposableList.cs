using System.Collections;

namespace IORing.Samples.Library;

public class DisposableList<T> : IList<T>, IDisposable
    where T : IDisposable
{
    private List<T>? _list = new();

    public T this[int index] => _list[index];
    public T this[nint index] => _list[(int)index];
    public int Count => _list.Count;
    public bool IsReadOnly => false;
    T IList<T>.this[int index]
    {
        get => _list[index];
        set => _list[index] = value;
    }
    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_list).GetEnumerator();
    public int IndexOf(T item) => _list.IndexOf(item);
    public void Insert(int index, T item) => _list.Insert(index, item);
    public void RemoveAt(int index) => _list.RemoveAt(index);
    public void Add(T item) => _list.Add(item);
    public void Clear() => _list.Clear();
    public bool Contains(T item) => _list.Contains(item);
    public void CopyTo(T[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);
    public bool Remove(T item) => _list.Remove(item);

    public void Dispose()
    {
        var list = Interlocked.Exchange(ref _list, null);

        if (list != null)
        {
            foreach (var item in list)
            {
                item.TryDispose();
            }
        }
    }
}
