using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Micro.Threading {
    public class WaitBagSlim : IDisposable {
        public const ushort MaxLength = ushort.MaxValue - 1;
        public bool ThrowWhenInvalidKey { get; set; } = false;
        public int Length
            => events.Count;
        ConcurrentDictionary<ushort, ManualResetEventSlim> events;
        Dictionary<ushort, object> results;

        public WaitBagSlim() {
            events = new ConcurrentDictionary<ushort, ManualResetEventSlim>();
            results = new Dictionary<ushort, object>();
        }
        public void Dispose() {
            lock (events) {
                foreach (var e in events.Values) {
                    e.Set();
                    e.Dispose();
                }
                events.Clear();
                results.Clear();
            }
        }
        public ushort PrepareTurn() {
            var e = new ManualResetEventSlim();
            lock (events) {
                var id = (ushort)Enumerable.Range(1, ushort.MaxValue)
                    .Except(events.Keys.Select(n => (int)n))
                    .First();
                if (!events.TryAdd(id, e))
                    throw new Exception("Unable to add turn to WaitBag.");
                return id;
            }
        }
        ManualResetEventSlim getTurn(in ushort key) {
            if (events.TryGetValue(key, out var e))
                return e;
            else if (ThrowWhenInvalidKey)
                throw new InvalidOperationException("WaitBag does not contain this key.");
            return null;
        }
        public void WaitTurn(in ushort key)
            => getTurn(key).Wait();
        public bool WaitTurn(int msTimeout, in ushort key)
            => getTurn(key).Wait(msTimeout);
        public object PopTurnResult(in ushort key) {
            lock (results) {
                if (results.ContainsKey(key)) {
                    var r = results[key];
                    results.Remove(key);
                    return r;
                } else
                    return null;
            }
        }
        public void Free(ushort key, object turnResult = null) {
            if (!events.TryRemove(key, out var e)) {
                if (ThrowWhenInvalidKey)
                    throw new InvalidOperationException("WaitBag does not contain this key.");
                return;
            }
            lock (results)
                results[key] = turnResult;
            e.Set();
            e.Dispose();
        }
    }
}
