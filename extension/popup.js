const extApi = globalThis.browser || globalThis.chrome;

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

  // Load preferences
  extApi.storage.local.get(['showToast', 'inFieldTransform', 'autoEnter', 'showFloatingInput'], (res) => {
    if (res.showToast !== undefined) showToastToggle.checked = res.showToast;
    if (res.inFieldTransform !== undefined) inFieldTransformToggle.checked = res.inFieldTransform;
    if (res.autoEnter !== undefined) autoEnterToggle.checked = res.autoEnter;
    if (res.showFloatingInput !== undefined) showFloatingInputToggle.checked = res.showFloatingInput;
  });

  // Domains from the tray app that still need browser permission
  const pendingBox = document.getElementById('pendingBox');
  extApi.storage.local.get('pendingDomains', ({ pendingDomains = [] }) => {
    for (const d of pendingDomains) {
      const btn = document.createElement('button');
      btn.className = 'btn primary';
      btn.textContent = `Allow ${d}`;
      btn.style.cssText = 'display:block;margin-top:6px;width:100%';
      btn.addEventListener('click', () => {
        extApi.permissions.request({ origins: [`*://${d}/*`, `*://*.${d}/*`] }, (ok) => { if (ok) btn.remove(); });
      });
      pendingBox.appendChild(btn);
    }
    if (pendingDomains.length) {
      pendingBox.prepend('New target domains need your permission:');
      pendingBox.style.display = 'block';
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
