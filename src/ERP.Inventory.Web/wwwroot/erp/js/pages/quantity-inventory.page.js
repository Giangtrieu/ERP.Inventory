// ── State ─────────────────────────────────────────────────────────────
let qtyBalanceRows = [];
let qtyActiveView = 'inventory'; // 'inventory' | 'receive' | 'issue' | 'adjust' | 'history'
let qtySelectedItem = null;      // { itemCode, itemName } — for detail drill-down

// ── Route Handler ─────────────────────────────────────────────────────
Router.register('quantity-inventory', async function () {
  $('#app').html(
    UI.pageHeader('Quantity Inventory', 'Home / Quantity Inventory',
      '<div class="permission-note"><i class="bi bi-shield-lock"></i>' + UI.t('Role and warehouse scoped') + '</div>') +
    `<div class="qty-layout gap-3">

      <!-- ── Left navigation ──────────────────────────────── -->
      <nav class="qty-nav" id="qtyNav">
        <div class="qty-nav-section">${UI.t('Inventory')}</div>
        <button class="qty-nav-item active" data-view="inventory">
          <i class="bi bi-boxes"></i>${UI.t('Quantity Stock')}
        </button>

        <hr class="qty-nav-sep">
        <div class="qty-nav-section">${UI.t('Operations')}</div>
        <button class="qty-nav-item" data-view="receive">
          <i class="bi bi-box-arrow-in-down"></i>${UI.t('Receive')}
        </button>
        <button class="qty-nav-item" data-view="issue">
          <i class="bi bi-box-arrow-up"></i>${UI.t('Issue')}
        </button>
        ${AppState.permissions.canManage
          ? `<button class="qty-nav-item" data-view="adjust">
               <i class="bi bi-sliders"></i>${UI.t('Adjust')}
             </button>`
          : ''}

        <hr class="qty-nav-sep">
        <div class="qty-nav-section">${UI.t('History')}</div>
        <button class="qty-nav-item" data-view="history">
          <i class="bi bi-clock-history"></i>${UI.t('Transactions')}
        </button>
      </nav>

      <!-- ── Right content area ────────────────────────────── -->
      <div class="qty-content" id="qtyContent">
        ${UI.loading()}
      </div>
    </div>`
  );

  // ── Nav click ────────────────────────────────────────────────────────
  $(document).off('click.qtyNav').on('click.qtyNav', '.qty-nav-item', function () {
    const view = $(this).data('view');
    $('.qty-nav-item').removeClass('active');
    $(this).addClass('active');
    qtySelectedItem = null;
    switchQtyView(view);
  });

  // ── Operation form events (delegated) ────────────────────────────────
  $(document).off('click.qtyAddLine').on('click.qtyAddLine', '#btnAddQuantityLine', function () {
    clearQuantityValidation();
      $('#quantityLineBody').append(renderQuantityLine());
      updateQuantityLineIndex();
  });
  $(document).off('click.qtyRemoveLine').on('click.qtyRemoveLine', '.btn-remove-quantity-line', function () {
    if ($('#quantityLineBody tr').length > 1) $(this).closest('tr').remove();
    clearQuantityValidation();
  });
    $(document).off('click.qtyPost').on('click.qtyPost', '#btnPostQuantity', postQuantityInventory);
    $(document).off('click.cancelQuantityEdit').on('click.cancelQuantityEdit', '#btnCancelQuantityEdit', () => {
        const viewType = AppState.documentEditor && String(AppState.documentEditor.type || '').startsWith('quantity-') ? AppState.documentEditor.type.replace('quantity-', '') : 'inventory';
        AppState.documentEditor = null;
        UI.toast(UI.t('Exited edit mode'), 'info');
        Router.go(`quantity-inventory`);
        $('.qty-nav-item').removeClass('active');
        $(`.qty-nav-item[data-view="${viewType}"]`).addClass('active');
        switchQtyView(viewType);
        
    });

  // ── Inventory filter events (delegated) ──────────────────────────────
  $(document).off('change.qtyFilter input.qtyFilter')
  .on('change.qtyFilter input.qtyFilter',
      '.cbo-input, #qtyContent [name="filterWarehouseId"], #qtyContent [name="filterItemId"], #qtyContent [name="keyword"]',
      UI.debounce(() => loadQuantityInventory(1), 250));
    
  // ── Detail back button ───────────────────────────────────────────────
  $(document).off('click.qtyBack').on('click.qtyBack', '#btnQtyBack', function () {
    qtySelectedItem = null;
    loadQtyInventoryPanel();
  });

  // ── Detail view button ───────────────────────────────────────────────
  //$(document).off('click.qtyDetail').on('click.qtyDetail', '.btn-qty-detail', function () {
  //  const itemCode = $(this).data('item-code');
  //  const itemName = $(this).data('item-name');
  //  const warehouseId = $(this).data('warehouse-id');
  //  qtySelectedItem = { itemCode, itemName, warehouseId };
  //  loadQtyDetailPanel(itemCode, itemName, warehouseId);
  //});

  // ── History filter events ────────────────────────────────────────────
  $(document).off('change.qtyHistFilter input.qtyHistFilter')
    .on('change.qtyHistFilter input.qtyHistFilter',
      '.cbo-input, #qtyContent [name="histWarehouseId"], #qtyContent [name="histItemId"], #qtyContent [name="histKeyword"]',
      UI.debounce(loadQuantityTransactions, 250));

  $(document).off('change.qtyDocFilter input.qtyDocFilter')
    .on('change.qtyDocFilter input.qtyDocFilter',
      '#qtyContent [name="qtyDocType"], #qtyContent [name="qtyDocKeyword"], #qtyContent [name="qtyDocFromDate"], #qtyContent [name="qtyDocToDate"]',
      UI.debounce(loadQuantityDocuments, 250));

  // ── Initial render ───────────────────────────────────────────────────
    const editor = AppState.documentEditor;
    const viewType = editor && String(editor.type || '').startsWith('quantity-') ? editor.type.replace('quantity-', '') : 'inventory';
    $('.qty-nav-item').removeClass('active');
    $(`.qty-nav-item[data-view="${viewType}"]`).addClass('active');
    await switchQtyView(viewType);
});

