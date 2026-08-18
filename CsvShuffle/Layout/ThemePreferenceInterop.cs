using Microsoft.JSInterop;

namespace CsvShuffle.Layout;

public sealed class ThemePreferenceInterop(IJSRuntime js) : IAsyncDisposable
{
    const string ModulePath = "./Layout/MainLayout.razor.js";
    const string InitializeMethod = "initialize";
    const string ApplyPreferenceMethod = "applyPreference";
    const string DisposeMethod = "dispose";

    IJSObjectReference? _module;

    async ValueTask<IJSObjectReference> GetModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", ModulePath);

    public async ValueTask<ThemePreference> InitializeAsync(DotNetObjectReference<MainLayout> layout) =>
        await (await GetModuleAsync()).InvokeAsync<ThemePreference>(InitializeMethod, layout);

    public async ValueTask<bool> ApplyPreferenceAsync(ColorMode colorMode) =>
        await (await GetModuleAsync()).InvokeAsync<bool>(ApplyPreferenceMethod, colorMode.ToString().ToLowerInvariant());

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
        }
    }
}

public sealed record ThemePreference(string? Mode, bool PrefersDark);
