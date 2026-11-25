let channel;

export function init(dotnetRef) {
   channel = new BroadcastChannel("shoppulse");
   channel.onmessage = (e) => dotnetRef.invokeMethodAsync("OnRemoteChange", e.data);
}

export function publish(type) {
   channel?.postMessage(type); // un onglet ne reçoit pas ses propres messages
}

export function close() {
   channel?.close();
   channel = null;
}