$(document).on('keydown.qtyRemoveLine', function (e) {
    // Ctrl + X
    if (e.altKey && e.key.toLowerCase() === 'x') {
        e.preventDefault();
        const $targetRow = $('#quantityLineBody tr.selected').length ? $('#quantityLineBody tr.selected') : $('#quantityLineBody tr:last');
        if ($targetRow.length) $targetRow.find('.btn-remove-quantity-line').trigger('click')
    }
});

$(document).on('keydown.quantityLineBody', function (e) {
    if (e.altKey && e.key.toLowerCase() === 'a') {
        e.preventDefault();
        $('#btnAddQuantityLine').trigger('click');
    }
});

// ── View router ───────────────────────────────────────────────────────
async function switchQtyView(view) {
  qtyActiveView = view;
  if (view === 'inventory')        { await loadQtyInventoryPanel(); }
  else if (view === 'history')     { await loadQtyHistoryPanel(); }
  else if (['receive','issue','adjust'].includes(view)) { loadQtyFormPanel(view); }
}

// ═══════════════════════════════════════════════════════════════
// PANEL 1 — Inventory (balance list + detail drill-down)
// ═══════════════════════════════════════════════════════════════
async function loadQtyInventoryPanel() {
  const preset = AppState.quantityPreset || {};
  AppState.quantityPreset = null;
  $('#qtyContent').html(`
    <div class="card"><div class="card-body">
      <div class="form-section-title">${UI.t('Quantity Stock')}</div>
      <div class="row g-3 mb-3">
        <div class="col-md-3">${UI.select('Warehouse', 'filterWarehouseId', AppState.lookups.warehouses, preset.warehouseId || AppState.lookups.warehouses?.[0]?.id || '')}</div>
        <div class="col-md-3">${UI.selectOption('Item','filterItemId', AppState.lookups.items)}</div>
        <div class="col-md-6">${UI.input('Keyword','text', preset.keyword || '', 'keyword')}</div>
      </div>
      <div id="quantityInventoryTable">${UI.loading()}</div>
    </div></div>`);
  await loadQuantityInventory();
}

