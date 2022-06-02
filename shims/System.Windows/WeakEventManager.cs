using System;
using System.Collections.Generic;

namespace NuGet.NetFxStubs.System.Windows;

public abstract class WeakEventManager
{
    readonly Dictionary<object, object> _sourceData = new();

    protected WeakEventManager()
    {
    }

    protected IDisposable ReadLock => throw new NotImplementedException();
    protected IDisposable WriteLock => throw new NotImplementedException();

    protected object this[object source] {
        get { return _sourceData[source]; }
        set { _sourceData[source] = value; }
    }

    protected void DeliverEvent(object sender, EventArgs args)
        => DeliverEventToList(sender, args, (ListenerList)this[sender]);

    protected void DeliverEventToList(object sender, EventArgs args, ListenerList list)
    {
        for (int i = 0; i < list.Count; i ++) {
            IWeakEventListener listener = list[i];
            listener.ReceiveWeakEvent(GetType(), sender, args);
        }
    }

    protected void ProtectedAddListener(object source, IWeakEventListener listener)
        => (_sourceData[source] as ListenerList)?.Add(listener);

    protected void ProtectedRemoveListener(object source, IWeakEventListener listener)
        => (_sourceData[source] as ListenerList)?.Remove(listener);

    protected virtual bool Purge(object source, object data, bool purgeAll)
        => throw new NotImplementedException();

    protected void Remove(object source)
        => throw new NotImplementedException();

    protected void ScheduleCleanup()
        => throw new NotImplementedException();

    protected abstract void StartListening(object source);

    protected abstract void StopListening(object source);

    protected static WeakEventManager GetCurrentManager(Type managerType)
        => throw new NotImplementedException();

    protected static void SetCurrentManager(Type managerType, WeakEventManager manager)
        => throw new NotImplementedException();

    protected class ListenerList
    {
        public static ListenerList Empty => throw new NotImplementedException();

        public ListenerList()
            => throw new NotImplementedException();

        public ListenerList(int capacity)
            => throw new NotImplementedException();

        public int Count => throw new NotImplementedException();
        public bool IsEmpty => throw new NotImplementedException();
        public IWeakEventListener this[int index] => throw new NotImplementedException();

        public void Add(IWeakEventListener listener)
            => throw new NotImplementedException();

        public bool BeginUse()
            => throw new NotImplementedException();

        public WeakEventManager.ListenerList Clone()
            => throw new NotImplementedException();

        public void EndUse()
            => throw new NotImplementedException();

        public static bool PrepareForWriting(ref WeakEventManager.ListenerList list)
            => throw new NotImplementedException();

        public bool Purge()
            => throw new NotImplementedException();

        public void Remove(IWeakEventListener listener)
            => throw new NotImplementedException();
    }
}
