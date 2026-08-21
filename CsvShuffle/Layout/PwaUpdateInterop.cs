using Microsoft.JSInterop;
using System.Reflection;

namespace CsvShuffle.Layout;

public sealed class PwaUpdateInterop(
    IJSRuntime js
) : IAsyncDisposable
{
    const string ModulePath = "./Layout/MainLayout.razor.js";
    const string InitializeMethod = "initializePwaUpdate";
    const string ApplyUpdateMethod = "applyPwaUpdate";
    const string DisposeMethod = "disposePwaUpdate";

    IJSObjectReference? _module;

    async ValueTask<IJSObjectReference> GetModuleAsync() => _module
        ??= await js.InvokeAsync<IJSObjectReference>("import", ModulePath);

    public async ValueTask InitializeAsync(DotNetObjectReference<MainLayout> layout)
    {
        string currentVersion = typeof(MainLayout).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            ?? "0.0.0";

        await (await GetModuleAsync()).InvokeVoidAsync(InitializeMethod, layout, currentVersion);
    }

    public async ValueTask ApplyUpdateAsync() =>
        await (await GetModuleAsync()).InvokeVoidAsync(ApplyUpdateMethod);

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync(DisposeMethod);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // Ignore JSDisconnectedException when the browser is closed or refreshed
        }
    }
}
