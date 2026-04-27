Router.register('operation', async function(type){
  const c = OperationModel.configs[type];
  if(!c){ Router.go(AppConfig.defaultRoute); return; }
  if(!AppState.permissions.canOperate && type !== 'adjustment'){
    $('#app').html(UI.pageHeader(c[0], c[1]) + UI.empty('Access denied for current role.'));
    return;
  }
  if(type === 'adjustment' && !AppState.permissions.canManage){
    $('#app').html(UI.pageHeader(c[0], c[1]) + UI.empty('Access denied for current role.'));
    return;
  }

  const vm = await buildOperationVm(type);
  $('#app').html(UI.pageHeader(c[0], c[1], '<div class="permission-note"><i class="bi bi-shield-lock"></i>' + UI.t('Role and warehouse scoped') + '</div>') +
    `<div class="row g-3">
      <div class="col-xl-8">
        <div class="card mb-3"><div class="card-body"><div class="form-section-title">${UI.t('Header')}</div>${renderOperationHeader(type, vm)}</div></div>
        <div class="card mb-3"><div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-3">
            <div class="form-section-title mb-0">${UI.t('Line Items')}</div>
            <button class="btn btn-sm btn-primary" id="btnAddOperationLine"><i class="bi bi-plus-circle me-2"></i>${UI.t('Add line')}</button>
          </div>
          <div id="operationValidation"></div>
          ${type === 'move' ? '<div id="moveOccupancyHints"></div>' : ''}
          ${type === 'inbound' ? `<div class="small text-muted mb-2">${UI.t('The same item can be entered on multiple lines when each serial/barcode and bin is unique.')}</div>` : ''}
          <div id="operationLines">${renderOperationLines(type, vm)}</div>
        </div></div>
        <div class="card"><div class="card-body"><div class="row g-3"><div class="col-md-6"><label class="form-label w-100"><span class="fw-semibold small">${UI.t('Attachments')}</span><input id="operationAttachments" type="file" class="form-control" multiple accept=".pdf,.jpg,.jpeg,.png,.xlsx,.xls,.docx" /></label><div class="small text-muted">${UI.t('Attach invoices, handover records, repair receipts or inventory evidence when available.')}</div></div><div class="col-md-6">${UI.input('Notes','text','','note')}</div></div></div></div>
      </div>
      <div class="col-xl-4">
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
        <div class="col-md-3">${UI.input('From Date','date','','docFromDate')}</div>
        <div class="col-md-3">${UI.input('To Date','date','','docToDate')}</div>
        <div class="col-md-4">${UI.input('Keyword','text','','docKeyword')}</div>
        <div class="col-md-2 d-flex align-items-end"><button class="btn btn-primary w-100" id="btnSearchDocuments">${UI.t('Search')}</button></div>
      </div>
      <div id="operationDocuments">${UI.loading()}</div>
    </div></div>`);

  wireOperationEvents(type, vm);
  await loadOperationDocuments(type);
});

async function buildOperationVm(type){
  const firstWarehouse = (AppState.lookups.warehouses || [])[0];
  const warehouseId = firstWarehouse ? firstWarehouse.id : '';
  const bins = await loadOperationBins(type, warehouseId);
  let itemInstances = [];
  let repairDocuments = [];
  let borrowDocuments = [];

  if(type === 'repair-send'){
    itemInstances = await UI.api('/Lookup/ItemInstances', { query: { statuses: 'InStock,Damaged', warehouseId } });
  } else if(['move','borrow-lend'].includes(type)){
    itemInstances = await UI.api('/Lookup/ItemInstances', { query: { status: 'InStock', warehouseId } });
  } else if(type === 'repair-receive'){
    itemInstances = await UI.api('/Lookup/ItemInstances', { query: { status: 'Repairing', warehouseId } });
    repairDocuments = await UI.api('/Lookup/RepairDocuments');
  } else if(type === 'borrow-return'){
    itemInstances = await UI.api('/Lookup/ItemInstances', { query: { status: 'LentOut', warehouseId } });
    borrowDocuments = await UI.api('/Lookup/BorrowDocuments');
  } else if(['adjustment','inventory-check'].includes(type)){
    itemInstances = await UI.api('/Lookup/ItemInstances', { query: { warehouseId } });
  }

  return { warehouseId, bins, itemInstances, repairDocuments, borrowDocuments };
}

function operationUsesAvailableBins(type){
  return ['inbound','repair-receive','borrow-return','adjustment'].includes(type);
}

function loadOperationBins(type, warehouseId){
  return UI.api('/Lookup/Bins', { query: { warehouseId, availableOnly: operationUsesAvailableBins(type), includeOccupancy: type === 'move' } });
}

