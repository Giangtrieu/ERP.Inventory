Router.register('tracking', function(){
  const keyword = AppState.currentTrackingKeyword || '';
  $('#app').html(UI.pageHeader('Tracking','Home / Tracking','<button class="btn btn-outline-secondary" data-go="inventory"><i class="bi bi-boxes me-2"></i>' + UI.t('Inventory List') + '</button>') +
    `<div class="card tracking-search">
      <div class="input-group input-group-lg">
        <span class="input-group-text"><i class="bi bi-upc-scan"></i></span>
        <input id="trackingKeyword" class="form-control scanner-input" autocomplete="off" placeholder="${UI.esc(UI.t('Scan or enter item code / serial / barcode'))}" autofocus value="${UI.esc(keyword)}" />
        <button id="btnTrackingSearch" class="btn btn-primary">${UI.t('Search')}</button>
      </div>
      <small class="text-muted">${UI.t('Scanner input, item code, serial number and barcode are resolved by /Tracking/Search.')}</small>
    </div>
    <div id="trackingEmpty">${UI.empty('Scan or enter an item to see current status and location.')}</div>
    <div id="trackingResult" class="d-none"></div>`);

  $('#trackingKeyword').focus();
  $('#btnTrackingSearch').on('click', trackingSearch);
  $('#trackingKeyword').on('keydown', e => { if(e.key === 'Enter') trackingSearch(); });
  $('#trackingKeyword').on('input', UI.debounce(() => {
    const v = $('#trackingKeyword').val().trim();
    if(v.length >= 3) trackingSearch(false);
  }, 350));
  if(keyword) trackingSearch(true);
});

let lastTrackingKeyword = null;

async function trackingSearch(explicit=true){
  const keyword = $('#trackingKeyword').val().trim();
  if(!keyword){ UI.toast(UI.t('Keyword is required.')); return; }
  if(keyword === lastTrackingKeyword && !explicit) return;
  lastTrackingKeyword = keyword;
  AppState.currentTrackingKeyword = keyword;
  $('#trackingEmpty').addClass('d-none');
  $('#trackingResult').removeClass('d-none').html(UI.loading());

  const result = await UI.api('/Tracking/Search', { query: { keyword } });
  if(!result.success){
    $('#trackingResult').html(UI.empty(UI.resultError(result)));
    return;
  }
  const rows = result.data || [];
  if(!rows.length){
    $('#trackingResult').html(UI.empty('No data'));
    return;
  }
  if(rows.length > 1) renderTrackingList(rows);
  renderTrackingDetail(rows[0]);
}

function renderTrackingList(rows){
  $('#trackingResult').html(`<div class="card mt-3"><div class="card-body">
    <div class="form-section-title">${UI.t('Search results')}</div>
    <div class="table-wrap"><table class="data-table"><thead><tr><th class="px-3">${UI.t('Item')}</th><th>${UI.t('Serial / Barcode')}</th><th>${UI.t('Status')}</th><th>${UI.t('Location')}</th><th></th></tr></thead>
      <tbody>${rows.map((r,i)=>`<tr><td class="px-3 fw-semibold">${UI.esc(r.itemCode)}<div class="small text-muted">${UI.esc(r.itemName)}</div></td><td>${UI.esc(r.serialNumber || r.barcode || '-')}</td><td>${UI.badge(r.status)}</td><td>${UI.esc(r.locationPath)}</td><td><button class="btn btn-light btn-sm btn-open-tracking-row" data-index="${i}"><i class="bi bi-eye"></i></button></td></tr>`).join('')}</tbody>
    </table></div></div></div>
    <div id="trackingDetail"></div>`);
  $(document).off('click.trackingRow').on('click.trackingRow', '.btn-open-tracking-row', function(){ renderTrackingDetail(rows[$(this).data('index')]); });
}

