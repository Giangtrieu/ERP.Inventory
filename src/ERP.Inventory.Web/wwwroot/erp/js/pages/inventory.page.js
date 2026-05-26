Router.register('inventory', function(){
  const preset = AppState.inventoryPreset || {};
  $('#app').html(UI.pageHeader('Inventory List', 'Home / Inventory', `<div class="card-body justify-content-between align-items-center"><button class="btn btn-primary" id="btnExportInventory"><i class="bi bi-download me-2"></i>${UI.t('Export Inventory')}</button> ${AppState.permissions.canOperate ? '<button class="btn btn-primary" data-go="inbound"><i class="bi bi-plus-circle me-2"></i>' + UI.t('New Inbound') + '</button>' : ''}</div>`) +
    `<div class="card"><div class="card-body">
      <div class="row g-3 mb-3">
        <div class="col-md-3">${UI.select('Warehouse','warehouseId', AppState.lookups.warehouses, preset.warehouseId || '')}</div>
        <div class="col-md-3">${UI.select('Status','status', AppState.lookups.statuses, preset.status || '')}</div>
        <div class="col-md-3">${UI.selectOption('Item','itemId', AppState.lookups.items)}</div>
        <div class="col-md-3">${UI.input('Keyword','text','','keyword')}</div>
      </div>
      <div id="inventoryTable">${UI.loading()}</div>
    </div></div>`);
  AppState.inventoryPreset = null;
  $('#app select, #app .cbo-value, #app input[name="keyword"], #app input[data-name="itemId"]').on('change input', UI.debounce(() => loadInventoryList(1), 250));
  $('#btnExportInventory').on('click', () => { exportInventoryFile('/Export/Inventory'); });
  loadInventoryList();
});

function exportInventoryFile(url) {
  window.location = `${UI.resolveUrl(url)}?${$.param(reportInventoryFilterQuery())}`;
}

function reportInventoryFilterQuery() {
  return {
    warehouseId: $('#app [name="warehouseId"]').val() || '',
    categoryId: $('#app [name="itemId"]').val() || '',
    status: $('#app [name="status"]').val() || '',
    keyword: $('#app [name="keyword"]').val() || '',
  };
}

async function loadInventoryList(page = 1, pageSize = AppState.pageSize || 25) {
  const query = {
    page,
    pageSize,
    warehouseId: $('#app [name="warehouseId"]').val() || null,
    itemId: $('#app [name="itemId"]').val() || null,
    status: $('#app [name="status"]').val() || null,
    keyword: $('#app [name="keyword"]').val() || null
  };
  $('#inventoryTable').html(UI.loading());
  const result = await UI.api('/Inventory/ListInventory', { query });
  if (!result.success) {
    $('#inventoryTable').html(UI.empty(UI.resultError(result)));
    return;
  }

  const data = result.data || { items: [], totalCount: 0, page: 1 };
  const totalPages = Math.ceil(data.totalCount / pageSize);
  AppState.inventoryRows = data.items || [];
  AppState.inventoryPage = page;
  AppState.inventoryPageSize = pageSize;

  if (!AppState.inventoryRows.length) {
    $('#inventoryTable').html(UI.empty('No data'));
    return;
  }

  const isAll = pageSize === 0;
  const from = isAll ? (data.totalCount ? 1 : 0) : (page - 1) * pageSize + 1;
  const to = isAll ? data.totalCount : Math.min(page * pageSize, data.totalCount);
  const totalPage = (to - from) + 1;
  const canEdit = !!(AppState.permissions && AppState.permissions.canManage);

  const pagination = Pagination.render({
    page,
    pageSize,
    total: data.totalCount,
    totalPages,
    isAll,
    selectId: 'inventoryPageSizeSelect',
    onChange: 'loadInventoryList'
  });

  $('#inventoryTable').html(`<div class="table-wrap">
    <table class="data-table"><thead><tr><th style="min-width: 25px;text-align: center;">STT</th><th class="px-3">${UI.t('Document No')}</th><th class="px-3 td_max-width160">${UI.t('Item')}</th><th class="td_max-width160">${UI.t('Serial / MT')}</th><th>${UI.t('OwnerName')}</th><th>${UI.t('Status')}</th><th>${UI.t('Current Location')}</th><th>${UI.t('Holder')}</th>${canEdit ? `<th>${UI.t('Actions')}</th>` : ''}</tr></thead>
      <tbody>${AppState.inventoryRows.map((r, i) => `<tr>
      <td style="min-width: 25px;text-align: center;">${UI.esc(i + 1)}</td>
      <td style="min-width: 25px;">${UI.esc(r.documentNo)}</td>
        <td class="px-3 fw-semibold td_max-width160">${UI.esc(r.itemCode)}<div class="small text-muted td_max-width160">${UI.esc(r.itemName || '')}</div></td>
        <td class="td_max-width160"><span class="link-item-tracking" data-key="${UI.esc(r.serialNumber || r.barcode || r.itemCode)}">${UI.esc(r.serialNumber || r.barcode || '-')}</span><div class="small text-muted td_max-width160">${UI.esc(r.mt || r.MT || '')}</div></td>
        <td class="px-3 fw-semibold">${UI.esc(r.ownerName)}</td>
        <td>${UI.badge(r.status)}</td>
        <td>${UI.esc(r.currentLocation)}</td>
        <td class="px-3 fw-semibold">${UI.esc(r.holder)}</td>
        ${canEdit ? `<td><button class="btn btn-light btn-sm btn-edit-inventory-item" title="${UI.t('Edit')}" data-id="${UI.esc(r.itemInstanceId)}"><i class="bi bi-pencil"></i></button><button class= "btn btn-sm btn-outline-danger btn-delete-inventory-item" title = "${UI.t('Hard Delete')}" data-id="${UI.esc(r.itemInstanceId)}" > <i class="bi bi-trash"></i></button></td>` : ''}
      </tr>`).join('')}</tbody>
    </table>
    <div class="server-footer"><span>${UI.endpoint('InventoryList')}: ${data.totalCount} ${UI.t('rows')}</span><span>${UI.t('Page')} ${data.page} &middot; ${totalPage} ${UI.t('rows')}</span></div>
  </div>${pagination}`);
}

