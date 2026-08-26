self.addEventListener("push", (event) => {
  const payload = event.data?.json() ?? {};
  event.waitUntil(
    self.registration.showNotification(payload.title ?? "Family Hub", {
      body: payload.body ?? "You have a new reminder.",
      icon: "icon-192.png",
      badge: "icon-192.png",
      tag: payload.tag,
      data: { url: payload.url ?? "/medications" },
    }),
  );
});

self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  const requestedPath = event.notification.data?.url;
  const safePath =
    typeof requestedPath === "string" &&
    requestedPath.startsWith("/") &&
    !requestedPath.startsWith("//")
      ? requestedPath
      : "/";
  const targetUrl = new URL(safePath, self.location.origin).href;
  event.waitUntil(
    self.clients
      .matchAll({ type: "window", includeUncontrolled: true })
      .then((windowClients) => {
        const existingClient = windowClients[0];
        return existingClient
          ? existingClient.navigate(targetUrl).then((client) => client.focus())
          : self.clients.openWindow(targetUrl);
      }),
  );
});
