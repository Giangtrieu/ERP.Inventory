Router.register('operation', async function (type) {
    const c = OperationModel.configs[type];
    if (!c) { Router.go(AppConfig.defaultRoute); return; }
    if (!AppState.permissions.canOperate && type !== 'adjustment') {
        $('#app').html(UI.pageHeader(c[0], c[1]) + UI.empty('Access denied for current role.'));
        return;
    }
    if (type === 'adjustment' && !AppState.permissions.canManage) {
        $('#app').html(UI.pageHeader(c[0], c[1]) + UI.empty('Access denied for current role.'));
        return;
    }

    const vm = await buildOperationVm(type);
    window.currentOperationType = type;
    window.currentVM = vm;
    $('#app').html(UI.pageHeader(c[0], c[1], '<div class="permission-note"><i class="bi bi-shield-lock"></i>' + UI.t('Role and warehouse scoped') + '</div>') +
        `<div class="row g-3">
      <div class="col-xl-9">
        <div class="card mb-3"><div class="card-body"><div class="form-section-title">${UI.t('Header')}</div>${renderOperationHeader(type, vm)}</div></div>
        <div class="card mb-3"><div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-3">
            <div class="form-section-title mb-0">${UI.t('Line Items')}</div>
            ${type != 'repair-send' && type != 'repair-receive' ? `<button class="btn btn-sm btn-primary" id="btnAddOperationLine"><i class="bi bi-plus-circle me-2"></i>${UI.t('Add line')}</button>` : ''}
            
          </div>
          <div id="operationValidation"></div>
          ${type === 'move' ? '<div id="moveOccupancyHints"></div>' : ''}
          ${type === 'inbound' ? `<div class="small text-muted mb-2">${UI.t('The same item can be entered on multiple lines when each serial/barcode and bin is unique.')}</div>` : ''}
          <div id="operationLines">${renderOperationLines(type, vm)}</div>
        </div></div>
        <div class="card"><div class="card-body"><div class="row g-3"><div class="col-md-6"><label class="form-label w-100"><span class="fw-semibold small">${UI.t('Attachments')}</span><input id="operationAttachments" type="file" class="form-control" multiple accept=".pdf,.jpg,.jpeg,.png,.xlsx,.xls,.docx" /></label><div class="small text-muted">${UI.t('Attach invoices, handover records, repair receipts or inventory evidence when available.')}</div></div><div class="col-md-6">${UI.input(UI.t('Notes'), 'text', '', 'note')}</div></div></div></div>
      </div>
      <div class="col-xl-3">
        <div class="card sticky-post"><div class="card-body">
          <div class="form-section-title">${UI.t('Post Summary')}</div>
          <p class="text-muted">${UI.esc(UI.t(c[3]))}</p>
          <div class="audit-footer mb-3">
            <div>${UI.t('CreatedBy')}: <b>${UI.esc(AppState.user.userName)}</b></div>
            <div>${UI.t('ApprovedBy')}: <b id="operationApprovedBy">${UI.esc(AppState.user.userName)}</b></div>
            <div>${UI.t('Posted')}: <b>${UI.t('Immediately')}</b></div>
            <div>${UI.t('History')}: <b>${UI.t('Append only')}</b></div>
          </div>
          <button class="btn btn-primary w-100" id="btnOperationPost"><i class="bi bi-check2-circle me-2"></i>${UI.t('Save & Post')}</button>
        </div></div>
      </div>
    </div>
    <div class="card mt-3"><div class="card-body">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div class="form-section-title mb-0">${UI.t('Documents')}</div>
        <button class="btn btn-outline-secondary btn-sm" id="btnLoadDocuments"><i class="bi bi-arrow-clockwise"></i></button>
      </div>
      <div class="row g-3 mb-3">
        <div class="col-md-3">${UI.input('From Date', 'date', firstDay(), 'docFromDate')}</div>
        <div class="col-md-3">${UI.input('To Date', 'date', today(), 'docToDate')}</div>
        <div class="col-md-4">${UI.input('Keyword', 'text', '', 'docKeyword')}</div>
        <div class="col-md-2 d-flex align-items-end"><label class="form-label w-100"><span class="fw-semibold small"></span><button class="btn btn-primary w-100" id="btnSearchDocuments">${UI.t('Search')}</button></label></div>
      </div>
      <div class="row g-3">
      <div class="col-xl-12" id="operationDocuments">${UI.loading()}</div>
      ${type === 'inventory-check' ? `
          
          <div class="col-xl-12"><div class="card h-100">
          <div class="card-body row g-3">
          <div class="card-body d-flex justify-content-between align-items-center"><div class="form-section-title">${UI.t('Inventory Preview')}</div><button class="btn btn-primary" id="btnExportInventory"><i class="bi bi-download me-2"></i>${UI.t('Export Inventory')}</button>
          </div>
          <div id="reportsInventory">${UI.loading()}</div></div></div></div>
      `: ''}
    </div></div></div>`);
    $('#btnExportInventory').on('click', () => exportFile('/Export/Inventory'));
    wireOperationEvents(type, vm);
    await loadOperationDocuments(type);
    setTimeout(() => initScanSystem(), 100);
    loadReportsInventory()
});

