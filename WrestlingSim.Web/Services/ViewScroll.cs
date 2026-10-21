using Microsoft.JSInterop;

namespace WrestlingSim.Web.Services;

/// <summary>
/// Puts a new view at the top of the page.
///
/// Blazor replaces the component tree without touching the scroll position, so a screen — or a
/// wizard step, which is a new view inside one screen — opens at whatever offset the previous
/// one was left at. Measured on the exhibition match flow: scrollY 423 on the beats step and
/// 423 on the result that replaced it, which put the winner and the star rating above the
/// viewport with nothing on screen to say so. The one moment the mode builds to was skipped by
/// default.
///
/// The reset is instant rather than smooth. This is a change of view and not a movement within
/// one, so there is no journey to animate — which also means there is no motion here for a
/// reduced-motion preference to be about.
/// </summary>
public sealed class ViewScroll(IJSRuntime js)
{
    public async Task ToTopAsync()
    {
        try
        {
            await js.InvokeVoidAsync("wrestleSim.toTop");
        }
        catch (JSDisconnectedException)
        {
            // The circuit went away mid-navigation. There is no view left to scroll.
        }
    }
}
