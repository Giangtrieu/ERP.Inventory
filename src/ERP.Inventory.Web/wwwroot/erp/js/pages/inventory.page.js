Router.register('inventory', function(){
  const preset = AppState.inventoryPreset || {};
  $('#app').html(UI.pageHeader('Inventory List','Home / Inventory', `${AppState.permissions.canOperate ? '<button class="btn btn-primary" data-go="inbound"><i class="bi bi-plus-circle me-2"></i>' + UI.t('New Inbound') + '</button>' : ''}`) +
    `<div class="card"><div class="card-body">
      <div class="row g-3 mb-3">
        <div class="col-md-3">${UI.select('Warehouse','warehouseId', AppState.lookups.warehouses, preset.warehouseId || '')}</div>
        <div class="col-md-3">${UI.select('Status','status', AppState.lookups.statuses, preset.status || '')}</div>
        <div class="col-md-3">${UI.select('Category','categoryId', AppState.lookups.categories)}</div>
        <div class="col-md-3">${UI.input('Keyword','text','','keyword')}</div>
      </div>
      <div id="inventoryTable">${UI.loading()}</div>
    </div></div>`);
  AppState.inventoryPreset = null;
  $('#app select, #app input[name="keyword"]').on('change input', UI.debounce(() => loadInventoryList(1), 250));
  loadInventoryList();
});

async function loadInventoryList(page=1){
  const query = {
    page,
    pageSize: 25,
    warehouseId: $('#app [name="warehouseId"]').val() || null,
    categoryId: $('#app [name="categoryId"]').val() || null,
    status: $('#app [name="status"]').val() || null,
    keyword: $('#app [name="keyword"]').val() || null
  };
  $('#inventoryTable').html(UI.loading());
  const result = await UI.api('/Inventory/List', { query });
  if(!result.success){
    $('#inventoryTable').html(UI.empty(UI.resultError(result)));
    return;
  }

  const data = result.data || { items: [], totalCount: 0, page: 1 };
  AppState.inventoryRows = data.items || [];
  if(!AppState.inventoryRows.length){
    $('#inventoryTable').html(UI.empty('No data'));
    return;
  }

  $('#inventoryTable').html(`<div class="table-wrap">
    <table class="data-table"><thead><tr><th class="px-3">${UI.t('Item')}</th><th>${UI.t('Serial / Barcode')}</th><th>${UI.t('Status')}</th><th>${UI.t('Current Location')}</th><th>${UI.t('Holder')}</th><th>${UI.t('Last Updated')}</th><th></th></tr></thead>
      <tbody>${AppState.inventoryRows.map((r,i)=>`<tr>
        <td class="px-3 fw-semibold">${UI.esc(r.itemCode)}<div class="small text-muted">${UI.esc(r.itemName)}</div></td>
        <td>${UI.esc(r.serialNumber || r.barcode || '-')}</td>
        <td>${UI.badge(r.status)}</td>
        <td>${UI.esc(r.currentLocation)}</td>
        <td>${UI.esc(r.holder)}</td>
        <td>${UI.formatDate(r.lastUpdatedAt)}</td>
        <td><button class="btn btn-light btn-sm btn-preview" data-index="${i}"><i class="bi bi-three-dots"></i></button></td>
      </tr>`).join('')}</tbody>
    </table>
    <div class="server-footer"><span>${UI.endpoint('InventoryList')}</span><span>${UI.t('Page')} ${data.page} &middot; ${data.totalCount} ${UI.t('rows')}</span></div>
  </div>`);
}

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
    <button class="btn btn-primary w-100 mt-3" id="btnDrawerTracking"><i class="bi bi-upc-scan me-2"></i>${UI.t('Open Tracking')}</button>`);
  $('#btnDrawerTracking').on('click', function(){
    AppState.currentTrackingKeyword = row.serialNumber || row.barcode || row.itemCode;
    Router.go('tracking');
  });
  $('#drawer').addClass('open');
}