function exportFile(url) {
    window.location = `${UI.resolveUrl(url)}?${$.param(reportFilterQuery())}`;
}

function reportFilterQuery() {
    return {
        warehouseId: $('#app [name="warehouseId"]').val() || '',
    };
}

async function buildOperationVm(type) {
    const firstWarehouse = (AppState.lookups.warehouses || [])[0];
    const warehouseId = firstWarehouse ? firstWarehouse.id : '';
    const bins = [];
    //const bins = await loadOperationBins(type, warehouseId);
    let itemInstances = [];
    let repairDocuments = [];
    let borrowDocuments = [];
    if (type === 'repair-receive') {
        repairDocuments = await UI.api('/Lookup/RepairDocuments');
    } else if (type === 'borrow-return') {
        borrowDocuments = await UI.api('/Lookup/BorrowDocuments');
    }

    return { warehouseId, bins, itemInstances, repairDocuments, borrowDocuments };
}

function operationUsesAvailableBins(type) {
    return ['inbound', 'repair-receive', 'borrow-return', 'adjustment'].includes(type);
}

function loadOperationBins(type, warehouseId) {
    return UI.api('/Lookup/Bins', { query: { warehouseId, availableOnly: operationUsesAvailableBins(type), includeOccupancy: type === 'move' } });
}

function renderOperationLines(type, vm) {
    const rows = renderLineRow(type, vm, 1);
    let tableClass = 'data-table';
    if (type === 'adjustment') tableClass = 'data-table-adjust';
    return `<div class="table-wrap"><table class="${tableClass}"><thead>${lineHeader(type)}</thead><tbody id="operationLineBody">${rows}</tbody></table></div>`;
}

function today() {
    return new Date().toISOString().slice(0, 10);
}
function firstDay() {
    return new Date(new Date().getFullYear(), 0, 1).toISOString().slice(0, 10);
}

function renderOperationHeader(type, vm) {
    const fields = OperationLineConfig.headers[type] || OperationLineConfig.headers.default;
    return `<div class="row g-3">${fields.map(f => renderHeaderField(f, vm)).join('')}</div>`;
}
function renderHeaderField(f, vm) {
    let html = '';
    if (f.type === 'select') html = UI.select(f.label, f.name, f.source ? f.source(vm) : [], f.value ? f.value(vm) : '');
    else if (f.type === 'input') html = UI.input(f.label, f.inputType || 'text', f.value ? f.value(vm) : '', f.name);
    else if (f.type === 'custom') html = f.render(vm);
    return ` <div class="${f.col || 'col-md-4'}">${html} </div>`;
}

function lineHeader(type) {
    const cfg = OperationLineConfig.tables[type] || OperationLineConfig.tables.default;
    return `<tr>${cfg.headers.map(h => `<th class="${h[0]}">${UI.t(h[1])}</th>`).join('')}</tr>`;
}

function renderLineRow(type, vm, index, bin) {
    const cfg = OperationLineConfig.tables[type] || OperationLineConfig.tables.default;
    return `<tr class="operation-line">${cfg.row({ vm, index, bin })}</tr>`;
}

function wireOperationEvents(type, vm) {
    $('#btnAddOperationLine').on('click', function () {
        clearOperationValidation();
        $('#operationLineBody').append(renderLineRow(type, vm, $('#operationLineBody tr').length + 1));
        refreshOperationRowAvailability(type);
    });
    $(document).off('click.removeOperationLine').on('click.removeOperationLine', '.btn-remove-line', function () {
        clearOperationValidation();
        if ($('#operationLineBody tr').length > 1) $(this).closest('tr').remove();
        $('#operationLineBody .line-no').each((i, el) => $(el).text(i + 1));
        refreshOperationRowAvailability(type);
    });
    $(document).off('change.operationLineSelect').on('change.operationLineSelect', '#operationLineBody select, #operationLineBody .cbo-value', function () {
        refreshOperationRowAvailability(type);
    });

    $('#btnOperationPost').on('click', function () {
        UI.confirm('Confirm Save & Post', 'This operation will be posted immediately.', operationSummary(type), async function () {
            await postOperation(type);
        });
    });
    $('#btnLoadDocuments, #btnSearchDocuments').on('click', () => loadOperationDocuments(type));
    $('#app input[name="docKeyword"], #app input[name="docFromDate"], #app input[name="docToDate"]').on('change input', UI.debounce(() => loadOperationDocuments(type), 300));
    $('#app [name="approvedBy"]').on('input', function () {
        $('#operationApprovedBy').text($(this).val() || '-');
    });
    refreshOperationRowAvailability(type);
    checkDuplicateSerialRealtime();
}

function getBorrowDocumentId() {
    const borrowDocumentReturnId = $('#app [name="borrowDocumentId"]').val();
    return borrowDocumentReturnId ? parseInt(borrowDocumentReturnId, 10) : null;
}

