'use strict';

// ─── State ────────────────────────────────────────────────────────────────────
/** @type {Map<number, object>} */
const deviceMap = new Map();

// ─── DOM References ───────────────────────────────────────────────────────────
const deviceGrid      = document.getElementById('device-grid');
const loadingSpinner  = document.getElementById('loading-spinner');
const errorBanner     = document.getElementById('error-banner');
const errorMessage    = document.getElementById('error-message');
const signalrBanner   = document.getElementById('signalr-status-banner');
const liveIndicator   = document.getElementById('live-indicator');

// ─── Status Config ────────────────────────────────────────────────────────────
const STATUS_CONFIG = {
  Online:  { cardClass: 'card-status-online',   badgeClass: 'bg-success text-white',   icon: 'bi-check-circle-fill',         sortOrder: 2 },
  Offline: { cardClass: 'card-status-offline',  badgeClass: 'bg-danger text-white',    icon: 'bi-x-circle-fill',             sortOrder: 0 },
  Timeout: { cardClass: 'card-status-timeout',  badgeClass: 'bg-warning text-dark',    icon: 'bi-exclamation-triangle-fill', sortOrder: 1 },
  Unknown: { cardClass: 'card-status-unknown',  badgeClass: 'bg-secondary text-white', icon: 'bi-question-circle-fill',      sortOrder: 3 },
};

function getStatusConfig(status) {
  return STATUS_CONFIG[status] ?? STATUS_CONFIG.Unknown;
}

function parseServerDate(isoString) {
  if (!isoString) return null;

  // SQLite + DateTime can round-trip without timezone info; treat such values as UTC.
  const hasTimezone = /[zZ]|[+-]\d{2}:\d{2}$/.test(isoString);
  return new Date(hasTimezone ? isoString : `${isoString}Z`);
}

// ─── Sort ─────────────────────────────────────────────────────────────────────
function sortDevices(devices) {
  return [...devices].sort((a, b) => {
    const orderA = getStatusConfig(a.status).sortOrder;
    const orderB = getStatusConfig(b.status).sortOrder;
    if (orderA !== orderB) return orderA - orderB;
    return a.name.localeCompare(b.name);
  });
}

// ─── Format Helpers ───────────────────────────────────────────────────────────
function formatLastSeen(isoString) {
  if (!isoString) return 'Never';
  const dt = parseServerDate(isoString);
  return dt ? dt.toLocaleTimeString() : 'Never';
}

function formatLastSeenFull(isoString) {
  if (!isoString) return 'Never';
  const dt = parseServerDate(isoString);
  return dt ? dt.toLocaleString() : 'Never';
}

// ─── Escape Helpers ───────────────────────────────────────────────────────────
function escapeHtml(value) {
  const div = document.createElement('div');
  div.appendChild(document.createTextNode(String(value ?? '')));
  return div.innerHTML;
}

function escapeAttr(value) {
  return String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}

// ─── Format Statistics ────────────────────────────────────────────────────────
function formatLatency(ms) {
  if (ms == null) return '–';
  return `${ms}ms`;
}

function formatUptime(percentage) {
  if (percentage == null) return '–';
  return `${percentage.toFixed(1)}%`;
}

