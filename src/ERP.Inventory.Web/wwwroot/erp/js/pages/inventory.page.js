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
  $('#btnExportInventory').on('click', () => {exportInventoryFile('/Export/Inventory') });
  loadInventoryList();
});

function exportInventoryFile(url) {
    window.location = `${url}?${$.param(reportInventoryFilterQuery())}`;
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
    if (!AppState.inventoryRows.length) {
        $('#inventoryTable').html(UI.empty('No data'));
        return;
    }

    const isAll = pageSize === 0;
    const from = isAll ? (data.totalCount ? 1 : 0): (page - 1) * pageSize + 1;
    const to = isAll ? data.totalCount: Math.min(page * pageSize, data.totalCount);
    const totalPage = (to - from) + 1;

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
    <table class="data-table"><thead><tr><th style="min-width: 25px;text-align: center;">STT</th><th class="px-3 td_max-width160">${UI.t('Item')}</th><th class="td_max-width160">${UI.t('Serial / MT')}</th><th>${UI.t('OwnerName')}</th><th>${UI.t('Status')}</th><th>${UI.t('Current Location')}</th><th>${UI.t('Holder')}</th></tr></thead>
      <tbody>${AppState.inventoryRows.map((r, i) => `<tr>
      <td style="min-width: 25px;text-align: center;">${UI.esc(i + 1)}</td>
        <td class="px-3 fw-semibold td_max-width160">${UI.esc(r.itemCode)}</td>
        <td class="td_max-width160"><span class="link-item-tracking" data-key="${UI.esc(r.serialNumber)}">${UI.esc(r.serialNumber || r.barcode || '-')}</span><div class="small text-muted td_max-width160">${UI.esc(r.MT)}</div></td>
        <td class="px-3 fw-semibold">${UI.esc(r.ownerName)}</td>
        <td>${UI.badge(r.status)}</td>
        <td>${UI.esc(r.currentLocation)}</td>
        <td class="px-3 fw-semibold">${UI.esc(r.holder)}</td>
        
        <!--<td></button><button class="btn btn-light btn-sm btn-preview" data-index="${i}"><i class="bi bi-three-dots"></i></button></td>-->
      </tr>`).join('')}</tbody>
    </table>
    <div class="server-footer"><span>${UI.endpoint('InventoryList')}: ${data.totalCount} ${UI.t('rows')}</span><span>${UI.t('Page')} ${data.page} &middot; ${totalPage} ${UI.t('rows')}</span></div>
  </div>${pagination}`);
}

$(document).on('change', '#inventoryPageSizeSelect', function () {
    const newSize = parseInt($(this).val());
    loadInventoryList(1, newSize);
});


$(document).on('click', '#btnSaveItem', async function () {
    const id = $(this).data('id');
    const data = {}; $('#drawerBody [name]').each(function () { data[$(this).attr('name')] = $(this).val(); });
    
    const result = await UI.api(`/Management/ItemInstanceUpdate/${id}` , { method: 'PUT', data });
    await afterStructureSave(result);
});

function openDrawer(row){
  if(!row) return;
  $('#drawer .fw-bold').first().text(UI.t('Item Preview'));
  $('#drawerBody').html(`<div class="mb-3">${UI.badge(row.status)}</div>
    <h4 class="fw-bold">${UI.esc(row.itemCode)}</h4>
    <p class="text-muted">${UI.esc(row.itemName)}</p>
    <div class="audit-footer">
      <div>${UI.t('Serial / Barcode')}: <b>${UI.esc(row.serialNumber || row.barcode || '-')}</b></div>
      <div>${UI.t('Current Location')}: <b>${UI.esc(row.currentLocation)}</b></div>
      <div>${UI.t('Holder')}: <b>${UI.esc(row.holder)}</b></div>
      <div>${UI.t('Last Updated')}: <b>${UI.formatDate(row.lastUpdatedAt)}</b></div>
    </div>
    <div class= "col-md-12 d-flex justify-content-center"><button class="btn btn-primary w-25 mt-3" id="btnDrawerTracking"><i class="bi bi-upc-scan me-2"></i>${UI.t('Open Tracking')}</button></div>`);
  $('#btnDrawerTracking').on('click', function(){
    AppState.currentTrackingKeyword = row.serialNumber || row.barcode || row.itemCode;
    Router.go('tracking');
  });
    $('#drawer').addClass('open');

}
