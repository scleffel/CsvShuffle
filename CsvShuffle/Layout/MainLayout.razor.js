const storageKey = 'csvShuffle.colorMode';
const systemColorScheme = window.matchMedia('(prefers-color-scheme: dark)');

let systemColorSchemeListener;

// noinspection JSUnusedGlobalSymbols
export function initialize(layout) {
    systemColorSchemeListener = async event => {
        try {
            await layout.invokeMethodAsync('OnSystemColorSchemeChanged', event.matches);
        } catch {
            // The component may have been disposed before the browser event fires.
        }
    };

    systemColorScheme.addEventListener('change', systemColorSchemeListener);

    return {
        mode: localStorage.getItem(storageKey),
        prefersDark: systemColorScheme.matches
    };
}

// noinspection JSUnusedGlobalSymbols
export function applyPreference(mode) {
    if (mode === 'system') {
        localStorage.removeItem(storageKey);
    } else {
        localStorage.setItem(storageKey, mode);
    }

    return systemColorScheme.matches;
}

// noinspection JSUnusedGlobalSymbols
export function dispose() {
    if (systemColorSchemeListener) {
        systemColorScheme.removeEventListener('change', systemColorSchemeListener);
        systemColorSchemeListener = undefined;
    }
}
