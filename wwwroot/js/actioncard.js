/**
 * ActionCard — Centralized notification/confirmation component
 * Replaces alert(), confirm(), prompt() across the entire system
 * Provides professional centered modals with blur backdrop
 */
(function () {
  "use strict";

  // ── Inject Styles ──
  const style = document.createElement("style");
  style.textContent = `
        .ac-overlay {
            position: fixed; inset: 0; z-index: 9999;
            display: flex; align-items: center; justify-content: center;
            background: rgba(0,0,0,0.45);
            backdrop-filter: blur(4px);
            -webkit-backdrop-filter: blur(4px);
            animation: ac-fadeIn .2s ease;
        }
        .ac-card {
            background: #fff; border-radius: 16px;
            box-shadow: 0 25px 60px rgba(0,0,0,.25);
            width: 90%; max-width: 420px;
            padding: 32px; text-align: center;
            animation: ac-slideUp .25s ease;
            position: relative;
        }
        .ac-icon {
            width: 64px; height: 64px; border-radius: 50%;
            display: flex; align-items: center; justify-content: center;
            margin: 0 auto 20px; font-size: 28px;
        }
        .ac-icon.success { background: #d1fae5; color: #059669; }
        .ac-icon.error   { background: #fee2e2; color: #dc2626; }
        .ac-icon.warning { background: #fef3c7; color: #d97706; }
        .ac-icon.info    { background: #dbeafe; color: #2563eb; }
        .ac-icon.confirm { background: #e0e7ff; color: #4338ca; }
        .ac-icon.prompt  { background: #ede9fe; color: #7c3aed; }

        .ac-title {
            font-size: 20px; font-weight: 700; color: #1f2937;
            margin-bottom: 8px;
        }
        .ac-desc {
            font-size: 14px; color: #6b7280; line-height: 1.5;
            margin-bottom: 24px;
        }
        .ac-input {
            width: 100%; padding: 10px 14px; border: 1px solid #d1d5db;
            border-radius: 8px; font-size: 14px; margin-bottom: 20px;
            outline: none; transition: border .15s;
        }
        .ac-input:focus { border-color: #6366f1; box-shadow: 0 0 0 3px rgba(99,102,241,.15); }

        .ac-actions { display: flex; gap: 12px; justify-content: center; }
        .ac-btn {
            padding: 10px 28px; border-radius: 10px; font-size: 14px;
            font-weight: 600; cursor: pointer; border: none;
            transition: all .15s;
        }
        .ac-btn:hover { transform: translateY(-1px); box-shadow: 0 4px 12px rgba(0,0,0,.15); }
        .ac-btn:active { transform: translateY(0); }

        .ac-btn.primary-success { background: #059669; color: #fff; }
        .ac-btn.primary-error   { background: #dc2626; color: #fff; }
        .ac-btn.primary-warning { background: #d97706; color: #fff; }
        .ac-btn.primary-info    { background: #2563eb; color: #fff; }
        .ac-btn.primary-confirm { background: #4338ca; color: #fff; }
        .ac-btn.primary-prompt  { background: #7c3aed; color: #fff; }
        .ac-btn.secondary {
            background: #f3f4f6; color: #374151;
        }
        .ac-btn.secondary:hover { background: #e5e7eb; }

        @keyframes ac-fadeIn { from { opacity: 0; } to { opacity: 1; } }
        @keyframes ac-slideUp { from { opacity: 0; transform: translateY(20px); } to { opacity: 1; transform: translateY(0); } }

        /* Auto-dismiss progress bar */
        .ac-progress {
            position: absolute; bottom: 0; left: 0; height: 4px;
            border-radius: 0 0 16px 16px;
            animation: ac-shrink linear forwards;
        }
        .ac-progress.success { background: #059669; }
        .ac-progress.error   { background: #dc2626; }
        .ac-progress.warning { background: #d97706; }
        .ac-progress.info    { background: #2563eb; }
        @keyframes ac-shrink { from { width: 100%; } to { width: 0%; } }
    `;
  document.head.appendChild(style);

  // ── Icon Map ──
  const iconMap = {
    success: '<i class="fas fa-check"></i>',
    error: '<i class="fas fa-times"></i>',
    warning: '<i class="fas fa-exclamation-triangle"></i>',
    info: '<i class="fas fa-info"></i>',
    confirm: '<i class="fas fa-question"></i>',
    prompt: '<i class="fas fa-pencil-alt"></i>',
  };

  // ── Core Builder ──
  function buildCard(opts) {
    const overlay = document.createElement("div");
    overlay.className = "ac-overlay";

    const type = opts.type || "info";
    let html = `<div class="ac-card">
            <div class="ac-icon ${type}">${iconMap[type] || iconMap.info}</div>
            <div class="ac-title">${opts.title || ""}</div>
            <div class="ac-desc">${opts.description || ""}</div>`;

    if (opts.input) {
      html += `<input class="ac-input" type="text" placeholder="${opts.placeholder || ""}" value="${opts.defaultValue || ""}" id="ac-prompt-input">`;
    }

    html += `<div class="ac-actions">`;
    if (opts.showCancel) {
      html += `<button class="ac-btn secondary" id="ac-cancel">${opts.cancelText || "Cancel"}</button>`;
    }
    html += `<button class="ac-btn primary-${type}" id="ac-ok">${opts.okText || "OK"}</button>`;
    html += `</div>`;

    if (opts.autoDismiss) {
      html += `<div class="ac-progress ${type}" style="animation-duration:${opts.autoDismiss}ms"></div>`;
    }

    html += `</div>`;
    overlay.innerHTML = html;
    return overlay;
  }

  function show(opts) {
    return new Promise((resolve) => {
      const overlay = buildCard(opts);
      document.body.appendChild(overlay);

      const okBtn = overlay.querySelector("#ac-ok");
      const cancelBtn = overlay.querySelector("#ac-cancel");
      const input = overlay.querySelector("#ac-prompt-input");
      let timer;

      function dismiss(value) {
        clearTimeout(timer);
        overlay.style.animation = "none";
        overlay.style.opacity = "0";
        overlay.style.transition = "opacity .15s";
        setTimeout(() => {
          overlay.remove();
          resolve(value);
        }, 150);
      }

      okBtn.addEventListener("click", () => {
        if (opts.input) dismiss(input.value);
        else dismiss(true);
      });

      if (cancelBtn) {
        cancelBtn.addEventListener("click", () =>
          dismiss(opts.input ? null : false),
        );
      }

      // Close on overlay click (not card body)
      overlay.addEventListener("click", (e) => {
        if (e.target === overlay && !opts.input && !opts.showCancel)
          dismiss(true);
      });

      // Keyboard
      overlay.addEventListener("keydown", (e) => {
        if (e.key === "Enter") {
          okBtn.click();
        }
        if (e.key === "Escape" && (opts.showCancel || !opts.input)) {
          dismiss(opts.input ? null : opts.showCancel ? false : true);
        }
      });

      if (input) input.focus();
      else okBtn.focus();

      if (opts.autoDismiss) {
        timer = setTimeout(() => dismiss(true), opts.autoDismiss);
      }
    });
  }

  // ── Public API ──
  window.ActionCard = {
    /**
     * Show a success card
     * @param {string} title
     * @param {string} description
     * @param {object} [opts] - { okText, autoDismiss }
     */
    success(title, description, opts = {}) {
      return show({ type: "success", title, description, ...opts });
    },

    /**
     * Show an error card
     */
    error(title, description, opts = {}) {
      return show({ type: "error", title, description, ...opts });
    },

    /**
     * Show a warning card
     */
    warning(title, description, opts = {}) {
      return show({ type: "warning", title, description, ...opts });
    },

    /**
     * Show an info card
     */
    info(title, description, opts = {}) {
      return show({ type: "info", title, description, ...opts });
    },

    /**
     * Show a confirm dialog with OK/Cancel
     * Returns Promise<boolean>
     */
    confirm(title, description, opts = {}) {
      return show({
        type: "confirm",
        title,
        description,
        showCancel: true,
        okText: opts.okText || "Confirm",
        cancelText: opts.cancelText || "Cancel",
        ...opts,
      });
    },

    /**
     * Show a prompt dialog with input + OK/Cancel
     * Returns Promise<string|null>
     */
    prompt(title, description, opts = {}) {
      return show({
        type: "prompt",
        title,
        description,
        showCancel: true,
        input: true,
        okText: opts.okText || "Submit",
        cancelText: opts.cancelText || "Cancel",
        ...opts,
      });
    },
  };

  // ── Auto-show TempData cards ──
  document.addEventListener("DOMContentLoaded", function () {
    const successEl = document.getElementById("ac-tempdata-success");
    const errorEl = document.getElementById("ac-tempdata-error");
    if (successEl && successEl.dataset.message) {
      ActionCard.success("Success", successEl.dataset.message, {
        autoDismiss: 3000,
      });
    }
    if (errorEl && errorEl.dataset.message) {
      ActionCard.error("Error", errorEl.dataset.message);
    }

    // Replace data-confirm forms
    document.querySelectorAll("form[data-confirm]").forEach(function (form) {
      // Remove old inline handlers
      form.removeAttribute("onsubmit");
      form.addEventListener("submit", function (e) {
        e.preventDefault();
        ActionCard.confirm(
          "Confirm Action",
          form.getAttribute("data-confirm"),
          { okText: "Yes", cancelText: "No" },
        ).then((ok) => {
          if (ok) form.submit();
        });
      });
    });
  });
})();
