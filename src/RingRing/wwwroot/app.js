(() => {
  "use strict";

  const $ = (id) => document.getElementById(id);
  const form = $("ring-form");
  const nameInput = $("name");
  const messageInput = $("message");
  const pinInput = $("pin");
  const button = $("ring-button");
  const error = $("error");
  const status = $("status");
  const statusIcon = $("status-icon");
  const statusLabel = $("status-label");
  const reply = $("reply");
  const again = $("again");

  let info = { name: "", pinRequired: false, cooldownSeconds: 5 };
  let pollToken = 0;

  const storage = {
    get(key) { try { return localStorage.getItem(key) || ""; } catch { return ""; } },
    set(key, value) { try { localStorage.setItem(key, value); } catch { /* sin storage */ } },
  };

  nameInput.value = storage.get("ringring.name");

  async function loadInfo() {
    try {
      const res = await fetch("/api/info");
      info = await res.json();
      const who = info.name ? `Llamar a ${info.name}` : "Llamar";
      $("title").textContent = who;
      document.title = `${who} · RingRing`;
      $("pin-field").hidden = !info.pinRequired;
      if (info.doNotDisturb) showError(`${info.name} activó No molestar.`);
    } catch {
      showError("No se pudo conectar con la PC.");
    }
  }

  function showError(text) {
    error.textContent = text;
    error.hidden = !text;
  }

  function showForm() {
    pollToken++;
    status.hidden = true;
    form.hidden = false;
    button.disabled = false;
  }

  function showStatus(label, { ringing = false, replyText = null, done = false } = {}) {
    form.hidden = true;
    status.hidden = false;
    statusLabel.textContent = label;
    statusIcon.classList.toggle("ringing", ringing);
    statusIcon.classList.toggle("done", done);
    reply.hidden = !replyText;
    reply.textContent = replyText || "";
  }

  function vibrate(pattern) {
    try { navigator.vibrate?.(pattern); } catch { /* no soportado */ }
  }

  function enableAgainAfterCooldown() {
    again.hidden = false;
    let left = Math.max(0, info.cooldownSeconds | 0);
    const tick = () => {
      if (left <= 0) { again.disabled = false; again.textContent = "Llamar de nuevo"; return; }
      again.disabled = true;
      again.textContent = `Llamar de nuevo (${left})`;
      left--;
      setTimeout(tick, 1000);
    };
    tick();
  }

  function render(call) {
    switch (call.status) {
      case "ringing":
        showStatus("Sonando en la PC...", { ringing: true });
        return false;
      case "seen":
        showStatus("Lo vio. Esperando respuesta...", { ringing: false });
        return false;
      case "replied":
        showStatus("Respondió", { replyText: call.reply, done: true });
        vibrate([80, 60, 80]);
        return true;
      case "expired":
        showStatus("Nadie respondió.");
        return true;
      default:
        return true;
    }
  }

  async function poll(id, version) {
    const token = ++pollToken;
    let since = version;
    let failures = 0;
    while (token === pollToken) {
      try {
        const res = await fetch(`/api/ring/${id}?since=${since}`);
        if (!res.ok) throw new Error(String(res.status));
        const call = await res.json();
        failures = 0;
        since = call.version;
        if (render(call)) { enableAgainAfterCooldown(); return; }
      } catch {
        failures++;
        if (failures >= 5) {
          showStatus("Se perdió la conexión con la PC.");
          enableAgainAfterCooldown();
          return;
        }
        await new Promise((r) => setTimeout(r, 2000));
      }
    }
  }

  form.addEventListener("submit", async (ev) => {
    ev.preventDefault();
    showError("");
    button.disabled = true;
    storage.set("ringring.name", nameInput.value.trim());

    try {
      const res = await fetch("/api/ring", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: nameInput.value.trim(),
          message: messageInput.value.trim(),
          pin: pinInput.value.trim(),
        }),
      });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) {
        showError(data.error || "No se pudo llamar.");
        button.disabled = false;
        return;
      }
      messageInput.value = "";
      vibrate(40);
      again.hidden = true;
      render(data);
      poll(data.id, data.version);
    } catch {
      showError("No se pudo conectar con la PC.");
      button.disabled = false;
    }
  });

  again.addEventListener("click", showForm);

  loadInfo();
})();