function refreshOperationRowAvailability(type) {
    if (type === 'adjustment') refreshAdjustmentCurrentBinOptions();
    duplicateControlledFields(type).forEach(fieldName => {
        const selected = new Set();
        $(`#operationLineBody select[name="${fieldName}"]`).each(function () {
            const value = $(this).val();
            if (value) selected.add(String(value));
        });

        $(`#operationLineBody select[name="${fieldName}"]`).each(function () {
            const currentValue = String($(this).val() || '');
            $(this).find('option').each(function () {
                const option = $(this);
                const value = String(option.attr('value') || '');
                if (!value) return;
                const baseText = option.attr('data-base-text') || option.text();
                const disabled = value !== currentValue && selected.has(value);
                option.prop('disabled', disabled);
                option.attr('title', disabled ? UI.t('Already selected in another row') : '');
                option.text(disabled ? `${baseText} - ${UI.t('Already selected')}` : baseText);
            });
        });
    });
    if (type === 'move') refreshMoveOccupancyHints();
}

function duplicateControlledFields(type) {
    const fields = [];
    if (type !== 'inbound') fields.push('itemInstanceId');
    if (type === 'inbound') fields.push('binLocationId');
    if (['move', 'repair-receive', 'borrow-return', 'adjustment'].includes(type)) fields.push('targetBinLocationId');
    return fields;
}

function refreshAdjustmentCurrentBinOptions() {
    $('#operationLineBody tr').each(function () {
        const row = $(this);
        const itemOption = row.find('select[name="itemInstanceId"] option:selected');
        const currentBinId = itemOption.attr('data-bin-location-id') || '';
        const currentLocation = itemOption.attr('data-location') || itemOption.attr('data-base-text') || '';
        const targetSelect = row.find('select[name="targetBinLocationId"]');
        if (!targetSelect.length) return;
        targetSelect.find('option[data-current-option="true"]').each(function () {
            if ($(this).attr('value') !== String(currentBinId)) $(this).remove();
        });
        if (!currentBinId) return;
        const label = `${currentLocation} - ${UI.t('Current location')}`;
        let option = targetSelect.find(`option[value="${String(currentBinId).replace(/"/g, '\\"')}"]`);
        if (!option.length) {
            option = $(`<option value="${UI.esc(currentBinId)}" data-current-option="true"></option>`);
            targetSelect.append(option);
        }
        option.attr('data-base-text', label).attr('title', UI.t('Current location')).text(label);
    });
}

function refreshMoveOccupancyHints() {
    const selectedItems = new Set();
    $('#operationLineBody select[name="itemInstanceId"]').each(function () {
        const value = $(this).val();
        if (value) selectedItems.add(String(value));
    });

    const hints = [];
    $('#operationLineBody tr').each(function (index) {
        const row = $(this);
        const selectedTarget = row.find('select[name="targetBinLocationId"] option:selected');
        const occupiedId = selectedTarget.attr('data-occupied-item-instance-id') || '';
        if (!occupiedId) return;
        const binText = selectedTarget.attr('data-base-text') || selectedTarget.text();
        const itemText = selectedTarget.attr('data-occupied-item-text') || occupiedId;
        const key = selectedItems.has(String(occupiedId)) ? 'Target bin swap is covered by another row.' : 'Target bin is occupied; add the occupying item as another move row or choose an empty bin.';
        const level = selectedItems.has(String(occupiedId)) ? 'info' : 'warning';
        hints.push({ level, text: `${UI.t('Row')} ${index + 1}: ${UI.t(key)} ${UI.esc(binText)} / ${UI.esc(itemText)}` });
    });

    if (!hints.length) {
        $('#moveOccupancyHints').empty();
        return;
    }

    $('#moveOccupancyHints').html(`<div class="move-hints mb-3">${hints.map(x => `<div class="move-hint move-hint-${x.level}"><i class="bi ${x.level === 'warning' ? 'bi-exclamation-triangle' : 'bi-arrow-left-right'}"></i><span>${x.text}</span></div>`).join('')}</div>`);
}

function operationSummary(type) {
    return `<div>${UI.t('Operation')}: <b>${UI.esc(UI.t(type))}</b></div><div>${UI.t('Rows')}: <b>${$('#operationLineBody tr').length}</b></div><div>${UI.t('System will validate role, warehouse scope and state transition.')}</div>`;
}

async function postOperation(type) {
    clearOperationValidation();
    const payload = buildOperationPayload(type);
    if (payload.validationErrors) { showOperationValidation(payload.validationErrors); return; }
    if (payload.error) { showOperationValidation([{ message: payload.error }]); return; }

    // Inventory Check: session-based (CreateSession then Scan)
    if (type === 'inventory-check') {
        await postInventoryCheckSession(payload);
        return;
    }

    const endpoints = {
        inbound: '/Inventory/Inbound',
        move: '/Inventory/MoveLocation',
        adjustment: '/Inventory/Adjust',
        'repair-send': '/Repair/SendToRepair',
        'repair-receive': '/Repair/ReceiveFromRepair',
        'borrow-lend': '/Borrow/Lend',
        'borrow-return': '/Borrow/Return'
    };
    const result = await UI.api(endpoints[type], { method: 'POST', data: payload });
    if (!result.success) {
        showOperationValidation(operationErrorsFromResult(result));
        return;
    }
    try {
        await uploadOperationAttachments(result.data ? result.data.documentType : '', result.data ? result.data.documentId : 0);
    } catch (err) {
        showOperationValidation([{ label: 'Attachments', message: err.message || 'Attachment upload failed.' }]);
        return;
    }
    UI.toast(`${UI.msg(result.message || 'Posted')} ${result.data ? result.data.documentNo : ''}`);
    AppState.currentTrackingKeyword = '';
    await loadLookups();
    Router.go(type);
}