// ─── Render Card ──────────────────────────────────────────────────────────────
function renderCard(device) {
  const { cardClass, badgeClass, icon } = getStatusConfig(device.status);
  const lastSeenDisplay = formatLastSeen(device.lastChecked);
  const lastSeenFull    = formatLastSeenFull(device.lastChecked);

  const col = document.createElement('div');
  col.className = 'col-12 col-sm-6 col-md-4 col-lg-3 col-xl-2';
  col.dataset.deviceId = device.id;

  col.innerHTML = `
    <div class="card shadow-sm rounded-3 h-100 ${cardClass}"
         id="card-${device.id}"
         data-device-id="${device.id}"
         role="button"
         tabindex="0"
         aria-label="${escapeAttr(device.name)} status: ${escapeAttr(device.status)}, click to view details">
      <div class="card-body p-3 d-flex flex-column gap-1">
        <h2 class="card-title fs-5 fw-bold mb-0 text-truncate" title="${escapeAttr(device.name)}">
          ${escapeHtml(device.name)}
        </h2>
        <p class="font-monospace text-muted small mb-0">${escapeHtml(device.ipAddress)}</p>
        <p class="font-monospace text-muted small mb-0">${escapeHtml(device.macAddress)}</p>
        <div class="mt-auto pt-2 d-flex align-items-center justify-content-between">
          <span class="badge ${badgeClass} rounded-pill px-2 py-1">
            <i class="bi ${icon} me-1" aria-hidden="true"></i>${escapeHtml(device.status)}
          </span>
          <small class="text-muted" title="Last checked: ${escapeAttr(lastSeenFull)}">
            ${escapeHtml(lastSeenDisplay)}
          </small>
        </div>
      </div>
    </div>
  `;

  const card = col.querySelector('.card');
  card.addEventListener('click', () => {
    console.log('[dashboard] Card clicked for device:', device.name);
    showStatsModal(device);
  });
  card.addEventListener('keypress', (e) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      showStatsModal(device);
    }
  });

  return col;
}

// ─── Update Card In-Place ─────────────────────────────────────────────────────
function updateCard(device) {
  const col = deviceGrid.querySelector(`[data-device-id="${device.id}"]`);
  if (!col) {
    // New device appeared — add it and re-sort
    deviceMap.set(device.id, device);
    rebuildGrid();
    return;
  }

  const card             = col.querySelector('.card');
  const { cardClass, badgeClass, icon } = getStatusConfig(device.status);
  const lastSeenDisplay  = formatLastSeen(device.lastChecked);
  const lastSeenFull     = formatLastSeenFull(device.lastChecked);

  // Update border / aria
  card.className = `card shadow-sm rounded-3 h-100 ${cardClass}`;
  card.setAttribute('aria-label', `${device.name} status: ${device.status}, click to view details`);

  // Update badge
  const badge        = card.querySelector('.badge');
  badge.className    = `badge ${badgeClass} rounded-pill px-2 py-1`;
  badge.innerHTML    = `<i class="bi ${icon} me-1" aria-hidden="true"></i>${escapeHtml(device.status)}`;

  // Update last-seen
  const small        = card.querySelector('small');
  small.title        = `Last checked: ${lastSeenFull}`;
  small.textContent  = lastSeenDisplay;

  deviceMap.set(device.id, device);
  updateSummaryBar();
}

// ─── Rebuild Grid ─────────────────────────────────────────────────────────────
function rebuildGrid() {
  const sorted = sortDevices([...deviceMap.values()]);
  deviceGrid.innerHTML = '';
  for (const device of sorted) {
    deviceGrid.appendChild(renderCard(device));
  }
  updateSummaryBar();
}

// ─── Summary Bar ──────────────────────────────────────────────────────────────
function updateSummaryBar() {
  let online = 0, offline = 0, timeout = 0, unknown = 0;
  for (const d of deviceMap.values()) {
    if      (d.status === 'Online')  online++;
    else if (d.status === 'Offline') offline++;
    else if (d.status === 'Timeout') timeout++;
    else                             unknown++;
  }
  document.getElementById('count-online').textContent  = online;
  document.getElementById('count-offline').textContent = offline;
  document.getElementById('count-timeout').textContent = timeout;
  document.getElementById('count-unknown').textContent = unknown;
}

// ─── Load Devices (REST) ──────────────────────────────────────────────────────
async function loadDevices() {
  try {
    const response = await fetch('/api/devices');
    if (!response.ok) throw new Error(`Server responded with ${response.status}`);
    const devices = await response.json();
    deviceMap.clear();
    for (const d of devices) deviceMap.set(d.id, d);
    rebuildGrid();
    hideLoading();
  } catch (err) {
    hideLoading();
    showError('Failed to load device data. Please refresh the page.');
    console.error('[dashboard] loadDevices error:', err);
  }
}

