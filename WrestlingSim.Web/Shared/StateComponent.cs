using Microsoft.AspNetCore.Components;
using WrestlingSim.Web.Services;

namespace WrestlingSim.Web.Shared;

/// <summary>
/// Base for screens that render from <see cref="GameState"/>.
///
/// The shell subscribes to state changes and re-renders, but Blazor does not reliably
/// push a parent's StateHasChanged down into a parameterless child component. Anything
/// that arrives asynchronously — the save list, an autosave result, a show that has just
/// been run — therefore lands after the screen has already rendered and is never shown.
/// Screens inherit this so they observe the state directly.
/// </summary>
public abstract class StateComponent : ComponentBase, IDisposable
{
    [Inject] protected GameState State { get; set; } = default!;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        State.Changed += OnStateChanged;
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    public virtual void Dispose() => State.Changed -= OnStateChanged;
}
