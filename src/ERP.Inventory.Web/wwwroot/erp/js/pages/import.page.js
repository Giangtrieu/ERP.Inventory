//const { UI } = require("winjs");

Router.register('import', async function(){
  const types = await UI.api('/Import/Types');
  $('#app').html(UI.pageHeader('Import Excel','Home / Import Excel','') +
  `<div class="row g-3">
    <div class="col-xl-3"><div class="card"><div class="card-body">${['Upload','Validate','Review','Confirm'].map((s,i)=>`<div class="step ${i===0?'active':''}"><div class="step-no">${i+1}</div>${UI.t(s)}</div>`).join('')}</div></div></div>
    <div class="col-xl-9">
      <div class="card mb-3"><div class="card-body">
        <div class="row g-3 align-items-end">
          <div class="col-md-3">${UI.select('Import Type','importType', types)}</div>
          <div class="col-md-5"><label class="form-label w-100"><span class="fw-semibold small">${UI.t('Select File')}</span><input type="file" id="importFile" class="form-control" accept=".xlsx,.csv,.tsv" /></label></div>
          <div class="col-md-4 d-flex gap-2"><label class="form-label w-100 gap-2 d-inline-flex justify-content-between"><span class="fw-semibold small"></span><button class="btn btn-outline-secondary w-50" id="btnImportTemplate">${UI.t('Template')}</button><button class="btn btn-primary w-50" id="btnImportUpload">${UI.t('Upload File')}</button></label></div>
        </div>
      </div></div>
      <div class="card"><div class="card-body"><div class="d-flex justify-content-between mb-3"><div class="form-section-title mb-0">${UI.t('Import Batches')}</div><button class="btn btn-outline-secondary btn-sm" id="btnReloadImports"><i class="bi bi-arrow-clockwise"></i></button></div><div id="importBatches">${UI.loading()}</div><div id="importRows" class="mt-3"></div></div></div>
    </div>
  </div>`);
  $('#btnImportTemplate').on('click', () => {
    const type = $('#app [name="importType"]').val();
    window.location = `/Import/Template?importType=${encodeURIComponent(type)}`;
  });
  $('#btnImportUpload').on('click', uploadImportFile);
  $('#btnReloadImports').on('click', loadImportBatches);
  await loadImportBatches();
});

async function uploadImportFile(){
  const file = $('#importFile')[0].files[0];
  if(!file){ UI.toast(UI.t('File is required.')); return; }
  const form = new FormData();
  form.append('importType', $('#app [name="importType"]').val());
  form.append('file', file);
  const token = $('meta[name="request-verification-token"]').attr('content');
  const result = await $.ajax({ url: '/Import/Upload', method: 'POST', data: form, processData: false, contentType: false, headers: token ? { RequestVerificationToken: token } : {} });
  if(!result.success){ UI.toast(UI.resultError(result)); return; }
  UI.toast(UI.t(result.message || 'Uploaded'));
  await loadImportBatches();
  await loadImportRows(result.data);
}

async function loadImportBatches(){
  const result = await UI.api('/Import/Batches');
  if(!result.success){ $('#importBatches').html(UI.empty(UI.resultError(result))); return; }
  const rows = result.data || [];
    if (!rows.length) { $('#importBatches').html(UI.empty('No data')); return; }
    $('#importBatches').html(`<div class="table-wrap"><table class="data-table-detail "><thead><tr><th class="px-3">${UI.t('File')}</th><th>${UI.t('Type')}</th><th>${UI.t('Status')}</th><th>${UI.t('Blocking')}/${UI.t('Total')}</th><th></th></tr></thead><tbody>${rows.map(r => `<tr><td class="px-3 fw-semibold" data-id="${r.id}">${UI.esc(r.fileName)}</td><td>${UI.esc(UI.t(r.importType))}</td><td>${UI.esc(UI.enum('ImportBatchStatus', r.status))}</td><td>${r.blockingErrorRows}/${r.totalRows}</td><td><div class="btn-group btn-group-sm"><button class="btn btn-light btn-import-rows" data-id="${r.id}"><i class="bi bi-eye"></i></button><button class="btn btn-outline-primary btn-import-validate" data-id="${r.id}" ${r.status === 'Confirmed' ? 'disabled' : ''}>${UI.t('Validate')}</button><button class="btn btn-primary btn-import-confirm" data-id="${r.id}" ${r.status === 'Confirmed' ? 'disabled' : ''}>${UI.t('Confirm')}</button></div></td></tr>`).join('')}</tbody></table></div>`);
  //$('#importBatches').html(`<div class="table-wrap"><table class="data-table"><thead><tr><th class="px-3">${UI.t('Batch')}</th><th>${UI.t('Type')}</th><th>${UI.t('File')}</th><th>${UI.t('Status')}</th><th>${UI.t('Total')}</th><th>${UI.t('Blocking')}</th><th></th></tr></thead><tbody>${rows.map(r => `<tr><td class="px-3 fw-semibold">${UI.esc(r.batchNo)}</td><td>${UI.esc(UI.t(`ImportType.${r.importType}`))}</td><td>${UI.esc(r.fileName)}</td><td>${UI.esc(UI.enum('ImportBatchStatus', r.status))}</td><td>${r.totalRows}</td><td>${r.blockingErrorRows}</td><td><div class="btn-group btn-group-sm"><button class="btn btn-light btn-import-rows" data-id="${r.id}"><i class="bi bi-eye"></i></button><button class="btn btn-outline-primary btn-import-validate" data-id="${r.id}">${UI.t('Validate')}</button><button class="btn btn-primary btn-import-confirm" data-id="${r.id}">${UI.t('Confirm')}</button></div></td></tr>`).join('')}</tbody></table></div>`);
}