// ─── UI State Helpers ─────────────────────────────────────────────────────────
function hideLoading() {
  loadingSpinner.classList.add('d-none');
}

function showError(msg) {
  errorMessage.textContent = msg;
  errorBanner.classList.remove('d-none');
}

function showDisconnectBanner() {
  signalrBanner.classList.remove('d-none');
  liveIndicator.classList.remove('text-success');
  liveIndicator.classList.add('text-warning');
}

function hideDisconnectBanner() {
  signalrBanner.classList.add('d-none');
  liveIndicator.classList.remove('text-warning');
  liveIndicator.classList.add('text-success');
}

// ─── SignalR ──────────────────────────────────────────────────────────────────
const connection = new signalR.HubConnectionBuilder()
  .withUrl('/hubs/status')
  .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
  .build();

connection.on('StatusUpdate', (devices) => {
  hideLoading();
  hideDisconnectBanner();
  if (deviceMap.size === 0) {
    // Initial state delivered via SignalR
    for (const d of devices) deviceMap.set(d.id, d);
    rebuildGrid();
  } else {
    // Incremental update — update each card in place
    for (const d of devices) {
      updateCard(d);
    }
  }
});

connection.onreconnecting(() => {
  showDisconnectBanner();
});

connection.onreconnected(() => {
  hideDisconnectBanner();
});

connection.onclose(() => {
  showDisconnectBanner();
});

async function startSignalR() {
  try {
    await connection.start();
    hideDisconnectBanner();
  } catch (err) {
    console.error('[dashboard] SignalR failed to connect:', err);
    showDisconnectBanner();
    // Fall back to REST API
    await loadDevices();
  }
}

