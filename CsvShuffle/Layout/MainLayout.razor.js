const storageKey = 'csvShuffle.colorMode';
const systemColorScheme = window.matchMedia('(prefers-color-scheme: dark)');

let systemColorSchemeListener;
const ON_PWA_UPDATE_AVAILABLE = 'OnPwaUpdateAvailable';
const VERSION_MANIFEST_URL = 'version.json';

let pwaRegistration;
let pwaAbortController;

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

async function notifyPwaUpdateAvailable(layout) {
    try {
        await layout.invokeMethodAsync(ON_PWA_UPDATE_AVAILABLE);
    } catch {
        // The application may have been disposed before the browser event fires.
    }
}

function compareVersions(left, right) {
    const parse = version => {
        const match = /^(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?$/.exec(version);
        if (!match)
            return null;

        return {
            numbers: [Number(match[1]), Number(match[2]), Number(match[3])],
            prerelease: match[4]?.split('.') ?? []
        };
    };

    const leftVersion = parse(left);
    const rightVersion = parse(right);
    if (!leftVersion || !rightVersion)
        return 0;

    for (let index = 0; index < leftVersion.numbers.length; index++) {
        const difference = leftVersion.numbers[index] - rightVersion.numbers[index];
        if (difference !== 0)
            return Math.sign(difference);
    }

    if (leftVersion.prerelease.length === 0)
        return rightVersion.prerelease.length === 0 ? 0 : 1;
    if (rightVersion.prerelease.length === 0)
        return -1;

    const count = Math.max(leftVersion.prerelease.length, rightVersion.prerelease.length);
    for (let index = 0; index < count; index++) {
        const leftPart = leftVersion.prerelease[index];
        const rightPart = rightVersion.prerelease[index];
        if (leftPart === undefined)
            return -1;
        if (rightPart === undefined)
            return 1;
        if (leftPart === rightPart)
            continue;

        const leftNumber = /^\d+$/.test(leftPart);
        const rightNumber = /^\d+$/.test(rightPart);
        if (leftNumber && rightNumber)
            return Math.sign(Number(leftPart) - Number(rightPart));
        if (leftNumber)
            return -1;
        if (rightNumber)
            return 1;
        return leftPart > rightPart ? 1 : -1;
    }

    return 0;
}

async function checkHostVersion(layout, currentVersion) {
    try {
        const response = await fetch(VERSION_MANIFEST_URL, { cache: 'no-store' });
        if (!response.ok)
            return;

        const manifest = await response.json();
        if (typeof manifest.version === 'string' && compareVersions(manifest.version, currentVersion) > 0)
            await notifyPwaUpdateAvailable(layout);
    } catch {
        // Leave the application usable when the host cannot be reached.
    }
}

export async function initializePwaUpdate(layout, currentVersion) {
    if ('serviceWorker' in navigator) {
        pwaAbortController = new AbortController();

        const hadController = navigator.serviceWorker.controller !== null;
        navigator.serviceWorker.addEventListener('controllerchange', () => {
            if (hadController)
                window.location.reload();
        }, { once: true, signal: pwaAbortController.signal });

        try {
            pwaRegistration = await navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
            await pwaRegistration.update();
        } catch {
            // Leave the application usable when the service worker cannot be registered.
        }
    }

    await checkHostVersion(layout, currentVersion);
}

export function applyPwaUpdate() {
    if (pwaRegistration?.waiting) {
        pwaRegistration.waiting.postMessage({ type: 'SKIP_WAITING' });
    } else {
        window.location.reload();
    }
}

export function disposePwaUpdate() {
    pwaAbortController?.abort();
    pwaAbortController = undefined;
    pwaRegistration = undefined;
}