async function postInventoryCheckSession(payload) {
    // Step 1: Create session
    const sessionResult = await UI.api('/InventoryCheck/CreateSession', {
        method: 'POST',
        data: {
            warehouseId: payload.warehouseId,
            sessionDate: payload.sessionDate,
            countMethod: payload.countMethod,
            responsibleStaff: payload.responsibleStaff,
            documentPeriodType: payload.documentPeriodType,
            note: payload.note || null
        }
    });
    if (!sessionResult.success) {
        showOperationValidation(operationErrorsFromResult(sessionResult));
        return;
    }
    const documentId = sessionResult.data && sessionResult.data.documentId;
    const documentNo = sessionResult.data && sessionResult.data.documentNo;
    if (!documentId) {
        showOperationValidation([{ label: 'System', message: 'Session created but documentId is missing.' }]);
        return;
    }

    // Step 2: Send scan batch
    const scanResult = await UI.api('/InventoryCheck/Scan', {
        method: 'POST',
        data: { documentId: documentId, lines: payload.lines }
    });
    if (!scanResult.success) {
        showOperationValidation(operationErrorsFromResult(scanResult));
        return;
    }
    const d = scanResult.data || {};
    const summary = `${UI.t('Matched')}: ${d.batchMatched || 0} | ${UI.t('WrongLocation')}: ${d.batchWrongLocation || 0} | ${UI.t('Extra')}: ${d.batchExtra || 0}`;
    UI.toast(`${UI.t('Inventory check session created.')} ${documentNo || ''} — ${summary}`);
    AppState.currentTrackingKeyword = '';
    await loadLookups();
    Router.go('inventory-check');
}

function buildOperationPayload(type) {

    const cfg = OperationPayloadConfig[type];
    const h = name => $('#app [name="' + name + '"]').val();
    const intOrNull = v => v ? parseInt(v, 10) : null;
    const rows = $('#operationLineBody tr').map(function () {
        const r = $(this);
        const val = name => r.find('[name="' + name + '"]').val();
        return cfg.row(val);
    }).get();

    if (!rows.length) {
        return { error: UI.t('At least one line is required.') };
    }
    const requiredErrors = validateRequiredOperation(type, rows, h);

    if (requiredErrors.length) {
        return { validationErrors: requiredErrors };
    }

    const duplicateErrors = validateOperationDuplicates(type, rows);
    if (duplicateErrors.length) {
        return { validationErrors: duplicateErrors };
    }

    if (cfg.validateRows) {
        const validationErrors = cfg.validateRows(rows);
        if (validationErrors.length) { return { validationErrors }; }
    }
    return cfg.payload(h, rows, intOrNull);
}

function validateRequiredOperation(type, rows, h) {
    const config = window.OperationRequiredConfig[type];
    if (!config) return [];
    const errors = [];
    const addHeader = (field, label) => {
        if (!String(h(field) || '').trim()) {
            errors.push({
                field,
                label,
                message: `${label} is required.`
            });
        }
    };

    const addRow = (rowNo, row, field, label) => {
        const value = row ? row[field] : null;
        if (value == null || String(value).trim() === '') {
            errors.push({
                row: rowNo,
                field,
                label,
                message: `${label} is required.`
            });

        }
    };

    Object.entries(config.headers || {})
        .forEach(([field, label]) => {
            addHeader(field, label);
        });

    rows.forEach((row, index) => {
        const rowNo = index + 1;
        if (type !== 'inbound' && !(type === 'inventory-check' && row.result === 'Extra')) addRow(rowNo, row, 'itemCode', 'Item Instance');
        Object.entries(config.rows || {})
            .forEach(([field, label]) => {
                addRow(rowNo, row, field, label);
            });
        (config.conditionalRows || [])
            .forEach(rule => {
                if (rule.when(row)) {
                    addRow(rowNo, row, rule.field, rule.label
                    );
                }
            });
    });
    return errors;
}

function validateOperationDuplicates(type, rows) {
    const errors = [];
    const checks = [];
    if (type !== 'inbound') checks.push(['serialNumber', 'Item Instance', 'Each item can only appear once per document.']);
    if (type === 'inbound') checks.push(['binCode', 'Bin', 'Each bin can only appear once per inbound document.']);
    if (['move', 'repair-receive', 'borrow-return', 'adjustment'].includes(type)) {
        checks.push(['targetBinCode', 'Target Bin', 'Each target bin can only appear once per document.']);
    }

    checks.forEach(([field, label, message]) => {
        const seen = new Map();
        rows.forEach((row, index) => {
            const value = row && row[field];
            if (!value) return;
            if (seen.has(value)) {
                errors.push({ row: index + 1, field, label, message });
                return;
            }

            seen.set(value, index + 1);
        });
    });

    return errors;
}

