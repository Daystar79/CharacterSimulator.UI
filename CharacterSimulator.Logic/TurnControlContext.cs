using System;
using System.Threading;
using System.Threading.Tasks;

namespace CharacterSimulator.Logic;

public enum SimulationState
{
    Ready,
    Running,
    Paused,
    Stopped
}

public class TurnControlContext
{
    private CancellationTokenSource _cts = new();
    private TaskCompletionSource<bool> _stepSignal = new();

    public SimulationState State { get; private set; } = SimulationState.Ready;
    public int DelayMs { get; set; } = 600;

    public event Action<SimulationState>? OnStateChanged;

    public CancellationToken CancellationToken => _cts.Token;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        State = SimulationState.Running;
        OnStateChanged?.Invoke(State);
    }

    public void Pause()
    {
        if (State == SimulationState.Running)
        {
            State = SimulationState.Paused;
            OnStateChanged?.Invoke(State);
        }
    }

    public void Resume()
    {
        if (State == SimulationState.Paused || State == SimulationState.Ready)
        {
            State = SimulationState.Running;
            _stepSignal.TrySetResult(true);
            OnStateChanged?.Invoke(State);
        }
    }

    public void Step()
    {
        State = SimulationState.Paused;
        _stepSignal.TrySetResult(true);
        OnStateChanged?.Invoke(State);
    }

    public void Stop()
    {
        State = SimulationState.Stopped;
        _cts.Cancel();
        _stepSignal.TrySetResult(false);
        OnStateChanged?.Invoke(State);
    }

    public async Task WaitTurnAsync()
    {
        if (_cts.IsCancellationRequested) return;

        if (State == SimulationState.Paused)
        {
            _stepSignal = new TaskCompletionSource<bool>();
            try
            {
                await _stepSignal.Task;
            }
            catch (TaskCanceledException) { }
        }
        else if (DelayMs > 0)
        {
            try
            {
                await Task.Delay(DelayMs, _cts.Token);
            }
            catch (TaskCanceledException) { }
        }
    }
}
