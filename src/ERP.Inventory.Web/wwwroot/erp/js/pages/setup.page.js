let masterRows = [];
let structureRows = [];
let systemUsers = [];
let systemRoles = [];

Router.register('warehouse-structure', async function(){
  $('#app').html(UI.pageHeader('Warehouse Structure','Home / Warehouse Structure','<button class="btn btn-primary" id="btnNewStructure"><i class="bi bi-plus-circle me-2"></i>' + UI.t('New Warehouse / Bin') + '</button>') +
    `<div class="card mb-3"><div class="card-body"><div class="form-section-title">${UI.t('Location Hierarchy')}</div><div class="structure-path"><span class="structure-node">${UI.t('Company')}</span><i class="bi bi-chevron-right text-muted"></i><span class="structure-node">${UI.t('Branch')}</span><i class="bi bi-chevron-right text-muted"></i><span class="structure-node">${UI.t('Warehouse')}</span><i class="bi bi-chevron-right text-muted"></i><span class="structure-node">${UI.t('Zone')}</span><i class="bi bi-chevron-right text-muted"></i><span class="structure-node">${UI.t('Rack')}</span><i class="bi bi-chevron-right text-muted"></i><span class="structure-node">${UI.t('Shelf')}</span><i class="bi bi-chevron-right text-muted"></i><span class="structure-node">${UI.t('BinLocation')}</span></div></div></div>
    <div class="card"><div class="card-body"><div class="row g-3 mb-3"><div class="col-md-3">${UI.select('Warehouse', 'warehouseId', AppState.lookups.warehouses)}</div><div class="col-md-3">${UI.select('Status', 'isActive', statusOptions())}</div><div class="col-md-4">${UI.input('Bin code', 'text', '', 'keyword')}</div><div class="col-md-2 d-flex align-items-end"><label class="form-label w-100"><span class="fw-semibold small"></span><button class="btn btn-primary w-100" id="btnLoadStructure">${UI.t('Load')}</button></label></div></div><div id="structureSummary"></div><div id="structureTable">${UI.loading()}</div></div></div>`);
  $('#btnLoadStructure').on('click', loadWarehouseStructure);
  $('#btnNewStructure').on('click', () => openStructureForm());
  $('#app select, #app .cbo-value, #app input[name="keyword"]').on('change input', UI.debounce(() => loadWarehouseStructure(1), 250));
  await loadWarehouseStructure();
});