// ─── Modal Handler ────────────────────────────────────────────────────────────
async function showStatsModal(device) {
  try {
    console.log('[dashboard] Opening stats modal for device:', device.name);
    
    // Fetch fresh device data from API to ensure stats are calculated
    console.log('[dashboard] Fetching fresh device data from API...');
    const response = await fetch('/api/devices');
    if (!response.ok) {
      throw new Error('Failed to fetch device data');
    }
    const allDevices = await response.json();
    const freshDevice = allDevices.find(d => d.id === device.id) || device;
    
    console.log('[dashboard] Fresh device data:', freshDevice);
    
    // Try to find modal element
    let modalElement = document.getElementById('statsModal');
    
    // If not found, create it dynamically
    if (!modalElement) {
      console.warn('[dashboard] statsModal not found in DOM, creating dynamically...');
      modalElement = createStatsModalElement();
      document.body.appendChild(modalElement);
    }
    
    console.log('[dashboard] Populating modal fields...');
    
    // Populate modal fields - with error checking
    const titleEl = document.getElementById('statsModalLabel');
    const ipEl = document.getElementById('statsModalIp');
    const statusEl = document.getElementById('modalStatus');
    const lastCheckedEl = document.getElementById('modalLastChecked');
    const uptimePercentEl = document.getElementById('modalUptimePercent');
    const uptimeBarEl = document.getElementById('modalUptimeBar');
    const lastLatencyEl = document.getElementById('modalLastLatency');
    const avgLatencyEl = document.getElementById('modalAvgLatency');
    const successfulEl = document.getElementById('modalSuccessful');
    const failedEl = document.getElementById('modalFailed');
    
    if (!titleEl || !ipEl || !statusEl) {
      console.error('[dashboard] Modal elements not found in created modal!');
      return;
    }
    
    titleEl.textContent = freshDevice.name;
    ipEl.textContent = freshDevice.ipAddress;
    
    const { cardClass, badgeClass, icon } = getStatusConfig(freshDevice.status);
    statusEl.className = `badge ${badgeClass} rounded-pill px-3 py-2`;
    statusEl.innerHTML = `<i class="bi ${icon} me-1" aria-hidden="true"></i>${escapeHtml(freshDevice.status)}`;
    
    // Update modal header gradient based on status
    const modalHeader = modalElement.querySelector('.modal-header');
    if (modalHeader) {
      modalHeader.className = `modal-header modal-header-${freshDevice.status.toLowerCase()} pb-0 px-4 pt-4 border-0`;
    }
    
    lastCheckedEl.textContent = formatLastSeenFull(freshDevice.lastChecked);
    
    // Uptime
    const uptimePercent = freshDevice.uptimePercentage ?? 0;
    console.log('[dashboard] Setting uptime percent:', uptimePercent);
    uptimePercentEl.textContent = uptimePercent.toFixed(1) + '%';
    uptimeBarEl.style.width = uptimePercent + '%';
    uptimeBarEl.setAttribute('aria-valuenow', uptimePercent.toFixed(1));
    
    // Latencies
    const lastLatencyText = freshDevice.lastLatencyMs ? `${freshDevice.lastLatencyMs}ms` : '–';
    const avgLatencyText = freshDevice.averageLatencyMs ? `${freshDevice.averageLatencyMs.toFixed(0)}ms` : '–';
    console.log('[dashboard] Setting latencies - last:', lastLatencyText, 'avg:', avgLatencyText);
    lastLatencyEl.textContent = lastLatencyText;
    avgLatencyEl.textContent = avgLatencyText;
    
    console.log('[dashboard] Stats set. Device uptime:', uptimePercent, 'lastLatency:', freshDevice.lastLatencyMs, 'avgLatency:', freshDevice.averageLatencyMs);
    
    // Display ping counts from device session data
    const successful = freshDevice.currentSessionSuccessfulPings ?? 0;
    const total = freshDevice.currentSessionTotalPings ?? 0;
    const failed = total - successful;
    
    if (successfulEl) successfulEl.textContent = successful;
    if (failedEl) failedEl.textContent = failed;
    
    console.log('[dashboard] Ping counts - successful:', successful, 'failed:', failed, 'total:', total);
    
    // Now show the modal - let Bootstrap handle the display
    console.log('[dashboard] Creating Bootstrap modal instance and showing...');
    const modal = new bootstrap.Modal(modalElement, { backdrop: 'static', keyboard: true });
    modal.show();
    
  } catch (err) {
    console.error('[dashboard] Error showing stats modal:', err);
  }
}

