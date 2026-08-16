using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WrestlingSim.Web;
using WrestlingSim.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// One game session for the lifetime of the tab: roster + the shared FeudBook.
builder.Services.AddSingleton<GameState>();

await builder.Build().RunAsync();
