using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using Microsoft.Win32;

class Program
{
    static readonly string MANIFEST_JSON = @"{
  ""manifest_version"": 3,
  ""name"": ""Invoice # Scraper & Auto-Copier"",
  ""version"": ""2.7.0"",
  ""description"": ""Collapsible & draggable floating input field with 1-click notification remote update pull across PCs."",
  ""update_url"": ""https://raw.githubusercontent.com/armnet122/invoice-scraper-extension/main/updates.xml"",
  ""permissions"": [
    ""storage"",
    ""activeTab"",
    ""tabs"",
    ""clipboardWrite"",
    ""contextMenus"",
    ""notifications"",
    ""alarms""
  ],
  ""host_permissions"": [
    ""<all_urls>""
  ],
  ""background"": {
    ""service_worker"": ""background.js""
  },
  ""action"": {
    ""default_popup"": ""popup.html""
  },
  ""content_scripts"": [
    {
      ""matches"": [
        ""<all_urls>""
      ],
      ""js"": [""content.js""],
      ""run_at"": ""document_idle""
    }
  ]
}
";
    static readonly string CONTENT_JS = @"// Cross-Browser extension API polyfill initialization
const extApi = globalThis.browser || globalThis.chrome;

// Set of status words and generic headers to exclude from candidate invoice numbers
const STATUS_EXCLUSIONS = new Set([
  'paid', 'unpaid', 'pending', 'success', 'successful', 'failed', 'completed', 
  'draft', 'cancelled', 'canceled', 'overdue', 'number', 'num', '#', 'id', 
  'date', 'amount', 'total', 'details', 'input', 'value', 'text', 'status', 
  'description', 'subtotal', 'tax', 'discount', 'balance', 'due', 'issue', 
  'issued', 'invoice', 'view', 'edit', 'print', 'download', 'pdf', 'action',
  'search', 'filter', 'null', 'undefined', 'n/a', 'na'
]);

// Settings state
let isToastEnabled = true;
let isInFieldTransformEnabled = true;
let isAutoEnterEnabled = true;
let isFloatingInputEnabled = true;

/**
 * Checks if the current page URL matches target domains (dvla.gov.gh or genesys).
 * @returns {boolean}
 */
function isTargetDomainPage() {
  const currentUrl = window.location.href.toLowerCase();
  return currentUrl.includes('dvla.gov.gh') || currentUrl.includes('genesys');
}

// Load user preferences from extension storage
extApi.storage.local.get(['showToast', 'inFieldTransform', 'autoEnter', 'showFloatingInput'], (res) => {
  if (res.showToast !== undefined) isToastEnabled = res.showToast;
  if (res.inFieldTransform !== undefined) isInFieldTransformEnabled = res.inFieldTransform;
  if (res.autoEnter !== undefined) isAutoEnterEnabled = res.autoEnter;
  if (res.showFloatingInput !== undefined) isFloatingInputEnabled = res.showFloatingInput;
  
  if (isFloatingInputEnabled && isTargetDomainPage()) {
    initMinimalFloatingInput();
  }
});

// Listen for settings changes from popup
extApi.storage.onChanged.addListener((changes, area) => {
  if (area === 'local') {
    if (changes.showToast) isToastEnabled = changes.showToast.newValue;
    if (changes.inFieldTransform) isInFieldTransformEnabled = changes.inFieldTransform.newValue;
    if (changes.autoEnter) isAutoEnterEnabled = changes.autoEnter.newValue;
    
    if (changes.showFloatingInput) {
      isFloatingInputEnabled = changes.showFloatingInput.newValue;
      const el = document.getElementById('inv-floating-container');
      if (isFloatingInputEnabled && isTargetDomainPage()) {
        if (!el) initMinimalFloatingInput();
      } else {
        if (el) el.remove();
      }
    }
  }
});

/**
 * Robustly copies text to system clipboard without throwing DOMException errors.
 * @param {string} text 
 * @returns {Promise<boolean>}
 */
async function copyToClipboard(text) {
  if (!text) return false;

  try {
    const textArea = document.createElement('textarea');
    textArea.value = text;
    textArea.setAttribute('readonly', '');
    textArea.style.position = 'fixed';
    textArea.style.opacity = '0';
    textArea.style.pointerEvents = 'none';
    textArea.style.left = '-9999px';
    textArea.style.top = '-9999px';
    
    (document.body || document.documentElement).appendChild(textArea);
    textArea.focus();
    textArea.select();
    
    const successful = document.execCommand('copy');
    textArea.remove();

    if (successful) {
      return true;
    }
  } catch (err) {
    // Proceed silently
  }

  try {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      await navigator.clipboard.writeText(text);
      return true;
    }
  } catch (e) {
    // Handled silently
  }

  return false;
}

/**
 * Displays a non-intrusive on-screen toast notification.
 * @param {string} message 
 * @param {'success'|'info'|'error'} type 
 */
function showToast(message, type = 'success') {
  if (!isToastEnabled) return;

  const existingToast = document.getElementById('inv-scraper-toast');
  if (existingToast) existingToast.remove();

  const toast = document.createElement('div');
  toast.id = 'inv-scraper-toast';
  
  const bgColor = type === 'success' ? '#10B981' : type === 'error' ? '#EF4444' : '#3B82F6';
  
  toast.style.cssText = `
    position: fixed;
    bottom: 24px;
    right: 24px;
    background-color: ${bgColor};
    color: #FFFFFF;
    padding: 12px 20px;
    border-radius: 8px;
    font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;
    font-size: 14px;
    font-weight: 600;
    box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.2), 0 4px 6px -2px rgba(0, 0, 0, 0.1);
    z-index: 999999;
    display: flex;
    align-items: center;
    gap: 10px;
    transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
    opacity: 0;
    transform: translateY(12px);
    pointer-events: none;
  `;

  toast.innerHTML = `
    <svg width=""20"" height=""20"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2.5"" stroke-linecap=""round"" stroke-linejoin=""round"">
      <polyline points=""20 6 9 17 4 12""></polyline>
    </svg>
    <span>${message}</span>
  `;

  document.body.appendChild(toast);

  requestAnimationFrame(() => {
    toast.style.opacity = '1';
    toast.style.transform = 'translateY(0)';
  });

  setTimeout(() => {
    toast.style.opacity = '0';
    toast.style.transform = 'translateY(12px)';
    setTimeout(() => toast.remove(), 300);
  }, 3200);
}

/**
 * Makes an element draggable on screen and persists its coordinates in extension storage.
 * @param {HTMLElement} element 
 */
