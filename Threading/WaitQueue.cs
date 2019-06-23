using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Micro.Threading {
    public class WaitQueue : IDisposable {
        public bool ThrowWhenEmpty { get; set; } = false;
        public int Length
            => events.Count;
        ConcurrentQueue<ManualResetEventSlim> events;
        
        public WaitQueue() {
            events = new ConcurrentQueue<ManualResetEventSlim>();
        }
        public void Dispose() {
            lock (events) {
                foreach (var e in events) {
                    e.Set();
                    e.Dispose();
                }
                for (int i = 0; i < events.Count; i++)
                    events.TryDequeue(out _);
            }
        }
        ManualResetEventSlim newEvent() {
            var e = new ManualResetEventSlim();
            events.Enqueue(e);
            return e;
        }
        public void WaitTurn()
            => newEvent().Wait();
        public bool WaitTurn(int msTimeout)
            => newEvent().Wait(msTimeout);
        public void FreeNext() {
            if (events.TryDequeue(out var e)) {
                e.Set();
                e.Dispose();
            } else if (ThrowWhenEmpty)
                throw new InvalidOperationException("The call queue is empty.");
        }
    }
}