function validateInboundRows(rows) {
    const errors = [];
    const binIds = new Set();
    const serials = new Set();
    //const barcodes = new Set();
    rows.forEach((row, index) => {
        const rowNo = index + 1;
        if (!row.itemCode) errors.push({ row: rowNo, field: 'itemCode', label: 'Item', message: 'Item is required.' });
        //if(!row.itemId) errors.push({ row: rowNo, field: 'itemId', label: 'Item', message: 'Item is required.' });
        if (!row.binCode) errors.push({ row: rowNo, field: 'binCode', label: 'Bin', message: 'Bin is required.' });
        //if(!row.binLocationId) errors.push({ row: rowNo, field: 'binLocationId', label: 'Bin', message: 'Bin is required.' });
        if (row.binCode && binIds.has(row.binCode)) errors.push({ row: rowNo, field: 'binCode', label: 'Bin', message: 'Each bin can only appear once per inbound document.' });
        //if(row.binLocationId && binIds.has(row.binLocationId)) errors.push({ row: rowNo, field: 'binLocationId', label: 'Bin', message: 'Each bin can only appear once per inbound document.' });
        if (row.binCode) binIds.add(row.binCode);
        //if(row.binLocationId) binIds.add(row.binLocationId);
        const serial = (row.serialNumber || '').trim().toLowerCase();
        if (serial) {
            if (serials.has(serial)) errors.push({ row: rowNo, field: 'serialNumber', label: 'Serial', message: 'Serial is duplicated in this inbound document.' });
            serials.add(serial);
        }
        //const barcode = (row.barcode || '').trim().toLowerCase();
        //if(barcode){
        //  if(barcodes.has(barcode)) errors.push({ row: rowNo, field: 'barcode', label: 'Barcode', message: 'Barcode is duplicated in this inbound document.' });
        //  barcodes.add(barcode);
        //}
    });
    return errors;
}

function clearOperationValidation() {
    $('#operationValidation').empty();
    $('#app .is-invalid').removeClass('is-invalid');
}

function showOperationValidation(errors) {
    const list = (errors || []).filter(Boolean);
    if (!list.length) return;
    list.forEach(e => {
        if (e.row && e.field) {
            $('#operationLineBody tr').eq(e.row - 1).find(`[name="${e.field}"]`).addClass('is-invalid');
        } else if (e.field) {
            $('#app [name="' + e.field + '"]').addClass('is-invalid');
        }
    });
    $('#operationValidation').html(`<div class="validation-panel mb-3">
    <div class="validation-title"><i class="bi bi-exclamation-triangle"></i>${UI.t('Please correct the highlighted data before posting.')}</div>
    <div class="validation-list">${list.map(e => `<div class="validation-item">
      <span class="validation-field">${e.row ? `${UI.t('Row')} ${UI.esc(e.row)} &middot; ` : ''}${UI.t(e.label || 'System')}</span>
      <span>${UI.esc(UI.msg(e.message || e))}</span>
    </div>`).join('')}</div>
  </div>`);
    const panel = $('#operationValidation')[0];
    if (panel) panel.scrollIntoView({ behavior: 'smooth', block: 'center' });
}

function operationErrorsFromResult(result) {
    const errors = Array.isArray(result && result.errors) && result.errors.length ? result.errors : [result && result.message ? result.message : 'Request failed.'];
    return errors.map(message => ({ label: 'System', message }));
}

async function uploadOperationAttachments(entityName, entityId) {
    const input = document.getElementById('operationAttachments');
    if (!input || !input.files || !input.files.length) return;
    if (!entityName || !entityId) throw new Error(UI.t('Attachment entity is invalid.'));
    for (const file of input.files) {
        const form = new FormData();
        form.append('entityName', entityName);
        form.append('entityId', entityId);
        form.append('file', file);
        const result = await UI.upload('/Attachments/Upload', form);
        if (!result.success) throw new Error(UI.resultError(result));
    }
}

