'use strict';

// ─── Constants ────────────────────────────────────────────────────────────────
const PAGE_SIZE = 20;

// ─── State ────────────────────────────────────────────────────────────────────
let currentPage   = 1;
let allEvents     = [];

// ─── DOM References ───────────────────────────────────────────────────────────
const deviceSelect        = document.getElementById('filter-device');
const fromInput           = document.getElementById('filter-from');
const toInput             = document.getElementById('filter-to');
const applyBtn            = document.getElementById('apply-filters');
const clearBtn            = document.getElementById('clear-filters');
const logsLoading         = document.getElementById('logs-loading');
const logsTableContainer  = document.getElementById('logs-table-container');
const logsTbody           = document.getElementById('logs-tbody');
const tableResponsive     = logsTableContainer.querySelector('.table-responsive');
const noResults           = document.getElementById('no-results');
const paginationContainer = document.getElementById('pagination-container');
const paginationList      = document.getElementById('pagination-list');
const paginationInfo      = document.getElementById('pagination-info');
const filterActiveBanner  = document.getElementById('filter-active-banner');
const dismissFilterBtn    = document.getElementById('dismiss-filter-banner');
const logsErrorBanner     = document.getElementById('logs-error-banner');
const logsErrorMessage    = document.getElementById('logs-error-message');

// ─── Date Helpers ─────────────────────────────────────────────────────────────
function getDefaultDates() {
  const to   = new Date();
  const from = new Date();
  from.setDate(from.getDate() - 7);
  return {
    from: from.toISOString().slice(0, 10),
    to:   to.toISOString().slice(0, 10),
  };
}

function parseServerDate(isoString) {
  if (!isoString) return null;

  // SQLite + DateTime can round-trip without timezone info; treat such values as UTC.
  const hasTimezone = /[zZ]|[+-]\d{2}:\d{2}$/.test(isoString);
  return new Date(hasTimezone ? isoString : `${isoString}Z`);
}

// ─── Format Helpers ───────────────────────────────────────────────────────────
function formatDateTime(isoString) {
  if (!isoString) return '';
  const dt = parseServerDate(isoString);
  if (!dt) return '';

  return dt.toLocaleString(undefined, {
    year:   'numeric',
    month:  'short',
    day:    '2-digit',
    hour:   '2-digit',
    minute: '2-digit',
  });
}

/**
 * Computes a human-readable duration between two ISO timestamps.
 * If `cameBack` is null, the duration is computed to "now" (ongoing).
 */
function formatDuration(wentOffline, cameBack) {
  const start      = parseServerDate(wentOffline);
  const end        = cameBack ? parseServerDate(cameBack) : new Date();
  if (!start || !end) return '< 1m';
  const totalMs    = end - start;
  if (totalMs < 0)    return '< 1m';
  const totalSecs  = Math.floor(totalMs / 1000);
  if (totalSecs < 60) return '< 1m';
  const minutes    = Math.floor(totalSecs / 60) % 60;
  const hours      = Math.floor(totalSecs / 3600) % 24;
  const days       = Math.floor(totalSecs / 86400);
  if (days >= 1)      return `${days}d ${hours}h`;
  if (hours >= 1)     return `${hours}h ${minutes}m`;
  return `${minutes}m`;
}

// ─── Escape Helper ────────────────────────────────────────────────────────────
function escapeHtml(value) {
  const div = document.createElement('div');
  div.appendChild(document.createTextNode(String(value ?? '')));
  return div.innerHTML;
}

// ─── Load Device Filter Dropdown ─────────────────────────────────────────────
async function loadDeviceFilter() {
  try {
    const res = await fetch('/api/devices');
    if (!res.ok) return;
    const devices = await res.json();
    devices.sort((a, b) => a.name.localeCompare(b.name));
    for (const d of devices) {
      const opt       = document.createElement('option');
      opt.value       = d.id;
      opt.textContent = d.name;
      deviceSelect.appendChild(opt);
    }
  } catch (err) {
    console.error('[logs] loadDeviceFilter error:', err);
  }
}

