// Triggers a browser download from a base64-encoded byte payload.
window.oeDownloader = {
    download: function (filename, mimeType, base64) {
        try {
            const binary = atob(base64);
            const bytes = new Uint8Array(binary.length);
            for (let i = 0; i < binary.length; i++) {
                bytes[i] = binary.charCodeAt(i);
            }
            const blob = new Blob([bytes], { type: mimeType || 'application/octet-stream' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = filename;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            // Defer revocation so the click has a chance to consume the URL.
            setTimeout(() => URL.revokeObjectURL(url), 1500);
            return true;
        } catch (e) {
            console.error('oeDownloader failed', e);
            return false;
        }
    }
};

// Tiny helpers for the preview pane: capture/restore scroll offset across a
// reseed so the user keeps their current view of a zoomed map.
window.oePreview = {
    getScroll: function (el) {
        if (!el) return [0, 0];
        return [el.scrollLeft || 0, el.scrollTop || 0];
    },
    setScroll: function (el, left, top) {
        if (!el) return;
        el.scrollLeft = left;
        el.scrollTop = top;
    },
};

// T-802 — global Ctrl-Z / Ctrl-Y / Ctrl-Shift-Z handler that forwards into
// the Blazor host's EditHistory. The .NET side hands us an object reference
// with [JSInvokable] Undo / Redo methods; we install a single window-level
// keydown listener and detach it on dispose.
window.oeUndoRedo = (function () {
    let dotNetRef = null;
    let listener = null;

    function isMod(ev) {
        // Treat metaKey (⌘ on macOS) the same as Ctrl so the hotkey works
        // for browser users on Mac as well.
        return ev.ctrlKey || ev.metaKey;
    }

    return {
        attach: function (ref) {
            this.detach();
            dotNetRef = ref;
            listener = function (ev) {
                if (!dotNetRef) return;
                if (!isMod(ev)) return;
                const key = (ev.key || "").toLowerCase();
                if (key === "z" && !ev.shiftKey) {
                    ev.preventDefault();
                    dotNetRef.invokeMethodAsync("OnUndoFromJs");
                } else if ((key === "z" && ev.shiftKey) || key === "y") {
                    ev.preventDefault();
                    dotNetRef.invokeMethodAsync("OnRedoFromJs");
                }
            };
            window.addEventListener("keydown", listener, true);
        },
        detach: function () {
            if (listener) {
                window.removeEventListener("keydown", listener, true);
                listener = null;
            }
            dotNetRef = null;
        },
    };
})();

window.oeShare = {
    getHash: function () {
        return (window.location.hash || "").replace(/^#/, "");
    },
    setHash: function (value) {
        const url = new URL(window.location.href);
        url.hash = value ? "#" + value : "";
        history.replaceState(null, "", url.toString());
    },
    buildShareUrl: function (encoded) {
        const url = new URL(window.location.href);
        url.hash = "#s=" + encoded;
        return url.toString();
    },
    copy: async function (text) {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            try {
                await navigator.clipboard.writeText(text);
                return true;
            } catch { /* fall through to textarea fallback */ }
        }
        const ta = document.createElement("textarea");
        ta.value = text;
        ta.style.position = "fixed";
        ta.style.opacity = "0";
        document.body.appendChild(ta);
        ta.select();
        let ok = false;
        try { ok = document.execCommand("copy"); } catch { ok = false; }
        document.body.removeChild(ta);
        return ok;
    },
};
