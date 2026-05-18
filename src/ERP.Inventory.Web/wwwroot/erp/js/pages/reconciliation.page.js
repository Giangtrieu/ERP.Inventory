// reconciliation.page.js — Đối soát tài sản
// Sử dụng đúng UI.api(url, { method, data }), UI.toast(msg), UI.t(key)

Router.register('reconciliation', async function () {
  await ReconciliationPage.render();
});

window.ReconciliationPage = (() => {
  let _lists = [], _sessions = [], _currentSessionId = null;
  let _filter = { resultType: '', keyword: '', page: 1, pageSize: 50 };

  // ── Helpers ────────────────────────────────────────────────────────────────
  const t = (k) => UI.t(k) || k;
  const esc = (v) => UI.esc(v ?? '');

  function sessionStatusBadge(s) {
    const map = {
      Draft: ['secondary', t('recon.status.draft')],
      Running: ['primary', t('recon.status.running')],
      Completed: ['success', t('recon.status.completed')],
      Archived: ['dark', t('recon.status.archived')]
    };
    const [cls, label] = map[s] || ['secondary', s];
    return `<span class="badge bg-${cls}">${label}</span>`;
  }

  function resultBadge(type) {
    const map = {
      Matched: ['success', 'bi-check-circle', t('recon.matched')],
      ERPOnly: ['info',    'bi-database-check', t('recon.erponly')],
      RefOnly: ['warning', 'bi-file-earmark-x', t('recon.refonly')]
    };
    const [cls, icon, label] = map[type] || ['secondary', 'bi-question-circle', type];
    return `<span class="badge bg-${cls}"><i class="bi ${icon} me-1"></i>${label}</span>`;
  }

  // ── Render shell ───────────────────────────────────────────────────────────
  async function render() {
    const $app = $('#app');
    $app.html(
      UI.pageHeader('recon.title', `Home / recon.home`) +
      `<div class="container-fluid py-3">
        <ul class="nav nav-tabs mb-3" id="recon-tabs">
          <li class="nav-item"><a class="nav-link active" href="#" data-recon-tab="lists">${t('recon.reflist')}</a></li>
          <li class="nav-item"><a class="nav-link" href="#" data-recon-tab="sessions">${t('recon.sessions')}</a></li>
          <li class="nav-item d-none"><a class="nav-link" href="#" id="tab-results" data-recon-tab="results">${t('recon.results')}</a></li>
        </ul>
        <div id="recon-body"></div>
      </div>`
    );

    $(document).off('click.recon').on('click.recon', '[data-recon-tab]', function (e) {
      e.preventDefault();
      $('[data-recon-tab]').removeClass('active');
      $(this).addClass('active');
      const tab = $(this).data('recon-tab');
      if (tab === 'lists') renderListsTab();
      else if (tab === 'sessions') loadSessions();
      else if (tab === 'results') loadResults(_currentSessionId, _filter);
    });

    await loadLists();
  }

  // ── TAB: Reference Lists ──────────────────────────────────────────────────
  async function loadLists() {
    $('#recon-body').html(UI.loading());
    const res = await UI.api('/Reconciliation/Lists');
    _lists = res?.data || [];
    renderListsTab();
  }

  function renderListsTab() {
    const rows = _lists.length === 0
      ? `<tr><td colspan="6" class="text-center text-muted py-3">${t('recon.nolists')}</td></tr>`
      : _lists.map(l => `<tr>
          <td><strong>${esc(l.listCode)}</strong></td>
          <td>${esc(l.name)}</td>
          <td>${esc(l.description || '-')}</td>
          <td><span class="badge bg-secondary">${esc(l.warehouseName)}</span></td>
          <td class="text-center">${l.itemCount}</td>
          <td>
            <button class="btn btn-xs btn-outline-primary me-1" id="btn-view-list-${l.id}" data-list-id="${l.id}" title="${t('recon.reflist')}">
              <i class="bi bi-eye"></i>
            </button>
            <button class="btn btn-xs btn-outline-success" id="btn-import-${l.id}" data-list-id="${l.id}" title="${t('recon.import')}">
              <i class="bi bi-upload"></i>
            </button>
          </td>
        </tr>`).join('');

    $('#recon-body').html(`
      <div class="card shadow-sm">
        <div class="card-header d-flex justify-content-between align-items-center">
          <span class="fw-semibold"><i class="bi bi-list-ul me-2"></i>${t('recon.reflist')}</span>
          <button class="btn btn-sm btn-primary" id="btn-new-list">
            <i class="bi bi-plus-lg me-1"></i>${t('recon.newlist')}
          </button>
        </div>
        <div class="card-body p-0">
          <table class="table table-hover mb-0">
            <thead class="table-light"><tr>
              <th>${t('recon.listcode')}</th>
              <th>${t('recon.listname')}</th>
              <th>${t('recon.description')}</th>
              <th>${t('Warehouse')}</th>
              <th class="text-center">${t('recon.itemcount')}</th>
              <th></th>
            </tr></thead>
            <tbody>${rows}</tbody>
          </table>
        </div>
      </div>`);

    $('#btn-new-list').on('click', showNewListModal);
    $('[data-list-id]').each(function () {
      const id = parseInt($(this).data('list-id'));
      if ($(this).attr('id').startsWith('btn-view-list'))
        $(this).on('click', () => openListDetail(id));
      else
        $(this).on('click', () => openImportModal(id));
    });
  }

  function showNewListModal() {
    const whOptions = (AppState.lookups?.warehouses || [])
      .map(w => `<option value="${w.id}">${esc(w.name)}</option>`).join('');

    showModal({
      title: t('recon.newlist'),
      body: `
        <div class="mb-3"><label class="form-label fw-semibold">${t('recon.listcode')} *</label>
          <input class="form-control" id="nl-code" placeholder="VD: REF-WH01-2026"></div>
        <div class="mb-3"><label class="form-label fw-semibold">${t('recon.listname')} *</label>
          <input class="form-control" id="nl-name"></div>
        <div class="mb-3"><label class="form-label fw-semibold">${t('recon.description')}</label>
          <input class="form-control" id="nl-desc"></div>
        <div class="mb-3"><label class="form-label fw-semibold">${UI.select('Warehouse', 'warehouseId', AppState.lookups.warehouses)} *</label>`,
      confirmText: t('Save'),
      onConfirm: async () => {
        const res = await UI.api('/Reconciliation/CreateList', {
          method: 'POST',
          data: JSON.stringify({
            listCode: $('#nl-code').val()?.trim(),
            name: $('#nl-name').val()?.trim(),
            description: $('#nl-desc').val()?.trim(),
            warehouseId: parseInt($('[name="warehouseId"]').val()) || 0
          })
        });
        if (res?.success) { UI.toast(res.message); hideModal(); loadLists(); }
        else UI.toast(res?.message || 'Failed');
      }
    });
  }

  async function openListDetail(listId) {
    const list = _lists.find(l => l.id === listId);
    const res = await UI.api(`/Reconciliation/ListItems/${listId}`);
    const items = res?.data || [];
    const rows = items.length === 0
      ? `<tr><td colspan="5" class="text-center text-muted">${t('recon.noitems')}</td></tr>`
      : items.map(i => `<tr>
          <td>${esc(i.itemCode)}</td>
          <td>${esc(i.serialNumber || '-')}</td>
          <td class="text-muted small">${esc(i.resolvedItemName || '-')}</td>
          <td>${i.isResolvedInERP ? '<span class="badge bg-success">✓</span>' : '<span class="badge bg-warning text-dark">?</span>'}</td>
          <td class="text-muted small">${esc(i.note || '')}</td>
        </tr>`).join('');

    showModal({
      size: 'xl',
      title: `${t('recon.reflist')} — ${list?.listCode || listId}`,
      body: `
        <div class="d-flex justify-content-between mb-2">
          <span class="text-muted small">${items.length} ${t('recon.itemcount')}</span>
          <div class="d-flex gap-2">
              <a href="${window.AppPathBase || ''}/Reconciliation/Template" class="btn btn-xs btn-outline-secondary" target="_blank">
              <i class="bi bi-download me-1"></i>${t('recon.template')}
            </a>
            <button class="btn btn-xs btn-outline-success" id="btn-import-detail">
              <i class="bi bi-upload me-1"></i>${t('recon.import')}
            </button>
            <button class="btn btn-xs btn-primary" id="btn-run-from-list">
              <i class="bi bi-play-fill me-1"></i>${t('recon.runaudit')}
            </button>
          </div>
        </div>
        <div style="max-height:420px;overflow-y:auto">
        <table class="table table-sm table-hover">
          <thead class="table-light sticky-top"><tr>
            <th>ItemCode</th><th>SerialNumber</th>
            <th>${t('Name')}</th><th>${t('recon.resolved')}</th><th>${t('Note')}</th>
          </tr></thead>
          <tbody>${rows}</tbody>
        </table></div>`,
      confirmText: null,
      onShown: () => {
        $('#btn-import-detail').on('click', () => { hideModal(); openImportModal(listId); });
        $('#btn-run-from-list').on('click', () => { hideModal(); createAndRunSession(listId); });
      }
    });
  }

  function openImportModal(listId) {
    showModal({
      title: t('recon.import'),
      body: `
        <div class="alert alert-info small mb-3">
          <i class="bi bi-info-circle me-1"></i>${t('recon.importinfo')}<br>
           <a href="${window.AppPathBase || ''}/Reconciliation/Template" target="_blank">📥 ${t('recon.template')}</a>
        </div>
        <div class="mb-3">
          <label class="form-label fw-semibold">${t('recon.importmode')} *</label>
          <select class="form-select" id="imp-mode">
            <option value="Supplement">${t('recon.supplement')}</option>
            <option value="Replace">${t('recon.replace')}</option>
          </select>
        </div>
        <div class="mb-3">
          <label class="form-label fw-semibold">Excel (.xlsx / .csv) *</label>
          <input type="file" class="form-control" id="imp-file" accept=".xlsx,.csv">
        </div>`,
      confirmText: t('Upload'),
      onConfirm: async () => {
        const file = document.getElementById('imp-file')?.files?.[0];
        const mode = $('#imp-mode').val();
        if (!file) { UI.toast(t('File is required.')); return false; }
        const fd = new FormData();
        fd.append('listId', listId);
        fd.append('importMode', mode);
        fd.append('file', file);
        const res = await UI.upload('/Reconciliation/ImportList', fd);
        if (res?.success) {
          const d = res.data;
          UI.toast(`✓ ${d.inserted} inserted, ${d.updated} updated, ${d.unresolvedInERP} unresolved`);
          hideModal(); loadLists();
        } else UI.toast(res?.message || 'Import failed');
      }
    });
  }

  // ── TAB: Sessions ─────────────────────────────────────────────────────────
  async function loadSessions() {
    $('#recon-body').html(UI.loading());
    const res = await UI.api('/Reconciliation/Sessions');
    _sessions = res?.data || [];
    renderSessionsTab();
  }

    window.ExportSession = async function (sessionId) {
        const params = $.param({ resultType: $('#filter-type').val(), keyword: $('#filter-kw').val() });
       /* window.location = `/Reconciliation/ExportSession/${sessionId}?${params}`;*/
        window.location = `${UI.resolveUrl(`/Reconciliation/ExportSession/${sessionId}`)}?${params}`;
        //const res = await UI.api(`/Reconciliation/ExportSession/${sessionId}?${params}`);
  }

  function renderSessionsTab() {
    const rows = _sessions.length === 0
      ? `<tr><td colspan="9" class="text-center text-muted py-3">${t('recon.nosessions')}</td></tr>`
      : _sessions.map(s => `<tr>
          <td><strong>${esc(s.sessionNo)}</strong></td>
          <td>${esc(s.referenceListName)}</td>
          <td><span class="badge bg-secondary">${esc(s.warehouseName)}</span></td>
          <td>${sessionStatusBadge(s.sessionStatus)}</td>
          <td class="text-center"><span class="badge bg-success">${s.matchedCount}</span></td>
          <td class="text-center"><span class="badge bg-info">${s.erpOnlyCount}</span></td>
          <td class="text-center"><span class="badge bg-warning text-dark">${s.refOnlyCount}</span></td>
          <td class="text-muted small">${s.completedAt ? UI.formatDate(s.completedAt) : '-'}</td>
          <td class="text-nowrap">
            ${s.sessionStatus === 'Draft' ? `<button class="btn btn-xs btn-success me-1 btn-run-session" data-id="${s.id}" title="${t('recon.runaudit')}"><i class="bi bi-play-fill"></i></button>` : ''}
            ${s.sessionStatus === 'Completed' ? `
              <button class="btn btn-xs btn-outline-primary me-1 btn-view-results" data-id="${s.id}" title="${t('recon.results')}"><i class="bi bi-eye"></i></button>
              <button onclick="ExportSession(${s.id})" class="btn btn-xs btn-outline-success" title="${t('recon.export')}"><i class="bi bi-download"></i></button>` : ''}
          </td>
        </tr>`).join('');

    $('#recon-body').html(`
      <div class="card shadow-sm">
        <div class="card-header d-flex justify-content-between align-items-center">
          <span class="fw-semibold"><i class="bi bi-play-circle me-2"></i>${t('recon.sessions')}</span>
          <button class="btn btn-sm btn-primary" id="btn-new-session">
            <i class="bi bi-plus-lg me-1"></i>${t('recon.newsession')}
          </button>
        </div>
        <div class="card-body p-0">
          <table class="table table-hover mb-0">
            <thead class="table-light"><tr>
              <th>${t('recon.sessionno')}</th>
              <th>${t('recon.reflist')}</th>
              <th>${t('Warehouse')}</th>
              <th>${t('Status')}</th>
              <th class="text-center">${t('recon.matched')}</th>
              <th class="text-center">${t('recon.erponly')}</th>
              <th class="text-center">${t('recon.refonly')}</th>
              <th>${t('Updated At')}</th>
              <th></th>
            </tr></thead>
            <tbody>${rows}</tbody>
          </table>
        </div>
      </div>`);

    $('#btn-new-session').on('click', showNewSessionModal);
    $('.btn-run-session').on('click', function () { runSession(parseInt($(this).data('id'))); });
    $('.btn-view-results').on('click', function () { viewResults(parseInt($(this).data('id'))); });
  }

  function showNewSessionModal() {
    if (!_lists.length) { UI.toast(t('recon.nolists')); return; }
    const opts = _lists.map(l => `<option value="${l.id}">${esc(l.listCode)} — ${esc(l.warehouseName)}</option>`).join('');
    showModal({
      title: t('recon.newsession'),
      body: `
        <div class="mb-3"><label class="form-label fw-semibold">${t('recon.reflist')} *</label>
          <select class="form-select" id="ns-list">${opts}</select></div>
        <div class="mb-3"><label class="form-label fw-semibold">${t('Note')}</label>
          <input class="form-control" id="ns-note"></div>`,
      confirmText: t('Confirm'),
      onConfirm: async () => {
        const res = await UI.api('/Reconciliation/CreateSession', {
          method: 'POST',
          data: JSON.stringify({
            referenceListId: parseInt($('#ns-list').val()) || 0,
            note: $('#ns-note').val()?.trim()
          })
        });
        if (res?.success) { UI.toast(res.message); hideModal(); loadSessions(); }
        else UI.toast(res?.message || 'Failed');
      }
    });
  }

  async function runSession(id) {
    if (!confirm(t('recon.confirmrun'))) return;
    UI.toast(t('recon.running'));
    const res = await UI.api(`/Reconciliation/RunSession/${id}`, { method: 'POST', data: '{}' });
    if (res?.success) { UI.toast(res.message); loadSessions(); }
    else UI.toast(res?.message || 'Failed');
  }

  async function createAndRunSession(listId) {
    UI.toast(t('recon.running'));
    const create = await UI.api('/Reconciliation/CreateSession', {
      method: 'POST', data: JSON.stringify({ referenceListId: listId, note: '' })
    });
    if (!create?.success) { UI.toast(create?.message || 'Failed'); return; }
    const sessionId = create.data?.id;
    const run = await UI.api(`/Reconciliation/RunSession/${sessionId}`, { method: 'POST', data: '{}' });
    if (run?.success) { UI.toast(run.message); viewResults(sessionId); }
    else UI.toast(run?.message || 'Failed');
  }

  // ── TAB: Results ──────────────────────────────────────────────────────────
  function viewResults(id) {
    _currentSessionId = id;
    _filter = { resultType: '', keyword: '', page: 1, pageSize: 50 };
    $('#tab-results').closest('li').removeClass('d-none');
    $('[data-recon-tab]').removeClass('active');
    $('#tab-results').addClass('active');
    loadResults(id, _filter);
  }

  async function loadResults(sessionId, filter) {
    if (!sessionId) return;
    $('#recon-body').html(UI.loading());
    const q = $.param({ resultType: filter.resultType, keyword: filter.keyword, page: filter.page, pageSize: filter.pageSize });
    const res = await UI.api(`/Reconciliation/SessionResults/${sessionId}?${q}`);
    if (res?.success) renderResultsTab(res.data);
    else $('#recon-body').html(`<div class="alert alert-danger">${esc(res?.message)}</div>`);
  }

  function renderResultsTab(data) {
    const s = data.session;
    const resultRows = data.results.length === 0
      ? `<tr><td colspan="7" class="text-center text-muted py-3">${t('recon.noresults')}</td></tr>`
      : data.results.map(r => `<tr>
          <td>${esc(r.itemCode)}</td>
          <td>${esc(r.serialNumber || '-')}</td>
         <!-- <td class="text-muted small">${esc(r.resolvedItemName || '-')}</td> -->
          <td>${resultBadge(r.resultType)}</td>
          <td>${r.erpStatus ? `<span class="badge ${statusCls(r.erpStatus)}">${esc(t(r.erpStatus))}</span>` : '-'}</td>
          <td class="small text-muted">${esc(r.erpLocationText || '-')}</td>
          <!--<td>${r.note ? `<span class="badge bg-secondary">${esc(r.note)}</span>` : ''}</td>-->
        </tr>`).join('');

    $('#recon-body').html(`
      <div class="row g-3 mb-3">
        <div class="col-auto">
          <div class="card border-0 bg-success text-white px-4 py-2 text-center shadow-sm" style="min-width:200px">
            <div class="fs-3 fw-bold">${s.matchedCount}</div>
            <div class="small">${t('recon.matched')}</div>
          </div>
        </div>
        <div class="col-auto">
          <div class="card border-0 bg-info text-white px-4 py-2 text-center shadow-sm" style="min-width:200px">
            <div class="fs-3 fw-bold">${s.erpOnlyCount}</div>
            <div class="small">${t('recon.erponly')}</div>
          </div>
        </div>
        <div class="col-auto">
          <div class="card border-0 bg-warning px-4 py-2 text-center shadow-sm" style="min-width:200px">
            <div class="fs-3 fw-bold">${s.refOnlyCount}</div>
            <div class="small">${t('recon.refonly')}</div>
          </div>
        </div>
        <div class="col-auto ms-auto d-flex align-items-center">
          <button onclick="ExportSession(${s.id})" class="btn btn-sm btn-outline-success">
            <i class="bi bi-download me-1"></i>${t('recon.export')}
          </button>
        </div>
      </div>
      <div class="card shadow-sm">
        <div class="card-header">
          <div class="row g-2 align-items-center">
            <div class="col-auto">
              <select class="form-select form-select-sm" id="filter-type" style="min-width:150px">
                <option value="">${t('recon.allresults')}</option>
                <option value="Matched">${t('recon.matched')}</option>
                <option value="ERPOnly">${t('recon.erponly')}</option>
                <option value="RefOnly">${t('recon.refonly')}</option>
              </select>
            </div>
            <div class="col-auto">
              <input class="form-control form-control-sm" id="filter-kw"
                placeholder="ItemCode / Serial..." style="min-width:200px"
                value="${esc(_filter.keyword)}">
            </div>
            <div class="col-auto">
              <button class="btn btn-sm btn-primary" id="btn-filter">
                <i class="bi bi-search me-1"></i>${t('recon.filter')}
              </button>
            </div>
          </div>
        </div>
        <div class="card-body p-0">
          <table class="data-table-detail">
            <thead class="table-light"><tr>
              <th>PN</th><th>SN</th>
              <th>${t('Result')}</th>
              <th>${t('Condition')}</th><th>${t('Bin')}</th>
            </tr></thead>
            <tbody>${resultRows}</tbody>
          </table>
        </div>
        <div class="card-footer d-flex justify-content-between align-items-center">
          <span class="text-muted small">${t('recon.total')}: ${data.totalCount}</span>
          <div class="d-flex gap-2">
            ${data.page > 1 ? `<button class="btn btn-xs btn-outline-secondary" id="btn-prev">← ${t('Page')}</button>` : ''}
            ${data.page * data.pageSize < data.totalCount ? `<button class="btn btn-xs btn-outline-secondary" id="btn-next">${t('Page')} →</button>` : ''}
          </div>
        </div>
      </div>`);

    $('#filter-type').val(_filter.resultType);
    $('#btn-filter').on('click', () => {
      _filter.resultType = $('#filter-type').val();
      _filter.keyword = $('#filter-kw').val();
      _filter.page = 1;
      loadResults(_currentSessionId, _filter);
    });
    $('#btn-prev').on('click', () => { _filter.page--; loadResults(_currentSessionId, _filter); });
    $('#btn-next').on('click', () => { _filter.page++; loadResults(_currentSessionId, _filter); });
  }


  function statusCls(s) {
    const m = AppConfig?.statusMeta?.[s];
    return m ? m[1] : 'bg-secondary';
  }

  // ── Modal helper (Bootstrap 5) ────────────────────────────────────────────


  function showModal({ title, body, confirmText, size, onConfirm, onShown }) {
    const id = 'reconModal';
    if (!document.getElementById(id)) {
      $('body').append(`
        <div class="modal fade" id="${id}" tabindex="-1" aria-hidden="true">
          <div class="modal-dialog modal-dialog-scrollable" id="${id}-dialog">
            <div class="modal-content">
              <div class="modal-header">
                <h5 class="modal-title fw-bold" id="${id}-title"></h5>
                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
              </div>
              <div class="modal-body" id="${id}-body"></div>
              <div class="modal-footer" id="${id}-footer">
                <button type="button" class="btn btn-light" data-bs-dismiss="modal">${t('Cancel')}</button>
                <button type="button" class="btn btn-primary" id="${id}-confirm"></button>
              </div>
            </div>
          </div>
        </div>`);
    }

    $(`#${id}-title`).text(title);
    $(`#${id}-body`).html(body);
    $(`#${id}-dialog`).attr('class', `modal-dialog modal-dialog-scrollable${size ? ' modal-' + size : ''}`);

    const $footer = $(`#${id}-footer`);
    const $confirm = $(`#${id}-confirm`);
    if (confirmText) {
      $confirm.text(confirmText).show();
      $confirm.off('click').on('click', async () => {
        const result = onConfirm && await onConfirm();
        if (result !== false && !result) hideModal();
      });
      $footer.show();
    } else {
      $confirm.hide();
      $footer.hide();
    }

    const el = document.getElementById(id);
    if (onShown) $(el).one('shown.bs.modal', onShown);
    $(el).modal('show');
  }

  function hideModal() {
    $('#reconModal').modal('hide');
  }

  return { render, viewResults };
})();