// ─── Load Logs ────────────────────────────────────────────────────────────────
async function loadLogs(deviceId, from, to, page) {
  showLoading();
  logsErrorBanner.classList.add('d-none');
  try {
    const params = new URLSearchParams();
    if (deviceId) params.set('deviceId', deviceId);
    if (from) {
      params.set('from', new Date(from).toISOString());
    }
    if (to) {
      const toDate = new Date(to);
      toDate.setHours(23, 59, 59, 999);
      params.set('to', toDate.toISOString());
    }

    const res = await fetch(`/api/logs?${params.toString()}`);
    if (!res.ok) {
      let errMsg = `Server error ${res.status}`;
      try {
        const body = await res.json();
        if (body.error) errMsg = body.error;
      } catch (_) { /* ignore parse error */ }
      throw new Error(errMsg);
    }

    allEvents    = await res.json();
    currentPage  = page ?? 1;
    hideLoading();
    renderLogs(allEvents, currentPage);
  } catch (err) {
    hideLoading();
    logsErrorMessage.textContent = err.message || 'Failed to load log data. Please try again.';
    logsErrorBanner.classList.remove('d-none');
    logsTableContainer.classList.add('d-none');
    console.error('[logs] loadLogs error:', err);
  }
}

// ─── Render Table Rows ────────────────────────────────────────────────────────
function renderLogs(events, page) {
  const totalPages = Math.ceil(events.length / PAGE_SIZE) || 1;
  const start      = (page - 1) * PAGE_SIZE;
  const end        = Math.min(start + PAGE_SIZE, events.length);
  const pageEvents = events.slice(start, end);

  logsTableContainer.classList.remove('d-none');
  logsTbody.innerHTML = '';

  if (events.length === 0) {
    noResults.classList.remove('d-none');
    tableResponsive.classList.add('d-none');
    paginationContainer.classList.add('d-none');
    return;
  }

  noResults.classList.add('d-none');
  tableResponsive.classList.remove('d-none');

  pageEvents.forEach((ev, idx) => {
    const rowNum    = start + idx + 1;
    const isOngoing = !ev.cameBackOnlineAt;

    const wentOfflineDisplay = formatDateTime(ev.wentOfflineAt);

    let cameBackCell;
    if (isOngoing) {
      cameBackCell = `<span class="badge bg-danger text-white rounded-pill px-2">
        <i class="bi bi-x-circle-fill me-1" aria-hidden="true"></i>Still Offline
      </span>`;
    } else {
      const display  = formatDateTime(ev.cameBackOnlineAt);
      const isoValue = escapeHtml(ev.cameBackOnlineAt);
      cameBackCell   = `<time datetime="${isoValue}">${escapeHtml(display)}</time>`;
    }

    let durationCell;
    if (isOngoing) {
      durationCell = `<span class="text-danger fw-semibold">Ongoing</span>`;
    } else {
      durationCell = escapeHtml(formatDuration(ev.wentOfflineAt, ev.cameBackOnlineAt));
    }

    const tr = document.createElement('tr');
    tr.innerHTML = `
      <td class="text-muted small">${rowNum}</td>
      <td class="fw-semibold">${escapeHtml(ev.deviceName)}</td>
      <td class="font-monospace text-muted">${escapeHtml(ev.ipAddress)}</td>
      <td><time datetime="${escapeHtml(ev.wentOfflineAt)}">${escapeHtml(wentOfflineDisplay)}</time></td>
      <td>${cameBackCell}</td>
      <td>${durationCell}</td>
    `;
    logsTbody.appendChild(tr);
  });

  renderPagination(totalPages, page, events.length);
}