function renderOperationHeader(type, vm){
  const today = new Date().toISOString().slice(0, 10);
  if(type === 'inbound'){
    return `<div class="row g-3">
      <div class="col-md-3">${UI.select('Source','sourceExternalPartyId', AppState.lookups.suppliers)}</div>
      <div class="col-md-3">${UI.select('Warehouse','warehouseId', AppState.lookups.warehouses, vm.warehouseId)}</div>
      <div class="col-md-3">${UI.input('Inbound Date','date',today,'documentDate')}</div>
      <div class="col-md-3">${UI.input('Document No Auto','text','Auto','documentNo')}</div>
    </div>`;
  }
  if(type === 'move'){
    return `<div class="row g-3"><div class="col-md-4">${UI.select('Warehouse','warehouseId', AppState.lookups.warehouses, vm.warehouseId)}</div><div class="col-md-4">${UI.input('Move Date','date',today,'documentDate')}</div><div class="col-md-4">${UI.input('Document No Auto','text','Auto','documentNo')}</div></div>`;
  }
  if(type === 'adjustment'){
    return `<div class="row g-3"><div class="col-md-4">${UI.select('Warehouse','warehouseId', AppState.lookups.warehouses, vm.warehouseId)}</div><div class="col-md-4">${UI.input('Adjustment Date','date',today,'documentDate')}</div><div class="col-md-4">${UI.input('Reason','text','','reason')}</div></div>`;
  }
  if(type === 'inventory-check'){
    return `<div class="row g-3"><div class="col-md-3">${UI.select('Warehouse','warehouseId', AppState.lookups.warehouses, vm.warehouseId)}</div><div class="col-md-3">${UI.input('Session Date','date',today,'sessionDate')}</div><div class="col-md-3">${UI.input('Count Method','text','Scan','countMethod')}</div><div class="col-md-3">${UI.input('Responsible Staff','text',AppState.user.userName,'responsibleStaff')}</div></div>`;
  }
  if(type === 'repair-send'){
    return `<div class="row g-3"><div class="col-md-3">${UI.select('Vendor','repairVendorId', AppState.lookups.vendors)}</div><div class="col-md-3">${UI.select('Warehouse','warehouseId', AppState.lookups.warehouses, vm.warehouseId)}</div><div class="col-md-2">${UI.input('Send Date','date',today,'sendDate')}</div><div class="col-md-2">${UI.input('Expected Return','date','','expectedReturnDate')}</div><div class="col-md-2">${UI.input('Reason','text','','reason')}</div></div>`;
  }
  if(type === 'repair-receive'){
    return `<div class="row g-3"><div class="col-md-4">${UI.select('Repair Document','repairDocumentId', vm.repairDocuments)}</div><div class="col-md-4">${UI.select('Result','result', AppState.lookups.repairResults)}</div><div class="col-md-4">${UI.select('Return Warehouse','warehouseId', AppState.lookups.warehouses, vm.warehouseId)}</div></div><div class="row g-3 mt-1"><div class="col-md-12">${UI.input('Result Note','text','','resultNote')}</div></div>`;
  }
  if(type === 'borrow-lend'){
    return `<div class="row g-3">
      <div class="col-md-3">${UI.input('Borrow Document No','text','','documentNo')}</div>
      <div class="col-md-3">${UI.select('Borrow Warehouse','warehouseId', AppState.lookups.warehouses, vm.warehouseId)}</div>
      <div class="col-md-3">${UI.select('Borrower','borrowerId', AppState.lookups.borrowers)}</div>
      <div class="col-md-3">${UI.input('Borrow Date','date',today,'borrowDate')}</div>
      <div class="col-md-3">${UI.input('Due Date','date',today,'dueDate')}</div>
      <div class="col-md-3">${UI.input('Borrow Department','text','','borrowDepartment')}</div>
      <div class="col-md-3">${UI.input('Approver','text','','approvedBy')}</div>
      <div class="col-md-3">${UI.input('Borrower Phone','text','','borrowerPhone')}</div>
      <div class="col-md-3">${UI.input('Department Owner','text','','departmentOwner')}</div>
      <div class="col-md-12">${UI.input('Purpose','text','','purpose')}</div>
    </div>`;
  }
  return `<div class="row g-3"><div class="col-md-4">${UI.select('Borrow Document','borrowDocumentId', vm.borrowDocuments)}</div><div class="col-md-4">${UI.input('Return Date','date',today,'returnDate')}</div><div class="col-md-4">${UI.select('Return Warehouse','warehouseId', AppState.lookups.warehouses, vm.warehouseId)}</div></div>`;
}

function renderOperationLines(type, vm){
  const row = renderLineRow(type, vm, 1);
  return `<div class="table-wrap"><table class="data-table"><thead>${lineHeader(type)}</thead><tbody id="operationLineBody">${row}</tbody></table></div>`;
}

