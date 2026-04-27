window.AppState = {
  user: null,
  permissions: { canView: true, canOperate: false, canManage: false },
  resources: {},
  lookups: {},
  inventoryRows: [],
  inventoryPreset: null,
  currentTrackingKeyword: ''
};

$(async function(){
  $('#swalCancel').on('click', () => $('#swalLite').hide());
  $('#btnCloseDrawer').on('click', () => $('#drawer').removeClass('open'));
  $(document).on('click', '.nav-link-js', function(){ Router.go($(this).data('screen')); });
  $(document).on('click', '[data-go]', function(){ Router.go($(this).data('go')); });
  $(document).on('click', '.btn-preview', function(){ openDrawer(AppState.inventoryRows[$(this).data('index')]); });

  $('#globalSearchInput').on('keydown', function(e){
    if(e.key !== 'Enter') return;
    AppState.currentTrackingKeyword = $(this).val().trim();
    if(AppState.currentTrackingKeyword) Router.go('tracking');
  });

  $('#languageSelect').on('change', async function(){
    const lang = $(this).val();
    await UI.api('/App/Language', { method: 'POST', data: { language: lang } });
    await loadResources(lang);
    await loadLookups();
    renderMenu();
    Router.go(Router.current || AppConfig.defaultRoute);
  });

  $('#btnNotifications').on('click', openNotifications);

  try {
    await bootstrap();
    Router.go(AppConfig.defaultRoute);
  } catch (err) {
    $('#app').html(UI.empty('Cannot load application bootstrap data.'));
  }
});

async function bootstrap(){
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

async function loadResources(lang){
  AppState.resources = await UI.api('/Localization/Resources', { query: { lang } });
  $('#globalSearchInput').attr('placeholder', UI.t('Global search item / serial / barcode'));
  $('.brand .small.text-muted').text(UI.t('Inventory Enterprise'));
}

async function loadLookups(){
  const [warehouses, categories, statuses, suppliers, vendors, borrowers, items, repairResults, returnConditions, checkResults, externalPartyTypes] = await Promise.all([
    UI.api('/Lookup/Warehouses'),
    UI.api('/Lookup/Categories'),
    UI.api('/Lookup/Statuses'),
    UI.api('/Lookup/ExternalParties', { query: { type: 'Supplier' } }),
    UI.api('/Lookup/ExternalParties', { query: { type: 'RepairVendor' } }),
    UI.api('/Lookup/ExternalParties', { query: { type: 'Borrower' } }),
    UI.api('/Lookup/Items'),
    UI.api('/Lookup/RepairResults'),
    UI.api('/Lookup/BorrowReturnConditions'),
    UI.api('/Lookup/InventoryCheckResults'),
    UI.api('/Lookup/ExternalPartyTypes')
  ]);

  AppState.lookups = { warehouses, categories, statuses, suppliers, vendors, borrowers, items, repairResults, returnConditions, checkResults, externalPartyTypes };
}

function renderMenu(){
  const html = AppConfig.menu
    .filter(m => canOpenScreen(m[0]))
    .map(m => `<button class="nav-link-js" data-screen="${m[0]}"><i class="bi ${m[1]}"></i>${UI.esc(UI.t(m[2]))}</button>`)
    .join('');
  $('#sidebarMenu').html(html);
}

function canOpenScreen(screen){
  if(screen === 'system') return isAdmin();
  if(['warehouse-structure','master-data','adjustment'].includes(screen)) return AppState.permissions.canManage;
  if(['inbound','move','inventory-check','repair-send','repair-receive','borrow-lend','borrow-return'].includes(screen)) return AppState.permissions.canOperate;
  return AppState.permissions.canView;
}

function isAdmin(){
  return AppState.user && AppState.user.roles && AppState.user.roles.includes('Admin');
}

function updateNotificationBadge(count){
  const badge = $('#notificationCount');
  badge.text(count || 0);
  badge.toggleClass('d-none', !count);
}

async function openNotifications(){
  $('#drawer .fw-bold').first().text(UI.t('Notifications'));
  $('#drawerBody').html(UI.loading());
  $('#drawer').addClass('open');
  const rows = await UI.api('/Notifications/Unread');
  updateNotificationBadge(rows.length);
  if(!rows.length){
    $('#drawerBody').html(UI.empty('No data'));
    return;
  }
  $('#drawerBody').html(rows.map(x => `
    <div class="audit-footer mb-2">
      <div class="fw-bold">${UI.esc(notificationText(x.title))}</div>
      <div class="small text-muted">${UI.formatDate(x.createdAt)}</div>
      <div class="mt-2">${UI.esc(notificationText(x.message))}</div>
      <button class="btn btn-sm btn-outline-primary mt-2 btn-mark-read" data-id="${x.id}">${UI.t('Mark read')}</button>
    </div>`).join(''));
}

function notificationText(value){
  if(!value) return '';
  let text = UI.t(value);
  const replacements = ['Inbound posted.', 'Borrow lend posted.', 'Borrow return posted.', 'Move posted.', 'Repair send posted.', 'Repair receive posted.', 'Adjustment posted.', 'Inventory check posted.', 'Inventory check completed with discrepancies.', 'Inventory check completed without discrepancy.'];
  replacements.forEach(k => {
    if(text === value && value.startsWith(k)) text = UI.t(k) + value.slice(k.length);
  });
  return text;
}

$(document).on('click', '.btn-mark-read', async function(){
  await UI.api(`/Notifications/MarkRead/${$(this).data('id')}`, { method: 'POST', data: {} });
  openNotifications();
});
