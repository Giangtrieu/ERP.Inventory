window.AppState = {
  user: null,
  permissions: { canView: true, canOperate: false, canManage: false },
  resources: {},
  lookups: {},
  inventoryRows: [],
  inventoryPreset: null,
  currentTrackingKeyword: '',
  page: 1,
  pageSize: 25,
};

$(async function () {
  // ─── Global event bindings ────────────────────────────────
  $('#swalCancel').on('click', () => $('#swalLite').hide());
  $('#btnCloseDrawer').on('click', () => { $('#btnPrintVoucher').toggleClass('d-none', false); $('#drawer').removeClass('open right-drawer-detail'); });
  $(document).on('click', '.nav-link-js', function () { Router.go($(this).data('screen')); });
  $(document).on('click', '[data-go]', function () { Router.go($(this).data('go')); });
  $(document).on('click', '.btn-preview', function () { openDrawer(AppState.inventoryRows[$(this).data('index')]); });
  // Global search
  $('#globalSearchInput').on('keydown', function (e) {
    if (e.key !== 'Enter') return;
    AppState.currentTrackingKeyword = $(this).val().trim();
    if (AppState.currentTrackingKeyword) Router.go('tracking');
  });

  // Language switch
  $('#languageSelect').on('change', async function () {
    const lang = $(this).val();
    await UI.api('/App/Language', { method: 'POST', data: { language: lang } });
    await loadResources(lang);
    await loadLookups();
    renderMenu();
    Router.go(Router.current || AppConfig.defaultRoute);
  });

  // Notifications
  $('#btnNotifications').on('click', openNotifications);
  $(document).on('click', '.btn-mark-read', async function () {
    await UI.api(`/Notifications/MarkRead/${$(this).data('id')}`, { method: 'POST', data: {} });
    openNotifications();
  });

  // ─── Sidebar toggle ──────────────────────────────────────
  initSidebar();

  // ─── Bootstrap ────────────────────────────────────────────
  try {
    await bootstrap();
    const screen = window.location.hash.replace('#', '');
    Router.go(screen || AppConfig.defaultRoute);
  } catch (err) {
    $('#app').html(UI.empty('Cannot load application bootstrap data.'));
  }
});

// ═════════════════════════════════════════════════════════════
// Sidebar Toggle (AdminLTE-style)
// ═════════════════════════════════════════════════════════════

function initSidebar() {
  const sidebar = $('#sidebar');
  const overlay = $('#sidebarOverlay');
  const mainContent = $('#mainContent');
  const isMobile = () => window.innerWidth <= 992;

  // Restore state from localStorage (desktop only)
  if (!isMobile() && localStorage.getItem('sidebar-collapsed') === '1') {
    sidebar.addClass('collapsed');
    mainContent.css({ 'margin-left': 'var(--sidebar-collapsed-w)', 'width': 'calc(100% - var(--sidebar-collapsed-w))' });
  }

  // Toggle button
  $('#menuToggle').on('click', function () {
    if (isMobile()) {
      sidebar.toggleClass('mobile-open');
      overlay.toggleClass('active');
    } else {
      sidebar.toggleClass('collapsed');
      const collapsed = sidebar.hasClass('collapsed');
      localStorage.setItem('sidebar-collapsed', collapsed ? '1' : '0');
      if (collapsed) {
        mainContent.css({ 'margin-left': 'var(--sidebar-collapsed-w)', 'width': 'calc(100% - var(--sidebar-collapsed-w))' });
      } else {
        mainContent.css({ 'margin-left': 'var(--sidebar-w)', 'width': 'calc(100% - var(--sidebar-w))' });
      }
    }
  });

  // Overlay click closes mobile sidebar
  overlay.on('click', function () {
    sidebar.removeClass('mobile-open');
    overlay.removeClass('active');
  });

  // Collapsed sidebar: show tooltip on hover
  $(document).on('mouseenter', '#sidebar.collapsed .nav-link-js', function () {
    $(this).attr('title', $(this).find('.menu-text').text());
  });
}

// ═════════════════════════════════════════════════════════════
// Bootstrap & Data Loading
// ═════════════════════════════════════════════════════════════

async function bootstrap() {
  const boot = await UI.api('/App/Bootstrap');
  AppState.user = boot.user;
  AppState.permissions = boot.permissions;
  $('#currentUserBadge').text(`${boot.user.displayName || boot.user.userName} · ${boot.user.roles.join(', ')}`);
  $('#languageSelect').val(boot.user.language || 'vi');
  updateNotificationBadge(boot.notifications.unread);
  await loadResources(boot.user.language || 'vi');
  await loadLookups();
  renderMenu();
}

async function loadResources(lang) {
  AppState.resources = await UI.api('/Localization/Resources', { query: { lang } });
  $('#globalSearchInput').attr('placeholder', UI.t('Global search item / serial / barcode'));
  $('.brand-text .fw-bold.text-mute').text(UI.t('Warehouse B34G'));
  $('.brand-text .small.text-muted').text(UI.t('Inventory Enterprise'));
  $('#btnPrintVoucherText').text(UI.t('Export PDF'));
}