async function loadQuantityInventory(page = 1, pageSize = AppState.pageSize || 25) {
  const query = {
    page, pageSize,
    warehouseId: $('#qtyContent [name="filterWarehouseId"]').val() || null,
    itemId:      $('#qtyContent [name="filterItemId"]').val() || null,
    status:      null,
    keyword:     $('#qtyContent [name="keyword"]').val() || null,
    ownerName:    null
  };
  $('#quantityInventoryTable').html(UI.loading());
  const result = await UI.api('/QuantityInventory/Balances', { query });
  if (!result.success) { $('#quantityInventoryTable').html(UI.empty(UI.resultError(result))); return; }

  const data = result.data || { items: [], totalCount: 0, page: 1 };
  qtyBalanceRows = data.items || [];
  if (!qtyBalanceRows.length) { $('#quantityInventoryTable').html(UI.empty('No data')); return; }

  const isAll = pageSize === 0;
  const pagination = Pagination.render({
    page, pageSize,
    total: data.totalCount,
    totalPages: Math.ceil(data.totalCount / pageSize),
    isAll,
    selectId: 'qtyBalancePageSizeSelect',
    onChange: 'loadQuantityInventory'
  });

  $('#quantityInventoryTable').html(`
    <div class="table-wrap">
      <table class="data-table"><thead><tr>
        <th class="px-3">${UI.t('Item')}</th>
        <th>${UI.t('Warehouse')}</th>
        <th>${UI.t('Quantity')}</th>
        <th>${UI.t('Last Updated')}</th>
        <th></th>
      </tr></thead>
      <tbody>${qtyBalanceRows.map(r => `<tr>
        <td class="px-3 fw-semibold">${UI.esc(r.itemCode)}<div class="small text-muted">${UI.esc(r.itemName)}</div></td>
        <td>${UI.esc(r.warehouseCode)}</td>
        <td class="fw-bold">${UI.esc(r.quantity)}</td>
        <td class="text-muted small">${UI.formatDate(r.lastUpdatedAt)}</td>
        <!--<td>
          <button class="btn btn-light btn-sm btn-qty-detail"
            data-item-code="${UI.esc(r.itemCode)}"
            data-item-name="${UI.esc(r.itemName)}"
            data-warehouse-id="${UI.esc(r.warehouseId)}"
            title="${UI.t('View Detail')}">
            <i class="bi bi-eye"></i>
          </button>
        </td>-->
      </tr>`).join('')}</tbody>
      </table>
      <div class="server-footer">
        <span>${UI.t('Quantity Stock')}: ${data.totalCount} ${UI.t('rows')}</span>
        <span>${UI.t('Page')} ${data.page}</span>
      </div>
    </div>${pagination}`);

  Pagination.bindPageSize('qtyBalancePageSizeSelect', loadQuantityInventory);
}

// ── Detail: ItemInstances for a specific ItemCode ──────────────────────
async function loadQtyDetailPanel(itemCode, itemName, warehouseId) {
  $('#qtyContent').html(`
    <div class="card"><div class="card-body">
      <div class="d-flex align-items-center gap-3 mb-3">
        <button class="btn btn-outline-secondary btn-sm" id="btnQtyBack">
          <i class="bi bi-arrow-left me-1"></i>${UI.t('Back')}
        </button>
        <div class="form-section-title mb-0">${UI.t('Item Instance')} — <span class="text-primary">${UI.esc(itemCode)}</span>
          <!--<span class="text-muted fw-normal small ms-2">${UI.esc(itemName)}</span>-->
        </div>
      </div>
      <div id="qtyDetailTable">${UI.loading()}</div>
    </div></div>`);

  const result = await UI.api('/QuantityInventory/Instances', {
    query: { itemCode, warehouseId: warehouseId || null }
  });

  if (!result.success) { $('#qtyDetailTable').html(UI.empty(UI.resultError(result))); return; }
  const rows = result.data || [];
  if (!rows.length) { $('#qtyDetailTable').html(UI.empty('No data')); return; }

  $('#qtyDetailTable').html(`
    <div class="table-wrap">
      <table class="data-table"><thead><tr>
        <th class="px-3">${UI.t('Document No')}</th>
        <th class="px-3">${UI.t('SN')}</th>
        <th>${UI.t('Warehouse')}</th>
        <th>${UI.t('Time')}</th>
      </tr></thead>
      <tbody>${rows.map(r => `<tr>
        <td class="px-3">${UI.esc(r.documentNo)}</td>
        <td class="px-3 fw-semibold font-monospace">${UI.esc(r.snCode)}</td>
        <td>${UI.esc(r.warehouseCode || '-')}</td>
        <td class="text-muted small">${UI.formatDate(r.createdAt)}</td>
      </tr>`).join('')}</tbody>
      </table>
      <div class="server-footer">
        <span>${UI.t('Item Instance')}: ${rows.length} ${UI.t('rows')}</span>
      </div>
    </div>`);
}

