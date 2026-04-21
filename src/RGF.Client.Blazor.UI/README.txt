-------------------------------------------------
------------ RecroGrid Framework - Client Blazor UI ------------
-------------------------------------------------

https://RecroGridFramework.com

Quickstart:
-------------------------------------------------------------------------
1. Register RecroGrid Framework Services
Open ~/Program.cs file and register the RecroGrid Framework Blazor UI Services in the client web app. 

//await builder.Build().RunAsync();

// Standalone WASM / bearer-token OIDC mode:
builder.Services.AddRgfBlazorWasmBearerServices(builder.Configuration);


var host = builder.Build();

await host.Services.InitializeRgfBlazorAsync();
await host.Services.InitializeRgfUIAsync();

await host.RunAsync();

-------------------------------------------------------------------------
3. Imports
Open ~/_Imports.razor file and import the RecroGrid Framework Blazor UI namespaces. 

@using Recrovit.RecroGridFramework.Client.Blazor.Parameters
@using Recrovit.RecroGridFramework.Client.Blazor.UI
@using Recrovit.RecroGridFramework.Client.Blazor.UI.Components

-------------------------------------------------------------------------
2. Init Router
In the App.razor file, you need to configure it to use the RecroGrid Framework's Blazor components for routing. 

<Router AppAssembly="@typeof(App).Assembly"
        AdditionalAssemblies="new[] { typeof(Recrovit.RecroGridFramework.Client.Blazor.Components.RgfEntityComponent).Assembly }">
    ...
</Router>

-------------------------------------------------------------------------
3. MainLayout
Layout/MainLayout.razor 
@inherits LayoutComponentBase

<RgfRootComponent >
    <MenuComponent MenuParameters="new RgfMenuParameters() { MenuId = 10, Navbar = true }" />

    @Body

</RgfRootComponent>

-------------------------------------------------------------------------
4. API access
wwwroot/appsettings.json 

  "Recrovit": {
    "RecroGridFramework": {
      "API": {
        "BaseAddress": "https://{API-DOMAIN}" // required for direct API modes
      }
    }
  }