async function loadOperationDocuments(type) {
    const result = await UI.api('/Documents/List', {
        query: {
            type,
            keyword: $('#app [name="docKeyword"]').val() || null,
            fromDate: $('#app [name="docFromDate"]').val() || null,
            toDate: $('#app [name="docToDate"]').val() || null
        }
    });
    const rows = result || [];
    if (!rows.length) {
        $('#operationDocuments').html(UI.empty('No data'));
        return;
    }
    $('#operationDocuments').html(`<div class="table-wrap"><table class="data-table"><thead><tr><th class="px-3">${UI.t('Document No')}</th><th>${UI.t('Document Date')}</th><th>${UI.t('Party')}</th><th>${UI.t('Warehouse')}</th><th style="min-width:120px;">${UI.t('Status')}</th><th>${UI.t('Lines')}</th><th>${UI.t('ApprovedBy')}</th><th></th></tr></thead><tbody>${rows.map(r => `<tr><td class="px-3 fw-semibold">${UI.esc(r.documentNo)}</td><td>${UI.formatDate(r.documentDate)}</td><td>${UI.esc(r.party || '-')}</td><td>${UI.esc(r.warehouse || '-')}</td><td><span class="badge text-bg-light">${UI.badgeTheadDocument(r.status || '-')}</span></td><td>${UI.esc(r.lines)}</td><td>${UI.esc(r.approvedBy || '-')}</td><td>${type === 'inventory-check' && r.sessionStatus === 'InProgress' ? `<button class="btn btn-warning btn-sm btn-doc-finalize me-1" data-id="${r.id}" title="${UI.t('Finalize Session')}"><i class="bi bi-check2-circle"></i></button>` : ''}<button class="btn btn-light btn-sm btn-doc-detail" data-id="${r.id}" data-type="${UI.esc(type)}" title="${UI.t('View Detail')}"><i class="bi bi-eye"></i></button></td></tr>`).join('')}</tbody></table><div class="server-footer"><span>${UI.endpoint('DocumentsList')}</span><span>${rows.length} ${UI.t('rows')}</span></div></div>`);
    //$('#operationDocuments').html(`<div class="table-wrap"><table class="data-table"><thead><tr><th class="px-3">${UI.t('Document No')}</th><th>${UI.t('Document Date')}</th><th>${UI.t('Party')}</th><th>${UI.t('Warehouse')}</th><th>${UI.t('Status')}</th><th>${UI.t('Lines')}</th><th>${UI.t('ApprovedBy')}</th><th></th></tr></thead><tbody>${rows.map(r => `<tr><td class="px-3 fw-semibold">${UI.esc(r.documentNo)}</td><td>${UI.formatDate(r.documentDate)}</td><td>${UI.esc(r.party || '-')}</td><td>${UI.esc(r.warehouse || '-')}</td><td><span class="badge text-bg-light">${UI.esc(UI.t(r.status || '-'))}</span></td><td>${UI.esc(r.lines)}</td><td>${UI.esc(r.approvedBy || '-')}</td><td><button class="btn btn-light btn-sm btn-doc-detail" data-id="${r.id}" data-type="${UI.esc(type)}" title="${UI.t('View Detail')}"><i class="bi bi-eye"></i></button></td></tr>`).join('')}</tbody></table><div class="server-footer"><span>${UI.endpoint('DocumentsList')}</span><span>${rows.length} ${UI.t('rows')}</span></div></div>`);
}

$(document).on('click', '.btn-doc-detail', async function () {
    const docType = $(this).data('type');
    const detail = await UI.api('/Documents/Detail', { query: { type: docType, id: $(this).data('id') } });
    window._currentDocDetail = detail;
    window._currentDocType = docType;
    $('#drawer .fw-bold').first().text(UI.t('View Detail'));
    $('#drawerBody').html(renderDocumentDetail(detail, docType));
    await loadDocumentAttachments(detail.header ? detail.header.entityName : '', detail.header ? detail.header.id : 0);
    $('#drawer').addClass('open right-drawer-detail');
});

$(document).on('click', '.btn-doc-finalize', function () {
    const docId = $(this).data('id');
    UI.confirm(UI.t('Finalize Session'), UI.t('Are you sure you want to finalize this inventory check session? Missing items will be calculated and adjustments generated if needed.'), '', async function () {
        const result = await UI.api('/InventoryCheck/Finalize/' + docId, { method: 'POST' });
        if (result.success) {
            UI.toast(UI.t('Inventory check finalized.'));
            loadOperationDocuments('inventory-check');
        } else {
            showOperationValidation(operationErrorsFromResult(result));
        }
    });
});

$(document).on('change', '#app [name="warehouseId"]', loadReportsInventory);

$(document).on('click', '#btnPrintVoucher', function () {
    if (window._currentDocDetail && window._currentDocType) {
        PrintVoucher.print(window._currentDocType, window._currentDocDetail);
    }
});

$(document).on('click', '.link-item-tracking', function () {
    AppState.currentTrackingKeyword = $(this).data('key');
    Router.go('tracking');
});

