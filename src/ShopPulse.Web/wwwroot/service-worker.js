// Service Worker de ShopPulse.
// Règles d'or Blazor Server :
//   - jamais de cache pour les requêtes non-GET (POST : formulaires, antiforgery)
//   - jamais de cache pour /_blazor (le circuit EST le réseau)
//   - JAMAIS de cache/SWR sur le HTML des pages : les marqueurs de préreendu Blazor
//     sont à usage unique. Servir une page périmée = îlots interactifs morts.
//     Le service worker ne fait pas fonctionner Blazor hors ligne : il sert les
//     assets statiques et une page de repli quand le réseau est absent.

const STATIC_CACHE = "shoppulse-static-v3";
const CURRENT = [STATIC_CACHE];

const PRECACHE = ["/offline.html", "/app.css"];

self.addEventListener("install", (event) => {
   event.waitUntil(
       caches.open(STATIC_CACHE).then((cache) => cache.addAll(PRECACHE)).then(() => self.skipWaiting())
   );
});

// Versioning des caches : supprime les anciens (et les caches "data" du module 5).
self.addEventListener("activate", (event) => {
   event.waitUntil((async () => {
       const names = await caches.keys();
       await Promise.all(names.filter((n) => !CURRENT.includes(n)).map((n) => caches.delete(n)));
       await self.clients.claim();
   })());
});

// Cache first : ressources statiques empreintes uniquement (CSS/JS/images avec hash).
async function cacheFirst(request) {
   const cache = await caches.open(STATIC_CACHE);
   const hit = await cache.match(request);
   if (hit) return hit;

   const response = await fetch(request);              // clone : un corps ne se lit qu'une fois
   if (response.ok) cache.put(request, response.clone());
   return response;
}

self.addEventListener("fetch", (event) => {
   const url = new URL(event.request.url);
   if (url.origin !== location.origin) return;          // tiers (hubs de l'API) : laisser passer

   if (event.request.method !== "GET") return;          // POST (formulaires, antiforgery) : réseau
   if (url.pathname.startsWith("/_blazor")) return;     // SignalR : jamais

   // App shell : navigation de page — RÉSEAU UNIQUEMENT, repli hors ligne si absent.
   if (event.request.mode === "navigate") {
       event.respondWith((async () => {
           try {
               return await fetch(event.request);
           } catch {
               const cache = await caches.open(STATIC_CACHE);
               return (await cache.match("/offline.html")) ?? Response.error();
           }
       })());
       return;
   }

   // Ressources empreintes / assets : cache first.
   if (url.pathname.startsWith("/_content") || url.pathname.startsWith("/_framework") || url.pathname.includes(".")) {
       event.respondWith(cacheFirst(event.request));
       return;
   }

   // Le reste : réseau, sans cache.
});