// ─── Create Modal Element Dynamically ──────────────────────────────────────────
function createStatsModalElement() {
  const div = document.createElement('div');
  div.className = 'modal fade';  // Let Bootstrap handle the 'show' class
  div.id = 'statsModal';
  div.setAttribute('tabindex', '-1');
  div.setAttribute('aria-labelledby', 'statsModalLabel');
  div.setAttribute('aria-hidden', 'true');
  
  div.innerHTML = `
    <div class="modal-dialog modal-dialog-centered modal-lg">
      <div class="modal-content border-0 shadow-lg">
        <div class="modal-header pb-0 px-4 pt-4 border-0">
          <div class="w-100">
            <h1 class="modal-title fs-4 fw-bold mb-1" id="statsModalLabel">
              <i class="bi bi-speedometer2 me-2 text-white"></i>Device Statistics
            </h1>
            <p class="font-monospace text-white small mb-0 opacity-80" id="statsModalIp"></p>
          </div>
          <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
        </div>
        <div class="modal-body px-4 py-4">
          <div class="row g-3">
            <!-- Status & Last Checked Row -->
            <div class="col-sm-6">
              <div class="card border-0 bg-gradient h-100" style="background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);">
                <div class="card-body text-white p-4">
                  <small class="d-block opacity-80 mb-2">
                    <i class="bi bi-info-circle me-1"></i>Current Status
                  </small>
                  <span id="modalStatus" class="badge rounded-pill px-3 py-2 bg-white text-dark fs-6"></span>
                </div>
              </div>
            </div>
            <div class="col-sm-6">
              <div class="card border-0 bg-light h-100">
                <div class="card-body p-4">
                  <small class="text-muted d-block mb-2">
                    <i class="bi bi-calendar-check me-1"></i>Last Checked
                  </small>
                  <small id="modalLastChecked" class="fw-semibold text-dark d-block fs-6"></small>
                </div>
              </div>
            </div>

            <!-- Uptime Card -->
            <div class="col-12">
              <div class="card border-0 bg-light">
                <div class="card-body p-4">
                  <div class="d-flex align-items-center justify-content-between mb-3">
                    <small class="text-muted fw-semibold">
                      <i class="bi bi-graph-up me-2 text-success"></i>Uptime (24 Hours)
                    </small>
                    <span id="modalUptimePercent" class="badge bg-success rounded-pill px-3 py-2 fw-bold">–</span>
                  </div>
                  <div class="progress" style="height: 28px;">
                    <div class="progress-bar bg-gradient" id="modalUptimeBar" role="progressbar" style="width: 0%; background: linear-gradient(90deg, #4CAF50 0%, #45a049 100%);" aria-valuenow="0" aria-valuemin="0" aria-valuemax="100"></div>
                  </div>
                </div>
              </div>
            </div>

            <!-- Latency Cards Row -->
            <div class="col-sm-6">
              <div class="card border-0 bg-light">
                <div class="card-body p-4">
                  <small class="text-muted d-block mb-3 fw-semibold">
                    <i class="bi bi-lightning-fill me-2" style="color: #ff9800;"></i>Last Latency
                  </small>
                  <h3 class="fs-4 fw-bold mb-0 text-dark">
                    <span id="modalLastLatency" class="text-warning">–</span>
                  </h3>
                </div>
              </div>
            </div>

            <div class="col-sm-6">
              <div class="card border-0 bg-light">
                <div class="card-body p-4">
                  <small class="text-muted d-block mb-3 fw-semibold">
                    <i class="bi bi-hourglass-split me-2 text-info"></i>Avg Latency (24h)
                  </small>
                  <h3 class="fs-4 fw-bold mb-0 text-dark">
                    <span id="modalAvgLatency" class="text-info">–</span>
                  </h3>
                </div>
              </div>
            </div>

            <!-- Ping Statistics -->
            <div class="col-12">
              <div class="row g-3">
                <div class="col-sm-6">
                  <div class="card border-0 bg-success bg-opacity-10">
                    <div class="card-body p-4 text-center">
                      <i class="bi bi-check-circle-fill text-success mb-2" style="font-size: 1.5rem;"></i>
                      <small class="text-muted d-block mb-2">Successful Pings</small>
                      <h4 class="fw-bold mb-0 text-success" id="modalSuccessful">0</h4>
                    </div>
                  </div>
                </div>
                <div class="col-sm-6">
                  <div class="card border-0 bg-danger bg-opacity-10">
                    <div class="card-body p-4 text-center">
                      <i class="bi bi-x-circle-fill text-danger mb-2" style="font-size: 1.5rem;"></i>
                      <small class="text-muted d-block mb-2">Failed Pings</small>
                      <h4 class="fw-bold mb-0 text-danger" id="modalFailed">0</h4>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `;
  
  return div;
}

// ─── Init ─────────────────────────────────────────────────────────────────────
console.log('[dashboard] Checking for modal at init...');
const initialModalCheck = document.getElementById('statsModal');
console.log('[dashboard] Modal exists at init:', !!initialModalCheck);
if (!initialModalCheck) {
  console.error('[dashboard] WARNING: statsModal not found in HTML! Check _Layout.cshtml');
}

startSignalR();

// Safety fallback: if SignalR hasn't delivered data within 5 s, use REST
setTimeout(async () => {
  if (deviceMap.size === 0) {
    console.warn('[dashboard] SignalR timeout — falling back to REST');
    await loadDevices();
  }
}, 5000);