function lineHeader(type){
  if(type === 'inbound') return `<tr><th class="px-3">#</th><th>${UI.t('Item')}</th><th>${UI.t('Serial')}</th><th>${UI.t('Barcode')}</th><th>${UI.t('Bin')}</th><th>${UI.t('Condition')}</th><th>${UI.t('Note')}</th><th></th></tr>`;
  if(type === 'adjustment') return `<tr><th class="px-3">#</th><th>${UI.t('Item Instance')}</th><th>${UI.t('New Status')}</th><th>${UI.t('Target Bin')}</th><th>${UI.t('Reason')}</th><th></th></tr>`;
  if(type === 'inventory-check') return `<tr><th class="px-3">#</th><th>${UI.t('Item Instance')}</th><th>${UI.t('Actual Bin')}</th><th>${UI.t('Result')}</th><th>${UI.t('Note')}</th><th></th></tr>`;
  if(type === 'borrow-return') return `<tr><th class="px-3">#</th><th>${UI.t('Item Instance')}</th><th>${UI.t('Condition')}</th><th>${UI.t('Target Bin')}</th><th>${UI.t('Note')}</th><th></th></tr>`;
  if(type === 'repair-receive') return `<tr><th class="px-3">#</th><th>${UI.t('Item Instance')}</th><th>${UI.t('Target Bin')}</th><th>${UI.t('New Serial if Replaced')}</th><th></th></tr>`;
  if(type === 'repair-send' || type === 'borrow-lend') return `<tr><th class="px-3">#</th><th>${UI.t('Item Instance')}</th><th>${UI.t('External Destination')}</th><th>${UI.t('Note')}</th><th></th></tr>`;
  return `<tr><th class="px-3">#</th><th>${UI.t('Item Instance')}</th><th>${UI.t('Target Bin')}</th><th></th></tr>`;
}

function renderLineRow(type, vm, index){
  if(type === 'inbound'){
    return `<tr class="operation-line"><td class="px-3 line-no">${index}</td><td>${selectInline('itemId', AppState.lookups.items)}</td><td><input class="form-control form-control-sm" name="serialNumber"></td><td><input class="form-control form-control-sm" name="barcode"></td><td>${selectInline('binLocationId', vm.bins)}</td><td><input class="form-control form-control-sm" name="condition" value="Normal"></td><td><input class="form-control form-control-sm" name="lineNote"></td><td>${removeBtn()}</td></tr>`;
  }
  if(type === 'adjustment'){
    return `<tr class="operation-line"><td class="px-3 line-no">${index}</td><td>${selectInline('itemInstanceId', vm.itemInstances)}</td><td>${selectInline('newStatus', AppState.lookups.statuses)}</td><td>${selectInline('targetBinLocationId', vm.bins)}</td><td><input class="form-control form-control-sm" name="lineReason"></td><td>${removeBtn()}</td></tr>`;
  }
  if(type === 'inventory-check'){
    return `<tr class="operation-line"><td class="px-3 line-no">${index}</td><td>${selectInline('itemInstanceId', vm.itemInstances)}</td><td>${selectInline('actualBinLocationId', vm.bins)}</td><td>${selectInline('result', AppState.lookups.checkResults)}</td><td><input class="form-control form-control-sm" name="lineNote"></td><td>${removeBtn()}</td></tr>`;
  }
  if(type === 'borrow-return'){
    return `<tr class="operation-line"><td class="px-3 line-no">${index}</td><td>${selectInline('itemInstanceId', vm.itemInstances)}</td><td>${selectInline('condition', AppState.lookups.returnConditions)}</td><td>${selectInline('targetBinLocationId', vm.bins)}</td><td><input class="form-control form-control-sm" name="lineNote"></td><td>${removeBtn()}</td></tr>`;
  }
  if(type === 'repair-receive'){
    return `<tr class="operation-line"><td class="px-3 line-no">${index}</td><td>${selectInline('itemInstanceId', vm.itemInstances)}</td><td>${selectInline('targetBinLocationId', vm.bins)}</td><td><input class="form-control form-control-sm" name="newSerialNumber"></td><td>${removeBtn()}</td></tr>`;
  }
  if(type === 'repair-send' || type === 'borrow-lend'){
    return `<tr class="operation-line"><td class="px-3 line-no">${index}</td><td>${selectInline('itemInstanceId', vm.itemInstances)}</td><td><input class="form-control form-control-sm" name="targetExternalLocation" placeholder="${UI.esc(UI.t('External Destination'))}"></td><td><input class="form-control form-control-sm" name="lineNote"></td><td>${removeBtn()}</td></tr>`;
  }
  return `<tr class="operation-line"><td class="px-3 line-no">${index}</td><td>${selectInline('itemInstanceId', vm.itemInstances)}</td><td>${type === 'move' ? selectInline('targetBinLocationId', vm.bins) : '<input class="form-control form-control-sm" name="lineNote">'}</td><td>${removeBtn()}</td></tr>`;
}

function selectInline(name, options) {
  return `<select class="form-select form-select-sm" name="${UI.esc(name)}"><option value="">--</option>${(options || []).map(x => `<option value="${UI.esc(x.id)}" data-base-text="${UI.esc(x.text)}">${UI.esc(x.text)}</option>`).join('')}</select>`;
  //return `<div class="inline-search-select"><input type="search" class="form-control form-control-sm select-search" placeholder="${UI.esc(UI.t('Search options'))}" autocomplete="off" /><select class="form-select form-select-sm searchable-select" name="${UI.esc(name)}"><option value="">--</option>${(options || []).map(x => `<option value="${UI.esc(x.id)}" data-base-text="${UI.esc(x.text)}"${UI.optionAttrs(x)}>${UI.esc(x.text)}</option>`).join('')}</select></div>`;
}

function removeBtn(){
  return '<button class="btn btn-light btn-sm btn-remove-line"><i class="bi bi-x-lg"></i></button>';
}

