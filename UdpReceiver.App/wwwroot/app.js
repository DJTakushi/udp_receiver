const rowsElement = document.getElementById("rows");
const lastUpdatedElement = document.getElementById("last-updated");
const themeToggle = document.getElementById("theme-toggle");

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
        <td colspan="3" class="placeholder">No messages received yet.</td>
      </tr>
    `;
    return;
  }

  const html = messages
    .map((message) => {
      const timestamp = new Date(message.timestamp).toISOString();
      const source = toSafeText(message.source);
      const payload = base64ToHex(message.payload);

      return `
        <tr>
          <td>${timestamp}</td>
          <td>${source}</td>
          <td>${payload}</td>
        </tr>
      `;
    })
    .join("");

  rowsElement.innerHTML = html;
}

async function loadMessages() {
  try {
    const response = await fetch("/api/messages", { cache: "no-store" });
    if (!response.ok) {
      throw new Error(`Request failed: ${response.status}`);
    }

    const messages = await response.json();
    renderRows(messages);
    lastUpdatedElement.textContent = `Last updated: ${new Date().toLocaleTimeString()}`;
  } catch (error) {
    rowsElement.innerHTML = `
      <tr>
        <td colspan="3" class="placeholder">Unable to fetch messages. ${toSafeText(error.message)}</td>
      </tr>
    `;
  }
}

loadMessages();
setInterval(loadMessages, 1000);
