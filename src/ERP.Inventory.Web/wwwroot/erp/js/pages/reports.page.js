Router.register('reports', async function(){
  $('#app').html(UI.pageHeader('Reports / Audit','Home / Reports','') +
  `<div class="card mb-3"><div class="card-body">
    <div class="row g-3 text-start">
      <div class="col-md-3">${UI.select('Warehouse','warehouseId', AppState.lookups.warehouses)}</div>
      <div class="col-md-3">${UI.select('Category','categoryId', AppState.lookups.categories)}</div>
      <div class="col-md-2">${UI.input('From Date','date','','fromDate')}</div>
      <div class="col-md-2">${UI.input('To Date','date','','toDate')}</div>
      <div class="col-md-2">${UI.select('Status','status', AppState.lookups.statuses)}</div>
      <div class="col-md-3">${UI.input('Keyword','text','','keyword')}</div>
      <div class="col-md-3">${UI.input('User','text','','userName')}</div>
      <div class="col-md-2">${UI.select('Audit Activity','action', auditActionOptions())}</div>
      <div class="col-md-2">${UI.select('Audit Object Type','entityName', auditEntityOptions())}</div>
      <div class="col-md-2">${UI.input('Reference No','text','','referenceNo')}</div>
    </div>
    <div class="d-flex gap-2 mt-3 flex-wrap">
      ${reportExportButtons().map(b => `<button class="btn ${b.style || 'btn-outline-primary'} btn-report-export" data-url="${UI.esc(b.url)}"><i class="bi bi-download me-2"></i>${UI.t(b.label)}</button>`).join('')}
    </div>
  </div></div>
  <div class="row g-3">
    <!--<div class="col-xl-6"><div class="card h-100"><div class="card-body"><div class="form-section-title">${UI.t('Inventory Preview')}</div><div id="reportsInventory">${UI.loading()}</div></div></div></div> -->
    <div class="col-xl-6"><div class="card h-100"><div class="card-body"><div class="form-section-title">${UI.t('Movement History')}</div><div id="reportsHistory">${UI.loading()}</div></div></div></div>
    <div class="col-xl-6"><div class="card"><div class="card-body"><div class="form-section-title">${UI.t('Audit Log')}</div><div id="reportsAudit">${UI.loading()}</div></div></div></div>
  </div>`);
  $('.btn-report-export').on('click', function(){ exportFile($(this).data('url')); });
  $('#app input, #app select, #app .cbo-value').on('change input', UI.debounce(loadReportPreviews, 300));
  await loadReportPreviews();
});

function reportFilterQuery(){
  return {
    warehouseId: $('#app [name="warehouseId"]').val() || '',
    categoryId: $('#app [name="categoryId"]').val() || '',
    fromDate: $('#app [name="fromDate"]').val() || '',
    toDate: $('#app [name="toDate"]').val() || '',
    status: $('#app [name="status"]').val() || '',
    keyword: $('#app [name="keyword"]').val() || '',
    userName: $('#app [name="userName"]').val() || '',
    action: $('#app [name="action"]').val() || '',
    entityName: $('#app [name="entityName"]').val() || '',
    referenceNo: $('#app [name="referenceNo"]').val() || ''
  };
}

function exportFile(url){
    window.location = `${UI.resolveUrl(url)}?${$.param(reportFilterQuery())}`;
}

function reportExportButtons(){
  const buttons = [
    { label: 'Export Inventory', url: '/Export/Inventory' },
    { label: 'Export Quantity Balance', url: '/Export/QuantityBalance' },
    { label: 'Export Inbound Documents', url: '/Export/InboundDocuments' },
    { label: 'Export Quantity Transactions', url: '/Export/QuantityTransactions' },
    { label: 'Export Adjustment Documents', url: '/Export/AdjustmentDocuments' },
    { label: 'Export Inventory Check Documents', url: '/Export/InventoryCheckDocuments' },
    { label: 'Export Move Documents', url: '/Export/MoveDocuments' },
    { label: 'Export Repair Documents', url: '/Export/RepairDocuments' },
    { label: 'Export Borrow Documents', url: '/Export/BorrowDocuments' },
    { label: 'Export History', url: '/Export/History', style: 'btn-outline-secondary' },
    { label: 'Export Audit', url: '/Export/Audit', style: 'btn-outline-secondary' }
  ];
  if(AppState.permissions && AppState.permissions.canManage){
    buttons.push(
      { label: 'Export Warehouse Structure', url: '/Export/WarehouseStructure', style: 'btn-outline-secondary' },
      { label: 'Export Item Catalog', url: '/Export/ItemMaster', style: 'btn-outline-secondary' }
    );
  }
  return buttons;
}

