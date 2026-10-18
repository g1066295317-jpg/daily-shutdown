using System;
using System.Threading;

namespace DailyShutdown
{
    internal sealed class SingleInstance : IDisposable
    {
        private readonly Mutex mutex;
        public readonly bool IsPrimary;
        public readonly EventWaitHandle Activation;
        public SingleInstance(string suffix = null)
        {
            string id = suffix ?? ScheduleStore.UserId;
            string eventName = "Local\\DailyShutdownShow-" + id;
            bool created;
            mutex = new Mutex(true, "Local\\DailyShutdownUI-" + id, out created);
            IsPrimary = created;
            if (IsPrimary) Activation = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            else
            {
                for (int i = 0; i < 30; i++)
                {
                    try { Activation = EventWaitHandle.OpenExisting(eventName); break; }
                    catch (WaitHandleCannotBeOpenedException) { Thread.Sleep(50); }
                }
            }
        }
        public void Dispose()
        {
            if (Activation != null) Activation.Dispose();
            if (IsPrimary) mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }
}
