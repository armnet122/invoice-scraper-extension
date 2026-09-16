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
 * Helper to compare semantic version strings (e.g. "2.8.0" > "2.7.0")
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
  const cleanUrl = url.toLowerCase();
  let urlUuidCandidate = null;
  if (cleanUrl) {
    const uuidMatch = cleanUrl.match(/([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})/i);
    if (uuidMatch && uuidMatch[1]) {
      urlUuidCandidate = uuidMatch[1].trim();
    } else {
      const urlMatch = cleanUrl.match(/\/invoice\/([A-Za-z0-9\-_]{4,80})/i);
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
 * Opens a URL in a temporary background tab, converts URL to lowercase, waits up to 5s for delayed SPA pages, extracts Invoice #, copies to clipboard, and closes tab.
 * @param {string} rawUrl 
 * @returns {Promise<{success: boolean, invoiceNumber?: string, error?: string}>}
 */
async function openScrapeAndCloseTab(rawUrl) {
  // Convert scanned/input URL to lowercase before processing
  let targetUrl = rawUrl.trim().toLowerCase();
  if (!/^https?:\/\//i.test(targetUrl) && !/^file:\/\//i.test(targetUrl)) {
    targetUrl = 'https://' + targetUrl;
  }

  let newTab = null;

  try {
    newTab = await extApi.tabs.create({ url: targetUrl, active: false });

    // Wait for tab load complete status with 15s timeout
    await new Promise((resolve) => {
      const timeout = setTimeout(() => {
        extApi.tabs.onUpdated.removeListener(onUpdatedListener);
        resolve(); // Proceed to DOM polling even if load is slow
      }, 15000);

      function onUpdatedListener(tabId, changeInfo) {
        if (tabId === newTab.id && changeInfo.status === 'complete') {
          clearTimeout(timeout);
          extApi.tabs.onUpdated.removeListener(onUpdatedListener);
          resolve();
        }
      }

      extApi.tabs.onUpdated.addListener(onUpdatedListener);
    });

    // 5-Second Wait & Asynchronous DOM Polling for slow/delaying SPA pages (20 attempts x 250ms = 5000ms)
    let extractedInvoiceNum = null;
    const maxAttempts = 20; 
    for (let attempt = 0; attempt < maxAttempts; attempt++) {
      await new Promise(r => setTimeout(r, 250));
      
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
