using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Scenarios.Blazor;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<SchemaEditor>("#app");
await builder.Build().RunAsync();