function wireOperationEvents(type, vm){
  $('#btnAddOperationLine').on('click', function(){
    clearOperationValidation();
    $('#operationLineBody').append(renderLineRow(type, vm, $('#operationLineBody tr').length + 1));
    refreshOperationRowAvailability(type);
  });
  $(document).off('click.removeOperationLine').on('click.removeOperationLine', '.btn-remove-line', function(){
    clearOperationValidation();
    if($('#operationLineBody tr').length > 1) $(this).closest('tr').remove();
    $('#operationLineBody .line-no').each((i, el) => $(el).text(i + 1));
    refreshOperationRowAvailability(type);
  });
  $(document).off('change.operationLineSelect').on('change.operationLineSelect', '#operationLineBody select', function(){
    refreshOperationRowAvailability(type);
  });
  $('#app [name="warehouseId"]').on('change', async function(){
    const warehouseId = $(this).val();
    vm.bins = await loadOperationBins(type, warehouseId);
    if(type === 'repair-send'){
      vm.itemInstances = await UI.api('/Lookup/ItemInstances', { query: { statuses: 'InStock,Damaged', warehouseId } });
    } else if(['move','borrow-lend'].includes(type)){
      vm.itemInstances = await UI.api('/Lookup/ItemInstances', { query: { status: 'InStock', warehouseId } });
    } else if(type === 'repair-receive'){
      vm.itemInstances = await UI.api('/Lookup/ItemInstances', { query: { status: 'Repairing', warehouseId } });
    } else if(type === 'borrow-return'){
      vm.itemInstances = await UI.api('/Lookup/ItemInstances', { query: { status: 'LentOut', warehouseId } });
    } else if(['adjustment','inventory-check'].includes(type)){
      vm.itemInstances = await UI.api('/Lookup/ItemInstances', { query: { status: type === 'move' ? 'InStock' : null, warehouseId } });
    }
    clearOperationValidation();
    $('#operationLines').html(renderOperationLines(type, vm));
    refreshOperationRowAvailability(type);
  });
  $('#btnOperationPost').on('click', function(){
    UI.confirm('Confirm Save & Post', 'This operation will be posted immediately.', operationSummary(type), async function(){
      await postOperation(type);
    });
  });
  $('#btnLoadDocuments, #btnSearchDocuments').on('click', () => loadOperationDocuments(type));
  $('#app input[name="docKeyword"], #app input[name="docFromDate"], #app input[name="docToDate"]').on('change input', UI.debounce(() => loadOperationDocuments(type), 300));
  $('#app [name="approvedBy"]').on('input', function(){
    $('#operationApprovedBy').text($(this).val() || '-');
  });
  refreshOperationRowAvailability(type);
}

function refreshOperationRowAvailability(type){
  if(type === 'adjustment') refreshAdjustmentCurrentBinOptions();
  duplicateControlledFields(type).forEach(fieldName => {
    const selected = new Set();
    $(`#operationLineBody select[name="${fieldName}"]`).each(function(){
      const value = $(this).val();
      if(value) selected.add(String(value));
    });

    $(`#operationLineBody select[name="${fieldName}"]`).each(function(){
      const currentValue = String($(this).val() || '');
      $(this).find('option').each(function(){
        const option = $(this);
        const value = String(option.attr('value') || '');
        if(!value) return;
        const baseText = option.attr('data-base-text') || option.text();
        const disabled = value !== currentValue && selected.has(value);
        option.prop('disabled', disabled);
        option.attr('title', disabled ? UI.t('Already selected in another row') : '');
        option.text(disabled ? `${baseText} - ${UI.t('Already selected')}` : baseText);
      });
    });
  });
  if(type === 'move') refreshMoveOccupancyHints();
}

function duplicateControlledFields(type){
  const fields = [];
  if(type !== 'inbound') fields.push('itemInstanceId');
  if(type === 'inbound') fields.push('binLocationId');
  if(['move','repair-receive','borrow-return','adjustment'].includes(type)) fields.push('targetBinLocationId');
  return fields;
}

function refreshAdjustmentCurrentBinOptions(){
  $('#operationLineBody tr').each(function(){
    const row = $(this);
    const itemOption = row.find('select[name="itemInstanceId"] option:selected');
    const currentBinId = itemOption.attr('data-bin-location-id') || '';
    const currentLocation = itemOption.attr('data-location') || itemOption.attr('data-base-text') || '';
    const targetSelect = row.find('select[name="targetBinLocationId"]');
    if(!targetSelect.length) return;
    targetSelect.find('option[data-current-option="true"]').each(function(){
      if($(this).attr('value') !== String(currentBinId)) $(this).remove();
    });
    if(!currentBinId) return;
    const label = `${currentLocation} - ${UI.t('Current location')}`;
    let option = targetSelect.find(`option[value="${String(currentBinId).replace(/"/g, '\\"')}"]`);
    if(!option.length){
      option = $(`<option value="${UI.esc(currentBinId)}" data-current-option="true"></option>`);
      targetSelect.append(option);
    }
    option.attr('data-base-text', label).attr('title', UI.t('Current location')).text(label);
  });
}

