// Focus et <dialog> accessibles (module 8, concepts 3 et 12).
export function openDialog(dialog) { if (!dialog.open) dialog.showModal(); }
export function closeDialog(dialog) { if (dialog.open) dialog.close(); }
export function onClose(dialog, dotnetRef) {
   dialog.addEventListener("close", () => dotnetRef.invokeMethodAsync("OnNativeClose"));
}
