// Détection de l'état réseau, notifiée au circuit Blazor.
export function init(dotnetRef) {
   const notify = () => dotnetRef.invokeMethodAsync("OnNetworkChanged", navigator.onLine);
   window.addEventListener("online", notify);
   window.addEventListener("offline", notify);
   notify();
}

export function isOnline() {
   return navigator.onLine;
}
