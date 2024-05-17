using System.Collections.Concurrent;

public class FixedSizedQueue<T>
{
    T item;
    readonly ConcurrentQueue<T> q = new ConcurrentQueue<T>();
    private object lockObject = new object();

    public int Limit { get; set; }

    public FixedSizedQueue(int limit, T head)
    {
        item = head;
        Limit = limit;
    }
    public void Enqueue(T obj)
    {
        q.Enqueue(obj);
        lock (lockObject)
        {
            while (q.Count > Limit && q.TryDequeue(out _)) ;
        }
    }
    public void Dequeue() 
    {
        lock (lockObject){
            q.TryDequeue(out _);
        }
    }
    public T[] ToArray()
    {
        return q.ToArray().Prepend(item).ToArray();
    }
}
