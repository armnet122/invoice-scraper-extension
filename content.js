// Cross-Browser extension API polyfill initialization
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

// Load user preferences from extension storage
extApi.storage.local.get(['showToast', 'inFieldTransform', 'autoEnter', 'showFloatingInput'], (res) => {
  if (res.showToast !== undefined) isToastEnabled = res.showToast;
  if (res.inFieldTransform !== undefined) isInFieldTransformEnabled = res.inFieldTransform;
  if (res.autoEnter !== undefined) isAutoEnterEnabled = res.autoEnter;
  if (res.showFloatingInput !== undefined) isFloatingInputEnabled = res.showFloatingInput;
  
  if (isFloatingInputEnabled) {
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
      if (isFloatingInputEnabled) {
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
    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
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
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
      <polyline points="20 6 9 17 4 12"></polyline>
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

  // Restore saved position if available
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

    // Prevent dragging when clicking inside interactive input elements
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
 */
function initMinimalFloatingInput() {
  if (document.getElementById('inv-floating-container')) return;

  const container = document.createElement('div');
  container.id = 'inv-floating-container';
  container.style.cssText = `
    position: fixed;
    bottom: 20px;
    right: 20px;
    z-index: 999999;
    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
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
    <div id="inv-expanded-wrapper">
      <input type="text" id="inv-minimal-floating-input" placeholder="Paste Invoice URL..." autocomplete="off" title="Paste invoice link & press Enter" />
      <button id="inv-minimize-btn" title="Minimize into floating icon">–</button>
    </div>

    <!-- Minimized View -->
    <div id="inv-minimized-pill" style="display: none;" title="Click to expand invoice scraper">
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
        <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
        <polyline points="14 2 14 8 20 8"></polyline>
      </svg>
      <span>Scrape Invoice</span>
    </div>
  `;

  document.body.appendChild(container);

  const expandedWrapper = container.querySelector('#inv-expanded-wrapper');
  const minimizedPill = container.querySelector('#inv-minimized-pill');
  const minimizeBtn = container.querySelector('#inv-minimize-btn');
  const input = container.querySelector('#inv-minimal-floating-input');

  // Restore minimized/expanded state from storage
  extApi.storage.local.get(['widgetMinimized'], (res) => {
    if (res.widgetMinimized) {
      expandedWrapper.style.display = 'none';
      minimizedPill.style.display = 'flex';
    }
  });

  // Handle Minimize click
  minimizeBtn.addEventListener('click', (e) => {
    e.stopPropagation();
    expandedWrapper.style.display = 'none';
    minimizedPill.style.display = 'flex';
    extApi.storage.local.set({ widgetMinimized: true });
  });

  // Handle Expand click on minimized pill
  minimizedPill.addEventListener('click', () => {
    minimizedPill.style.display = 'none';
    expandedWrapper.style.display = 'flex';
    input.focus();
    extApi.storage.local.set({ widgetMinimized: false });
  });

  // Enable drag-and-drop repositioning
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

  const inputs = Array.from(document.querySelectorAll('input, textarea, select, [contenteditable="true"]'));
  for (const input of inputs) {
    if (input.id === 'inv-minimal-floating-input') continue;

    let labelText = '';
    if (input.id) {
      const label = document.querySelector(`label[for="${CSS.escape(input.id)}"]`);
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

  const specificElements = Array.from(document.querySelectorAll('[id*="invoice"], [id*="inv"], [class*="invoice"], [class*="inv-num"], [class*="inv_num"], [data-field*="invoice"]'));
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
  if (!target.matches('input, textarea, [contenteditable="true"]')) return;

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
  if (!target.matches('input, textarea, [contenteditable="true"]')) return;

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
