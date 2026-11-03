(function () {
    var INSTALL_KEY = 'rmb_install_dismiss';
    var NOTIFY_KEY = 'rmb_notify_dismiss';
    var deferredPrompt = null;

    function byId(id) {
        return document.getElementById(id);
    }

    function show(el) {
        if (el) el.hidden = false;
    }

    function hide(el) {
        if (el) el.hidden = true;
    }

    function isInstalled() {
        return window.matchMedia('(display-mode: standalone)').matches
            || window.navigator.standalone === true;
    }

    function canPush() {
        return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
    }

    // VAPID public key comes as url-safe base64
    function toUint8Array(base64) {
        var pad = '='.repeat((4 - (base64.length % 4)) % 4);
        var raw = atob((base64 + pad).replace(/-/g, '+').replace(/_/g, '/'));
        var out = new Uint8Array(raw.length);
        for (var i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
        return out;
    }

    function registerWorker() {
        if (!('serviceWorker' in navigator)) return Promise.resolve();
        return navigator.serviceWorker.register('/sw.js').catch(function () { });
    }

    // Installed app should keep the cookie until they sign out themselves
    function keepSignedIn() {
        return fetch('/api/push/keep-signed-in', {
            method: 'POST',
            credentials: 'same-origin'
        }).catch(function () { });
    }

    async function turnOnNotifications() {
        if (!canPush()) {
            alert('Push notifications are not supported in this browser.');
            return false;
        }

        var permission = await Notification.requestPermission();
        if (permission !== 'granted') return false;

        var reg = await navigator.serviceWorker.ready;
        var keyRes = await fetch('/api/push/public-key', { credentials: 'same-origin' });
        if (!keyRes.ok) {
            alert('Server push setup is not ready yet.');
            return false;
        }

        var keys = await keyRes.json();
        var sub = await reg.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: toUint8Array(keys.publicKey)
        });

        var save = await fetch('/api/push/subscribe', {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(sub.toJSON())
        });

        return save.ok;
    }

    async function maybeShowNotifyBanner() {
        var banner = byId('pwaNotifyBanner');
        if (!banner || !canPush()) return;
        if (localStorage.getItem(NOTIFY_KEY) === '1') return;
        if (Notification.permission === 'denied') return;

        try {
            var res = await fetch('/api/push/status', { credentials: 'same-origin' });
            if (!res.ok) return;
            var status = await res.json();
            if (!status.configured || !status.isInternal) return;
            if (status.subscribed && Notification.permission === 'granted') return;
            show(banner);
        } catch (e) { }
    }

    window.addEventListener('beforeinstallprompt', function (e) {
        e.preventDefault();
        deferredPrompt = e;
        if (localStorage.getItem(INSTALL_KEY) !== '1') {
            show(byId('pwaInstallBanner'));
        }
    });

    window.addEventListener('appinstalled', function () {
        deferredPrompt = null;
        hide(byId('pwaInstallBanner'));
        keepSignedIn();
    });

    document.addEventListener('DOMContentLoaded', function () {
        registerWorker().then(function () {
            maybeShowNotifyBanner();
        });

        if (isInstalled()) {
            keepSignedIn();
        }

        var installBtn = byId('pwaInstallBtn');
        var installDismiss = byId('pwaInstallDismiss');
        var notifyBtn = byId('pwaNotifyBtn');
        var notifyDismiss = byId('pwaNotifyDismiss');

        if (installBtn) {
            installBtn.addEventListener('click', async function () {
                if (!deferredPrompt) {
                    hide(byId('pwaInstallBanner'));
                    return;
                }
                deferredPrompt.prompt();
                try { await deferredPrompt.userChoice; } catch (e) { }
                deferredPrompt = null;
                hide(byId('pwaInstallBanner'));
            });
        }

        if (installDismiss) {
            installDismiss.addEventListener('click', function () {
                localStorage.setItem(INSTALL_KEY, '1');
                hide(byId('pwaInstallBanner'));
            });
        }

        if (notifyBtn) {
            notifyBtn.addEventListener('click', async function () {
                notifyBtn.disabled = true;
                var ok = await turnOnNotifications();
                notifyBtn.disabled = false;
                if (ok) {
                    localStorage.setItem(NOTIFY_KEY, '1');
                    hide(byId('pwaNotifyBanner'));
                }
            });
        }

        if (notifyDismiss) {
            notifyDismiss.addEventListener('click', function () {
                localStorage.setItem(NOTIFY_KEY, '1');
                hide(byId('pwaNotifyBanner'));
            });
        }
    });
})();