// ═══════════════════════════════════════════════════════════════
// PANEL 2 — Operation Form (Receive / Issue / Adjust)
// ═══════════════════════════════════════════════════════════════
function loadQtyFormPanel(operation) {
  const opLabel = { receive: UI.t('Receive'), issue: UI.t('Issue'), adjust: UI.t('Adjust') }[operation] || operation;
  const externalFields = operation === 'receive'
    ? `
        <div class="col-xl-3 col-md-4">${UI.input('Sender Code','text','','senderCode')}</div>
        <div class="col-xl-3 col-md-4">${UI.input('Sender Name','text','','senderName')}</div>
        <div class="col-xl-3 col-md-4">${UI.input('Sender Phone','text','','senderPhone')}</div>`
    : `
        <div class="col-xl-3 col-md-4">${UI.input('Receiver Code','text','','receiverCode')}</div>
        <div class="col-xl-3 col-md-4">${UI.input('Receiver Name','text','','receiverName')}</div>
        <div class="col-xl-3 col-md-4">${UI.input('Receiver Phone','text','','receiverPhone')}</div>`;
  const adjustmentFields = operation === 'adjust'
    ? `<div class="col-xl-3 col-md-4">${UI.select('Adjustment Type', 'adjustmentDirection', [
        { id: 'Increase', text: UI.t('Increase') },
        { id: 'Decrease', text: UI.t('Decrease') }
      ], 'Increase')}</div>`
    : '';

  $('#qtyContent').html(`
    <div class="card quantity-operation-card"><div class="card-body">
      <div class="form-section-title">${UI.t('Quantity Operation')} — ${opLabel}</div>
      <div id="quantityOperationValidation"></div>

      <div class="row g-3 mb-3">
        <div class="col-xl-3 col-md-4">${UI.select('Warehouse', 'operationWarehouseId', AppState.lookups.warehouses, AppState.lookups.warehouses[0].id)}</div>
        <!--<div class="col-xl-3 col-md-4">${UI.input('Category Code','text','','itemCategoryCode')}</div>-->
        <!--<div class="col-xl-3 col-md-4">${UI.input('Item Code','text','','quantityItemCode')}</div>-->
        <div class="col-xl-3 col-md-4">${UI.input('Document Date','date',today(),'documentDate')}</div>
        <div class="col-xl-3 col-md-4">${UI.input('Document No','text','','documentNo')}</div>
        <div class="col-xl-3 col-md-4">${UI.input('Approver','text',AppState.user.userName || '','approvedBy')}</div>
        <div class="col-xl-3 col-md-4">${UI.input('Notes','text','','note')}</div>
        ${externalFields}
        ${adjustmentFields}
      </div>

      <div class="d-flex justify-content-between align-items-center mb-2">
        <div class="fw-semibold small">${UI.t('Line Items')}</div>
        <button class="btn btn-sm btn-primary" id="btnAddQuantityLine">
          <i class="bi bi-plus-circle me-1"></i>${UI.t('Add line')}
        </button>
      </div>
      <div class="table-wrap quantity-line-wrap">
        <table class="data-table quantity-line-table">
          <thead><tr>
            <th class="col-stt">#</th>
            <th>${UI.t('Category Code')}</th>
            <th>${UI.t('PN')}</th>
            <th>${UI.t('Quantity')}</th>
            <th class="quantity-col-action"></th>
          </tr></thead>
          <tbody id="quantityLineBody">${renderQuantityLine()}</tbody>
        </table>
      </div>

      <div class="d-flex justify-content-end gap-2 mt-3">
        <button class="btn btn-primary" id="btnPostQuantity" data-operation="${operation}"><i class="bi bi-check2-circle me-2"></i>${UI.t('Save & Post')}</button>
        <button class="btn btn-outline-secondary d-none" id="btnCancelQuantityEdit"><i class="bi bi-x-circle me-2"></i>${UI.t('Cancel Edit')}</button>
      </div>
    </div></div>`);
    updateQuantityLineIndex();
    ScanQtySystem.init();
    applyQuantityEditorState(operation);
}

