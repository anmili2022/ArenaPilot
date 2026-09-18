using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using System.Numerics;

namespace ArenaPilot;

public sealed class VnavmeshClient
{
    private readonly ICallGateSubscriber<bool> isReady;
    private readonly ICallGateSubscriber<bool> isRunning;
    private readonly ICallGateSubscriber<List<Vector3>, bool, object> moveTo;
    private readonly ICallGateSubscriber<object> stop;

    public VnavmeshClient(IDalamudPluginInterface pluginInterface)
    {
        isReady = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        isRunning = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning");
        moveTo = pluginInterface.GetIpcSubscriber<List<Vector3>, bool, object>("vnavmesh.Path.MoveTo");
        stop = pluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
    }

    public bool IsReady => InvokeBool(isReady);
    public bool IsRunning => InvokeBool(isRunning);

    public bool MoveTo(Vector3 position)
    {
        try
        {
            if (!IsReady)
                return false;

            if (IsRunning)
                Stop();

            var player = DalamudApi.ObjectTable.LocalPlayer;
            var target = player == null ? position : position with { Y = player.Position.Y };
            moveTo.InvokeAction([target], false);
            DalamudApi.Log.Information("vnavmesh Path.MoveTo: target={Target}.", target);
            return true;
        }
        catch (Exception ex)
        {
            DalamudApi.Log.Warning(ex, "vnavmesh 移动失败");
            return false;
        }
    }

    public void Stop()
    {
        try { stop.InvokeAction(); }
        catch (Exception ex) { DalamudApi.Log.Debug(ex, "停止移动功能失败"); }
    }

    private static bool InvokeBool(ICallGateSubscriber<bool> subscriber)
    {
        try { return subscriber.InvokeFunc(); }
        catch { return false; }
    }
}