function refreshMoveOccupancyHints(){
  const selectedItems = new Set();
  $('#operationLineBody select[name="itemInstanceId"]').each(function(){
    const value = $(this).val();
    if(value) selectedItems.add(String(value));
  });

  const hints = [];
  $('#operationLineBody tr').each(function(index){
    const row = $(this);
    const selectedTarget = row.find('select[name="targetBinLocationId"] option:selected');
    const occupiedId = selectedTarget.attr('data-occupied-item-instance-id') || '';
    if(!occupiedId) return;
    const binText = selectedTarget.attr('data-base-text') || selectedTarget.text();
    const itemText = selectedTarget.attr('data-occupied-item-text') || occupiedId;
    const key = selectedItems.has(String(occupiedId)) ? 'Target bin swap is covered by another row.' : 'Target bin is occupied; add the occupying item as another move row or choose an empty bin.';
    const level = selectedItems.has(String(occupiedId)) ? 'info' : 'warning';
    hints.push({ level, text: `${UI.t('Row')} ${index + 1}: ${UI.t(key)} ${UI.esc(binText)} / ${UI.esc(itemText)}` });
  });

  if(!hints.length){
    $('#moveOccupancyHints').empty();
    return;
  }

  $('#moveOccupancyHints').html(`<div class="move-hints mb-3">${hints.map(x => `<div class="move-hint move-hint-${x.level}"><i class="bi ${x.level === 'warning' ? 'bi-exclamation-triangle' : 'bi-arrow-left-right'}"></i><span>${x.text}</span></div>`).join('')}</div>`);
}

function operationSummary(type){
  return `<div>${UI.t('Operation')}: <b>${UI.esc(UI.t(type))}</b></div><div>${UI.t('Rows')}: <b>${$('#operationLineBody tr').length}</b></div><div>${UI.t('System will validate role, warehouse scope and state transition.')}</div>`;
}

async function postOperation(type){
  clearOperationValidation();
  const payload = buildOperationPayload(type);
  if(payload.validationErrors){ showOperationValidation(payload.validationErrors); return; }
  if(payload.error){ showOperationValidation([{ message: payload.error }]); return; }
  const endpoints = {
    inbound: '/Inventory/Inbound',
    move: '/Inventory/MoveLocation',
    adjustment: '/Inventory/Adjust',
    'inventory-check': '/Inventory/InventoryCheck',
    'repair-send': '/Repair/SendToRepair',
    'repair-receive': '/Repair/ReceiveFromRepair',
    'borrow-lend': '/Borrow/Lend',
    'borrow-return': '/Borrow/Return'
  };
  const result = await UI.api(endpoints[type], { method: 'POST', data: payload });
  if(!result.success){
    showOperationValidation(operationErrorsFromResult(result));
    return;
  }
  try {
    await uploadOperationAttachments(result.data ? result.data.documentType : '', result.data ? result.data.documentId : 0);
  } catch(err) {
    showOperationValidation([{ label: 'Attachments', message: err.message || 'Attachment upload failed.' }]);
    return;
  }
  UI.toast(`${UI.msg(result.message || 'Posted')} ${result.data ? result.data.documentNo : ''}`);
  AppState.currentTrackingKeyword = '';
  await loadLookups();
  Router.go(type);
}

function buildOperationPayload(type){
  const h = name => $('#app [name="' + name + '"]').val();
  const intOrNull = v => v ? parseInt(v, 10) : null;
  const rows = $('#operationLineBody tr').map(function(){
    const r = $(this);
    const val = name => r.find('[name="' + name + '"]').val();
    if(type === 'inbound') return { itemId: intOrNull(val('itemId')), serialNumber: val('serialNumber'), barcode: val('barcode'), quantity: 1, binLocationId: intOrNull(val('binLocationId')), condition: val('condition'), note: val('lineNote') };
    if(type === 'move') return { itemInstanceId: intOrNull(val('itemInstanceId')), targetBinLocationId: intOrNull(val('targetBinLocationId')), note: val('lineNote') };
    if(type === 'adjustment') return { itemInstanceId: intOrNull(val('itemInstanceId')), newStatus: val('newStatus'), targetBinLocationId: intOrNull(val('targetBinLocationId')), reason: val('lineReason') };
    if(type === 'inventory-check') return { itemInstanceId: intOrNull(val('itemInstanceId')), actualBinLocationId: intOrNull(val('actualBinLocationId')), result: val('result'), note: val('lineNote') };
    if(type === 'repair-send') return { itemInstanceId: intOrNull(val('itemInstanceId')), targetExternalLocation: val('targetExternalLocation'), note: val('lineNote') };
    if(type === 'repair-receive') return { itemInstanceId: intOrNull(val('itemInstanceId')), targetBinLocationId: intOrNull(val('targetBinLocationId')), newSerialNumber: val('newSerialNumber') };
    if(type === 'borrow-lend') return { itemInstanceId: intOrNull(val('itemInstanceId')), targetExternalLocation: val('targetExternalLocation'), note: val('lineNote') };
    if(type === 'borrow-return') return { itemInstanceId: intOrNull(val('itemInstanceId')), condition: val('condition'), targetBinLocationId: intOrNull(val('targetBinLocationId')), note: val('lineNote') };
    return intOrNull(val('itemInstanceId'));
  }).get();

  if(!rows.length) return { error: UI.t('At least one line is required.') };
  const requiredErrors = validateRequiredOperation(type, rows, h);
  if(requiredErrors.length) return { validationErrors: requiredErrors };
  const duplicateErrors = validateOperationDuplicates(type, rows);
  if(duplicateErrors.length) return { validationErrors: duplicateErrors };
  if(type === 'inbound'){
    const validationErrors = validateInboundRows(rows);
    if(validationErrors.length) return { validationErrors };
    return { sourceExternalPartyId: intOrNull(h('sourceExternalPartyId')), warehouseId: intOrNull(h('warehouseId')), documentDate: h('documentDate'), note: h('note'), lines: rows };
  }
  if(type === 'move') return { warehouseId: intOrNull(h('warehouseId')), documentDate: h('documentDate'), note: h('note'), lines: rows };
  if(type === 'adjustment') return { warehouseId: intOrNull(h('warehouseId')), documentDate: h('documentDate'), reason: h('reason'), lines: rows };
  if(type === 'inventory-check') return { warehouseId: intOrNull(h('warehouseId')), sessionDate: h('sessionDate'), countMethod: h('countMethod'), responsibleStaff: h('responsibleStaff'), lines: rows };
  if(type === 'repair-send') return { repairVendorId: intOrNull(h('repairVendorId')), sendDate: h('sendDate'), expectedReturnDate: h('expectedReturnDate') || null, reason: h('reason'), lines: rows };
  if(type === 'repair-receive') return { repairDocumentId: intOrNull(h('repairDocumentId')), result: h('result'), resultNote: h('resultNote'), lines: rows };
  if(type === 'borrow-lend') return { documentNo: h('documentNo'), warehouseId: intOrNull(h('warehouseId')), borrowerId: intOrNull(h('borrowerId')), borrowDate: h('borrowDate'), dueDate: h('dueDate'), purpose: h('purpose'), borrowDepartment: h('borrowDepartment'), approvedBy: h('approvedBy'), borrowerPhone: h('borrowerPhone'), departmentOwner: h('departmentOwner'), lines: rows };
  return { borrowDocumentId: intOrNull(h('borrowDocumentId')), returnDate: h('returnDate'), lines: rows };
}