// ═══════════════════════════════════════════════════════════════
// PANEL 3 — Transaction History
// ═══════════════════════════════════════════════════════════════
async function loadQtyHistoryPanel() {
  $('#qtyContent').html(`
    <div class="card"><div class="card-body">
      <div class="form-section-title">${UI.t('Quantity Transactions')}</div>
      <div class="row g-3 mb-3">
        <div class="col-md-4">${UI.select('Warehouse', 'histWarehouseId', AppState.lookups.warehouses, AppState.lookups.warehouses[0].id)}</div>
        <div class="col-md-4">${UI.selectOption('Item','histItemId', AppState.lookups.items)}</div>
        <div class="col-md-4">${UI.input('Keyword','text','','histKeyword')}</div>
      </div>
      <div id="quantityTransactionTable">${UI.loading()}</div>
      <hr class="my-4">
      <div class="form-section-title">${UI.t('Documents')}</div>
      <div class="row g-3 mb-3">
        <div class="col-md-4">${UI.select('Document Type', 'qtyDocType', [
          { id: 'quantity-receive', text: UI.t('Receive') },
          { id: 'quantity-issue', text: UI.t('Issue') },
          { id: 'quantity-adjust', text: UI.t('Adjust') }
        ], '')}</div>
        <div class="col-md-4">${UI.input('Keyword','text','','qtyDocKeyword')}</div>
        <div class="col-md-2">${UI.input('From Date', 'date', firstDay(), 'qtyDocFromDate')}</div>
        <div class="col-md-2">${UI.input('To Date', 'date', today(), 'qtyDocToDate')}</div>
      </div>
      <div id="quantityDocumentTable">${UI.loading()}</div>
    </div></div>`);
  await loadQuantityTransactions();
  await loadQuantityDocuments();
}

function today() {
    return new Date().toISOString().slice(0, 10);
}
function firstDay() {
    return new Date(new Date().getFullYear(), 0, 1).toISOString().slice(0, 10);
}

async function loadQuantityTransactions() {
  const query = {
    warehouseId: $('#qtyContent [name="histWarehouseId"]').val() || null,
    itemId:      $('#qtyContent [name="histItemId"]').val() || null,
    keyword:     $('#qtyContent [name="histKeyword"]').val() || null,
    take: 100
  };
  $('#quantityTransactionTable').html(UI.loading());
  const result = await UI.api('/QuantityInventory/Transactions', { query });
  const rows = result.data || [];
  if (!rows.length) { $('#quantityTransactionTable').html(UI.empty('No data')); return; }

  $('#quantityTransactionTable').html(`
    <div class="table-wrap">
      <table class="data-table"><thead><tr>
        <th class="px-3">${UI.t('Time')}</th>
        <th >${UI.t('Document No')}</th>
        <th>${UI.t('Operation')}</th>
        <th>${UI.t('Item')}</th>
        <th>${UI.t('Qty')}</th>
        <th>${UI.t('User')}</th>
      </tr></thead>
      <tbody>${rows.map(r => `<tr>
        <td class="px-3 small text-muted">${UI.formatDate(r.postedAt)}</td>
        <td>${UI.t(r.documentNo)}</td>
        <td>${UI.t(r.transactionType)}</td>
        <td>${UI.esc(r.itemCode)}</td>
        <td class="${r.quantityDelta < 0 ? 'text-danger' : 'text-success'} fw-bold">${r.quantityDelta > 0 ? '+' : ''}${UI.esc(r.quantityDelta)}</td>
        <td class="small text-muted">${UI.esc(r.postedBy)}</td>
      </tr>`).join('')}</tbody>
      </table>
      <div class="server-footer">
        <span>${UI.t('Transactions')}: ${rows.length} ${UI.t('rows')}</span>
      </div>
    </div>`);
}

async function loadQuantityDocuments() {
  if (!$('#quantityDocumentTable').length) return;
  const type = $('#qtyContent [name="qtyDocType"]').val() || 'quantity-receive';
  const rows = await UI.api('/Documents/List', {
    query: {
      type,
      keyword: $('#qtyContent [name="qtyDocKeyword"]').val() || null,
      fromDate: $('#qtyContent [name="qtyDocFromDate"]').val() || null,
      toDate: $('#qtyContent [name="qtyDocToDate"]').val() || null
    }
  });
  if (!rows.length) { $('#quantityDocumentTable').html(UI.empty('No data')); return; }
  $('#quantityDocumentTable').html(`<div class="table-wrap"><table class="data-table"><thead><tr><th class="px-3">${UI.t('Document No')}</th><th>${UI.t('Document Date')}</th><th>${UI.t('Warehouse')}</th><th>${UI.t('Lines')}</th><th>${UI.t('Actions')}</th></tr></thead><tbody>${rows.map(r => `<tr><td class="px-3 fw-semibold">${UI.esc(r.documentNo)}</td><td>${UI.formatDate(r.documentDate)}</td><td>${UI.esc(r.warehouse || '-')}</td><td>${UI.esc(r.lines)}</td><td>${buildDocumentActionButtons(type, r.id)}</td></tr>`).join('')}</tbody></table></div>`);
}