function auditActionOptions(){
  return ['Inbound','MoveLocation','SendToRepair','ReceiveFromRepair','BorrowLend','BorrowReturn','Adjustment','InventoryCheck','ImportOperation','Create','Update','SoftDelete','Restore','HardDelete']
    .map(x => ({ id: x, text: UI.auditAction(x) }));
}

function auditEntityOptions(){
  return ['InboundDocument','MoveDocument','RepairDocument','BorrowDocument','AdjustmentDocument','InventoryCheckDocument','Item','ItemCategory','ExternalParty','BinLocation','SystemUser','ImportBatch']
    .map(x => ({ id: x, text: UI.auditEntity(x) }));
}

async function loadReportPreviews(){
  await Promise.all([loadReportsHistory(), loadReportsAudit()]);
  //await Promise.all([loadReportsInventory(), loadReportsHistory(), loadReportsAudit()]);
}

async function loadReportsInventory(){
  const result = await UI.api('/Reports/InventoryPreview', { query: reportFilterQuery() });
  const rows = result.success && result.data ? (result.data.items || []) : [];
  if(!rows.length){ $('#reportsInventory').html(UI.empty('No data')); return; }
  $('#reportsInventory').html(`<div class="table-wrap report-scroll-wrap"><table class="data-table"><thead><tr><th class="px-3">${UI.t('Item')}</th><th>${UI.t('Serial / Barcode')}</th><th>${UI.t('Status')}</th><th>${UI.t('Current Location')}</th></tr></thead><tbody>${rows.map(r => `<tr><td class="px-3 fw-semibold">${UI.esc(r.itemCode)}<div class="small text-muted">${UI.esc(r.itemName)}</div></td><td>${UI.esc(r.serialNumber || r.barcode || '-')}</td><td>${UI.badge(r.status)}</td><td>${UI.esc(r.currentLocation || '-')}</td></tr>`).join('')}</tbody></table><div class="server-footer"><span>${UI.endpoint('ReportsInventoryPreview')}</span><span>${result.data.totalCount} ${UI.t('rows')}</span></div></div>`);
}

async function loadReportsHistory(){
  const result = await UI.api('/Reports/HistoryPreview', { query: reportFilterQuery() });
  const rows = result.success && result.data ? (result.data.items || []) : [];
  if(!rows.length){ $('#reportsHistory').html(UI.empty('No data')); return; }
  $('#reportsHistory').html(`<div class="table-wrap report-scroll-wrap"><table class="data-table"><thead><tr><th class="px-3">${UI.t('Time')}</th><th>${UI.t('Item')}</th><th>${UI.t('Action')}</th><th>${UI.t('Status')}</th><th>${UI.t('Document No')}</th></tr></thead><tbody>${rows.map(r => `<tr><td class="px-3">${UI.formatDate(r.performedAt)}</td><td class="fw-semibold">${UI.esc(r.itemCode || '-')}<div class="small text-muted">${UI.esc(r.serialNumber || r.itemName || '-')}</div></td><td>${UI.esc(UI.enum('MovementActionType', r.actionType))}</td><td>${UI.badge(r.newStatus)}</td><td>${UI.esc(r.documentNo || '-')}</td></tr>`).join('')}</tbody></table><div class="server-footer"><span>${UI.endpoint('ReportsHistoryPreview')}</span><span>${result.data.totalCount} ${UI.t('rows')}</span></div></div>`);
}

async function loadReportsAudit(){
  const result = await UI.api('/Management/AuditLogs', { query: { page: 1, pageSize: 25, ...reportFilterQuery() } });
  const rows = result.items || [];
  if(!rows.length){ $('#reportsAudit').html(UI.empty('No data')); return; }
  $('#reportsAudit').html(`<div class="table-wrap report-scroll-wrap"><table class="data-table"><thead><tr><th class="px-3">${UI.t('Time')}</th><th>${UI.t('User')}</th><th>${UI.t('Action')}</th><th>${UI.t('Entity')}</th><th>${UI.t('Reference')}</th><th>${UI.t('Result')}</th></tr></thead><tbody>${rows.map(r => `<tr><td class="px-3">${UI.formatDate(r.createdAt)}</td><td>${UI.esc(r.userName)}</td><td>${UI.esc(UI.auditAction(r.action))}</td><td>${UI.esc(UI.auditEntity(r.entityName))}</td><td>${UI.esc(r.referenceNo || '-')}</td><td><span class="badge text-bg-success">${UI.esc(UI.msg(r.result))}</span></td></tr>`).join('')}</tbody></table><div class="server-footer"><span>${UI.endpoint('AuditLogs')}</span><span>${result.totalCount} ${UI.t('rows')}</span></div></div>`);
}