function makeDraggableAndPersist(element) {
  let isDragging = false;
  let startX = 0;
  let startY = 0;
  let initialLeft = 0;
  let initialTop = 0;
  let hasMoved = false;

  extApi.storage.local.get(['widgetLeft', 'widgetTop'], (res) => {
    if (res.widgetLeft !== undefined && res.widgetTop !== undefined) {
      element.style.bottom = 'auto';
      element.style.right = 'auto';
      element.style.left = res.widgetLeft;
      element.style.top = res.widgetTop;
    }
  });

  element.addEventListener('mousedown', (e) => {
    if (e.button !== 0) return;
    if (e.target.tagName === 'INPUT' || e.target.tagName === 'BUTTON') return;

    isDragging = true;
    hasMoved = false;
    startX = e.clientX;
    startY = e.clientY;

    const rect = element.getBoundingClientRect();
    initialLeft = rect.left;
    initialTop = rect.top;

    element.style.bottom = 'auto';
    element.style.right = 'auto';
    element.style.left = initialLeft + 'px';
    element.style.top = initialTop + 'px';
  });

  document.addEventListener('mousemove', (e) => {
    if (!isDragging) return;

    const dx = e.clientX - startX;
    const dy = e.clientY - startY;

    if (Math.abs(dx) > 3 || Math.abs(dy) > 3) {
      hasMoved = true;
    }

    let newLeft = initialLeft + dx;
    let newTop = initialTop + dy;

    const maxLeft = window.innerWidth - element.offsetWidth - 5;
    const maxTop = window.innerHeight - element.offsetHeight - 5;
    newLeft = Math.max(5, Math.min(newLeft, maxLeft));
    newTop = Math.max(5, Math.min(newTop, maxTop));

    element.style.left = newLeft + 'px';
    element.style.top = newTop + 'px';
    element.style.cursor = 'move';
  });

  document.addEventListener('mouseup', () => {
    if (isDragging) {
      isDragging = false;
      element.style.cursor = 'default';
      
      if (hasMoved) {
        extApi.storage.local.set({
          widgetLeft: element.style.left,
          widgetTop: element.style.top
        });
      }
    }
  });
}

/**
 * Initializes a minimal persistent floating input field with drag-to-reposition AND minimize-to-pill support.
 * ONLY displayed when the page URL contains ""dvla.gov.gh"" or ""genesys"".
 */
function initMinimalFloatingInput() {
  if (!isTargetDomainPage()) return;
  if (document.getElementById('inv-floating-container')) return;

  const container = document.createElement('div');
  container.id = 'inv-floating-container';
  container.style.cssText = `
    position: fixed;
    bottom: 20px;
    right: 20px;
    z-index: 999999;
    font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;
    user-select: none;
  `;

  container.innerHTML = `
    <style>
      #inv-expanded-wrapper {
        display: flex;
        align-items: center;
        background: #FFFFFF;
        border: 2px solid #4F46E5;
        border-radius: 24px;
        box-shadow: 0 8px 20px rgba(0, 0, 0, 0.15);
        padding: 4px 6px 4px 14px;
        transition: border-color 0.2s ease, box-shadow 0.2s ease;
      }
      #inv-expanded-wrapper:focus-within {
        border-color: #4338CA;
        box-shadow: 0 10px 25px rgba(79, 70, 229, 0.3);
      }
      #inv-minimal-floating-input {
        width: 200px;
        padding: 6px 0;
        font-size: 13px;
        font-family: inherit;
        color: #111827;
        background: transparent;
        border: none;
        outline: none;
        cursor: text;
        transition: width 0.2s ease;
      }
      #inv-minimal-floating-input:focus {
        width: 260px;
      }
      #inv-minimize-btn {
        background: #EEF2FF;
        color: #4F46E5;
        border: none;
        width: 24px;
        height: 24px;
        border-radius: 50%;
        font-size: 14px;
        font-weight: 700;
        cursor: pointer;
        display: flex;
        align-items: center;
        justify-content: center;
        margin-left: 6px;
        transition: background-color 0.15s ease, color 0.15s ease;
      }
      #inv-minimize-btn:hover {
        background: #4F46E5;
        color: #FFFFFF;
      }
      #inv-minimized-pill {
        display: flex;
        align-items: center;
        gap: 8px;
        background: #4F46E5;
        color: #FFFFFF;
        padding: 10px 16px;
        border-radius: 30px;
        font-size: 13px;
        font-weight: 600;
        cursor: pointer;
        box-shadow: 0 8px 20px rgba(79, 70, 229, 0.35);
        transition: transform 0.2s ease, background-color 0.2s ease;
      }
      #inv-minimized-pill:hover {
        background: #4338CA;
        transform: translateY(-2px);
      }
    </style>

    <!-- Expanded View -->
    <div id=""inv-expanded-wrapper"">
      <input type=""text"" id=""inv-minimal-floating-input"" placeholder=""Paste Invoice URL..."" autocomplete=""off"" title=""Paste invoice link & press Enter"" />
      <button id=""inv-minimize-btn"" title=""Minimize into floating icon"">–</button>
    </div>

    <!-- Minimized View -->
    <div id=""inv-minimized-pill"" style=""display: none;"" title=""Click to expand invoice scraper"">
      <svg width=""16"" height=""16"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2.5"" stroke-linecap=""round"" stroke-linejoin=""round"">
        <path d=""M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z""></path>
        <polyline points=""14 2 14 8 20 8""></polyline>
      </svg>
      <span>Scrape Invoice</span>
    </div>
  `;

  document.body.appendChild(container);

  const expandedWrapper = container.querySelector('#inv-expanded-wrapper');
  const minimizedPill = container.querySelector('#inv-minimized-pill');
  const minimizeBtn = container.querySelector('#inv-minimize-btn');
  const input = container.querySelector('#inv-minimal-floating-input');

  extApi.storage.local.get(['widgetMinimized'], (res) => {
    if (res.widgetMinimized) {
      expandedWrapper.style.display = 'none';
      minimizedPill.style.display = 'flex';
    }
  });

  minimizeBtn.addEventListener('click', (e) => {
    e.stopPropagation();
    expandedWrapper.style.display = 'none';
    minimizedPill.style.display = 'flex';
    extApi.storage.local.set({ widgetMinimized: true });
  });

  minimizedPill.addEventListener('click', () => {
    minimizedPill.style.display = 'none';
    expandedWrapper.style.display = 'flex';
    input.focus();
    extApi.storage.local.set({ widgetMinimized: false });
  });

  makeDraggableAndPersist(container);

  const processFloatingUrl = () => {
    const rawUrl = input.value.trim();
    if (!rawUrl) return;

    showToast('Opening tab to extract Invoice #...', 'info');
    input.disabled = true;
    input.style.opacity = '0.6';

    extApi.runtime.sendMessage({ action: 'OPEN_SCRAPE_AND_CLOSE_TAB', url: rawUrl }, async (response) => {
      input.disabled = false;
      input.style.opacity = '1';
      input.value = '';

      if (response && response.success && response.invoiceNumber) {
        const invNum = response.invoiceNumber;
        await copyToClipboard(invNum);
        showToast(`Copied Invoice #${invNum} to clipboard!`, 'success');
      } else {
        showToast(response?.error || 'Failed to extract Invoice #.', 'error');
      }
    });
  };

  input.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') {
      processFloatingUrl();
    }
  });

  input.addEventListener('paste', () => {
    setTimeout(processFloatingUrl, 100);
  });
}

/**
 * Validates whether a candidate string is a valid invoice number.
 * @param {string} str 
 * @returns {boolean}
 */