// ═══════════════════════════════════════════════════════════════
// POST OPERATION
// ═══════════════════════════════════════════════════════════════
async function postQuantityInventory() {
  clearQuantityValidation();

  const operation = $('#btnPostQuantity').data('operation') || 'receive';
  const validationErrors = validateQuantityBeforePost();
  if (validationErrors.length) { showQuantityValidation(validationErrors); return; }

    const warehouseId = parseInt($('#qtyContent [name="operationWarehouseId"]').val() || '0', 10);
  //const itemCode     = $('#qtyContent [name="quantityItemCode"]').val().trim();
  //const itemCategoryCode = $('#qtyContent [name="itemCategoryCode"]').val().trim();
  const lines = $('#quantityLineBody tr').map(function () {
    const row = $(this);
      return {
          itemCategoryCode: row.find('[name="itemCategoryCode"]').val() || '',
          itemCode: row.find('[name="quantityItemCode"]').val() || '',
          snCode: '',
          quantity: parseFloat(row.find('[name="quantity"]').val() || '0'),
          status:   row.find('[name="lineStatus"]').val() || 'Normal'
    };
  }).get().filter(l => l.quantity > 0);

    const itemCodes = [...new Set(
        lines.map(x => x.itemCode).filter(Boolean)
    )];

  const payload = {
    warehouseId, 
    documentDate: $('#qtyContent [name="documentDate"]').val(),
    documentNo:   $('#qtyContent [name="documentNo"]').val().trim(),
    approvedBy:   $('#qtyContent [name="approvedBy"]').val().trim(),
    senderCode:   $('#qtyContent [name="senderCode"]').val()?.trim() || '',
    senderName:   $('#qtyContent [name="senderName"]').val()?.trim() || '',
    senderPhone:  $('#qtyContent [name="senderPhone"]').val()?.trim() || '',
    receiverCode: $('#qtyContent [name="receiverCode"]').val()?.trim() || '',
    receiverName: $('#qtyContent [name="receiverName"]').val()?.trim() || '',
    receiverPhone: $('#qtyContent [name="receiverPhone"]').val()?.trim() || '',
    adjustmentDirection: $('#qtyContent [name="adjustmentDirection"]').val() || '',
    ownerName:     null,
    note:         $('#qtyContent [name="note"]').val().trim(),
    lines
  };

  const opLabel = { receive: UI.t('Receive'), issue: UI.t('Issue'), adjust: UI.t('Adjust') }[operation];
  UI.confirm('Confirm Save & Post', 'This operation will be posted immediately.',
    `${UI.t('Operation')}: <b>${opLabel}</b><br>${UI.t('Item')}: <b>${UI.esc(itemCodes)}</b><br>${UI.t('Rows')}: <b>${lines.length}</b>`,
    async function () {
      const apiOp = operation.charAt(0).toUpperCase() + operation.slice(1);
      const editor = AppState.documentEditor;
      const btn = $('#btnPostQuantity');
      if (btn.prop('disabled')) return;
      btn.prop('disabled', true).html(`<span class="spinner-border spinner-border-sm me-2"></span>${UI.t(editor && editor.type === `quantity-${operation}` ? 'Saving' : 'Save & Post')}`);
      const result = editor && editor.type === `quantity-${operation}`
        ? await UI.api('/Documents/Edit', { method: 'POST', query: { type: editor.type, id: editor.id }, data: payload })
        : await UI.api(`/QuantityInventory/${apiOp}`, { method: 'POST', data: payload });
      btn.prop('disabled', false).html(`<i class="bi bi-check2-circle me-2"></i>${UI.t(editor && editor.type === `quantity-${operation}` ? 'Save Changes' : 'Save & Post')}`);
      if (!result.success) { showQuantityValidation(quantityErrorsFromResult(result)); return; }
      UI.toast(UI.msg(result.message || (editor && editor.type === `quantity-${operation}` ? 'Document updated.' : 'Quantity inventory posted.')));
      AppState.documentEditor = null;
      // Switch back to inventory view to show updated balances
      $('.qty-nav-item').removeClass('active');
      $('.qty-nav-item[data-view="inventory"]').addClass('active');
      await switchQtyView('inventory');
    });
}