function validateRequiredOperation(type, rows, h){
  const errors = [];
  const addHeader = (field, label) => {
    if(!String(h(field) || '').trim()) errors.push({ field, label, message: `${label} is required.` });
  };
  const addRow = (rowNo, row, field, label) => {
    const value = row ? row[field] : null;
    if(value == null || String(value).trim() === '') errors.push({ row: rowNo, field, label, message: `${label} is required.` });
  };

  if(type === 'inbound'){
    addHeader('warehouseId', 'Warehouse');
    addHeader('documentDate', 'Inbound Date');
    return errors;
  }
  if(type === 'move'){
    addHeader('warehouseId', 'Warehouse');
    addHeader('documentDate', 'Move Date');
  }
  if(type === 'adjustment'){
    addHeader('warehouseId', 'Warehouse');
    addHeader('documentDate', 'Adjustment Date');
    addHeader('reason', 'Reason');
  }
  if(type === 'inventory-check'){
    addHeader('warehouseId', 'Warehouse');
    addHeader('sessionDate', 'Session Date');
    addHeader('countMethod', 'Count Method');
    addHeader('responsibleStaff', 'Responsible Staff');
  }
  if(type === 'repair-send'){
    addHeader('repairVendorId', 'Vendor');
    addHeader('warehouseId', 'Warehouse');
    addHeader('sendDate', 'Send Date');
    addHeader('reason', 'Reason');
  }
  if(type === 'repair-receive'){
    addHeader('repairDocumentId', 'Repair Document');
    addHeader('result', 'Result');
    addHeader('warehouseId', 'Return Warehouse');
  }
  if(type === 'borrow-lend'){
    ['documentNo','warehouseId','borrowerId','borrowDate','dueDate','borrowDepartment','approvedBy','borrowerPhone','departmentOwner','purpose']
      .forEach(field => addHeader(field, {
        documentNo: 'Borrow Document No',
        warehouseId: 'Borrow Warehouse',
        borrowerId: 'Borrower',
        borrowDate: 'Borrow Date',
        dueDate: 'Due Date',
        borrowDepartment: 'Borrow Department',
        approvedBy: 'Approver',
        borrowerPhone: 'Borrower Phone',
        departmentOwner: 'Department Owner',
        purpose: 'Purpose'
      }[field]));
  }
  if(type === 'borrow-return'){
    addHeader('borrowDocumentId', 'Borrow Document');
    addHeader('returnDate', 'Return Date');
    addHeader('warehouseId', 'Return Warehouse');
  }

  rows.forEach((row, index) => {
    const rowNo = index + 1;
    if(type !== 'inbound' && !(type === 'inventory-check' && row.result === 'Extra')) addRow(rowNo, row, 'itemInstanceId', 'Item Instance');
    if(type === 'move') addRow(rowNo, row, 'targetBinLocationId', 'Target Bin');
    if(type === 'adjustment'){
      addRow(rowNo, row, 'newStatus', 'New Status');
      addRow(rowNo, row, 'reason', 'Reason');
      if(!['Lost','Disposed'].includes(row.newStatus)) addRow(rowNo, row, 'targetBinLocationId', 'Target Bin');
    }
    if(type === 'inventory-check'){
      addRow(rowNo, row, 'result', 'Result');
      if(['Matched','WrongLocation','Damaged','Extra'].includes(row.result)) addRow(rowNo, row, 'actualBinLocationId', 'Actual Bin');
    }
    if(type === 'repair-send') addRow(rowNo, row, 'targetExternalLocation', 'External Destination');
    if(type === 'repair-receive') addRow(rowNo, row, 'targetBinLocationId', 'Target Bin');
    if(type === 'borrow-lend') addRow(rowNo, row, 'targetExternalLocation', 'External Destination');
    if(type === 'borrow-return'){
      addRow(rowNo, row, 'condition', 'Condition');
      if(row.condition !== 'Lost') addRow(rowNo, row, 'targetBinLocationId', 'Target Bin');
    }
  });

  return errors;
}

