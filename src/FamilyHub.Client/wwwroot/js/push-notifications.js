function getPermissionStatus() {
  if (
    !("serviceWorker" in navigator) ||
    !("PushManager" in window) ||
    !("Notification" in window)
  ) {
    return "unsupported";
  }

  return Notification.permission;
}

async function subscribe(publicKey, requestPermission) {
  const currentStatus = getPermissionStatus();
  if (currentStatus === "unsupported") {
    return { status: currentStatus };
  }

  let permission = Notification.permission;
  if (permission === "default" && requestPermission) {
    permission = await Notification.requestPermission();
  }

  if (permission !== "granted") {
    return { status: permission };
  }

  // Any of the steps below can hang indefinitely (stalled worker install, unresponsive push
  // service, etc.), which would otherwise leave the caller's UI stuck forever. Bound the whole
  // flow so it always settles.
  return await Promise.race([
    subscribeCore(publicKey),
    new Promise((_, reject) =>
      setTimeout(
        () => reject(new Error("Push subscription setup timed out")),
        15000,
      ),
    ),
  ]);
}

async function subscribeCore(publicKey) {
  const registration = await navigator.serviceWorker.ready;
  let subscription = await registration.pushManager.getSubscription();
  if (!subscription) {
    subscription = await registration.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: urlBase64ToUint8Array(publicKey),
    });
  }

  const json = subscription.toJSON();
  return {
    status: "granted",
    endpoint: subscription.endpoint,
    p256dh: json.keys.p256dh,
    auth: json.keys.auth,
    timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone,
  };
}

function urlBase64ToUint8Array(value) {
  const padding = "=".repeat((4 - (value.length % 4)) % 4);
  const base64 = (value + padding).replace(/-/g, "+").replace(/_/g, "/");
  const raw = atob(base64);
  return Uint8Array.from([...raw].map((character) => character.charCodeAt(0)));
}

window.familyHubPushNotifications = { getPermissionStatus, subscribe };