async function loadWarehouseStructure(page = 1, pageSize = AppState.pageSize || 25){
    const query = {
        page,
        pageSize,
        warehouseId:$('#app [name="warehouseId"]').val() || null,
        isActive: boolFilter($('#app [name="isActive"]').val()),
        keyword:$('#app [name="keyword"]').val() || null
    };

    $('#structureTable').html(UI.loading());
    const result = await UI.api('/Management/WarehouseStructure',{ query });
    const rows = result.data || [];
    const total = result.total || 0;

    structureRows = rows;
    if (!structureRows.length) {
        $('#structureSummary').empty();
        $('#structureTable').html(UI.empty('No data'));
        return;
    }
    const summary = structureRows.reduce((acc, row) => {acc[row.warehouse] =(acc[row.warehouse] || 0) + 1;return acc;}, {});

    $('#structureSummary').html(`
        <div class="d-flex flex-wrap gap-2 mb-3">
            ${Object.entries(summary).map(([warehouse, count]) => `
                <span class="badge text-bg-light border">
                    ${UI.esc(warehouse)}:
                    ${UI.esc(count)}
                    ${UI.t('bins')}
                </span>
            `).join('')}
        </div>`);

    const totalPages = pageSize === 0 ? 1 : Math.ceil(total / pageSize);
    const isAll = pageSize === 0;
    const from = isAll? (total ? 1 : 0): (page - 1) * pageSize + 1;
    const to = isAll? total: Math.min(page * pageSize, total);
    const totalPageRows = (to - from) + 1;
    const pagination = Pagination.render({
        page,
        pageSize,
        total,
        totalPages,
        isAll,
        selectId: 'structurePageSizeSelect',
        onChange: 'loadWarehouseStructure'
    });

    $('#structureTable').html(`
        <div class="table-wrap">
            <table class="data-table">
                <thead>
                    <tr>
                        <th class="px-3">${UI.t('Warehouse')} </th>
                        <th>${UI.t('Zone')} </th>
                        <th>${UI.t('Rack')}</th>
                        <th>${UI.t('Shelf')}</th>
                        <th>${UI.t('Bin')}</th>
                        <th>${UI.t('Full Path')}</th>
                        <th>${UI.t('Status')}</th>
                        <th>${UI.t('Actions')} </th>
                    </tr>
                </thead>
                <tbody>
                    ${structureRows.map(r => `
                        <tr>
                            <td class="px-3">${UI.esc(r.warehouse)}</td>
                            <td>${UI.esc(r.zone)}</td>
                            <td>${UI.esc(r.rack)}</td>
                            <td>${UI.esc(r.shelf)}</td>
                            <td class="fw-semibold">${UI.esc(r.bin)}</td>
                            <td>${UI.esc(r.fullPath)}</td>
                            <td><span class="badge ${r.isActive ? 'text-bg-success' : 'text-bg-secondary'}">${r.isActive ? UI.t('Active') : UI.t('Inactive')}</span></td>
                            <td>${rowActions('structure',r.id, r.isActive)}
                            </td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
            <div class="server-footer">
                <span>${UI.endpoint('WarehouseStructure')}: ${total} ${UI.t('rows')}</span><span>${UI.t('Page')} ${page} &middot; ${totalPageRows} ${UI.t('rows')}</span>
            </div>
        </div>
        ${pagination}`);

    Pagination.bindPageSize('structurePageSizeSelect',loadWarehouseStructure);
}


Router.register('master-data', async function(){
  $('#app').html(UI.pageHeader('Master Data','Home / Master Data','<button class="btn btn-primary" id="btnNewMaster"><i class="bi bi-plus-circle me-2"></i>' + UI.t('New Master Record') + '</button>') +
    `<div id="masterSummary" class="row g-3 mb-3">${UI.loading()}</div>
    <div class="card"><div class="card-body"><div class="row g-3 mb-3"><div class="col-md-3">${UI.select('Entity', 'entity', [{ id: 'items', text: UI.t('Items') }, { id: 'categories', text: UI.t('Categories') }, { id: 'parties', text: UI.t('External Parties') }], 'items')}</div><div class="col-md-3">${UI.select('Status', 'isActive', statusOptions())}</div><div class="col-md-4">${UI.input('Keyword', 'text', '', 'keyword')}</div><div class="col-md-2 d-flex align-items-end"><label class="form-label w-100"><span class="fw-semibold small"></span><button class="btn btn-primary w-100" id="btnLoadMaster">${UI.t('Load')}</button></label></div></div><div id="masterTable">${UI.loading()}</div></div></div>`);
  $('#btnLoadMaster').on('click', loadMasterData);
  $('#btnNewMaster').on('click', () => openMasterForm());
  $('#app select, #app .cbo-value, #app input[name="keyword"]').on('change input', UI.debounce(() => loadMasterData(1), 250));
  await refreshMasterSummary();
  await loadMasterData();
});

async function refreshMasterSummary(){
  const s = await UI.api('/Management/MasterDataSummary');
  $('#masterSummary').html([
    ['Items', s.items, `${UI.t('Serial-managed')}: ${s.serialManaged}`],
    ['Categories', s.categories, UI.t('Active/inactive from DB')],
    ['External Parties', s.externalParties, UI.t('Supplier, borrower, repair vendor')],
    ['Translations', s.translations, UI.t('Item and resource translations')]
  ].map(c => `<div class="col-xl-3 col-md-6"><div class="card"><div class="card-body"><div class="text-muted small">${UI.esc(UI.t(c[0]))}</div><div class="display-6 fw-bold">${UI.esc(c[1])}</div><div class="small text-muted">${UI.esc(c[2])}</div></div></div></div>`).join(''));
}

async function loadMasterData(page = 1, pageSize = AppState.pageSize || 25) {

    const entity = $('#app [name="entity"]').val() || 'items';
    const isActiveValue = $('#app [name="isActive"]').val();

    const query = {
        entity,
        page,
        pageSize,
        isActive:
        isActiveValue === ''? null: isActiveValue === 'true',
        keyword: $('#app [name="keyword"]').val()?.trim() || null
    };

    $('#masterTable').html(UI.loading());

    const result = await UI.api('/Management/MasterDataList',{ query });

    const rows = result.data || [];
    const total = result.total || 0;

    masterRows = rows;
    if (!masterRows.length) { $('#masterTable').html(UI.empty('No data'));return;}

    const totalPages = pageSize === 0 ? 1: Math.ceil(total / pageSize);
    const isAll = pageSize === 0;
    const from = isAll? (total ? 1 : 0): (page - 1) * pageSize + 1;
    const to = isAll? total: Math.min(page * pageSize, total);
    const totalPageRows = (to - from) + 1;
    const pagination = Pagination.render({
        page,
        pageSize,
        total,
        totalPages,
        isAll,
        selectId: 'masterPageSizeSelect',
        onChange: 'loadMasterData'
    });

    $('#masterTable').html(`
        <div class="table-wrap">
            <table class="data-table">
                <thead>
                    <tr>
                        <th class="px-3">${UI.t('Code')} </th>
                        <th>${UI.t('Name')}</th>
                        <th>${UI.t('Type')} </th>
                        <th>${UI.t('Serial Tracking')}</th>
                        <th>${UI.t('Language Coverage')}</th>
                        <th>${UI.t('Status')}</th>
                        <th>${UI.t('Actions')}</th>
                    </tr>
                </thead>
                <tbody>
                    ${masterRows.map(r => `
                        <tr>
                            <td class="px-3 fw-semibold">${UI.esc(r.code)} </td>
                            <td>${UI.esc(r.name)}</td>
                            <td>${UI.esc(displayMasterType(r))}</td>
                            <td>${UI.esc(r.serialTracking === 'Yes' ? UI.t('Yes'): r.serialTracking === 'No'  ? UI.t('No') : r.serialTracking)} </td>
                            <td>${UI.esc(r.languageCoverage || '-')}</td>
                            <td><span class="badge ${r.isActive ? 'text-bg-success': 'text-bg-secondary'}"> ${UI.esc(UI.t(r.status))}</span></td>
                            <td>${rowActions('master', r.id,r.isActive)}</td>
                            </tr>
                    `).join('')}
                </tbody>
            </table>
            <div class="server-footer">
                <span>${UI.endpoint('MasterDataList')}: ${total} ${UI.t('rows')}</span><span>${UI.t('Page')} ${page} &middot; ${totalPageRows} ${UI.t('rows')}</span>
            </div>
        </div>
        ${pagination}`);
    Pagination.bindPageSize('masterPageSizeSelect',loadMasterData);
}

function displayMasterType(row){
  if(row.entity === 'parties') return UI.enum('ExternalPartyType', row.type);
  return UI.t(row.type);
}

async function openMasterForm(id){
  const entity = $('#app [name="entity"]').val();
  const endpoint = masterEndpoint(entity);
  const data = id ? await UI.api(`/Management/${endpoint}/${id}`) : {};
  $('#drawer .fw-bold').first().text(UI.t(id ? 'Edit' : 'New Master Record'));
  if(entity === 'categories'){
      $('#drawerBody').html(`<div class="row g-2">${UI.inputform('Category Code', 'text', data.categoryCode || '', 'categoryCode')}${UI.inputform('Name','text',data.name || '','name') }</div>${activeCheck(data.isActive !== false)}${saveButton('btnSaveCategory', id)}`);
  } else if(entity === 'parties'){
      $('#drawerBody').html(`<div class="row g-2">${UI.inputform('Party Code', 'text', data.partyCode || '', 'partyCode')}${UI.inputform('Name', 'text', data.name || '', 'name')}</div>${UI.select('Type', 'partyType', AppState.lookups.externalPartyTypes, data.partyType || '')}<div class="row g-2">${UI.inputform('Contact Name', 'text', data.contactName || '', 'contactName')}${UI.inputform('Phone', 'text', data.phone || '', 'phone')}</div>${UI.inputform('Email','email',data.email || '','email')}${activeCheck(data.isActive !== false)}${saveButton('btnSaveParty', id)}`);
  } else {
      $('#drawerBody').html(`<div class="row g-2">${UI.inputform('Item Code', 'text', data.itemCode || '', 'itemCode')}${UI.inputform('Default Name', 'text', data.defaultName || '', 'defaultName')}</div>${UI.select('Category', 'categoryId', AppState.lookups.categories, data.categoryId || '')}<div class="row g-2">${UI.inputform('Unit Code', 'text', data.unitCode || 'PCS', 'unitCode')}${UI.inputform('Unit Name','text',data.unitName || '','unitName')}<div><label class="form-check my-2"><input class="form-check-input" type="checkbox" name="isSerialManaged" ${data.isSerialManaged ? 'checked' : ''}> ${UI.t('Serial managed')}</label>${UI.input('Name VI','text',data.nameVi || '','nameVi')}${UI.input('Name EN','text',data.nameEn || '','nameEn')}${UI.input('Name ZH','text',data.nameZh || '','nameZh')}${activeCheck(data.isActive !== false)}${saveButton('btnSaveItem', id)}`);
  } 
  $('#drawer').addClass('open');
}

async function openStructureForm(id){
  const data = id ? await UI.api(`/Management/WarehouseStructure/${id}`) : {};
  $('#drawer .fw-bold').first().text(UI.t(id ? 'Edit' : 'New Warehouse / Bin'));
  const hasWarehouses = (AppState.lookups.warehouses || []).length > 0;
    const mode = data.warehouseId || (!id && hasWarehouses) ? 'existing' : 'new';
    //<div class="small text-muted mb-6">${UI.t('Warehouse company, branch and code are inherited from the selected warehouse.')}</div>
    $('#drawerBody').html(`
   <div class="row g-2">
     <div class="col-md-5">${UI.selectform('Structure Mode','structureMode', structureModeOptions(), mode)}</div>
    <div class="structure-mode-existing col-md-7">
      ${UI.selectform('Existing Warehouse','warehouseId', AppState.lookups.warehouses, data.warehouseId || $('#app [name="warehouseId"]').val() || '')}
    </div>
    </div>
    <div class="structure-mode-new">
      <div class="row g-2">
          <div class="col-md-5">${UI.input('Company Code', 'text', data.companyCode || UI.t('Default CompanyCode'), 'companyCode')}</div>
          <div class="col-md-7">${UI.input('Company Name', 'text', data.companyName || UI.t('Default CompanyName'), 'companyName')}</div>
          <div class="col-md-5">${UI.input('Branch Code', 'text', data.branchCode || UI.t('Default BranchCode'), 'branchCode')}</div>
          <div class="col-md-7">${UI.input('Branch Name', 'text', data.branchName || UI.t('Default BranchName'), 'branchName')}</div>
          <div class="col-md-5"> ${UI.input('Warehouse Code', 'text', data.warehouseCode || '', 'warehouseCode')}</div>
          <div class="col-md-7">${UI.input('Warehouse Name', 'text', data.warehouseName || '', 'warehouseName')}</div>
      </div>
    </div>
    <div class="row g-2">
      <div class="col-md-5">${UI.input('Zone Code','text',data.zoneCode || '','zoneCode')}</div>
      <div class="col-md-7">${UI.input('Zone Name','text',data.zoneName || '','zoneName')}</div>
      <div class="col-md-5">${UI.input('Rack Code','text',data.rackCode || '','rackCode')}</div>
      <div class="col-md-7">${UI.input('Rack Name','text',data.rackName || '','rackName')}</div>
      <div class="col-md-5">${UI.input('Shelf Code','text',data.shelfCode || '','shelfCode')}</div>
      <div class="col-md-7">${UI.input('Shelf Name','text',data.shelfName || '','shelfName')}</div>
      <div class="col-md-5">${UI.input('Bin Code','text',data.binCode || '','binCode')}</div>
    </div>
    ${activeCheck(data.isActive !== false)}
    ${saveButton('btnSaveStructure', id)}`);
  toggleStructureMode();
  wireStructureBinCodeAuto(data.binCode || '');
  $('#drawer').addClass('open');
}

function structureModeOptions(){
  return [
    { id:'existing', text:UI.t('Add position to existing warehouse') },
    { id:'new', text:UI.t('Create new warehouse hierarchy') }
  ];
}

function toggleStructureMode(){
  const mode = $('#drawerBody [name="structureMode"]').val() || 'existing';
  $('.structure-mode-existing').toggle(mode === 'existing');
  $('.structure-mode-new').toggle(mode === 'new');
}

$(document).on('change', '#drawerBody [name="structureMode"]', toggleStructureMode);

function wireStructureBinCodeAuto(existingCode){
    const bin = selectObjectByName('binCode');
    const rackName = selectObjectByName('rackName'); 
    const shelfName = selectObjectByName('shelfName'); 
    const zoneName = selectObjectByName('zoneName'); 
    const warehouseName = selectObjectByName('warehouseName'); 

  let lastGenerated = existingCode || '';
  let manual = !!existingCode;

  const selectedWarehouseCode = () => {
      const mode = selectObjectByName('structureMode').val() || 'existing';
      if (mode === 'new') return selectObjectByName('warehouseCode').val() || '';
    const option = $('#drawerBody [name="warehouseId"] option:selected');
    return (option.attr('data-code') || option.text().split(' - ')[0] || '').trim();
    };

    const cleanCode = value =>
        String(value || '').trim().replace(/\s+/g, '').toUpperCase();

    const forceUpper = function () {
        const val = cleanCode($(this).val());
        $(this).val(val);
    };

    const updateNames = () => {
        const rackCode = cleanCode(selectObjectByName('rackCode').val());
        const shelfCode = cleanCode(selectObjectByName('shelfCode').val());
        const zoneCode = cleanCode(selectObjectByName('zoneCode').val());
        const mode = $('#drawerBody [name="structureMode"]').val() || 'existing';
        if (mode === 'new') {
            selectObjectByName('warehouseCode').off('input.uppercase').on('input.uppercase', forceUpper);
            const warehouseCode = selectObjectByName('warehouseCode').val();
            warehouseName.val(warehouseCode);
        }


        selectObjectByName('rackCode').add(selectObjectByName('shelfCode')).add(selectObjectByName('zoneCode')).off('input.uppercase').on('input.uppercase', forceUpper);

        zoneName.val(zoneCode || '');
        rackName.val(rackCode ? `Rack ${rackCode}` : '');
        shelfName.val(shelfCode ? `Shelf ${shelfCode}` : '');
    };

  const generate = () => {
    const warehouseCode = selectedWarehouseCode();
    const rackCode = selectObjectByName('rackCode').val() || '';
    const shelfCode = selectObjectByName('shelfCode').val() || '';
    return [warehouseCode, rackCode, shelfCode]
      .map(x => String(x || '').trim().replace(/\s+/g, '').toUpperCase())
      .filter(Boolean)
      .join('_');
  };

  const update = () => {
    updateNames();
    const next = generate();
    const current = bin.val() || '';
    if(!next) return;
    if(!manual || !current || current === lastGenerated){
      bin.val(next);
      lastGenerated = next;
      manual = false;
    }
    };

  bin.off('input.binAuto').on('input.binAuto', function(){
    manual = String($(this).val() || '') !== lastGenerated;
  });
    $('#drawerBody [name="structureMode"], #drawerBody [name="warehouseId"], #drawerBody [name="warehouseCode"], #drawerBody [name="zoneCode"], #drawerBody [name="rackCode"], #drawerBody [name="shelfCode"]')
    .off('change.binAuto input.binAuto')
    .on('change.binAuto input.binAuto', update);
  update();
}

function selectObjectByName(name) {
    return $(`#drawerBody [name="${name}"]`);
}

$(document).on('click', '#btnSaveCategory', async function(){
  const id = $(this).data('id');
  const result = await UI.api(id ? `/Management/Category/${id}` : '/Management/Category', { method: id ? 'PUT' : 'POST', data:{ categoryCode: $('[name="categoryCode"]').val(), name: $('[name="name"]').val(), isActive: $('[name="isActive"]').is(':checked') } });
  await afterMasterSave(result);
});
$(document).on('click', '#btnSaveParty', async function(){
  const id = $(this).data('id');
  const result = await UI.api(id ? `/Management/ExternalParty/${id}` : '/Management/ExternalParty', { method: id ? 'PUT' : 'POST', data:{ partyCode: $('[name="partyCode"]').val(), name: $('[name="name"]').val(), partyType: $('[name="partyType"]').val(), contactName: $('[name="contactName"]').val(), phone: $('[name="phone"]').val(), email: $('[name="email"]').val(), isActive: $('[name="isActive"]').is(':checked') } });
  await afterMasterSave(result);
});
$(document).on('click', '#btnSaveItem', async function(){
  const id = $(this).data('id');
  const result = await UI.api(id ? `/Management/Item/${id}` : '/Management/Item', { method: id ? 'PUT' : 'POST', data:{ itemCode: $('[name="itemCode"]').val(), defaultName: $('[name="defaultName"]').val(), categoryId: parseInt($('[name="categoryId"]').val(),10), unitCode: $('[name="unitCode"]').val(), unitName: $('[name="unitName"]').val(), isSerialManaged: $('[name="isSerialManaged"]').is(':checked'), isActive: $('[name="isActive"]').is(':checked'), nameVi: $('[name="nameVi"]').val(), nameEn: $('[name="nameEn"]').val(), nameZh: $('[name="nameZh"]').val() } });
  await afterMasterSave(result);
});
$(document).on('click', '#btnSaveStructure', async function(){
  const id = $(this).data('id');
  const data = {}; $('#drawerBody [name]').each(function(){ data[$(this).attr('name')] = $(this).attr('type') === 'checkbox' ? $(this).is(':checked') : $(this).val(); });
  if(data.structureMode === 'existing'){
    data.warehouseId = data.warehouseId ? parseInt(data.warehouseId, 10) : null;
  } else {
    data.warehouseId = null;
  }
  const result = await UI.api(id ? `/Management/WarehouseStructure/${id}` : '/Management/WarehouseStructure', { method: id ? 'PUT' : 'POST', data });
  await afterStructureSave(result);
});

$(document).on('click', '.btn-edit-master', function(){ openMasterForm($(this).data('id')); });
$(document).on('click', '.btn-toggle-master', function(){ toggleMaster($(this).data('id'), $(this).data('active') === true || $(this).data('active') === 'true'); });
$(document).on('click', '.btn-delete-master', function(){ hardDeleteMaster($(this).data('id')); });
$(document).on('click', '.btn-edit-structure', function(){ openStructureForm($(this).data('id')); });
$(document).on('click', '.btn-toggle-structure', function(){ toggleStructure($(this).data('id'), $(this).data('active') === true || $(this).data('active') === 'true'); });
$(document).on('click', '.btn-delete-structure', function(){ hardDeleteStructure($(this).data('id')); });

async function toggleMaster(id, active){
  const endpoint = masterEndpoint($('#app [name="entity"]').val());
  const result = await UI.api(`/Management/${endpoint}/${id}/${active ? 'Deactivate' : 'Restore'}`, { method:'POST', data:{} });
  UI.toast(result.success ? UI.t(active ? 'Record deactivated.' : 'Record restored.') : UI.resultError(result));
  await loadLookups();
  await loadMasterData();
}

function hardDeleteMaster(id){
  const endpoint = masterEndpoint($('#app [name="entity"]').val());
  UI.confirm('Hard Delete', 'This permanently removes unused trash data only.', `<div>ID: <b>${id}</b></div>`, async function(){
    const result = await UI.api(`/Management/${endpoint}/${id}`, { method:'DELETE', data:{} });
    UI.toast(result.success ? UI.t('Record deleted.') : UI.resultError(result));
    await loadLookups();
    await loadMasterData();
  });
}

async function toggleStructure(id, active){
  const result = await UI.api(`/Management/WarehouseStructure/${id}/${active ? 'Deactivate' : 'Restore'}`, { method:'POST', data:{} });
  UI.toast(result.success ? UI.t(active ? 'Record deactivated.' : 'Record restored.') : UI.resultError(result));
  await loadLookups();
  await loadWarehouseStructure();
}

function hardDeleteStructure(id){
  UI.confirm('Hard Delete', 'This permanently removes unused trash data only.', `<div>ID: <b>${id}</b></div>`, async function(){
    const result = await UI.api(`/Management/WarehouseStructure/${id}`, { method:'DELETE', data:{} });
    UI.toast(result.success ? UI.t('Record deleted.') : UI.resultError(result));
    await loadLookups();
    await loadWarehouseStructure();
  });
}

Router.register('system', async function(){
  const action = isAdmin() ? '<button class="btn btn-primary" id="btnNewUser"><i class="bi bi-person-plus me-2"></i>' + UI.t('Create User') + '</button>' : '';
  $('#app').html(UI.pageHeader('System','Home / System', action) +
    `<div id="systemSummary" class="row g-3 mb-3">${UI.loading()}</div>
    <div class="card mb-3"><div class="card-body"><div class="d-flex justify-content-between mb-3"><div class="form-section-title mb-0">${UI.t('Users')}</div><button class="btn btn-outline-secondary btn-sm" id="btnReloadUsers"><i class="bi bi-arrow-clockwise"></i></button></div><div id="systemUsers">${UI.loading()}</div></div></div>
    <div class="card"><div class="card-body"><div class="form-section-title">${UI.t('Audit Log')}</div><div id="systemAudit">${UI.loading()}</div></div></div>`);
  try {
    const s = await UI.api('/Management/SystemSummary');
    $('#systemSummary').html([
        ['Users', s.users, UI.t('SystemUsers')],
        ['Roles', s.roles, UI.t('SystemRoles')],
        ['Warehouse Permissions', s.warehousePermissions, UI.t('UserWarehousePermissions')],
        ['Unread Notifications', s.unreadNotifications, UI.t('Notifications')]
    ].map(c => `<div class="col-xl-3 col-md-6"><div class="card"><div class="card-body"><div class="text-muted small">${UI.esc(UI.t(c[2]))}</div><div class="display-6 fw-bold">${UI.esc(c[1])}</div><div class="small text-muted">${UI.esc(UI.t(c[0]))}</div></div></div></div>`).join(''));
    systemRoles = await UI.api('/Management/Roles');
    await loadUsers();
  } catch(err) {
    $('#systemSummary').html(UI.empty('Access denied for current role.'));
    $('#systemUsers').html(UI.empty('Access denied for current role.'));
  }
  $('#btnNewUser').on('click', () => openUserForm());
  $('#btnReloadUsers').on('click', loadUsers);
  await loadAuditLog();
});

async function loadUsers(){
  systemUsers = await UI.api('/Management/Users');
  if(!systemUsers.length){ $('#systemUsers').html(UI.empty('No data')); return; }
  $('#systemUsers').html(`<div class="table-wrap"><table class="data-table"><thead><tr><th class="px-3">${UI.t('User Name')}</th><th>${UI.t('Display Name')}</th><th>Email</th><th>${UI.t('Roles')}</th><th>${UI.t('Assigned Warehouses')}</th><th>${UI.t('Status')}</th><th>${UI.t('Actions')}</th></tr></thead><tbody>${systemUsers.map(u => `<tr><td class="px-3 fw-semibold">${UI.esc(u.userName)}</td><td>${UI.esc(u.displayName)}</td><td>${UI.esc(u.email || '-')}</td><td>${UI.esc(UI.t((u.roles || []).join(', ')))}</td><td>${UI.esc((u.warehouses || []).join(', ') || '-')}</td><td><span class="badge ${u.isActive ? 'text-bg-success' : 'text-bg-secondary'}">${u.isActive ? UI.t('Active') : UI.t('Inactive')}</span></td><td>${rowActions('user', u.id, u.isActive)}</td></tr>`).join('')}</tbody></table></div>`);
}

async function openUserForm(id){
  const data = id ? await UI.api(`/Management/User/${id}`) : { roleIds: [], warehouseIds: [], isActive: true, preferredLanguage: 'vi' };
  $('#drawer .fw-bold').first().text(UI.t(id ? 'Edit' : 'Create User'));
  $('#drawerBody').html(`${UI.input('User Name','text',data.userName || '','userName')}${UI.input('Display Name','text',data.displayName || '','displayName')}${UI.input('Email','email',data.email || '','email')}${UI.input('Password','password','','password')}${UI.select('Preferred Language','preferredLanguage',[{id:'vi',text:UI.t('Vietnamese')},{id:'en',text:UI.t('English')},{id:'zh',text:UI.t('Chinese')}], data.preferredLanguage || 'vi')}<div class="form-section-title">${UI.t('Roles')}</div>${checkboxList('roleIds', systemRoles.map(r => ({ id:r.id, text:r.name })), data.roleIds || [])}<div class="form-section-title mt-3">${UI.t('Assigned Warehouses')}</div>${checkboxList('warehouseIds', AppState.lookups.warehouses, data.warehouseIds || [])}${activeCheck(data.isActive !== false)}${saveButton('btnSaveUser', id || '')}`);
  $('#drawer').addClass('open');
}

$(document).on('click', '#btnSaveUser', async function(){
  const id = $(this).data('id');
  const result = await UI.api(id ? `/Management/User/${id}` : '/Management/User', { method: id ? 'PUT' : 'POST', data:{
    userName: $('[name="userName"]').val(),
    displayName: $('[name="displayName"]').val(),
    email: $('[name="email"]').val(),
    password: $('[name="password"]').val(),
    preferredLanguage: $('[name="preferredLanguage"]').val(),
    isActive: $('[name="isActive"]').is(':checked'),
    roleIds: checkedValues('roleIds').map(Number),
    warehouseIds: checkedValues('warehouseIds').map(Number)
  }});
  UI.toast(result.success ? UI.t('Saved') : UI.resultError(result));
  if(result.success){
    $('#drawer').removeClass('open');
    await loadUsers();
  }
});
$(document).on('click', '.btn-edit-user', function(){ openUserForm($(this).data('id')); });
$(document).on('click', '.btn-toggle-user', async function(){
  const active = $(this).data('active') === true || $(this).data('active') === 'true';
  const result = await UI.api(`/Management/User/${$(this).data('id')}/${active ? 'Deactivate' : 'Restore'}`, { method:'POST', data:{} });
  UI.toast(result.success ? UI.t(active ? 'Record deactivated.' : 'Record restored.') : UI.resultError(result));
  await loadUsers();
});
$(document).on('click', '.btn-delete-user', function(){
  const id = $(this).data('id');
  UI.confirm('Hard Delete', 'This permanently removes unused trash data only.', `<div>ID: <b>${UI.esc(id)}</b></div>`, async function(){
    const result = await UI.api(`/Management/User/${id}`, { method:'DELETE', data:{} });
    UI.toast(result.success ? UI.t('Record deleted.') : UI.resultError(result));
    await loadUsers();
  });
});

async function loadAuditLog( page = 1, pageSize = AppState.pageSize || 25) {
    const result = await UI.api('/Management/AuditLogs', { query: { page: page, pageSize: pageSize } });
    const rows = result.items || [];
    const total = result.totalCount || 0;
    if (!rows.length) {$('#systemAudit').html(UI.empty('No data')); return; }

    const totalPages = pageSize === 0 ? 1 : Math.ceil(total / pageSize);
    const isAll = pageSize === 0;
    const from = isAll? (total ? 1 : 0) : (page - 1) * pageSize + 1;
    const to = isAll ? total : Math.min(page * pageSize, total);
    const totalPageRows = (to - from) + 1;
    const pagination = Pagination.render({
        page,
        pageSize,
        total,
        totalPages,
        isAll,
        selectId: 'auditPageSizeSelect',
        onChange: 'loadAuditLog'
    });

    $('#systemAudit').html(`
        <div class="table-wrap">
            <table class="data-table">
                <thead>
                    <tr>
                        <th class="px-3">${UI.t('Time')}</th>
                        <th>${UI.t('User')}</th>
                        <th>${UI.t('Action')}</th>
                        <th>${UI.t('Entity')}</th>
                        <th>${UI.t('Reference')}</th>
                        <th>${UI.t('Result')}</th>
                    </tr>
                </thead>

                <tbody>
                    ${rows.map(r => `
                        <tr>
                            <td class="px-3">${UI.formatDate(r.createdAt)}</td>
                            <td>${UI.esc(r.userName)}</td>
                            <td>${UI.esc(UI.auditAction(r.action))}</td>
                            <td>${UI.esc(UI.auditEntity(r.entityName))}</td>
                            <td>${UI.esc(r.referenceNo || '-')}</td>
                            <td>
                                <span class="badge text-bg-success">
                                    ${UI.esc(UI.msg(r.result))}
                                </span>
                            </td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>

            <div class="server-footer">
                <span>${UI.endpoint('AuditLogs')}: ${total} ${UI.t('rows')}</span>
                <span>${UI.t('Page')} ${page} &middot; ${totalPageRows} ${UI.t('rows')}</span>
            </div>
        </div>
        ${pagination}`);

    Pagination.bindPageSize('auditPageSizeSelect',(p, s) => loadAuditLog( p, s));
}

function statusOptions(){
  return [{ id:'', text:UI.t('All') }, { id:'true', text:UI.t('Active') }, { id:'false', text:UI.t('Inactive') }];
}

function boolFilter(value){
  return value === '' || value == null ? null : value;
}

function masterEndpoint(entity){
  if(entity === 'categories') return 'Category';
  if(entity === 'parties') return 'ExternalParty';
  return 'Item';
}

function rowActions(kind, id, active){
  const hardDelete = isAdmin() ? `<button class="btn btn-outline-danger btn-${kind === 'structure' ? 'delete-structure' : kind === 'user' ? 'delete-user' : 'delete-master'}" title="${UI.t('Hard Delete')}" data-id="${UI.esc(id)}"><i class="bi bi-trash"></i></button>` : '';
  return `<div class="btn-group btn-group-sm"><button class="btn btn-light btn-edit-${kind}" title="${UI.t('Edit')}" data-id="${UI.esc(id)}"><i class="bi bi-pencil"></i></button><button class="btn btn-outline-secondary btn-toggle-${kind}" title="${active ? UI.t('Soft Delete') : UI.t('Restore')}" data-id="${UI.esc(id)}" data-active="${active}"><i class="bi ${active ? 'bi-slash-circle' : 'bi-arrow-counterclockwise'}"></i></button>${hardDelete}</div>`;
}

function activeCheck(active){
  return `<label class="form-check my-2"><input class="form-check-input" type="checkbox" name="isActive" ${active ? 'checked' : ''}> ${UI.t('Active')}</label>`;
}

function saveButton(id, rowId){
    return `<div class="col-md-12 d-flex justify-content-center"><button class="btn btn-primary w-25 mt-2" id="${id}" data-id="${UI.esc(rowId || '')}">${UI.t('Save')}</button></div>`;
}

async function afterSave(result, route){
  UI.toast(result.success ? UI.t('Saved') : UI.resultError(result));
  if(!result.success) return;
  $('#drawer').removeClass('open');
  await loadLookups();
  Router.go(route);
}

async function afterMasterSave(result){
  UI.toast(result.success ? UI.t('Saved') : UI.resultError(result));
  if(!result.success) return;
  $('#drawer').removeClass('open');
  await loadLookups();
  await refreshMasterSummary();
  await loadMasterData();
}

async function afterStructureSave(result){
  UI.toast(result.success ? UI.t('Saved') : UI.resultError(result));
  if(!result.success) return;
  $('#drawer').removeClass('open');
  await loadLookups();
  if(result.data && result.data.warehouseId){
    $('#app [name="warehouseId"]').val(String(result.data.warehouseId));
  }
  await loadWarehouseStructure();
}

function checkboxList(name, options, selected){
  const values = (selected || []).map(String);
  return `<div class="border rounded p-2" style="max-height: 180px; overflow:auto">${(options || []).map(x => `<label class="form-check"><input class="form-check-input" type="checkbox" name="${name}" value="${UI.esc(x.id)}" ${values.includes(String(x.id)) ? 'checked' : ''}> ${UI.esc(UI.t(x.text || x.name || x.id))}</label>`).join('')}</div>`;
}

function checkedValues(name){
  return $(`[name="${name}"]:checked`).map(function(){ return $(this).val(); }).get();
}