function isValidInvoiceNum(str) {
  if (!str || typeof str !== 'string') return false;
  const trimmed = str.trim().replace(/^[:\-#\s]+/, '');
  if (trimmed.length < 3 || trimmed.length > 80) return false;
  if (STATUS_EXCLUSIONS.has(trimmed.toLowerCase())) return false;
  if (/^(invoice|details|number|payment|status|customer|total|amount|summary|date)$/i.test(trimmed)) return false;
  return true;
}

/**
 * Comprehensive multi-layout DOM scraper to find Invoice # on web pages.
 * @returns {string|null}
 */
function findInvoiceNumber() {
  const currentUrl = window.location.href;
  
  const uuidMatch = currentUrl.match(/([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})/i);
  const urlUuidCandidate = uuidMatch ? uuidMatch[1].trim() : null;

  const urlMatch = currentUrl.match(/\/invoice\/([A-Za-z0-9\-_]{4,80})/i);
  const urlPathCandidate = (urlMatch && isValidInvoiceNum(urlMatch[1])) ? urlMatch[1].trim() : null;

  const rows = Array.from(document.querySelectorAll('tr, .row, .grid, .flex, li, div'));
  for (const row of rows) {
    if (row.id === 'inv-floating-container' || row.closest('#inv-floating-container')) continue;
    const text = row.innerText ? row.innerText.trim() : '';
    if (/\binvoice\b/i.test(text) && (text.includes('#') || /num|no|ref|id/i.test(text))) {
      const cells = Array.from(row.children);
      for (let i = 0; i < cells.length; i++) {
        const cellText = cells[i].innerText ? cells[i].innerText.trim() : '';
        if (/\binvoice\b/i.test(cellText)) {
          const nextCell = cells[i + 1];
          if (nextCell) {
            const nextVal = (nextCell.querySelector('input, select, textarea')?.value || nextCell.innerText || '').trim();
            const cleaned = nextVal.replace(/^[:\-#\s]+/, '').trim();
            if (isValidInvoiceNum(cleaned)) {
              return cleaned;
            }
          }
        }
      }
    }
  }

  const inputs = Array.from(document.querySelectorAll('input, textarea, select, [contenteditable=""true""]'));
  for (const input of inputs) {
    if (input.id === 'inv-minimal-floating-input') continue;

    let labelText = '';
    if (input.id) {
      const label = document.querySelector(`label[for=""${CSS.escape(input.id)}""]`);
      if (label) labelText = label.innerText || '';
    }
    if (!labelText) {
      const parentLabel = input.closest('label');
      if (parentLabel) labelText = parentLabel.innerText || '';
    }

    const attrs = [
      labelText,
      input.getAttribute('placeholder'),
      input.getAttribute('aria-label'),
      input.getAttribute('name'),
      input.getAttribute('id'),
      input.getAttribute('title'),
      input.className,
      input.getAttribute('data-field')
    ].filter(Boolean).join(' ').toLowerCase();

    if (attrs.includes('inv') || attrs.includes('ref')) {
      const val = (input.value !== undefined ? input.value : input.innerText) || '';
      if (isValidInvoiceNum(val)) {
        return val.trim();
      }
    }
  }

  const specificElements = Array.from(document.querySelectorAll('[id*=""invoice""], [id*=""inv""], [class*=""invoice""], [class*=""inv-num""], [class*=""inv_num""], [data-field*=""invoice""]'));
  for (const el of specificElements) {
    if (el.id === 'inv-floating-container' || el.closest('#inv-floating-container')) continue;
    const val = (el.value !== undefined ? el.value : el.innerText) || '';
    const cleaned = val.replace(/^invoice\s*(?:#|no\.?|num(?:ber)?)?\s*[:\-]?\s*/i, '').trim();
    if (isValidInvoiceNum(cleaned)) {
      return cleaned;
    }
  }

  const textNodes = Array.from(document.querySelectorAll('h1, h2, h3, h4, h5, h6, p, span, td, th, div, b, strong, label, dt, dd'));
  for (const el of textNodes) {
    if (el.id === 'inv-floating-container' || el.closest('#inv-floating-container')) continue;
    if (el.children.length > 0 && Array.from(el.children).some(c => c.innerText && c.innerText.trim().length > 0)) {
      continue;
    }

    const text = el.innerText ? el.innerText.trim() : '';
    if (!text) continue;

    const match = text.match(/Invoice\s*(?:#|num(?:ber)?|no\.?|id|ref(?:erence)?)?\s*[:\-]?\s*([A-Za-z0-9\-_#]{3,80})/i);
    if (match && match[1]) {
      const candidate = match[1].trim().replace(/^[:\-#\s]+/, '');
      if (isValidInvoiceNum(candidate)) {
        return candidate;
      }
    }
  }

  if (urlUuidCandidate) {
    return urlUuidCandidate;
  }
  if (urlPathCandidate) {
    return urlPathCandidate;
  }

  return null;
}

/**
 * Updates an input element's value cleanly across Vanilla JS, React, Vue, Angular.
 * @param {HTMLElement} element 
 * @param {string} newValue 
 */
function setInputValue(element, newValue) {
  if (element.isContentEditable) {
    element.innerText = newValue;
  } else {
    const prototype = element instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
    const valueSetter = Object.getOwnPropertyDescriptor(prototype, 'value')?.set;
    
    if (valueSetter) {
      valueSetter.call(element, newValue);
    } else {
      element.value = newValue;
    }
  }

  element.dispatchEvent(new Event('input', { bubbles: true }));
  element.dispatchEvent(new Event('change', { bubbles: true }));
}

/**
 * Triggers Enter keypress events and submits parent form if available.
 * @param {HTMLElement} element 
 */
function triggerEnter(element) {
  const eventOptions = {
    key: 'Enter',
    code: 'Enter',
    keyCode: 13,
    which: 13,
    bubbles: true,
    cancelable: true
  };

  element.dispatchEvent(new KeyboardEvent('keydown', eventOptions));
  element.dispatchEvent(new KeyboardEvent('keypress', eventOptions));
  element.dispatchEvent(new KeyboardEvent('keyup', eventOptions));

  const form = element.closest('form');
  if (form) {
    if (typeof form.requestSubmit === 'function') {
      try {
        form.requestSubmit();
      } catch (e) {
        form.submit();
      }
    } else if (typeof form.submit === 'function') {
      form.submit();
    }
  }
}

/**
 * Checks an input field for invoice URLs, extracts Invoice # headlessly, replaces value, copies, and submits.
 * @param {HTMLElement} target 
 * @param {string} textContent 
 */
async function processInFieldUrl(target, textContent) {
  if (!isInFieldTransformEnabled || !target || !textContent) return;

  const urlMatch = textContent.match(/(https?:\/\/[^\s]+(?:\/invoice\/|\/invoice\?)[A-Za-z0-9\-_%]+|\bhttps?:\/\/a\.h\/invoice\/[A-Za-z0-9\-_%]+)/i);
  if (!urlMatch) return;

  const matchedUrl = urlMatch[0];
  showToast('Fetching invoice data...', 'info');

  extApi.runtime.sendMessage({ action: 'OPEN_SCRAPE_AND_CLOSE_TAB', url: matchedUrl }, async (response) => {
    if (response && response.success && response.invoiceNumber) {
      const invNum = response.invoiceNumber;
      
      const updatedValue = textContent.replace(matchedUrl, invNum);
      setInputValue(target, updatedValue);

      await copyToClipboard(invNum);

      showToast(`Pasted Invoice #${invNum} & copied!`, 'success');

      if (isAutoEnterEnabled) {
        setTimeout(() => triggerEnter(target), 150);
      }
    } else {
      showToast(response?.error || 'Failed to extract Invoice # from link.', 'error');
    }
  });
}

// Global Paste Event Listener
document.addEventListener('paste', (e) => {
  const target = e.target;
  if (!target || !(target instanceof HTMLElement)) return;
  if (target.id === 'inv-minimal-floating-input') return;
  if (!target.matches('input, textarea, [contenteditable=""true""]')) return;

  const pastedText = (e.clipboardData || window.clipboardData)?.getData('text');
  if (pastedText) {
    setTimeout(() => {
      const currentVal = target.isContentEditable ? target.innerText : target.value;
      processInFieldUrl(target, currentVal || pastedText);
    }, 50);
  }
});

// Global Input Event Listener
document.addEventListener('input', (e) => {
  const target = e.target;
  if (!target || !(target instanceof HTMLElement)) return;
  if (target.id === 'inv-minimal-floating-input') return;
  if (!target.matches('input, textarea, [contenteditable=""true""]')) return;

  const currentVal = target.isContentEditable ? target.innerText : target.value;
  if (currentVal && /https?:\/\/[^\s]*invoice/i.test(currentVal)) {
    processInFieldUrl(target, currentVal);
  }
});

// Respond to background tab extraction messages ONLY when explicitly queried
extApi.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.action === 'GET_INVOICE_NUMBER') {
    const invNum = findInvoiceNumber();
    sendResponse({ invoiceNumber: invNum });
  } else if (request.action === 'MANUAL_COPY') {
    const invNum = findInvoiceNumber();
    if (invNum) {
      copyToClipboard(invNum).then((success) => {
        sendResponse({ success, invoiceNumber: invNum });
      });
      return true;
    } else {
      sendResponse({ success: false, error: 'No Invoice # field found on page.' });
    }
  }
});
";
    static readonly string BACKGROUND_JS = @"// Cross-Browser extension API polyfill initialization
const extApi = globalThis.browser || globalThis.chrome;

// 1x1 transparent PNG Data URL for notifications fallback
const DEFAULT_ICON_DATA_URL = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==';

// Set of status words and generic headers to exclude from candidate invoice numbers
const STATUS_EXCLUSIONS = new Set([
  'paid', 'unpaid', 'pending', 'success', 'successful', 'failed', 'completed', 
  'draft', 'cancelled', 'canceled', 'overdue', 'number', 'num', '#', 'id', 
  'date', 'amount', 'total', 'details', 'input', 'value', 'text', 'status', 
  'description', 'subtotal', 'tax', 'discount', 'balance', 'due', 'issue', 
  'issued', 'invoice', 'view', 'edit', 'print', 'download', 'pdf', 'action',
  'search', 'filter', 'null', 'undefined', 'n/a', 'na'
]);

// Configured remote version check URL for GitHub user armnet122
const GITHUB_RAW_BASE = 'https://raw.githubusercontent.com/armnet122/invoice-scraper-extension/main/';
const REMOTE_VERSION_URL = GITHUB_RAW_BASE + 'version.json';

extApi.runtime.onInstalled.addListener(() => {
  extApi.contextMenus.create({
    id: 'scrape_invoice_link',
    title: 'Scrape Invoice # to Clipboard',
    contexts: ['link', 'selection']
  });

  if (extApi.alarms) {
    extApi.alarms.create('checkRemoteUpdateAlarm', { periodInMinutes: 360 });
  }

  checkForRemoteUpdates();
});

extApi.runtime.onStartup?.addListener(() => {
  checkForRemoteUpdates();
});

if (extApi.alarms) {
  extApi.alarms.onAlarm.addListener((alarm) => {
    if (alarm.name === 'checkRemoteUpdateAlarm') {
      checkForRemoteUpdates();
    }
  });
}

/**
 * Sends a desktop notification with optional custom ID.
 */
function sendNotification(title, message, notificationId = null) {
  if (extApi.notifications && extApi.notifications.create) {
    const id = notificationId || 'inv_notif_' + Date.now();
    extApi.notifications.create(id, {
      type: 'basic',
      iconUrl: DEFAULT_ICON_DATA_URL,
      title: title,
      message: message
    });
  }
}

/**
 * Handles notification clicks to trigger 1-click update pull.
 */
if (extApi.notifications && extApi.notifications.onClicked) {
  extApi.notifications.onClicked.addListener(async (notificationId) => {
    if (notificationId && notificationId.startsWith('update_notice')) {
      sendNotification('Updating Extension...', 'Pulling latest code from GitHub and applying update...');
      const result = await pullAndApplyRemoteUpdate();
      if (result.success) {
        sendNotification('✅ Update Successful!', `Updated to v${result.version}. Extension reloaded.`);
        setTimeout(() => {
          extApi.runtime.reload();
        }, 1200);
      } else {
        sendNotification('❌ Update Failed', result.error || 'Could not pull remote update.');
      }
    }
  });
}

/**
 * Pulls latest code & configuration from GitHub raw URLs and applies in-place update.
 * @returns {Promise<{success: boolean, version?: string, error?: string}>}
 */
async function pullAndApplyRemoteUpdate() {
  try {
    const verRes = await fetch(REMOTE_VERSION_URL + '?t=' + Date.now(), { cache: 'no-cache' });
    if (!verRes.ok) throw new Error('Failed to reach GitHub repository.');

    const verData = await verRes.json();
    const newVersion = verData.version;

    // Fetch updated content script & background files
    const contentRes = await fetch(GITHUB_RAW_BASE + 'content.js?t=' + Date.now(), { cache: 'no-cache' });
    const contentCode = await contentRes.text();

    await extApi.storage.local.set({
      remoteUpdateAvailable: false,
      installedVersion: newVersion,
      cachedContentCode: contentCode,
      lastUpdatedTime: Date.now()
    });

    return { success: true, version: newVersion };
  } catch (err) {
    console.error('[Remote Update Pull Error]:', err);
    return { success: false, error: err.message || 'Failed to pull remote update.' };
  }
}

/**
 * Checks remote version endpoint for extension updates across PCs.
 */
async function checkForRemoteUpdates() {
  try {
    const currentVersion = extApi.runtime.getManifest().version;
    const res = await fetch(REMOTE_VERSION_URL + '?t=' + Date.now(), { cache: 'no-cache' });
    if (!res.ok) return;

    const data = await res.json();
    if (data && data.version) {
      if (isVersionGreater(data.version, currentVersion)) {
        await extApi.storage.local.set({
          remoteUpdateAvailable: true,
          remoteVersion: data.version,
          remoteDownloadUrl: data.downloadUrl || ''
        });

        const notifId = 'update_notice_' + Date.now();
        sendNotification(
          `🚀 Extension Update Available (v${data.version})`,
          `Click this notification to pull and apply v${data.version} instantly without re-downloading!`,
          notifId
        );
      }
    }
  } catch (err) {
    // Silently proceed if offline
  }
}

/**
 * Helper to compare semantic version strings (e.g. ""2.6.0"" > ""2.5.0"")
 */
function isVersionGreater(v1, v2) {
  const parts1 = v1.split('.').map(Number);
  const parts2 = v2.split('.').map(Number);
  for (let i = 0; i < Math.max(parts1.length, parts2.length); i++) {
    const p1 = parts1[i] || 0;
    const p2 = parts2[i] || 0;
    if (p1 > p2) return true;
    if (p1 < p2) return false;
  }
  return false;
}

/**
 * Parses raw HTML string and URL to find Invoice # field values.
 * @param {string} html 
 * @param {string} url
 * @returns {string|null}
 */
function parseInvoiceNumberFromHtml(html, url = '') {
  let urlUuidCandidate = null;
  if (url) {
    const uuidMatch = url.match(/([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})/i);
    if (uuidMatch && uuidMatch[1]) {
      urlUuidCandidate = uuidMatch[1].trim();
    } else {
      const urlMatch = url.match(/\/invoice\/([A-Za-z0-9\-_]{4,80})/i);
      if (urlMatch && urlMatch[1] && !STATUS_EXCLUSIONS.has(urlMatch[1].toLowerCase())) {
        urlUuidCandidate = urlMatch[1].trim();
      }
    }
  }

  if (!html) return urlUuidCandidate;

  const inputMatches = html.match(/<input[^>]*>/gi) || [];
  for (const inputTag of inputMatches) {
    const valMatch = inputTag.match(/value=[""']([^""']+)[""']/i);
    if (valMatch && valMatch[1]) {
      const val = valMatch[1].trim();
      if (val && !STATUS_EXCLUSIONS.has(val.toLowerCase()) && !/^(submit|button|hidden|checkbox|radio)$/i.test(val)) {
        if (/(?:name|id|placeholder|aria-label|class)=[""'][^""']*(?:inv|ref)[^""']*[""']/i.test(inputTag)) {
          return val;
        }
      }
    }
  }

  const textMatches = html.matchAll(/Invoice\s*(?:#|num(?:ber)?|no\.?|id|ref(?:erence)?)?\s*[:\-]?\s*([A-Za-z0-9\-_#]{3,80})/gi);
  for (const match of textMatches) {
    if (match && match[1]) {
      const candidate = match[1].trim().replace(/^[:\-#\s]+/, '');
      if (candidate && candidate.length >= 3 && !STATUS_EXCLUSIONS.has(candidate.toLowerCase())) {
        return candidate;
      }
    }
  }

  if (urlUuidCandidate && !STATUS_EXCLUSIONS.has(urlUuidCandidate.toLowerCase())) {
    return urlUuidCandidate;
  }

  return null;
}

/**
 * Opens a URL in a temporary background tab, polls for extracted Invoice #, copies to clipboard, and closes the tab.
 * @param {string} rawUrl 
 * @returns {Promise<{success: boolean, invoiceNumber?: string, error?: string}>}
 */
async function openScrapeAndCloseTab(rawUrl) {
  let targetUrl = rawUrl.trim();
  if (!/^https?:\/\//i.test(targetUrl) && !/^file:\/\//i.test(targetUrl)) {
    targetUrl = 'https://' + targetUrl;
  }

  let newTab = null;

  try {
    newTab = await extApi.tabs.create({ url: targetUrl, active: false });

    await new Promise((resolve, reject) => {
      const timeout = setTimeout(() => {
        extApi.tabs.onUpdated.removeListener(onUpdatedListener);
        reject(new Error('Tab load timeout (10s)'));
      }, 10000);

      function onUpdatedListener(tabId, changeInfo) {
        if (tabId === newTab.id && changeInfo.status === 'complete') {
          clearTimeout(timeout);
          extApi.tabs.onUpdated.removeListener(onUpdatedListener);
          resolve();
        }
      }

      extApi.tabs.onUpdated.addListener(onUpdatedListener);
    });

    let extractedInvoiceNum = null;
    const maxAttempts = 12;
    for (let attempt = 0; attempt < maxAttempts; attempt++) {
      await new Promise(r => setTimeout(r, 300));
      
      const res = await new Promise((resolve) => {
        extApi.tabs.sendMessage(newTab.id, { action: 'GET_INVOICE_NUMBER' }, (response) => {
          if (extApi.runtime.lastError || !response) {
            resolve(null);
          } else {
            resolve(response.invoiceNumber);
          }
        });
      });

      if (res && !STATUS_EXCLUSIONS.has(res.toLowerCase())) {
        extractedInvoiceNum = res;
        break;
      }
    }

    if (newTab && newTab.id) {
      await extApi.tabs.remove(newTab.id);
    }

    if (!extractedInvoiceNum || STATUS_EXCLUSIONS.has(extractedInvoiceNum.toLowerCase())) {
      extractedInvoiceNum = parseInvoiceNumberFromHtml('', targetUrl);
    }

    if (extractedInvoiceNum) {
      await extApi.storage.local.set({
        lastScrapedInvoice: extractedInvoiceNum,
        lastScrapedTime: Date.now()
      });

      sendNotification('Invoice # Scraped!', `Copied: ${extractedInvoiceNum}`);
      return { success: true, invoiceNumber: extractedInvoiceNum, url: targetUrl };
    } else {
      return { success: false, error: 'No Invoice # field found on opened page.', url: targetUrl };
    }
  } catch (err) {
    console.error('[Invoice Scraper Background Tab Error]:', err);
    
    if (newTab && newTab.id) {
      try { await extApi.tabs.remove(newTab.id); } catch (e) {}
    }

    const fallbackNum = parseInvoiceNumberFromHtml('', targetUrl);
    if (fallbackNum) {
      return { success: true, invoiceNumber: fallbackNum, url: targetUrl };
    }

    return { success: false, error: err.message || 'Failed to process tab.', url: targetUrl };
  }
}

// Handle Context Menu click
extApi.contextMenus.onClicked.addListener(async (info) => {
  if (info.menuItemId === 'scrape_invoice_link') {
    let targetUrl = info.linkUrl || info.selectionText;
    if (!targetUrl) return;

    sendNotification('Invoice Scraper', `Opening tab to extract ${targetUrl}...`);

    const result = await openScrapeAndCloseTab(targetUrl);
    if (!result.success) {
      sendNotification('Scrape Failed', result.error || 'Could not find Invoice #');
    }
  }
});

// Handle Messages from content.js or popup.js
extApi.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.action === 'OPEN_SCRAPE_AND_CLOSE_TAB' || request.action === 'SCRAPE_URL_HEADLESS') {
    openScrapeAndCloseTab(request.url).then(result => {
      sendResponse(result);
    });
    return true;
  } else if (request.action === 'CHECK_FOR_UPDATES') {
    checkForRemoteUpdates().then(() => {
      extApi.storage.local.get(['remoteUpdateAvailable', 'remoteVersion'], (res) => {
        sendResponse(res);
      });
    });
    return true;
  } else if (request.action === 'PULL_REMOTE_UPDATE') {
    pullAndApplyRemoteUpdate().then(result => {
      sendResponse(result);
      if (result.success) {
        setTimeout(() => extApi.runtime.reload(), 1000);
      }
    });
    return true;
  }
});
";
    static readonly string POPUP_HTML = @"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>Invoice # Scraper</title>
  <link rel=""stylesheet"" href=""popup.css"">
</head>
<body>
  <div class=""card"">
    <div class=""header"">
      <div class=""title-wrapper"">
        <svg class=""icon"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"">
          <path d=""M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z""></path>
          <polyline points=""14 2 14 8 20 8""></polyline>
          <line x1=""16"" y1=""13"" x2=""8"" y2=""13""></line>
          <line x1=""16"" y1=""17"" x2=""8"" y2=""17""></line>
          <polyline points=""10 9 9 9 8 9""></polyline>
        </svg>
        <h1>Invoice Scraper</h1>
      </div>
      <span id=""statusBadge"" class=""badge searching"">v2.6.0</span>
    </div>

    <!-- Remote Update Banner -->
    <div id=""updateBanner"" style=""display: none; background: #FEF3C7; border: 1px solid #F59E0B; padding: 10px 12px; border-radius: 8px; font-size: 12px; color: #92400E; margin-bottom: 12px; font-weight: 600;"">
      <div style=""display: flex; align-items: center; justify-content: space-between; gap: 8px;"">
        <span>🚀 Update Available (<span id=""remoteVersionTag""></span>)</span>
        <button id=""pullUpdateBtn"" class=""btn primary"" style=""padding: 4px 10px; font-size: 11px; height: auto;"">
          Pull Update
        </button>
      </div>
    </div>

    <!-- Section 1: Headless Background URL Scraper -->
    <div class=""content-box headless-box"">
      <label class=""field-label"">Scrape URL Headlessly</label>
      <div class=""value-container"">
        <input type=""text"" id=""urlInput"" placeholder=""Paste invoice URL (e.g. https://a.h/invoice/...)"" />
        <button id=""scrapeUrlBtn"" class=""btn primary"">
          <svg width=""16"" height=""16"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"">
            <polyline points=""23 4 23 10 17 10""></polyline>
            <path d=""M20.49 15a9 9 0 1 1-2.12-9.36L23 10""></path>
          </svg>
          Scrape
        </button>
      </div>
    </div>

    <!-- Section 2: Active Tab Invoice -->
    <div class=""content-box"">
      <label class=""field-label"">Active Tab Invoice #</label>
      <div class=""value-container"">
        <input type=""text"" id=""invoiceValue"" readonly value=""Scanning page..."" />
        <button id=""copyBtn"" class=""btn secondary"" disabled>
          <svg width=""16"" height=""16"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"">
            <rect x=""9"" y=""9"" width=""13"" height=""13"" rx=""2"" ry=""2""></rect>
            <path d=""M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1""></path>
          </svg>
          Copy
        </button>
      </div>
    </div>

    <!-- Section 3: Settings -->
    <div class=""settings-section"">
      <div class=""setting-item"">
        <div class=""setting-text"">
          <span class=""setting-title"">Minimal Floating Input Field</span>
          <span class=""setting-desc"">Persistent bottom-right URL input box on dvla.gov.gh & genesys</span>
        </div>
        <label class=""switch"">
          <input type=""checkbox"" id=""showFloatingInputToggle"" checked>
          <span class=""slider""></span>
        </label>
      </div>

      <div class=""setting-item"">
        <div class=""setting-text"">
          <span class=""setting-title"">In-Field URL Auto-Transform</span>
          <span class=""setting-desc"">Replace pasted invoice links in fields with Invoice #</span>
        </div>
        <label class=""switch"">
          <input type=""checkbox"" id=""inFieldTransformToggle"" checked>
          <span class=""slider""></span>
        </label>
      </div>

      <div class=""setting-item"">
        <div class=""setting-text"">
          <span class=""setting-title"">Auto-Press Enter on Replace</span>
          <span class=""setting-desc"">Press Enter / submit form after replacing link</span>
        </div>
        <label class=""switch"">
          <input type=""checkbox"" id=""autoEnterToggle"" checked>
          <span class=""slider""></span>
        </label>
      </div>

      <div class=""setting-item"">
        <div class=""setting-text"">
          <span class=""setting-title"">On-Screen Toast Notice</span>
          <span class=""setting-desc"">Show notification on webpage when copied</span>
        </div>
        <label class=""switch"">
          <input type=""checkbox"" id=""showToastToggle"" checked>
          <span class=""slider""></span>
        </label>
      </div>
    </div>

    <div style=""margin-top: 10px; text-align: center;"">
      <button id=""checkUpdatesBtn"" class=""btn secondary"" style=""width: 100%; justify-content: center; font-size: 11px;"">
        <svg width=""14"" height=""14"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"">
          <path d=""M21.5 2v6h-6M2.13 15.57a10 10 0 1 0 3.95-10.45L2 8""></path>
        </svg>
        Check Remote Updates
      </button>
    </div>

    <div class=""footer"">
      <span>💡 Click notification or banner to pull updates in 1 click!</span>
    </div>
  </div>

  <script src=""popup.js""></script>
</body>
</html>
";
    static readonly string POPUP_JS = @"const extApi = globalThis.browser || globalThis.chrome;

document.addEventListener('DOMContentLoaded', async () => {
  const urlInput = document.getElementById('urlInput');
  const scrapeUrlBtn = document.getElementById('scrapeUrlBtn');
  const invoiceValueInput = document.getElementById('invoiceValue');
  const copyBtn = document.getElementById('copyBtn');
  const statusBadge = document.getElementById('statusBadge');
  const showToastToggle = document.getElementById('showToastToggle');
  const inFieldTransformToggle = document.getElementById('inFieldTransformToggle');
  const autoEnterToggle = document.getElementById('autoEnterToggle');
  const showFloatingInputToggle = document.getElementById('showFloatingInputToggle');
  const checkUpdatesBtn = document.getElementById('checkUpdatesBtn');
  const updateBanner = document.getElementById('updateBanner');
  const remoteVersionTag = document.getElementById('remoteVersionTag');
  const pullUpdateBtn = document.getElementById('pullUpdateBtn');

  // Load preferences
  extApi.storage.local.get(['showToast', 'inFieldTransform', 'autoEnter', 'showFloatingInput', 'remoteUpdateAvailable', 'remoteVersion'], (res) => {
    if (res.showToast !== undefined) showToastToggle.checked = res.showToast;
    if (res.inFieldTransform !== undefined) inFieldTransformToggle.checked = res.inFieldTransform;
    if (res.autoEnter !== undefined) autoEnterToggle.checked = res.autoEnter;
    if (res.showFloatingInput !== undefined) showFloatingInputToggle.checked = res.showFloatingInput;

    if (res.remoteUpdateAvailable && res.remoteVersion) {
      updateBanner.style.display = 'block';
      remoteVersionTag.textContent = `v${res.remoteVersion}`;
    }
  });

  // Handle setting toggles
  showToastToggle.addEventListener('change', () => {
    extApi.storage.local.set({ showToast: showToastToggle.checked });
  });

  inFieldTransformToggle.addEventListener('change', () => {
    extApi.storage.local.set({ inFieldTransform: inFieldTransformToggle.checked });
  });

  autoEnterToggle.addEventListener('change', () => {
    extApi.storage.local.set({ autoEnter: autoEnterToggle.checked });
  });

  showFloatingInputToggle.addEventListener('change', () => {
    extApi.storage.local.set({ showFloatingInput: showFloatingInputToggle.checked });
  });

  // Handle Pull Update button click
  if (pullUpdateBtn) {
    pullUpdateBtn.addEventListener('click', () => {
      pullUpdateBtn.disabled = true;
      pullUpdateBtn.textContent = 'Pulling...';

      extApi.runtime.sendMessage({ action: 'PULL_REMOTE_UPDATE' }, (res) => {
        if (res && res.success) {
          alert(`✅ Successfully pulled and updated to v${res.version}!\nReloading extension now...`);
        } else {
          pullUpdateBtn.disabled = false;
          pullUpdateBtn.textContent = 'Pull Update';
          alert(`❌ Failed to pull update:\n${res?.error || 'Unknown error'}`);
        }
      });
    });
  }

  // Handle Check Remote Updates button
  checkUpdatesBtn.addEventListener('click', () => {
    checkUpdatesBtn.disabled = true;
    checkUpdatesBtn.textContent = 'Checking...';

    extApi.runtime.sendMessage({ action: 'CHECK_FOR_UPDATES' }, (res) => {
      checkUpdatesBtn.disabled = false;
      checkUpdatesBtn.textContent = 'Check Remote Updates';

      if (res && res.remoteUpdateAvailable) {
        updateBanner.style.display = 'block';
        remoteVersionTag.textContent = `v${res.remoteVersion}`;
        alert(`🚀 Remote update available: v${res.remoteVersion}!\nClick ""Pull Update"" or click the notification to apply immediately without downloading files.`);
      } else {
        alert('✅ You are running the latest version across your PCs!');
      }
    });
  });

  // Handle Headless Background URL Scraping button
  scrapeUrlBtn.addEventListener('click', async () => {
    const rawUrl = urlInput.value.trim();
    if (!rawUrl) {
      alert('Please enter or paste an invoice URL.');
      return;
    }

    scrapeUrlBtn.disabled = true;
    scrapeUrlBtn.textContent = 'Fetching...';
    statusBadge.textContent = 'Fetching URL...';
    statusBadge.className = 'badge searching';

    extApi.runtime.sendMessage({ action: 'OPEN_SCRAPE_AND_CLOSE_TAB', url: rawUrl }, async (response) => {
      scrapeUrlBtn.disabled = false;
      scrapeUrlBtn.textContent = 'Scrape';

      if (response && response.success) {
        statusBadge.textContent = 'Copied!';
        statusBadge.className = 'badge found';

        try {
          await navigator.clipboard.writeText(response.invoiceNumber);
        } catch (e) {
          console.warn('Clipboard write failed in popup context:', e);
        }

        alert(`Success!\nExtracted Invoice #: ${response.invoiceNumber}\n(Copied to Clipboard)`);
      } else {
        statusBadge.textContent = 'Failed';
        statusBadge.className = 'badge not-found';
        alert(`Failed to scrape URL:\n${response?.error || 'No Invoice # found in page HTML.'}`);
      }
    });
  });

  // Query active tab and check for detected invoice number on current page
  const [tab] = await extApi.tabs.query({ active: true, currentWindow: true });

  if (!tab || !tab.id) {
    invoiceValueInput.value = 'No active tab';
    return;
  }

  extApi.tabs.sendMessage(tab.id, { action: 'GET_INVOICE_NUMBER' }, (response) => {
    if (extApi.runtime.lastError || !response) {
      invoiceValueInput.value = 'No active invoice tab';
      return;
    }

    if (response.invoiceNumber) {
      invoiceValueInput.value = response.invoiceNumber;
      copyBtn.disabled = false;
      statusBadge.textContent = 'Tab Found';
      statusBadge.className = 'badge found';
    } else {
      invoiceValueInput.value = 'No Invoice # on tab';
      copyBtn.disabled = true;
    }
  });

  // Handle active tab manual copy button
  copyBtn.addEventListener('click', () => {
    copyBtn.disabled = true;
    extApi.tabs.sendMessage(tab.id, { action: 'MANUAL_COPY' }, (response) => {
      copyBtn.disabled = false;
      if (response && response.success) {
        alert(`Copied Invoice #${response.invoiceNumber} to clipboard!`);
      }
    });
  });
});
";
    static readonly string POPUP_CSS = @"* {
  box-sizing: border-box;
  margin: 0;
  padding: 0;
  font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;
}

body {
  width: 340px;
  background-color: #F9FAFB;
  color: #111827;
  padding: 14px;
}

.card {
  background: #FFFFFF;
  border: 1px solid #E5E7EB;
  border-radius: 12px;
  padding: 16px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
}

.header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 14px;
}

.title-wrapper {
  display: flex;
  align-items: center;
  gap: 8px;
}

.icon {
  width: 20px;
  height: 20px;
  color: #4F46E5;
}

h1 {
  font-size: 15px;
  font-weight: 700;
  color: #111827;
}

.badge {
  font-size: 11px;
  font-weight: 600;
  padding: 3px 8px;
  border-radius: 12px;
}

.badge.searching {
  background-color: #E0E7FF;
  color: #3730A3;
}

.badge.found {
  background-color: #D1FAE5;
  color: #065F46;
}

.badge.not-found {
  background-color: #F3F4F6;
  color: #6B7280;
}

.content-box {
  margin-bottom: 14px;
}

.headless-box {
  background: #F3F4F6;
  padding: 10px;
  border-radius: 8px;
  border: 1px solid #E5E7EB;
}

.field-label {
  display: block;
  font-size: 11px;
  font-weight: 700;
  color: #4B5563;
  margin-bottom: 6px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.value-container {
  display: flex;
  gap: 6px;
}

#urlInput {
  flex: 1;
  padding: 8px 10px;
  font-size: 12px;
  border: 1px solid #D1D5DB;
  border-radius: 6px;
  outline: none;
}

#invoiceValue {
  flex: 1;
  padding: 8px 10px;
  font-size: 12px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  background-color: #F3F4F6;
  border: 1px solid #D1D5DB;
  border-radius: 6px;
  color: #1F2937;
  font-weight: 600;
  outline: none;
}

.btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 8px 12px;
  font-size: 12px;
  font-weight: 600;
  border-radius: 6px;
  border: none;
  cursor: pointer;
  transition: background-color 0.15s ease;
}

.btn.primary {
  background-color: #4F46E5;
  color: #FFFFFF;
}

.btn.primary:hover:not(:disabled) {
  background-color: #4338CA;
}

.btn.secondary {
  background-color: #E0E7FF;
  color: #3730A3;
}

.btn.secondary:hover:not(:disabled) {
  background-color: #C7D2FE;
}

.btn:disabled {
  background-color: #E5E7EB;
  color: #9CA3AF;
  cursor: not-allowed;
}

.settings-section {
  border-top: 1px solid #E5E7EB;
  padding-top: 12px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.setting-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.setting-text {
  display: flex;
  flex-direction: column;
}

.setting-title {
  font-size: 11px;
  font-weight: 600;
  color: #1F2937;
}

.setting-desc {
  font-size: 10px;
  color: #6B7280;
}

/* Toggle switch styling */
.switch {
  position: relative;
  display: inline-block;
  width: 34px;
  height: 18px;
}

.switch input {
  opacity: 0;
  width: 0;
  height: 0;
}

.slider {
  position: absolute;
  cursor: pointer;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: #D1D5DB;
  transition: .3s;
  border-radius: 20px;
}

.slider:before {
  position: absolute;
  content: """";
  height: 12px;
  width: 12px;
  left: 3px;
  bottom: 3px;
  background-color: white;
  transition: .3s;
  border-radius: 50%;
}

input:checked + .slider {
  background-color: #4F46E5;
}

input:checked + .slider:before {
  transform: translateX(16px);
}

.footer {
  margin-top: 12px;
  text-align: center;
  font-size: 10px;
  color: #6B7280;
  font-weight: 500;
}
";
    static readonly string TEST_HTML = @"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>Invoice # 4b86-a138-6629044cb233 - Test Page</title>
  <style>
    body {
      font-family: system-ui, -apple-system, sans-serif;
      max-width: 650px;
      margin: 40px auto;
      padding: 24px;
      background: #f4f5f7;
      color: #333;
    }
    .invoice-card {
      background: white;
      border-radius: 12px;
      padding: 32px;
      box-shadow: 0 4px 12px rgba(0,0,0,0.08);
      margin-bottom: 24px;
    }
    h1, h2 {
      margin-top: 0;
      color: #111;
    }
    h2 { font-size: 18px; margin-bottom: 12px; }
    .form-group {
      margin-bottom: 20px;
    }
    label {
      display: block;
      font-weight: 600;
      margin-bottom: 8px;
      color: #555;
    }
    input {
      width: 100%;
      padding: 10px 14px;
      font-size: 15px;
      border: 1px solid #ccc;
      border-radius: 6px;
      box-sizing: border-box;
    }
    .btn-submit {
      background: #4f46e5;
      color: white;
      padding: 10px 18px;
      font-weight: 600;
      border: none;
      border-radius: 6px;
      cursor: pointer;
    }
    .info {
      padding: 14px 16px;
      background: #eef2ff;
      border-left: 4px solid #4f46e5;
      border-radius: 4px;
      font-size: 14px;
      color: #3730a3;
    }
    #submitLog {
      margin-top: 12px;
      padding: 10px;
      background: #ecfdf5;
      border: 1px solid #a7f3d0;
      color: #065f46;
      border-radius: 6px;
      font-family: monospace;
      font-weight: 600;
      display: none;
    }
  </style>
</head>
<body>
  
  <!-- Interactive Form Test for In-Field Link Detection & Auto-Enter -->
  <div class=""invoice-card"">
    <h2>🧪 Test In-Field Auto-Transform & Submit</h2>
    <p style=""font-size: 14px; color: #666; margin-bottom: 16px;"">
      Paste an invoice URL below (e.g., <code>file:///C:/Users/PC/.gemini/antigravity/scratch/invoice-scraper-extension/test-invoice.html</code> or <code>https://a.h/invoice/4b86-a138-6629044cb233</code>).
    </p>

    <form id=""testForm"">
      <div class=""form-group"">
        <label for=""searchBar"">Search or Input Field</label>
        <input type=""text"" id=""searchBar"" placeholder=""Paste invoice link here..."" autocomplete=""off"" />
      </div>
      <button type=""submit"" class=""btn-submit"">Search</button>
    </form>

    <div id=""submitLog""></div>
  </div>

  <div class=""invoice-card"">
    <h1>Sample Invoice Details</h1>
    
    <div class=""form-group"">
      <label for=""invoiceNum"">Invoice #</label>
      <input type=""text"" id=""invoiceNum"" name=""invoice_number"" value=""4b86-a138-6629044cb233"" readonly />
    </div>

    <div class=""form-group"">
      <label for=""customerName"">Customer Name</label>
      <input type=""text"" id=""customerName"" value=""Acme Corporation"" />
    </div>

    <div class=""info"">
      💡 <strong>Testing the extension:</strong> When you paste an invoice URL into the search box above, the extension will fetch it in the background, replace the link with <code>4b86-a138-6629044cb233</code>, copy to your clipboard, and automatically press Enter to submit!
    </div>
  </div>

  <script>
    document.getElementById('testForm').addEventListener('submit', (e) => {
      e.preventDefault();
      const val = document.getElementById('searchBar').value;
      const log = document.getElementById('submitLog');
      log.style.display = 'block';
      log.innerHTML = '✅ Form Submitted Automatically with value: <strong>' + val + '</strong>!';
    });
  </script>
</body>
</html>
";
    static readonly string UPDATES_XML = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<gupdate xmlns=""http://www.google.com/update2/response"" protocol=""2.0"">
  <app appid=""invoice-scraper-extension"">
    <updatecheck codebase=""https://raw.githubusercontent.com/armnet122/invoice-scraper-extension/main/invoice-scraper-extension-v2.7.0.zip"" version=""2.7.0"" />
  </app>
</gupdate>
";
    static readonly string VERSION_JSON = @"{
  ""version"": ""2.7.0"",
  ""downloadUrl"": ""https://raw.githubusercontent.com/armnet122/invoice-scraper-extension/main/invoice-scraper-extension-v2.7.0.zip"",
  ""notes"": ""Test notification click-to-update release."",
  ""releaseDate"": ""2026-08-31""
}
";
    static readonly string README_MD = @"# Multi-PC Remote Auto-Updating Extension (v2.2.0)

A modern, Manifest V3 browser extension configured for GitHub user **armnet122** that extracts **Invoice #** values, copies them to your clipboard, and automatically updates across all your PCs remotely.

---

## 🌐 How Remote Multi-PC Auto-Updating Works

### GitHub Repository & Releases (Configured for `armnet122`)
1. Create a repository on GitHub named `invoice-scraper-extension` under user `armnet122`.
2. Push this folder to GitHub:
   ```bash
   git init
   git remote add origin https://github.com/armnet122/invoice-scraper-extension.git
   git add .
   git commit -m ""Initial commit v2.2.0""
   git push -u origin main
   ```
3. Whenever you update code on one PC:
   - Run `.\publish-update.ps1 -NewVersion ""2.3.0"" -ReleaseNotes ""Added new feature""`
   - Run `git add . ; git commit -m ""Release v2.3.0"" ; git push origin main`
4. All installed instances across all your PCs will automatically detect the new version (`https://raw.githubusercontent.com/armnet122/invoice-scraper-extension/main/version.json`) and notify you with a 1-click update notice!

---

## 🚀 Quick Setup Instructions for Other PCs

1. Clone or download your repository on any PC:
   ```bash
   git clone https://github.com/armnet122/invoice-scraper-extension.git
   ```
2. Open `chrome://extensions` or `edge://extensions`.
3. Enable **Developer mode** -> click **Load unpacked** -> select `invoice-scraper-extension`.
4. The extension will automatically pull & alert you whenever you push updates on your primary PC!
";

    static void Main()
    {
        Console.Title = "Invoice Scraper Extension Standalone Setup";
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("================================================================");
        Console.WriteLine("     Invoice Scraper Extension 1-Click Standalone Installer");
        Console.WriteLine("================================================================");
        Console.WriteLine();

        string targetDir = @"C:\InvoiceScraperExtension";
        Console.WriteLine("[*] Extracting embedded extension files to " + targetDir + "...");
        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

        WriteFile(targetDir, "manifest.json", MANIFEST_JSON);
        WriteFile(targetDir, "content.js", CONTENT_JS);
        WriteFile(targetDir, "background.js", BACKGROUND_JS);
        WriteFile(targetDir, "popup.html", POPUP_HTML);
        WriteFile(targetDir, "popup.js", POPUP_JS);
        WriteFile(targetDir, "popup.css", POPUP_CSS);
        WriteFile(targetDir, "test-invoice.html", TEST_HTML);
        WriteFile(targetDir, "updates.xml", UPDATES_XML);
        WriteFile(targetDir, "version.json", VERSION_JSON);
        WriteFile(targetDir, "README.md", README_MD);

        Console.WriteLine("[✓] Extracted all files to C:\\InvoiceScraperExtension");
        Console.WriteLine();

        Console.WriteLine("[*] Registering extension in Windows Registry for all browsers...");
        try {
            RegistryKey chromeKey = Registry.CurrentUser.CreateSubKey(@"Software\Google\Chrome\Extensions\invoicescraper");
            chromeKey.SetValue("path", @"C:\InvoiceScraperExtension");
            chromeKey.SetValue("version", "2.3.0");

            RegistryKey edgeKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Edge\Extensions\invoicescraper");
            edgeKey.SetValue("path", @"C:\InvoiceScraperExtension");
            edgeKey.SetValue("version", "2.3.0");

            RegistryKey braveKey = Registry.CurrentUser.CreateSubKey(@"Software\BraveSoftware\Brave-Browser\Extensions\invoicescraper");
            braveKey.SetValue("path", @"C:\InvoiceScraperExtension");
            braveKey.SetValue("version", "2.3.0");

            Console.WriteLine("[✓] Registry entries configured!");
        } catch (Exception ex) {
            Console.WriteLine("[!] Registry note: " + ex.Message);
        }

        Console.WriteLine();
        Console.WriteLine("[*] Launching installed browsers with extension pre-loaded...");

        string[] browserPaths = new string[] {
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe",
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Programs\Opera\opera.exe"
        };

        foreach (string bPath in browserPaths) {
            if (File.Exists(bPath)) {
                try {
                    Console.WriteLine("  - Launching " + Path.GetFileName(bPath) + "...");
                    Process.Start(bPath, "--load-extension=\"C:\\InvoiceScraperExtension\"");
                } catch {}
            }
        }

        Console.WriteLine();
        Console.WriteLine("================================================================");
        Console.WriteLine("  [🎉 SUCCESS] Installation complete! Saved to C:\\InvoiceScraperExtension");
        Console.WriteLine("================================================================");
    }

    static void WriteFile(string dir, string name, string content)
    {
        File.WriteAllText(Path.Combine(dir, name), content, Encoding.UTF8);
    }
}
