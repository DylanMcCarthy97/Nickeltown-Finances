var CACHE = 'nickeltown-finances-v9';
var SHELL = [
  '/upload', '/styles.css', '/manifest.json', '/offline.html',
  '/assets/logo.png', '/icons/icon-192.png', '/icons/icon-512.png',
  '/css/tokens.css', '/css/base.css', '/css/components.css', '/css/screens.css',
  '/js/settings.js', '/js/queue.js', '/js/history.js', '/js/connection.js',
  '/js/upload-service.js', '/js/camera.js', '/js/app.js'
];

self.addEventListener('install', function (event) {
  event.waitUntil(
    caches.open(CACHE).then(function (cache) {
      return cache.addAll(SHELL);
    }).then(function () { return self.skipWaiting(); })
  );
});

self.addEventListener('activate', function (event) {
  event.waitUntil(
    caches.keys().then(function (keys) {
      return Promise.all(keys.filter(function (k) { return k !== CACHE; }).map(function (k) { return caches.delete(k); }));
    }).then(function () { return self.clients.claim(); })
  );
});

function isApi(url) {
  return url.indexOf('/api/') >= 0;
}

function isStaticScript(url) {
  return url.indexOf('/js/') >= 0 || url.indexOf('/styles.css') >= 0 || url.indexOf('/css/') >= 0;
}

self.addEventListener('fetch', function (event) {
  if (event.request.method !== 'GET') return;

  var url = event.request.url;

  if (isApi(url)) {
    event.respondWith(fetch(event.request));
    return;
  }

  if (isStaticScript(url)) {
    event.respondWith(
      fetch(event.request).then(function (response) {
        if (response && response.status === 200) {
          var copy = response.clone();
          caches.open(CACHE).then(function (cache) { cache.put(event.request, copy); });
        }
        return response;
      }).catch(function () {
        return caches.match(event.request).then(function (c) { return c || caches.match('/offline.html'); });
      })
    );
    return;
  }

  event.respondWith(
    caches.match(event.request).then(function (cached) {
      if (cached) return cached;
      return fetch(event.request).then(function (response) {
        if (response && response.status === 200) {
          var copy = response.clone();
          caches.open(CACHE).then(function (cache) { cache.put(event.request, copy); });
        }
        return response;
      }).catch(function () {
        if (event.request.mode === 'navigate') return caches.match('/offline.html');
        return caches.match('/offline.html');
      });
    })
  );
});

self.addEventListener('sync', function (event) {
  if (event.tag === 'receipt-upload-sync') {
    event.waitUntil(
      self.clients.matchAll().then(function (clients) {
        clients.forEach(function (client) { client.postMessage({ type: 'FLUSH_QUEUE' }); });
      })
    );
  }
});

self.addEventListener('message', function (event) {
  if (event.data && event.data.type === 'SKIP_WAITING') self.skipWaiting();
});