function validateOperationDuplicates(type, rows){
  const errors = [];
  const checks = [];
  if(type !== 'inbound') checks.push(['itemInstanceId', 'Item Instance', 'Each item can only appear once per document.']);
  if(type === 'inbound') checks.push(['binLocationId', 'Bin', 'Each bin can only appear once per inbound document.']);
  if(['move','repair-receive','borrow-return','adjustment'].includes(type)){
    checks.push(['targetBinLocationId', 'Target Bin', 'Each target bin can only appear once per document.']);
  }

  checks.forEach(([field, label, message]) => {
    const seen = new Map();
    rows.forEach((row, index) => {
      const value = row && row[field];
      if(!value) return;
      if(seen.has(value)){
        errors.push({ row: index + 1, field, label, message });
        return;
      }

      seen.set(value, index + 1);
    });
  });

  return errors;
}

function validateInboundRows(rows){
  const errors = [];
  const binIds = new Set();
  const serials = new Set();
  const barcodes = new Set();
  rows.forEach((row, index) => {
    const rowNo = index + 1;
    if(!row.itemId) errors.push({ row: rowNo, field: 'itemId', label: 'Item', message: 'Item is required.' });
    if(!row.binLocationId) errors.push({ row: rowNo, field: 'binLocationId', label: 'Bin', message: 'Bin is required.' });
    if(row.binLocationId && binIds.has(row.binLocationId)) errors.push({ row: rowNo, field: 'binLocationId', label: 'Bin', message: 'Each bin can only appear once per inbound document.' });
    if(row.binLocationId) binIds.add(row.binLocationId);
    const serial = (row.serialNumber || '').trim().toLowerCase();
    if(serial){
      if(serials.has(serial)) errors.push({ row: rowNo, field: 'serialNumber', label: 'Serial', message: 'Serial is duplicated in this inbound document.' });
      serials.add(serial);
    }
    const barcode = (row.barcode || '').trim().toLowerCase();
    if(barcode){
      if(barcodes.has(barcode)) errors.push({ row: rowNo, field: 'barcode', label: 'Barcode', message: 'Barcode is duplicated in this inbound document.' });
      barcodes.add(barcode);
    }
  });
  return errors;
}

function clearOperationValidation(){
  $('#operationValidation').empty();
  $('#app .is-invalid').removeClass('is-invalid');
}

