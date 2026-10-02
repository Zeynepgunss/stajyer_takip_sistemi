(function () {
    function el(id) { return document.getElementById(id); }
    const STORAGE_KEY = "belediye_assistani_history_v1";

    const toggleBtn = el("chatToggleBtn");
    const panel = el("chatPanel");
    const closeBtn = el("chatCloseBtn");
    const sendBtn = el("chatSendBtn");
    const input = el("chatInput");
    const messages = el("chatMessages");
    const suggestions = el("chatSuggestions");

    if (!toggleBtn || !panel) return;

    function appendMessage(text, fromUser) {
        const row = document.createElement("div");
        row.className = "chat-msg " + (fromUser ? "chat-msg-user" : "chat-msg-bot");
        row.textContent = text;
        messages.appendChild(row);
        messages.scrollTop = messages.scrollHeight;
    }

    function appendLinks(links) {
        if (!Array.isArray(links) || links.length === 0) return;
        const wrap = document.createElement("div");
        wrap.className = "chat-links";
        links.forEach(function (l) {
            if (!l || !l.url) return;
            const a = document.createElement("a");
            a.className = "chat-link-btn";
            a.href = l.url;
            a.textContent = l.text || l.url;
            wrap.appendChild(a);
        });
        messages.appendChild(wrap);
        messages.scrollTop = messages.scrollHeight;
    }

    function loadHistory() {
        try {
            const raw = localStorage.getItem(STORAGE_KEY);
            return raw ? JSON.parse(raw) : [];
        } catch (_) {
            return [];
        }
    }

    function saveHistory(history) {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(history.slice(-12)));
        } catch (_) {}
    }

    function setSuggestions(items) {
        suggestions.innerHTML = "";
        (items || []).forEach(function (s) {
            const btn = document.createElement("button");
            btn.type = "button";
            btn.className = "chat-suggestion-btn";
            btn.textContent = s;
            btn.addEventListener("click", function () {
                input.value = s;
                sendMessage();
            });
            suggestions.appendChild(btn);
        });
    }

    async function sendMessage() {
        const text = (input.value || "").trim();
        if (!text) return;

        appendMessage(text, true);
        input.value = "";
        sendBtn.disabled = true;
        const history = loadHistory();
        history.push({ role: "user", content: text });
        saveHistory(history);

        try {
            const res = await fetch("/api/chat/ask", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ message: text, history: history.slice(-8) })
            });

            const data = await res.json();
            if (!res.ok || !data.success) {
                appendMessage(data.answer || "Şu anda yanıt veremiyorum.", false);
                return;
            }

            appendMessage(data.answer, false);
            appendLinks(data.links);
            setSuggestions(data.suggestions);
            history.push({ role: "assistant", content: data.answer || "" });
            saveHistory(history);
        } catch (e) {
            appendMessage("Bağlantı hatası oluştu. Lütfen tekrar deneyin.", false);
        } finally {
            sendBtn.disabled = false;
            input.focus();
        }
    }

    toggleBtn.addEventListener("click", function () {
        panel.classList.toggle("show");
        if (panel.classList.contains("show")) {
            input.focus();
        }
    });

    closeBtn.addEventListener("click", function () {
        panel.classList.remove("show");
    });

    sendBtn.addEventListener("click", sendMessage);
    input.addEventListener("keydown", function (e) {
        if (e.key === "Enter") {
            e.preventDefault();
            sendMessage();
        }
    });

    suggestions.querySelectorAll(".chat-suggestion-btn").forEach(function (btn) {
        btn.addEventListener("click", function () {
            input.value = btn.textContent || "";
            sendMessage();
        });
    });
})();