async function renderTrackingDetail(item){
  const target = $('#trackingDetail').length ? $('#trackingDetail') : $('#trackingResult');
    target.html(`<div class="card p-3 mt-2"><div class="row g-3">
    <div class="form-title">${UI.t('Item Information')} ${item.serialNumber}</div>
    <div class="col-lg-4 d-flex"><div class="card info-card w-100"><div class="card-body"><h3>${UI.esc(item.itemCode)}</h3><p class="text-muted mb-2">${UI.esc(item.itemName)}</p><p class="mb-1">${UI.t('Serial')}: <b>${UI.esc(item.serialNumber || '-')}</b></p><p class="mb-0">${UI.t('Barcode')}: ${UI.esc(item.barcode || '-')}</p></div></div></div>
    <div class="col-lg-4 d-flex"><div class="card info-card w-100"><div class="card-body">${UI.badge(item.status)}<p class="mt-3 mb-1">${UI.t('Holder')}: <b>${UI.esc(item.holderName)}</b></p><p class="mb-0">${UI.t('Document No')}: <b>${UI.esc(item.referenceDocumentNo || '-')}</b></p></div></div></div>
    <div class="col-lg-4 d-flex"><div class="card info-card w-100"><div class="card-body"><h3>${UI.t('Current Location')}</h3><p>${UI.esc(item.locationPath)}</p><p class="small text-muted">${UI.t('Updated')}: ${UI.formatDate(item.updatedAt)} ${UI.t('by')} ${UI.esc(item.updatedBy)}</p>${renderQuickActions(item)}</div></div></div>
  </div></div>  
  <ul class="nav nav-tabs mt-3">
    <li class="nav-item"><button class="nav-link active" data-bs-toggle="tab" data-bs-target="#timelineTab">${UI.t('Timeline')}</button></li>
    <li class="nav-item"><button class="nav-link" data-bs-toggle="tab" data-bs-target="#documentsTab">${UI.t('Related Documents')}</button></li>
  </ul>
  <div class="tab-content card tab-card">
    <div class="tab-pane fade show active" id="timelineTab"><div id="timeline">${UI.loading()}</div></div>
    <div class="tab-pane fade p-4" id="documentsTab">${UI.loading()}</div>
    <div class="tab-pane fade p-4" id="stockTab"><div class="audit-footer">${UI.t('Current state is calculated from CurrentItemLocation and StockBalance in SQL Server.')}</div></div>
  </div>`);
    //<li class="nav-item"><button class="nav-link" data-bs-toggle="tab" data-bs-target="#stockTab">${UI.t('Stock Balance')}</button></li> // bo ton kho
  await renderTimeline(item.itemInstanceId);
  await relatedDocumentsTable(item.itemInstanceId);
}

function renderQuickActions(item){
  if(!AppState.permissions.canOperate) return `<div class="text-muted small">${UI.t('No quick action available for current role.')}</div>`;
  const actions = [];
  if(item.canMove) actions.push(`<button class="btn btn-outline-primary" data-go="move">${UI.t('Move')}</button>`);
  if(item.canSendRepair) actions.push(`<button class="btn btn-outline-primary" data-go="repair-send">${UI.t('Repair')}</button>`);
  if(item.canLend) actions.push(`<button class="btn btn-outline-primary" data-go="borrow-lend">${UI.t('Lend')}</button>`);
  return actions.length ? `<div class="btn-group btn-group-sm">${actions.join('')}</div>` : `<div class="text-muted small">${UI.t('No quick action available for current status.')}</div>`;
}

async function renderTimeline(itemInstanceId){
  const result = await UI.api(`/Tracking/History/${itemInstanceId}`, { query: { page: 1, pageSize: 20 } });
  if(!result.success){ $('#timeline').html(UI.empty(UI.resultError(result))); return; }
  const rows = (result.data && result.data.items) || [];
  if(!rows.length){ $('#timeline').html(UI.empty('No data')); return; }
  $('#timeline').html(rows.map(r => `<div class="timeline-item">
    <div class="timeline-time">${UI.formatDate(r.performedAt)}</div>
    <div><div class="fw-bold">${UI.esc(UI.enum('MovementActionType', r.actionType))}</div><div class="text-muted small">${UI.esc(r.fromLocation || '-')} <i class="bi bi-chevron-right"></i> ${UI.esc(r.toLocation || '-')}</div></div>
    <div>${UI.badge(r.newStatus)}<div class="small text-muted mt-1">${UI.esc(r.documentNo)} &middot; ${UI.esc(r.performedBy)}</div></div>
  </div>`).join('') + `<div class="server-footer"><span>${UI.t('Page')} ${result.data.page}</span><span>${result.data.totalCount} ${UI.t('history rows')}</span></div>`);
}

async function relatedDocumentsTable(itemInstanceId) {
    const result = await UI.api(`/Tracking/History/${itemInstanceId}`, { query: { page: 1, pageSize: 20 } });
    if (!result.success) { $('#timeline').html(UI.empty(UI.resultError(result))); return; }
    const rows = result.data?.items || [];

    if (!rows.length) {
        $('#documentsTab').html(UI.empty('No data'));
        return;
    }

    const html = `
        <div class="table-wrap">
            <table class="data-table">
                <thead>
                    <tr>
                        <th class="px-3">${UI.t('Document No')}</th>
                        <th>${UI.t('Current Status')}</th>
                        <th>${UI.t('Holder')}</th>
                        <th>${UI.t('Updated At')}</th>
                        <th></th>
                    </tr>
                </thead>
                <tbody>
                    ${rows.map(r => `
                        <tr>
                            <td class="px-3">${UI.esc(r.documentNo || '-')}</td>
                            <td>${UI.badge(r.newStatus)}</td>
                            <td>${UI.esc(r.toLocation || '-')}</td>
                            <td>${UI.formatDate(r.performedAt)}</td>
                            <td>${buildDocumentActionButtons(mapDocumentType(r.documentType), r.documentId)}</td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
        </div>
    `;

    $('#documentsTab').html(html);
}

function mapDocumentType(docType) {
    const map = {
        BorrowDocument: "borrow-lend",
        InboundDocument: "inbound",
        MoveDocument: "move",
        InventoryCheckDocument: "inventory-check",
        RepairDocument: "repair-send",
        AdjustmentDocument: "adjustment"
    };

    return map[docType] || "";
}