async function loadLookups() {
  const [warehouses, categories, statuses, inventoryStatuses, suppliers, vendors, borrowers, items, repairResults, returnConditions, checkResults, externalPartyTypes, documentPeriodType] = await Promise.all([
    UI.api('/Lookup/Warehouses'),
    UI.api('/Lookup/Categories'),
    //UI.api('/Lookup/Statuses'),
    UI.api('/Lookup/ItemStatusView'),
    UI.api('/Lookup/InventoryStatuses'),
    UI.api('/Lookup/ExternalParties', { query: { type: 'Supplier' } }),
    UI.api('/Lookup/ExternalParties', { query: { type: 'RepairVendor' } }),
    UI.api('/Lookup/ExternalParties', { query: { type: 'Borrower' } }),
    UI.api('/Lookup/Items'),
    UI.api('/Lookup/RepairResults'),
    UI.api('/Lookup/BorrowReturnConditions'),
    UI.api('/Lookup/InventoryCheckResults'),
    UI.api('/Lookup/ExternalPartyTypes'),
    UI.api('/Lookup/DocumentPeriodType'),
  ]);

  // Inbound condition = only in-warehouse sub-statuses: Normal / Damaged / Scrapped
  const inboundConditions = [
    { id: 'Normal',  text: UI.t('Enum.ItemStatus.Normal')  || 'Normal'  },
    { id: 'Damaged', text: UI.t('Enum.ItemStatus.Damaged') || 'Damaged' },
    { id: 'Scrapped',text: UI.t('Enum.ItemStatus.Scrapped')|| 'Scrapped'},
  ];

  AppState.lookups = { warehouses, categories, statuses, inventoryStatuses, suppliers, vendors, borrowers, items, repairResults, returnConditions, checkResults, externalPartyTypes, inboundConditions, documentPeriodType };
}

// ═════════════════════════════════════════════════════════════
// Menu & Permissions
// ═════════════════════════════════════════════════════════════

function renderMenu() {
  const html = AppConfig.menu
    .filter(m => canOpenScreen(m[0]))
    .map(m => `<button class="nav-link-js" data-screen="${m[0]}"><i class="bi ${m[1]}"></i><span class="menu-text">${UI.esc(UI.t(m[2]))}</span></button>`)
    .join('');
  $('#sidebarMenu').html(html);
}

function canOpenScreen(screen) {
  if (screen === 'system') return isAdmin();
  if (['warehouse-structure', 'master-data', 'adjustment', 'reconciliation'].includes(screen)) return AppState.permissions.canManage;
  if (['inbound', 'move', 'inventory-check', 'repair-send', 'repair-receive', 'borrow-lend', 'borrow-return', 'quantity-inventory', 'import', 'reconciliation'].includes(screen)) return AppState.permissions.canOperate;
  return AppState.permissions.canView;
}

function isAdmin() {
  return AppState.user && AppState.user.roles && AppState.user.roles.includes('Admin');
}

// ═════════════════════════════════════════════════════════════
// Notifications
// ═════════════════════════════════════════════════════════════

function updateNotificationBadge(count) {
  const badge = $('#notificationCount');
  badge.text(count || 0);
  badge.toggleClass('d-none', !count);
}

async function openNotifications() {
  $('#drawer .fw-bold').first().text(UI.t('Notifications'));
  $('#drawerBody').html(UI.loading());
  $('#drawer').addClass('open');
  $('#drawer').removeClass('right-drawer-detail');
  $('#btnPrintVoucher').toggleClass('d-none', true);

  const lang = $('#languageSelect').val();
  const rows = await UI.api('/Notifications/Unread', { method: 'POST', data: { language: lang } });
  updateNotificationBadge(rows.length);

  if (!rows.length) {
    $('#drawerBody').html(UI.empty('No data'));
    return;
  }

  $('#drawerBody').html(rows.map(x => `
    <div class="audit-footer mb-2">
      <div class="fw-bold">${UI.esc(UI.t(notificationText(x.title)))}</div>
      <div class="small text-muted">${UI.formatDate(x.createdAt)}</div>
      <div class="mt-2">${UI.esc(notificationText(x.message))}</div>
      <button class="btn btn-sm btn-outline-primary mt-2 btn-mark-read" data-id="${x.id}">${UI.t('Mark read')}</button>
    </div>`).join(''));
}

function notificationText(value) {
  if (!value) return '';
  let text = UI.t(value);
  const replacements = [
    'Inbound posted.', 'Borrow lend posted.', 'Borrow return posted.',
    'Move posted.', 'Repair send posted.', 'Repair receive posted.',
    'Adjustment posted.', 'Inventory check posted.',
    'Inventory check completed with discrepancies.',
    'Inventory check completed without discrepancy.'
  ];
  replacements.forEach(k => {
    if (text === value && value.startsWith(k)) text = UI.t(k) + value.slice(k.length);
  });
  return text;
}
