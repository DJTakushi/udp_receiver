const rowsElement = document.getElementById("rows");
const lastUpdatedElement = document.getElementById("last-updated");
const themeToggle = document.getElementById("theme-toggle");
const downloadLogButton = document.getElementById("download-log");
const clearLogButton = document.getElementById("clear-log");
const portCountersElement = document.getElementById("port-counters");

(function initTheme() {
  const stored = localStorage.getItem("theme");
  const prefersDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
  const isDark = stored === "dark" || (!stored && prefersDark);
  applyTheme(isDark ? "dark" : "light");
})();

function applyTheme(theme) {
  document.documentElement.setAttribute("data-theme", theme);
  themeToggle.textContent = theme === "dark" ? "☀️ Light" : "🌙 Dark";
  localStorage.setItem("theme", theme);
}

themeToggle.addEventListener("click", () => {
  const current = document.documentElement.getAttribute("data-theme");
  applyTheme(current === "dark" ? "light" : "dark");
});

downloadLogButton.addEventListener("click", async () => {
  const originalLabel = downloadLogButton.textContent;
  downloadLogButton.disabled = true;
  downloadLogButton.textContent = "Preparing...";

  try {
    const response = await fetch("/api/messages/log", { cache: "no-store" });
    if (!response.ok) {
      throw new Error(`Export failed: ${response.status}`);
    }

    const blob = await response.blob();
    const contentDisposition = response.headers.get("content-disposition") || "";
    const match = /filename="?([^\";]+)"?/i.exec(contentDisposition);
    const fileName = match && match[1] ? match[1] : "can-log.log";

    const objectUrl = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = objectUrl;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(objectUrl);
  } catch (error) {
    alert(`Unable to download CAN log: ${toSafeText(error.message)}`);
  } finally {
    downloadLogButton.disabled = false;
    downloadLogButton.textContent = originalLabel;
  }
});

clearLogButton.addEventListener("click", async () => {
  const confirmed = window.confirm("Clear all currently stored CAN frames?");
  if (!confirmed) {
    return;
  }

  const originalLabel = clearLogButton.textContent;
  clearLogButton.disabled = true;
  clearLogButton.textContent = "Clearing...";

  try {
    const response = await fetch("/api/messages/clear", {
      method: "POST",
      cache: "no-store",
    });

    if (!response.ok) {
      throw new Error(`Clear failed: ${response.status}`);
    }

    await loadMessages();
  } catch (error) {
    alert(`Unable to clear CAN logs: ${toSafeText(error.message)}`);
  } finally {
    clearLogButton.disabled = false;
    clearLogButton.textContent = originalLabel;
  }
});

function toSafeText(value) {
  return String(value ?? "");
}

// The API serialises byte[] as a base64 string. Convert to spaced hex for display.
function base64ToHex(base64) {
  if (!base64) return "";
  const binary = atob(base64);
  return Array.from(binary, (ch) =>
    ch.charCodeAt(0).toString(16).padStart(2, "0")
  ).join(" ");
}

function renderRows(messages) {
  if (!messages || messages.length === 0) {
    rowsElement.innerHTML = `
      <tr>
        <td colspan="11" class="placeholder">No frames received yet.</td>
      </tr>
    `;
    return;
  }

  const html = messages
    .map((message) => {
      const timestamp = new Date(message.timestamp).toISOString();
      const source = toSafeText(message.source);
      const target = toSafeText(message.target);
      const endpointParts = splitEndpoint(target);
      const targetIp = endpointParts.ip;
      const targetPort = endpointParts.port;
      const identity = toSafeText(message.identity);
      const frameInfo = message.frameInfo.toString(16).padStart(2, "0").toUpperCase();
      const canId = message.canId.toString(16).padStart(8, "0").toUpperCase();
      const canDlc = Number(message.canDlc ?? 0);
      const isExtended = message.isExtended ? "Y" : "N";
      const isRtr = message.isRtr ? "Y" : "N";
      const dataBytes = base64ToHex(message.dataBytes);

      return `
        <tr>
          <td>${timestamp}</td>
          <td>${source}</td>
          <td>${targetIp}</td>
          <td>${targetPort}</td>
          <td>${identity}</td>
          <td>${frameInfo}</td>
          <td>${isExtended}</td>
          <td>${isRtr}</td>
          <td>${canDlc}</td>
          <td>${canId}</td>
          <td>${dataBytes}</td>
        </tr>
      `;
    })
    .join("");

  rowsElement.innerHTML = html;
}

function renderPortTotals(portTotals) {
  const entries = Object.entries(portTotals ?? {})
    .map(([port, count]) => ({ port: Number(port), count: Number(count) }))
    .sort((a, b) => a.port - b.port);

  if (entries.length === 0) {
    portCountersElement.innerHTML = '<span class="counter-placeholder">No traffic yet.</span>';
    return;
  }

  const html = entries
    .map(({ port, count }) => `
      <div class="counter-chip">
        <span class="label">Port ${port}</span>
        <span class="value">${count.toLocaleString()}</span>
      </div>
    `)
    .join("");

  portCountersElement.innerHTML = html;
}

async function loadMessages() {
  try {
    const response = await fetch("/api/messages", { cache: "no-store" });
    if (!response.ok) {
      throw new Error(`Request failed: ${response.status}`);
    }

    const payload = await response.json();
    renderRows(payload.messages);
    renderPortTotals(payload.portTotals);
    lastUpdatedElement.textContent = `Last updated: ${new Date().toLocaleTimeString()}`;
  } catch (error) {
    rowsElement.innerHTML = `
      <tr>
        <td colspan="11" class="placeholder">Unable to fetch frames. ${toSafeText(error.message)}</td>
      </tr>
    `;
    renderPortTotals({});
  }
}

function splitEndpoint(endpoint) {
  if (!endpoint) {
    return { ip: "", port: "" };
  }

  if (endpoint.startsWith("[")) {
    const closeBracketIndex = endpoint.indexOf("]");
    if (closeBracketIndex > 0) {
      const ip = endpoint.slice(0, closeBracketIndex + 1);
      const port = endpoint.slice(closeBracketIndex + 2);
      return { ip, port };
    }
  }

  const separatorIndex = endpoint.lastIndexOf(":");
  if (separatorIndex < 0) {
    return { ip: endpoint, port: "" };
  }

  return {
    ip: endpoint.slice(0, separatorIndex),
    port: endpoint.slice(separatorIndex + 1),
  };
}

loadMessages();
setInterval(loadMessages, 100);