function showOperationValidation(errors){
  const list = (errors || []).filter(Boolean);
  if(!list.length) return;
  list.forEach(e => {
    if(e.row && e.field){
      $('#operationLineBody tr').eq(e.row - 1).find(`[name="${e.field}"]`).addClass('is-invalid');
    } else if(e.field){
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
  if(panel) panel.scrollIntoView({ behavior: 'smooth', block: 'center' });
}

function operationErrorsFromResult(result){
  const errors = Array.isArray(result && result.errors) && result.errors.length ? result.errors : [result && result.message ? result.message : 'Request failed.'];
  return errors.map(message => ({ label: 'System', message }));
}

async function uploadOperationAttachments(entityName, entityId){
  const input = document.getElementById('operationAttachments');
  if(!input || !input.files || !input.files.length) return;
  if(!entityName || !entityId) throw new Error(UI.t('Attachment entity is invalid.'));
  for(const file of input.files){
    const form = new FormData();
    form.append('entityName', entityName);
    form.append('entityId', entityId);
    form.append('file', file);
    const result = await UI.upload('/Attachments/Upload', form);
    if(!result.success) throw new Error(UI.resultError(result));
  }
}

async function loadOperationDocuments(type){
  const result = await UI.api('/Documents/List', { query: {
    type,
    keyword: $('#app [name="docKeyword"]').val() || null,
    fromDate: $('#app [name="docFromDate"]').val() || null,
    toDate: $('#app [name="docToDate"]').val() || null
  }});
  const rows = result || [];
  if(!rows.length){
    $('#operationDocuments').html(UI.empty('No data'));
    return;
  }
  $('#operationDocuments').html(`<div class="table-wrap"><table class="data-table"><thead><tr><th class="px-3">${UI.t('Document No')}</th><th>${UI.t('Document Date')}</th><th>${UI.t('Party')}</th><th>${UI.t('Warehouse')}</th><th>${UI.t('Status')}</th><th>${UI.t('Lines')}</th><th>${UI.t('ApprovedBy')}</th><th></th></tr></thead><tbody>${rows.map(r => `<tr><td class="px-3 fw-semibold">${UI.esc(r.documentNo)}</td><td>${UI.formatDate(r.documentDate)}</td><td>${UI.esc(r.party || '-')}</td><td>${UI.esc(r.warehouse || '-')}</td><td><span class="badge text-bg-light">${UI.esc(r.status || '-')}</span></td><td>${UI.esc(r.lines)}</td><td>${UI.esc(r.approvedBy || '-')}</td><td><button class="btn btn-light btn-sm btn-doc-detail" data-id="${r.id}" data-type="${UI.esc(type)}" title="${UI.t('View Detail')}"><i class="bi bi-eye"></i></button></td></tr>`).join('')}</tbody></table><div class="server-footer"><span>${UI.endpoint('DocumentsList')}</span><span>${rows.length} ${UI.t('rows')}</span></div></div>`);
}

$(document).on('click', '.btn-doc-detail', async function(){
  const detail = await UI.api('/Documents/Detail', { query: { type: $(this).data('type'), id: $(this).data('id') } });
  $('#drawer .fw-bold').first().text(UI.t('View Detail'));
  $('#drawerBody').html(renderDocumentDetail(detail));
  await loadDocumentAttachments(detail.header ? detail.header.entityName : '', detail.header ? detail.header.id : 0);
  $('#drawer').addClass('open');
});

function renderDocumentDetail(detail){
  const h = detail.header || {};
  const extra = h.extra || {};
  const extraRows = Object.keys(extra).length ? `<div class="audit-footer mt-2">${Object.entries(extra).map(([k,v]) => `<div>${UI.t(detailLabel(k))}: <b>${UI.esc(v || '-')}</b></div>`).join('')}</div>` : '';
  const lines = detail.lines || [];
  return `<div class="audit-footer">
    <div>${UI.t('Document No')}: <b>${UI.esc(h.documentNo || '-')}</b></div>
    <div>${UI.t('Document Date')}: <b>${UI.formatDate(h.documentDate)}</b></div>
    <div>${UI.t('Party')}: <b>${UI.esc(h.party || '-')}</b></div>
    <div>${UI.t('Warehouse')}: <b>${UI.esc(h.warehouse || '-')}</b></div>
    <div>${UI.t('CreatedBy')}: <b>${UI.esc(h.createdBy || '-')}</b></div>
    <div>${UI.t('ApprovedBy')}: <b>${UI.esc(h.approvedBy || '-')}</b></div>
  </div>${extraRows}
  <div class="form-section-title mt-3">${UI.t('Line Items')}</div>
  <div class="table-wrap"><table class="data-table"><thead><tr><th class="px-3">${UI.t('Item')}</th><th>${UI.t('Serial / Barcode')}</th><th>${UI.t('Status')}</th><th>${UI.t('Location')}</th><th>${UI.t('Note')}</th></tr></thead><tbody>${lines.map(l => `<tr><td class="px-3">${UI.esc(l.item || '-')}</td><td>${UI.esc(l.serial || l.barcode || '-')}</td><td>${UI.esc(l.condition || l.result || l.newStatus || l.returned || '-')}</td><td>${UI.esc(l.targetBin || l.to || l.bin || l.fromBin || '-')}</td><td>${UI.esc(l.note || '-')}</td></tr>`).join('')}</tbody></table></div>
  <div class="form-section-title mt-3">${UI.t('Attachments')}</div>
  <div id="documentAttachments">${UI.loading()}</div>`;
}

async function loadDocumentAttachments(entityName, entityId){
  if(!entityName || !entityId){ $('#documentAttachments').html(UI.empty('No attachments')); return; }
  const rows = await UI.api('/Attachments/List', { query: { entityName, entityId } });
  if(!rows.length){ $('#documentAttachments').html(UI.empty('No attachments')); return; }
  $('#documentAttachments').html(`<div class="table-wrap"><table class="data-table"><thead><tr><th class="px-3">${UI.t('File Name')}</th><th>${UI.t('Size')}</th><th>${UI.t('Uploaded At')}</th><th></th></tr></thead><tbody>${rows.map(r => `<tr><td class="px-3">${UI.esc(r.fileName)}</td><td>${UI.esc(formatFileSize(r.fileSize))}</td><td>${UI.formatDate(r.createdAt)}</td><td><a class="btn btn-light btn-sm" href="/Attachments/Download/${UI.esc(r.id)}"><i class="bi bi-download"></i> ${UI.t('Download')}</a></td></tr>`).join('')}</tbody></table></div>`);
}

function formatFileSize(bytes){
  const n = Number(bytes || 0);
  if(n < 1024) return `${n} B`;
  if(n < 1024 * 1024) return `${Math.round(n / 1024)} KB`;
  return `${(n / 1024 / 1024).toFixed(1)} MB`;
}

function detailLabel(key){
  return ({
    purpose: 'Purpose',
    borrowDepartment: 'Borrow Department',
    borrowerPhone: 'Borrower Phone',
    departmentOwner: 'Department Owner',
    dueDate: 'Due Date'
  })[key] || key;
}
