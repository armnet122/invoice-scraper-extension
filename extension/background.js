// Cross-Browser extension API polyfill initialization
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


extApi.runtime.onInstalled.addListener(() => {
  extApi.contextMenus.create({
    id: 'scrape_invoice_link',
    title: 'Scrape Invoice # to Clipboard',
    contexts: ['link', 'selection']
  });

});

/**
 * Sends a desktop notification with optional custom ID.
 */
function sendNotification(title, message, notificationId = null) {
  if (extApi.notifications && extApi.notifications.create) {
    const id = notificationId || 'inv_notif_' + Date.now();
    const options = {
      type: 'basic',
      iconUrl: DEFAULT_ICON_DATA_URL,
      title: title,
      message: message,
      priority: 2
    };

    extApi.notifications.create(id, options);
  }
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
    const valMatch = inputTag.match(/value=["']([^"']+)["']/i);
    if (valMatch && valMatch[1]) {
      const val = valMatch[1].trim();
      if (val && !STATUS_EXCLUSIONS.has(val.toLowerCase()) && !/^(submit|button|hidden|checkbox|radio)$/i.test(val)) {
        if (/(?:name|id|placeholder|aria-label|class)=["'][^"']*(?:inv|ref)[^"']*["']/i.test(inputTag)) {
          return val;
        }
      }
    }
  }

  // Check plain text (handling any HTML tags like <span>, <b>, <td> between label and number)
  const plainText = html.replace(/<[^>]+>/g, ' ');
  const textMatches = plainText.matchAll(/Invoice\s*(?:#|num(?:ber)?|no\.?|id|ref(?:erence)?)?\s*[:\-]?\s*([A-Za-z0-9\-_#]{3,80})/gi);
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
  }
});

// ---- Target domains (set by the tray app via managed policy) ----
const DEFAULT_DOMAINS = ['dvla.gov.gh', 'genesys.com', 'genesyscloud.com'];
const DOMAIN_RE = /^(?!-)[a-z0-9-]+(\.[a-z0-9-]+)+$/;
const domainPatterns = (d) => [`*://${d}/*`, `*://*.${d}/*`];

async function getDomains() {
  try {
    const m = await extApi.storage.managed.get('domains');
    if (Array.isArray(m.domains)) {
      return m.domains.map(d => String(d).toLowerCase().trim()).filter(d => DOMAIN_RE.test(d));
    }
  } catch (e) {}
  return DEFAULT_DOMAINS;
}

/** Injects content.js only on domains we hold permission for; the rest are listed for the popup to request. */
async function syncContentScripts() {
  const granted = [];
  const pending = [];
  for (const d of await getDomains()) {
    (await extApi.permissions.contains({ origins: domainPatterns(d) }) ? granted : pending).push(d);
  }
  try { await extApi.scripting.unregisterContentScripts({ ids: ['inv-main'] }); } catch (e) {}
  if (granted.length) {
    await extApi.scripting.registerContentScripts([{
      id: 'inv-main',
      matches: granted.flatMap(domainPatterns),
      js: ['content.js'],
      runAt: 'document_idle',
      persistAcrossSessions: true
    }]);
  }
  await extApi.storage.local.set({ pendingDomains: pending });
}

extApi.runtime.onInstalled.addListener(syncContentScripts);
extApi.runtime.onStartup.addListener(syncContentScripts);
extApi.permissions.onAdded.addListener(syncContentScripts);
extApi.storage.onChanged.addListener((_, area) => { if (area === 'managed') syncContentScripts(); });