// ─── Pagination ───────────────────────────────────────────────────────────────
function renderPagination(totalPages, page, totalEvents) {
  if (totalPages <= 1) {
    paginationContainer.classList.add('d-none');
    return;
  }

  paginationContainer.classList.remove('d-none');
  paginationList.innerHTML = '';

  // Previous button
  const prevLi = createPageItem('&laquo;', 'Previous page', page === 1, () => goToPage(page - 1));
  paginationList.appendChild(prevLi);

  // Page number buttons
  for (const p of getPageNumbers(page, totalPages)) {
    if (p === '...') {
      const li = document.createElement('li');
      li.className = 'page-item disabled';
      li.innerHTML = `<span class="page-link">…</span>`;
      paginationList.appendChild(li);
    } else {
      const li = document.createElement('li');
      li.className = `page-item${p === page ? ' active' : ''}`;
      if (p === page) li.setAttribute('aria-current', 'page');
      const btn = document.createElement('button');
      btn.className   = 'page-link';
      btn.textContent = p;
      btn.addEventListener('click', () => goToPage(p));
      li.appendChild(btn);
      paginationList.appendChild(li);
    }
  }

  // Next button
  const nextLi = createPageItem('&raquo;', 'Next page', page === totalPages, () => goToPage(page + 1));
  paginationList.appendChild(nextLi);

  const displayStart = (page - 1) * PAGE_SIZE + 1;
  const displayEnd   = Math.min(page * PAGE_SIZE, totalEvents);
  paginationInfo.textContent = `Page ${page} of ${totalPages} — showing ${displayStart}–${displayEnd} of ${totalEvents} events`;
}

function createPageItem(label, ariaLabel, disabled, onClick) {
  const li  = document.createElement('li');
  li.className = `page-item${disabled ? ' disabled' : ''}`;
  const btn = document.createElement('button');
  btn.className         = 'page-link';
  btn.innerHTML         = label;
  btn.setAttribute('aria-label', ariaLabel);
  if (disabled) btn.disabled = true;
  btn.addEventListener('click', onClick);
  li.appendChild(btn);
  return li;
}

function getPageNumbers(current, total) {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
  if (current <= 4) {
    return [1, 2, 3, 4, 5, '...', total];
  }
  if (current >= total - 3) {
    return [1, '...', total - 4, total - 3, total - 2, total - 1, total];
  }
  return [1, '...', current - 1, current, current + 1, '...', total];
}

function goToPage(page) {
  currentPage = page;
  renderLogs(allEvents, currentPage);
  window.scrollTo({ top: 0, behavior: 'smooth' });
}

// ─── UI Helpers ───────────────────────────────────────────────────────────────
function showLoading() {
  logsLoading.classList.remove('d-none');
  logsTableContainer.classList.add('d-none');
}

function hideLoading() {
  logsLoading.classList.add('d-none');
}

function isFiltered() {
  return deviceSelect.value !== '' || fromInput.value !== '' || toInput.value !== '';
}

// ─── Filter Event Listeners ───────────────────────────────────────────────────
applyBtn.addEventListener('click', () => {
  currentPage = 1;
  filterActiveBanner.classList.toggle('d-none', !isFiltered());
  loadLogs(deviceSelect.value, fromInput.value, toInput.value, 1);
});

clearBtn.addEventListener('click', () => {
  deviceSelect.value = '';
  const defaults     = getDefaultDates();
  fromInput.value    = defaults.from;
  toInput.value      = defaults.to;
  filterActiveBanner.classList.add('d-none');
  currentPage = 1;
  loadLogs('', defaults.from, defaults.to, 1);
});

dismissFilterBtn.addEventListener('click', () => {
  filterActiveBanner.classList.add('d-none');
});

// ─── Init ─────────────────────────────────────────────────────────────────────
async function init() {
  const defaults  = getDefaultDates();
  fromInput.value = defaults.from;
  toInput.value   = defaults.to;
  await loadDeviceFilter();
  await loadLogs('', defaults.from, defaults.to, 1);
}

init();
