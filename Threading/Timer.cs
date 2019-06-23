using System;
using System.Collections.Generic;
using System.Threading;
using TTimer = System.Timers.Timer;

namespace Micro.Threading {
    public class Timer : IDisposable {
        public event Action Tick;
        public double Intervall {
            get => _timer.Interval;
            set => _timer.Interval = value;
        }
        public bool AsyncCalls {
            get => _timer.AutoReset;
            set => _timer.AutoReset = value;
        }
        public bool Enabled {
            get => _timer.Enabled;
            set => _timer.Enabled = _exec = value;
        }
        public string Name { get; set; }
        bool _exec = false;
        TTimer _timer;
        List<Thread> threadsTicks;

        /// <summary>
        /// Generates events after a set interval.
        /// </summary>
        /// <param name="interval">Time in milliseconds between events.</param>
        /// <param name="tick">Delegate to call at every tick.</param>
        /// <param name="name">Name of the background thread.</param>
        /// <param name="asyncCalls">Do not wait for the delegate call before the next tick.</param>
        public Timer(double interval, Action tick, string name = null, bool asyncCalls = false) {
            Tick += tick;
            _timer = new TTimer(interval) {
                Interval = interval,
                AutoReset = asyncCalls
            };
            Name = name ?? typeof(Timer).FullName;
            _timer.Elapsed += (a, b) => work();
            threadsTicks = new List<Thread>(20);
        }
        public void Dispose() {
            Stop(true);
            _timer.Dispose();
            Tick = null;
            threadsTicks.Clear();
        }
        public void Start() {
            if (_exec)
                return;
            lock (_timer)
                Enabled = true;
        }
        public void Stop(bool abortThreads = false) {
            if (!_exec)
                return;
            lock (_timer)
                Enabled = false;
            if (abortThreads) {
                lock (threadsTicks) {
                    foreach (var t in threadsTicks)
                        t.Abort();
                    threadsTicks.Clear();
                }
            }
        }

        void work() {
            var currThread = Thread.CurrentThread;
            currThread.Name = Name;
            threadsTicks.Add(currThread);
            Tick?.Invoke();
            threadsTicks.Remove(currThread);
            if (!AsyncCalls && _exec)
                Enabled = true;
        }
    }
}