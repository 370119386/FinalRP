namespace VEngine
{
    using System.Collections.Generic;
    public interface IPooledObject
    {
        void OnCreate();
        void OnGet();
        void OnPut();
        void OnDestroy();
    }

    public class ObjectPool<T> where T : class, new()
    {
        readonly Stack<T> pooled;
        int maxCapacity;
        public ObjectPool(int capacity = 128, int maxCapacity = 256)
        {
            pooled = new Stack<T>(capacity);
            this.maxCapacity = maxCapacity;
        }

        public T Get()
        {
            T inst = null;
            if (pooled.Count > 0)
            {
                inst = pooled.Pop();
                if(inst is IPooledObject pooledObject)
                    pooledObject.OnGet();
                return inst;
            }
            else
            {
                inst = new T();
                if (inst is IPooledObject pooledObject)
                    pooledObject.OnCreate();
            }
            return inst;
        }

        public void Put(T obj)
        {
            if(null == obj)
            {
                if (pooled.Count < maxCapacity)
                {
                    if (obj is IPooledObject pooledObject)
                        pooledObject.OnPut();
                    pooled.Push(obj);
                }
                else
                {
                    if (obj is IPooledObject pooledObject)
                        pooledObject.OnDestroy();
                }
            }
        }
    }
}