async function loadImportRows(id){
  const result = await UI.api(`/Import/Rows/${id}`);
  const rows = result.data || [];
    if (!rows.length) { $('#importRows').html(UI.empty('No data')); return; }
  //$('#importRows').html(`<div class="form-section-title">${UI.t('Validation Result')}</div><div class="table-wrap"><table class="data-table"><thead><tr><th class="px-3">Row</th><th>Severity</th><th>Message</th><th>Suggested Fix</th></tr></thead><tbody>${rows.map(r => `<tr><td class="px-3">${r.rowNumber}</td><td><span class="badge ${r.severity === 'Blocking' ? 'text-bg-danger' : 'text-bg-success'}">${UI.esc(UI.enum('ValidationSeverity', r.severity))}</span></td><td>${UI.esc(r.message || '-')}</td><td>${UI.esc(r.suggestedFix || '-')}</td></tr>`).join('')}</tbody></table></div>`);
  $('#importRows').html(`<div class="form-section-title">${UI.t('Validation Result')}</div><div class="table-wrap"><table class="data-table"><thead><tr><th class="px-3">${UI.t('Row')}</th><th>${UI.t('Severity')}</th><th>${UI.t('Message')}</th><th>${UI.t('Suggested Fix')}</th></tr></thead><tbody>${rows.map(r => `<tr><td class="px-3">${r.rowNumber}</td><td><span class="badge ${r.severity === 'Blocking' ? 'text-bg-danger' : 'text-bg-success'}">${UI.esc(UI.enum('ValidationSeverity', r.severity))}</span></td><td>${UI.esc(UI.msg(r.message || '-'))}</td><td>${UI.esc(UI.msg(r.suggestedFix || '-'))}</td></tr>`).join('')}</tbody></table></div>`);
}

$(document).on('click', '.btn-import-rows', function(){ loadImportRows($(this).data('id')); });
$(document).on('click', '.btn-import-validate', async function(){
  const result = await UI.api(`/Import/Validate/${$(this).data('id')}`, { method: 'POST', data: {} });
  UI.toast(UI.t(result.message || 'Validated'));
  await loadImportBatches();
  await loadImportRows($(this).data('id'));
});
$(document).on('click', '.btn-import-confirm', function () {
    const id = $(this).data('id');
    UI.confirm('Confirm Import', UI.t('Valid rows will be inserted into operational tables.'), `<div>${UI.t('Batch')}: <b>${$(`td[data-id="${id}"]`).text()}</b></div><div>${UI.t('Backend will re - validate before commit.')}</div>`, async function(){
    const result = await UI.api(`/Import/Confirm/${id}`, { method: 'POST', data: {} });
    if(!result.success){ UI.toast(UI.resultError(result)); return; }
    UI.toast(UI.t(UI.msg(`${result.message} Rows: ${result.data}.`)));
    //UI.toast(`${result.message} Rows: ${result.data}`);
    await loadLookups();
    await loadImportBatches();
    await loadImportRows(id);
  });
});