$(document).on('click', '.btn-delete-inventory-item', function () {hardDeleteInventoryItem($(this).data('id')); });

$(document).on('change', '#inventoryPageSizeSelect', function () {
  const newSize = parseInt($(this).val(), 10);
  loadInventoryList(1, newSize);
});

$(document).on('click', '.btn-edit-inventory-item', function () {
  openInventoryItemForm($(this).data('id'));
});

$(document).on('click', '#btnSaveInventoryItem', async function () {
  const id = $(this).data('id');
  const data = {
    itemId: parseInt($('#drawerBody [name="itemId"]').val(), 10),
    serialNumber: $('#drawerBody [name="serialNumber"]').val(),
    MT: $('#drawerBody [name="MT"]').val(),
    barcode: $('#drawerBody [name="barcode"]').val(),
    ownerName: $('#drawerBody [name="ownerName"]').val()
  };

  const result = await UI.api(`/Management/ItemInstanceUpdate/${id}`, { method: 'PUT', data });
  await afterInventoryItemSave(result);
});

function hardDeleteInventoryItem(id) {
    UI.confirm('Hard Delete', 'Only permanently delete items with no transaction history.', `<div>ID: <b>${id}</b></div>`, async function () {
        const result = await UI.api(`/Management/ItemInstanceDelete/${id}`, { method: 'DELETE', data: {id} });
        UI.toast(result.success ? UI.t('Record deleted.') : UI.resultError(result));
        await loadLookups();
        await loadInventoryList(AppState.inventoryPage || 1, AppState.inventoryPageSize || AppState.pageSize || 25);
    });
}

async function openInventoryItemForm(id) {
  if (!id) return;
  const data = await UI.api(`/Management/ItemInstance/${id}`);
  $('#drawer .fw-bold').first().text(UI.t('Edit'));
  $('#drawerBody').html(`
    <div class="mb-3">${UI.badge(data.status)}</div>
    <div class="audit-footer mb-3">
      <div>${UI.t('Item')}: <b>${UI.esc(data.itemCode || '-')}</b> ${data.itemName ? `<span class="text-muted">(${UI.esc(data.itemName)})</span>` : ''}</div>
      <div>${UI.t('Current Location')}: <b>${UI.esc(data.currentLocation || '-')}</b></div>
    </div>
    <div class="row g-2">
      <div class="col-md-6">${UI.selectform('Item', 'itemId', AppState.lookups.items, data.itemId || '')}</div>
      ${UI.inputform('SN', 'text', data.serialNumber || '', 'serialNumber')}
      ${UI.inputform('MT', 'text', data.mt || data.MT || '', 'MT')}
      ${UI.inputform('Barcode', 'text', data.barcode || '', 'barcode')}
      ${UI.inputform('OwnerName', 'text', data.ownerName || '', 'ownerName')}
    </div>
    <div class="col-md-12 d-flex justify-content-center"><button class="btn btn-primary w-25 mt-2" id="btnSaveInventoryItem" data-id="${UI.esc(id)}">${UI.t('Save')}</button></div>`);
  $('#drawer').removeClass('right-drawer-detail').addClass('open');
}

async function afterInventoryItemSave(result) {
  UI.toast(result.success ? UI.t('Saved') : UI.resultError(result));
  if (!result.success) return;
  $('#drawer').removeClass('open');
  await loadInventoryList(AppState.inventoryPage || 1, AppState.inventoryPageSize || AppState.pageSize || 25);
}
