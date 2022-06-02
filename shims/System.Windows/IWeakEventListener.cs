using System;

namespace NuGet.NetFxStubs.System.Windows;

public interface IWeakEventListener
{
    bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e);
}