function renderDocumentDetail(detail, docType) {
    const h = detail.header || {};
    const extra = h.extra || {};
    const extraRows = Object.keys(extra).length ? `<div class="audit-footer mt-2"><div class="row g-3 mb-3">${Object.entries(extra).map(([k, v]) => { let value = v || '-'; if (k.toLowerCase().includes('date')) { value = v ? UI.formatDate(v) : '-'; } return `<div class="col-md-6">${UI.t(detailLabel(k))}: <b>${UI.esc(UI.t(value))}</b></div>`; }).join('')}</div></div>` : '';
    const lines = detail.lines || [];
    const historyHtml = detail.history && detail.history.length ? `
      <div class="form-section-title mt-3">${UI.t('History & Timeline')}</div>
      <div class="table-wrap report-scroll-wrap"><table class="data-table-detail-history"><thead><tr><th>${UI.t('Time')}</th><th>${UI.t('Action')}</th><th>${UI.t('PN/SN')}</th><th>${UI.t('Party')}</th><th>${UI.t('Status')}</th><th>${UI.t('Location')}</th><th>${UI.t('By')}</th></tr></thead>
        <tbody>${detail.history.map(x => {
          const party = x.borrower || x.receiver || '-';
          const dept = x.borrowDepartment || x.receiverDepartment || '';
          const phone = x.borrowerPhone || x.receiverPhone || '';
          const owner = x.departmentOwner || '';
          const partyDetail = [dept, phone, owner].filter(Boolean).join(' · ');
          return `<tr>
          <td class="text-muted small">${UI.formatDate(x.timestamp)}</td>
          <td><span class="badge text-bg-secondary">${UI.esc(x.actionTypeText || UI.t(x.actionType || '-'))}</span></td>
          <td><span class="link-item-tracking fw-semibold" data-key="${UI.esc(x.serialNumber)}">${UI.esc(x.itemCode || '-')}</span><br/><small class="text-muted">${UI.esc(x.serialNumber || '')}</small></td>
          <td class="small"><span class="fw-semibold">${UI.esc(party)}</span>${partyDetail ? `<br/><small class="text-muted">${UI.esc(partyDetail)}</small>` : ''}</td>
          <td class="small">${UI.badge(x.oldStatus, x.oldStatusText)} <i class="bi bi-arrow-right text-muted"></i> ${UI.badge(x.newStatus, x.newStatusText)}</td>
          <td class="small text-muted">${UI.esc(x.oldLocation || '\u2014')} <i class="bi bi-arrow-right"></i> ${UI.esc(x.newLocation || '\u2014')}</td>
          <td class="text-muted small">${UI.esc(x.performedBy || '-')}</td>
        </tr>`;}).join('')}</tbody>
      </table></div>
    ` : '';

    return `
  <div class="audit-footer">
   <div class="row g-3 mb-3">
    <div class="col-md-6">${UI.t('Document No')}: <b>${UI.esc(h.documentNo || '-')}</b></div>
    <div class="col-md-6">${UI.t('Document Date')}: <b>${UI.formatDate(h.documentDate)}</b></div>
    <div class="col-md-6">${UI.t('Party')}: <b>${UI.esc(h.party || '-')}</b></div>
    <div class="col-md-6">${UI.t('Warehouse')}: <b>${UI.esc(h.warehouse || '-')}</b></div>
    <div class="col-md-6">${UI.t('CreatedBy')}: <b>${UI.esc(h.createdBy || '-')}</b></div>
    <div class="col-md-6">${UI.t('ApprovedBy')}: <b>${UI.esc(h.approvedBy || '-')}</b></div>
    </div>
  </div>${extraRows}
  <div class="form-section-title mt-3">${UI.t('Line Items')}</div>
  <div class="table-wrap report-scroll-wrap"><table class="data-table-detail"><thead><tr><th class="px-3">${UI.t('Item')}</th><th>${UI.t('SN')}</th><th style="min-width: 120px;">${UI.t('Status')}</th><th>${UI.t('Location')}</th></tr></thead>
    <tbody>${lines.map(l =>
        `<tr><td class="px-3"><span class="link-item-tracking" data-key="${UI.esc(l.serial)}">${UI.esc(l.item || '-')}</span></td><td><span class="link-item-tracking" data-key="${UI.esc(l.serial)}">${UI.esc(l.serial || l.barcode || '-')}</span></td><td>${UI.badge(l.condition || l.result || l.newStatus || l.status || '', l.conditionText || l.resultText || '')}${l.returned ? ' <span class="badge text-bg-success ms-1"><i class="bi bi-check-circle"></i></span>' : ''}</td><td>${UI.esc(l.targetBin || l.to || l.bin || l.fromBin || '-')}</td></tr>`).join('')}
    </tbody>
  </table></div>
  ${historyHtml}
  <div class="form-section-title mt-3">${UI.t('Attachments')}</div>
  <div id="documentAttachments">${UI.loading()}</div>`;
}


async function loadDocumentAttachments(entityName, entityId) {
    if (!entityName || !entityId) { $('#documentAttachments').html(UI.empty('No attachments')); return; }
    const rows = await UI.api('/Attachments/List', { query: { entityName, entityId } });
    if (!rows.length) { $('#documentAttachments').html(UI.empty('No attachments')); return; }
    $('#documentAttachments').html(`<div class="table-wrap"><table class="data-table p-3"><thead><tr><th class="px-3">${UI.t('File Name')}</th><th>${UI.t('Size')}</th><th>${UI.t('Uploaded At')}</th><th></th></tr></thead><tbody>${rows.map(r => `<tr><td class="px-3">${UI.esc(r.fileName)}</td><td>${UI.esc(formatFileSize(r.fileSize))}</td><td>${UI.formatDate(r.createdAt)}</td><td><a class="btn btn-light btn-sm" href="/Attachments/Download/${UI.esc(r.id)}"><i class="bi bi-download"></i> ${UI.t('Download')}</a></td></tr>`).join('')}</tbody></table></div>`);
}

function formatFileSize(bytes) {
    const n = Number(bytes || 0);
    if (n < 1024) return `${n} B`;
    if (n < 1024 * 1024) return `${Math.round(n / 1024)} KB`;
    return `${(n / 1024 / 1024).toFixed(1)} MB`;
}

function detailLabel(key) {
    return ({
        purpose: 'Purpose',
        borrowDepartment: 'Borrow Department',
        borrowerPhone: 'Borrower Phone',
        departmentOwner: 'Department Owner',
        dueDate: 'Due Date'
    })[key] || key;
}

// ================= INIT =================
document.addEventListener('DOMContentLoaded', () => {
    initScanSystem();
});