function applyQuantityEditorState(operation) {
  const editor = AppState.documentEditor;
  if (!editor || editor.type !== `quantity-${operation}` || !editor.payload) return;
    $('#btnPostQuantity').html(`<i class="bi bi-save me-2"></i>${UI.t('Save Changes')}`);
    $('#btnCancelQuantityEdit').removeClass('d-none');
  $('#quantityOperationValidation').before(`<div class="alert alert-warning py-2">${UI.t('Editing document')}: <b>${UI.esc(editor.documentNo || editor.payload.documentNo || '')}</b></div>`);
  const payload = editor.payload;
  const fieldMap = {
    warehouseId: 'operationWarehouseId',
    itemCode: 'quantityItemCode',
    itemCategoryCode: 'itemCategoryCode',
    documentDate: 'documentDate',
    documentNo: 'documentNo',
    approvedBy: 'approvedBy',
    senderCode: 'senderCode',
    senderName: 'senderName',
    senderPhone: 'senderPhone',
    receiverCode: 'receiverCode',
    receiverName: 'receiverName',
    receiverPhone: 'receiverPhone',
    adjustmentDirection: 'adjustmentDirection',
    ownerName: '',
    note: 'note',
  };
  Object.entries(payload).forEach(([key, value]) => {
      if (key === 'lines') return;
      if (key === 'documentDate' && value) {
          value = value.split('T')[0];
      }
    const el = $(`#qtyContent [name="${fieldMap[key] || key}"]`);
    if (el.length) el.val(value == null ? '' : value);
  });
  const lines = Array.isArray(payload.lines) ? payload.lines : [];
  if (!lines.length) return;
  $('#quantityLineBody').html(lines.map(() => renderQuantityLine()).join(''));
  $('#quantityLineBody tr').each(function (index) {
    const line = lines[index] || {};
    $(this).find('[name="itemCategoryCode"]').val(line.itemCategoryCode || '');
    $(this).find('[name="quantityItemCode"]').val(line.itemCode || '');
    $(this).find('[name="quantity"]').val(line.quantity || 0);
    $(this).find('[name="lineStatus"]').val(line.status || 'Normal');
  });
  updateQuantityLineIndex();
}

// ═══════════════════════════════════════════════════════════════
// VALIDATION HELPERS
// ═══════════════════════════════════════════════════════════════
function clearQuantityValidation() {
  $('#quantityOperationValidation').empty();
  $('#qtyContent .is-invalid').removeClass('is-invalid');
}

function showQuantityValidation(errors) {
  const list = (errors || []).filter(Boolean);
  if (!list.length) return;
  list.forEach(e => {
    if (e.row && e.field) {
      $('#quantityLineBody tr').eq(e.row - 1).find(`[name="${e.field}"]`).addClass('is-invalid');
    } else if (e.field) {
      $(`#qtyContent [name="${e.field}"]`).addClass('is-invalid');
    }
  });
  $('#quantityOperationValidation').html(`<div class="validation-panel mb-3">
    <div class="validation-title"><i class="bi bi-exclamation-triangle"></i>${UI.t('Please correct the highlighted data before posting.')}</div>
    <div class="validation-list">${list.map(e => `<div class="validation-item">
      <span class="validation-field">${e.row ? `${UI.t('Row')} ${UI.esc(e.row)} &middot; ` : ''}${UI.t(e.label || 'System')}</span>
      <span>${UI.esc(UI.msg(e.message || e))}</span>
    </div>`).join('')}</div>
  </div>`);
  const panel = $('#quantityOperationValidation')[0];
  if (panel) panel.scrollIntoView({ behavior: 'smooth', block: 'center' });
}

function quantityErrorsFromResult(result) {
  const errors = Array.isArray(result && result.errors) && result.errors.length
    ? result.errors
    : [result && result.message ? result.message : 'Request failed.'];
  return errors.map(message => ({ label: 'System', message }));
}

function validateQuantityBeforePost() {
  const errors = [];
  if (!$('#qtyContent [name="operationWarehouseId"]').val())
    errors.push({ field: 'operationWarehouseId', label: 'Warehouse', message: 'Warehouse is required.' });

  $('#quantityLineBody tr').each(function (index) {
      const row = $(this);
      if (!row.find('[name="quantityItemCode"]').val().trim())
          errors.push({ field: 'quantityItemCode', label: 'Item Code', message: 'Item Code is required.' });
    const qty = parseFloat(row.find('[name="quantity"]').val() || '0');
    if (qty <= 0) errors.push({ row: index + 1, field: 'quantity', label: 'Qty', message: 'Quantity must be greater than zero.' });
  });
  return errors;
}

// ═══════════════════════════════════════════════════════════════
// LINE TEMPLATE
// ═══════════════════════════════════════════════════════════════
function renderQuantityLine() {
    return `<tr class="quantity-line">
    <td class="col-stt"></td>
    <td>${UI.input('Category Code', 'text', '', 'itemCategoryCode')}</td>
    <td>${UI.input('PN', 'text', '', 'quantityItemCode')}</td>
    <td>${UI.input('Quantity', 'number', '1', 'quantity')}</td>
    <td><button class="btn btn-light btn-sm btn-remove-quantity-line" type="button"><i class="bi bi-x-lg"></i></button></td>
  </tr>`;
}

