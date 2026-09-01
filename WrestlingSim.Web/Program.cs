using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WrestlingSim.Web;
using WrestlingSim.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// One game session for the lifetime of the tab: the roster, and the career if one
// is open. SaveStore is what makes a career outlive the tab.
builder.Services.AddSingleton<SaveStore>();
builder.Services.AddSingleton<GameState>();

await builder.Build().RunAsync();