function initScanSystem() {
    const table = document.querySelector('#operationLineBody');
    if (!table) return;

    focusFirstInput();

    table.addEventListener('keydown', handleEnterFlow);

    // realtime duplicate check
    $(document)
        .off('input.scan')
        .on('input.scan', '#operationLineBody input[name="serialNumber"]', function () {
            checkDuplicateSerialRealtime();
        });

    // auto select text
    document.addEventListener('focusin', e => {
        if (e.target.matches('#operationLineBody input')) {
            setTimeout(() => e.target.select(), 0);
        }
    });
}

let enterLock = false;
// ================= ENTER FLOW =================
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

    const skipNames = ['condition', 'mt', 'newStatus', 'lineReason', 'lineNote', 'result'];

    const row = input.closest('tr');
    const inputs = Array.from(row.querySelectorAll('input, select')).filter
        (el => {
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
        addNewRow();
        nextRow = row.nextElementSibling;
    }

    if (nextRow) {
        focusRowFirstInput(nextRow);
    }
}


// ================= ADD ROW =================
function addNewRow() {
    const tbody = document.getElementById('operationLineBody');
    const type = window.currentOperationType;
    const vm = window.currentVM || {};
    const index = tbody.children.length + 1;
    const html = renderLineRow(type, vm, index);
    tbody.insertAdjacentHTML('beforeend', html);

    checkDuplicateSerialRealtime();
}

// ================= FOCUS =================
function focusFirstInput() {
    const el = document.querySelector('#operationLineBody tr:first-child input');
    if (el) el.focus();
}

function focusRowFirstInput(row) {
    const el = row.querySelector('input, select');
    if (el) el.focus();
}

// ================= DUPLICATE CHECK =================
function checkDuplicateSerialRealtime() {
    const map = new Map();
    const binMap = new Map();
    const rows = $('#operationLineBody tr');

    // clear trạng thái cũ
    rows.removeClass('row-duplicate');
    rows.find('input').removeClass('input-duplicate');

    rows.each(function (index) {
        const row = $(this);

        const serial = (row.find('[name="serialNumber"]').val() || '').trim().toLowerCase();
        const item = (row.find('[name="itemCode"]').val() || '').trim().toLowerCase();
        const binCode = (row.find('[name="binCode"]').val() || '').trim().toLowerCase();

        if (!serial || !item) return;

        // 🔥 KEY = item + serial
        const key = `${item}__${serial}`;

        if (map.has(key)) {
            const firstIndex = map.get(key);

            // current row
            row.addClass('row-duplicate');
            row.find('[name="serialNumber"], [name="itemCode"]').addClass('input-duplicate');

            // first row
            const firstRow = rows.eq(firstIndex);
            firstRow.addClass('row-duplicate');
            firstRow.find('[name="serialNumber"], [name="itemCode"]').addClass('input-duplicate');

            beep();
        } else {
            map.set(key, index);
        }

        if (binCode) {

            if (binMap.has(binCode)) {

                const firstIndex = binMap.get(binCode);

                // current row
                row.addClass('row-duplicate');

                row.find('[name="binCode"]')
                    .addClass('input-duplicate');

                // first row
                const firstRow = rows.eq(firstIndex);

                firstRow.addClass('row-duplicate');

                firstRow.find('[name="binCode"]')
                    .addClass('input-duplicate');

                beep();

            } else {

                binMap.set(binCode, index);

            }
        }
    });
}

// ================= SOUND =================
function beep() {
    try {
        const ctx = new (window.AudioContext || window.webkitAudioContext)();
        const osc = ctx.createOscillator();
        osc.type = 'square';
        osc.frequency.setValueAtTime(800, ctx.currentTime);
        osc.connect(ctx.destination);
        osc.start();
        osc.stop(ctx.currentTime + 0.1);
    } catch { }
}

// ================= SCROLL =================
function scrollToFirstDuplicate() {
    const el = document.querySelector('.input-duplicate');
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
}

async function loadReportsInventory() {
    const result = await UI.api('/Reports/InventoryPreview', { query: reportFilterQuery() });
    const rows = result.success && result.data ? (result.data.items || []) : [];
    if (!rows.length) { $('#reportsInventory').html(UI.empty('No data')); return; }
    $('#reportsInventory').html(`<div class="table-wrap report-scroll-wrap"><table class="data-table"><thead><tr><th class="px-3">${UI.t('Item')}</th><th>${UI.t('Serial / Barcode')}</th><th>${UI.t('Status')}</th><th>${UI.t('Current Location')}</th></tr></thead><tbody>${rows.map(r => `<tr><td class="px-3 fw-semibold">${UI.esc(r.itemCode)}<div class="small text-muted">${UI.esc(r.itemName)}</div></td><td>${UI.esc(r.serialNumber || r.barcode || '-')}</td><td>${UI.badge(r.status)}</td><td>${UI.esc(r.currentLocation || '-')}</td></tr>`).join('')}</tbody></table><div class="server-footer"><span>${UI.endpoint('ReportsInventoryPreview')}</span><span>${result.data.totalCount} ${UI.t('rows')}</span></div></div>`);
}