function updateQuantityLineIndex() {
    $('.quantity-line-table tbody tr').each(function (index) {
        $(this).find('.col-stt').text(index + 1);
    });
}

// ═══════════════════════════════════════════════════════════════
// UTILITY
// ═══════════════════════════════════════════════════════════════
function today() {
  return new Date().toISOString().slice(0, 10);
}

//document.addEventListener('DOMContentLoaded', () => {
//    ScanQtySystem.init();
//});

window.ScanQtySystem = (() => {

let enterLock = false;

function init() {
    const table = document.querySelector('#quantityLineBody');
    if (!table) return;

    focusFirstInput();

    table.addEventListener('keydown', handleEnterFlow);

    $(document)
        .off('input.scan')
        .on('input.scan', '#quantityLineBody input[name="SN"]', function () {
            checkDuplicateSerialRealtime();
        });

    document.addEventListener('focusin', e => {
        if (e.target.matches('#quantityLineBody input')) {
            setTimeout(() => e.target.select(), 0);
        }
    });
}

function handleEnterFlow(e) {
    if (e.key !== 'Enter') return;

    if (enterLock) {
        e.preventDefault();
        return;
    }

    enterLock = true;

    setTimeout(() => {
        enterLock = false;
    }, 150);

    const input = e.target;

    if (!input.matches('input, select')) return;

    e.preventDefault();

    if (input.classList.contains('input-duplicate')) {
        beep();
        scrollToFirstDuplicate();
        input.value = '';
        input.focus();
        return;
    }

    const skipNames = ['condition', 'newStatus', 'lineStatus', 'lineNote', 'result'];
    const row = input.closest('tr');
    const inputs = Array.from(
        row.querySelectorAll('input, select')
    ).filter(el => {
        if (el.disabled || el.readOnly) return false;
        if (skipNames.includes(el.name)) return false;
        if (el.type === 'hidden') return false;
        if (el.offsetParent === null) return false;
        return true;
    });

    const index = inputs.indexOf(input);

    if (index < inputs.length - 1) {
        inputs[index + 1].focus();
        return;
    }

    let nextRow = row.nextElementSibling;

    if (!nextRow) {
        addNewQutyRow();
        nextRow = row.nextElementSibling;
    }

    if (nextRow) { focusRowFirstInput(nextRow); }
}

function addNewQutyRow() {
    const tbody = document.getElementById('quantityLineBody');

    const html = renderQuantityLine();
    updateQuantityLineIndex();
    tbody.insertAdjacentHTML('beforeend', html);
    checkDuplicateSerialRealtime();
}

function focusFirstInput() {
    const el = document.querySelector('#quantityLineBody tr:first-child input'); if (el) el.focus();
}

function focusRowFirstInput(row) {
    const el = row.querySelector('input, select'); if (el) el.focus();
}

function checkDuplicateSerialRealtime() {
    const map = new Map();

    const rows = $('#quantityLineBody tr');

    rows.removeClass('row-duplicate');
    rows.find('input').removeClass('input-duplicate');

    rows.each(function (index) {
        const row = $(this);
        const serial = (row.find('[name="snCode"]').val() || ''
        ).trim().toLowerCase();

        if (!serial) return;

        if (map.has(serial)) {

            const firstIndex = map.get(serial);
            row.addClass('row-duplicate');
            row.find('[name="snCode"]').addClass('input-duplicate');
            const firstRow = rows.eq(firstIndex);
            firstRow.addClass('row-duplicate');
            firstRow.find('[name="snCode"]').addClass('input-duplicate');

            beep();

        } else {

            map.set(serial, index);
        }
    });
}

function beep() {
    try {
        const ctx = new ( window.AudioContext || window.webkitAudioContext
        )();
        const osc = ctx.createOscillator();
        osc.type = 'square';
        osc.frequency.setValueAtTime(800, ctx.currentTime);
        osc.connect(ctx.destination);
        osc.start();
        osc.stop(ctx.currentTime + 0.1);
    } catch { }
}

function scrollToFirstDuplicate() {
    const el = document.querySelector('.input-duplicate');

    if (el) {
        el.scrollIntoView({
            behavior: 'smooth',
            block: 'center'
        });
    }
}

return {
    init
};

}) ();
