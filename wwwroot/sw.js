/* Service worker — app install shell + push clicks */
var CACHE = 'rmb-shell-v1';
var PRECACHE = ['/manifest.webmanifest', '/icons/icon-192.png', '/icons/icon-512.png'];

self.addEventListener('install', function (event) {
    event.waitUntil(
        caches.open(CACHE).then(function (cache) {
            return Promise.all(PRECACHE.map(function (url) {
                return cache.add(url).catch(function () { });
            }));
        }).then(function () {
            return self.skipWaiting();
        })
    );
});

self.addEventListener('activate', function (event) {
    event.waitUntil(
        caches.keys().then(function (keys) {
            return Promise.all(keys.filter(function (k) {
                return k !== CACHE;
            }).map(function (k) {
                return caches.delete(k);
            }));
        }).then(function () {
            return self.clients.claim();
        })
    );
});

self.addEventListener('push', function (event) {
    var data = {
        title: 'Rush My Bookings',
        body: 'You have a new notification.',
        url: '/',
        tag: 'rmb-default'
    };

    try {
        if (event.data) {
            var parsed = event.data.json();
            if (parsed.title) data.title = parsed.title;
            if (parsed.body) data.body = parsed.body;
            if (parsed.url) data.url = parsed.url;
            if (parsed.tag) data.tag = parsed.tag;
        }
    } catch (e) {
        try {
            if (event.data) data.body = event.data.text();
        } catch (e2) { }
    }

    event.waitUntil(
        self.registration.showNotification(data.title, {
            body: data.body,
            icon: '/icons/icon-192.png',
            badge: '/icons/icon-192.png',
            tag: data.tag,
            renotify: true,
            data: { url: data.url }
        })
    );
});

self.addEventListener('notificationclick', function (event) {
    event.notification.close();
    var target = (event.notification.data && event.notification.data.url) || '/';

    event.waitUntil(
        self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (list) {
            for (var i = 0; i < list.length; i++) {
                var client = list[i];
                if ('focus' in client) {
                    client.navigate(target);
                    return client.focus();
                }
            }
            if (self.clients.openWindow) {
                return self.clients.openWindow(target);
            }
        })
    );
});
