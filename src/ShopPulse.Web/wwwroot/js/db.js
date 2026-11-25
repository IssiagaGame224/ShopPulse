// IndexedDB : base clé-valeur transactionnelle du navigateur (file d'attente hors ligne,
// cache de données en lecture). Blazor Server y accède via l'interop (BrowserDb côté C#).

const open = () => new Promise((resolve, reject) => {
   const req = indexedDB.open("shoppulse", 1);
   req.onupgradeneeded = () => {
       req.result.createObjectStore("queue", { keyPath: "id" });
       req.result.createObjectStore("products", { keyPath: "id" });
   };
   req.onsuccess = () => resolve(req.result);
   req.onerror = () => reject(req.error);
});

export async function put(store, value) {
   const db = await open();
   return new Promise((resolve, reject) => {
       const tx = db.transaction(store, "readwrite");
       tx.objectStore(store).put(value);
       tx.oncomplete = () => resolve();
       tx.onerror = () => reject(tx.error);
   });
}

export async function getAll(store) {
   const db = await open();
   return new Promise((resolve, reject) => {
       const req = db.transaction(store).objectStore(store).getAll();
       req.onsuccess = () => resolve(req.result);
       req.onerror = () => reject(req.error);
   });
}

export async function remove(store, id) {
   const db = await open();
   return new Promise((resolve, reject) => {
       const tx = db.transaction(store, "readwrite");
       tx.objectStore(store).delete(id);
       tx.oncomplete = () => resolve();
       tx.onerror = () => reject(tx.error);
   });
}
