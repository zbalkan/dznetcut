using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CSArp.Model
{
    public static class ThreadBuffer
    {
        public static int Count => buffer.Count;

        public static int AliveCount => buffer.Count(t => t.IsAlive);

        private static List<Thread> buffer;

        public static void Init() => buffer = new List<Thread>();

        public static void Add(Thread thread)
        {
            buffer.Add(thread);
            thread.Start();
        }

        public static void AddWithPrefix(Thread thread, string prefix)
        {
            if (thread.Name == null)
            {
                thread.Name = prefix + ":" + thread.ManagedThreadId;
            }

            buffer.Add(thread);
            thread.Start();
        }

        public static void StopThreadByPrefix(string prefix)
        {
            foreach (var t in buffer.Where(t => t.Name != null && t.Name.StartsWith(prefix)))
            {
                t.Abort();
            }
        }

        public static void Clear()
        {
            foreach (var t in buffer)
            {
                if (t.IsAlive)
                {
                    t.Abort();
                }
            }
            buffer.Clear();
        }
    }
}