using DemoBlazorShared;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Demo>("#app");
await builder.Build().RunAsync();
