window.UI = {
  esc(v) { return $('<div>').text(v ?? '').html(); },
  t(key) { return (window.AppState && AppState.resources && AppState.resources[key]) || key; },
  enum(type, value) { return this.t(`Enum.${type}.${value}`); },
  endpoint(key) { return this.t(`Endpoint.${key}`); },

  auditAction(action) {
    const key = String(action || '');
    return this.t(key.startsWith('AuditAction.') ? key : `AuditAction.${key}`);
  },

  auditEntity(entity) {
    const key = String(entity || '');
    return this.t(key.startsWith('AuditEntity.') ? key : `AuditEntity.${key}`);
  },

  msg(message) {
    const text = String(message || 'Request failed.');
    if (text.includes('; ')) {
      return text.split(';').map(x => x.trim()).filter(Boolean).map(x => this.msg(x)).join('; ');
    }
    const exact = this.t(text);
    if (exact !== text) return exact;

    const patterns = [
      [/^Target bin (.+) already contains another active item\.$/, 'Target bin {0} already contains another active item.'],
      [/^Target bin (.+) is occupied by item (.+) that is not moved in this document\.$/, 'Target bin {0} is occupied by item {1} that is not moved in this document.'],
      [/^Target bin (.+) is already used in another line\.$/, 'Target bin {0} is already used in another line.'],
      [/^Target bin (.+) is invalid\.$/, 'Target bin {0} is invalid.'],
      [/^Actual bin (.+) is invalid for warehouse (.+)\.$/, 'Actual bin {0} is invalid for warehouse {1}.'],
      [/^Bin (.+) already contains another active item\.$/, 'Bin {0} already contains another active item.'],
      [/^Bin (.+) is already used in another inbound line\.$/, 'Bin {0} is already used in another inbound line.'],
      [/^Bin (.+) is invalid for warehouse (.+)\.$/, 'Bin {0} is invalid for warehouse {1}.'],
      [/^Serial (.+) is duplicated in this inbound document\.$/, 'Serial {0} is duplicated in this inbound document.'],
      [/^Serial (.+) already exists\.$/, 'Serial {0} already exists.'],
      [/^SerialNumber (.+) is duplicated in this import file\.$/, 'SerialNumber {0} is duplicated in this import file.'],
      [/^SerialNumber (.+) already exists\.$/, 'SerialNumber {0} already exists.'],
      [/^Barcode (.+) is duplicated in this inbound document\.$/, 'Barcode {0} is duplicated in this inbound document.'],
      [/^Barcode (.+) already exists\.$/, 'Barcode {0} already exists.'],
      [/^Barcode (.+) is duplicated in this import file\.$/, 'Barcode {0} is duplicated in this import file.'],
      [/^BinCode (.+) already contains another active item\.$/, 'BinCode {0} already contains another active item.'],
      [/^Item (.+) is invalid\.$/, 'Item {0} is invalid.'],
      [/^Item (.+) requires serial number\.$/, 'Item {0} requires serial number.'],
      [/^Item instance (.+) not found\.$/, 'Item instance {0} not found.'],
      [/^Item instance (.+) is not InStock\.$/, 'Item instance {0} is not InStock.'],
      [/^Item instance (.+) cannot be sent to repair\.$/, 'Item instance {0} cannot be sent to repair.'],
      [/^Item instance (.+) is not repairing\.$/, 'Item instance {0} is not repairing.'],
      [/^Item instance (.+)\/(.+) is not repairing\.$/, 'Item instance {0}/{1} is not repairing.'],
      [/^Item instance (.+) cannot be lent\.$/, 'Item instance {0} cannot be lent.'],
      [/^Item instance (.+) is not lent out\.$/, 'Item instance {0} is not lent out.'],
      [/^Item instance (.+)\/(.+) is not lent out\.$/, 'Item instance {0}/{1} is not lent out.'],
      [/^Item instance (.+) is already used in another line\.$/, 'Item instance {0} is already used in another line.'],
      [/^Item instance (.+)\/(.+) is already used in another line\.$/, 'Item instance {0}/{1} is already used in another line.'],
      [/^Item instance (.+)\/(.+) is not part of this repair document\.$/, 'Item instance {0}/{1} is not part of this repair document.'],
      [/^Item instance (.+)\/(.+) does not belong to selected warehouse\.$/, 'Item instance {0}/{1} does not belong to selected warehouse.'],
      [/^Item instance (.+) is not located in a warehouse bin\.$/, 'Item instance {0} is not located in a warehouse bin.'],
      [/^Item instance (.+)\/(.+) is not located in a warehouse bin\.$/, 'Item instance {0}/{1} is not located in a warehouse bin.'],
      [/^Item (.+) is not included in borrow document (.+)\.$/, 'Item {0} is not included in borrow document {1}.'],

      [/^Permission denied for item instance (.+)\.$/, 'Permission denied for item instance {0}.'],
      [/^Permission denied for item instance (.+)\/(.+)\.$/, 'Permission denied for item instance {0}/{1}.'],
      [/^Permission denied for item (.+)\/(.+)\.$/, 'Permission denied for item {0}/{1}.'],
      [/^Bin code (.+) already exists in warehouse (.+)\.$/, 'Bin code {0} already exists in warehouse {1}.'],
      [/^Import confirmed. Rows: (.+)\.$/, 'Import confirmed. Rows: {0}'],
      [/^ItemCode (.+) already exists in the system\.$/, 'ItemCode {0} already exists in the system.'],
      [/^Item type (.+) not found. Cannot create extra item\.$/, 'Item type {line.ItemCode} not found. Cannot create extra item.'],
      [/^Item (.+) with serial(.+) not found\.$/, 'Item {0} with serial {1} not found.'],
      [/^BinCode (.+) does not belong to warehouse (.+)\.$/, 'BinCode {0} does not belong to warehouse {1}.'],
      [/^Item (.+)\/(.+) cannot be sent to repair (status: (.+)).\.$/,  (match, p1, p2, p3) =>`Item ${p1}/${p2} cannot be sent to repair (status: ${this.t(p3)}).`],
      [/^Item (.+)\/(.+) cannot be lent (status: (.+)).\.$/, (match, p1, p2, p3) => `Item ${p1}/${p2} cannot be lent (status: ${this.t(p3)}).`],
      [/^Found at (.+) instead of expected location\.$/, `Found at { 0} instead of expected location.`],
      [/^Extra item found at (.+)\.$/, `Extra item found at {0}.`],
      [/^Warehouse (.+) not found\.$/, `Warehouse {0} not found.`],
      [/^Item (.+) not found\.$/, `Item {0} not found.`],
      [/^Target bin (.+) not found\.$/, `Target bin {0} not found.`],
      [/^BinCode (.+) not found\.$/, `BinCode {0} not found.`],
      [/^Serial (.+) already exists for item (.+)\.$/, `Serial {0} already exists for item {1}.`],
      [/^DocumentNo (.+) already exists\.$/, 'DocumentNo {0} already exists.'],
      [/^Insufficient quantity for item (.+) SN (.+)\.$/, 'Insufficient quantity for item {0} SN {1}.'],

    ];
    for (const [pattern, key] of patterns) {
      const match = text.match(pattern);
      if (match) {
        let translated = this.t(key);
        match.slice(1).forEach((value, index) => {
          translated = translated.replace(`{${index}}`, value);
        });
        return translated;
      }
    }
    const required = text.match(/^(.+) is required\.$/);
    if (required) {
      return this.t('{0} is required.').replace('{0}', this.fieldLabel(required[1]));
    }
    return text;
  },

  fieldLabel(field) {
    const map = {
      CategoryCode: 'Category Code',
      PartyCode: 'Party Code',
      PartyType: 'Type',
      ItemCode: 'Item Code',
      DefaultName: 'Default Name',
      UnitCode: 'Unit Code',
      WarehouseCode: 'Warehouse Code',
      WarehouseName: 'Warehouse Name',
      ZoneCode: 'Zone Code',
      RackCode: 'Rack Code',
      ShelfCode: 'Shelf Code',
      BinCode: 'Bin Code',
      UserName: 'User Name',
      DisplayName: 'Display Name',
      'Item Instance': 'Item Instance',
      'Target Bin': 'Target Bin',
      'Actual Bin': 'Actual Bin',
      'External Destination': 'External Destination'
    };
    return this.t(map[field] || field);
  },

  pageHeader(title, crumb, action = '') {
    const breadcrumb = String(crumb || '').split('/').map(x => this.t(x.trim())).join(' / ');
    return `<div class="page-header"><div class="page-title"><h1>${this.esc(this.t(title))}</h1><div class="breadcrumb-lite" onclick="Router.go('dashboard');" style="cursor:pointer;">${this.esc(breadcrumb)}</div></div><div>${action}</div></div>`;
  },

  badge(status) {
    const s = AppConfig.statusMeta[status] || AppConfig.statusMeta.InStock;
    return `<span class="status-badge ${s[1]}"><i class="bi ${s[0]}"></i>${this.esc(this.enum('ItemStatus', status) || status || '-')}</span>`;
  },

  badgeDocument(status) {
    const s = AppConfig.statusDocument[status] || AppConfig.statusDocument.Return;
    return `<span class="status-badge w-75 ${s[1]} d-flex justify-content-center"><i class="bi ${s[0]}"></i>${this.esc(this.t(status) || this.enum(status) || status || '-')}</span>`;
  },

  badgeTheadDocument(status) {
    const s = AppConfig.statusDocument[status] || AppConfig.statusDocument.Return;
    return `<span class="status-badge w-100 ${s[1]} d-flex justify-content-center"><i class="bi ${s[0]}"></i>${this.esc(this.t(status) || this.enum(status) || status || '-')}</span>`;
  },

  input(label, type = 'text', value = '', name = '') {
    return `<label class="form-label w-100"><span class="fw-semibold small">${this.esc(this.t(label))}</span><input name="${this.esc(name || label)}" type="${type}" class="form-control" value="${this.esc(value)}" placeholder="${this.esc(this.t(label))}" /></label>`;
  },

  inputBorrorer(label, type = 'text', value = '', name = '', placeholder = '') {
    return `<label class="form-label w-100"><span class="fw-semibold small">${this.esc(this.t(label))}</span><input name="${this.esc(name || label)}" type="${type}" class="form-control" value="${this.esc(value)}" placeholder="${this.esc(this.t(placeholder))}" /></label>`;
  },

  inputform(label, type = 'text', value = '', name = '') {
    return `<div class="col-md-6"><label class="form-label w-100"><span class="fw-semibold small">${this.esc(this.t(label))}</span><input name="${this.esc(name || label)}" type="${type}" class="form-control" value="${this.esc(value)}" placeholder="${this.esc(this.t(label))}" /></label></div>`;
  },

  //*
  // * Renders a searchable combobox replacing native <select>.
  // * The hidden input (name attr) carries the value for payload reading.
  // * CustomCombobox wires behaviour after DOM insertion.
   
  selectOption(label, name, options = [], selected = '') {
    const cbId = 'cb_' + (name || label).replace(/\W/g, '_') + '_' + Math.random().toString(36).slice(2, 7);
    const placeholder = `-- ${this.esc(this.t(label))} --`;
    const opts = (options || []).map(x => {
      const val = String(x.id ?? x.value ?? '');
      const text = x.text ?? x.name ?? val;
      return `<div class="cbo-option" data-value="${this.esc(val)}">${this.esc(text)}</div>`;
    }).join('');
    const selStr = String(selected ?? '');
    const selOpt = (options || []).find(x => String(x.id ?? x.value ?? '') === selStr);
    const selText = selOpt ? (selOpt.text ?? selOpt.name ?? selStr) : '';
    return `<label class="form-label w-100"><span class="fw-semibold small">${this.esc(this.t(label))}</span>` +
      `<div class="cbo-wrap" id="${cbId}" data-name="${this.esc(name || label)}">` +
      `<input type="text" class="form-control cbo-input" autocomplete="off" placeholder="${placeholder}" value="${this.esc(selText)}" data-name="${this.esc(name || label)}"/>` +
      `<input type="hidden" name="${this.esc(name || label)}" value="${this.esc(selStr)}" class="cbo-value" />` +
      `<div class="cbo-dropdown">${opts || '<div class="cbo-option cbo-empty">--</div>'}</div>` +
      `</div></label>`;
    },
    select(label, name, options = [], selected = '') {
        const opts = [`<option value="">-- ${this.esc(this.t(label))} --</option>`]
            .concat((options || []).map(x => {
                const id = x.id ?? x.value ?? '';
                const text = x.text ?? x.name ?? id;
                return `<option value="${this.esc(id)}" ${String(id) === String(selected) ? 'selected' : ''}>${this.esc(text)}</option>`;
            })).join('');
        return `<label class="form-label w-100"><span class="fw-semibold small">${this.esc(this.t(label))}</span><select name="${this.esc(name || label)}" class="form-select">${opts}</select></label>`;
    },

  // Alias for backward compatibility
  selectform(label, name, options = [], selected = '') {
    return this.select(label, name, options, selected);
  },

  loading(text = 'Loading...') {
    return `<div class="empty-state"><span class="spinner-border spinner-border-sm me-2"></span>${this.esc(this.t(text))}</div>`;
  },

  empty(text = 'No data') {
    return `<div class="empty-state">${this.esc(this.t(text))}</div>`;
  },

  formatDate(v) {
    if (!v) return '-';
    const d = new Date(v);
    return Number.isNaN(d.getTime()) ? this.esc(v) : d.toLocaleDateString();
  },

  resultError(result) {
    if (!result) return this.msg('Request failed.');
    if (Array.isArray(result.errors) && result.errors.length) return result.errors.map(x => this.msg(x)).join('\n');
    return this.msg(result.message || 'Request failed.');
  },

  api(url, options = {}) {
    const token = $('meta[name="request-verification-token"]').attr('content');
    const ajax = {
      url,
      method: options.method || 'GET',
      contentType: options.contentType || 'application/json',
      dataType: options.dataType || 'json'
    };
    if (token) ajax.headers = { 'RequestVerificationToken': token };
    if (options.data !== undefined) ajax.data = typeof options.data === 'string' ? options.data : JSON.stringify(options.data);
    if (options.query) ajax.url += (url.includes('?') ? '&' : '?') + $.param(options.query);
    return $.ajax(ajax);
  },

  upload(url, formData) {
    const token = $('meta[name="request-verification-token"]').attr('content');
    const ajax = {
      url,
      method: 'POST',
      data: formData,
      processData: false,
      contentType: false,
      dataType: 'json'
    };
    if (token) ajax.headers = { 'RequestVerificationToken': token };
    return $.ajax(ajax);
  },

  toast(msg) { $('#toastLite').text(msg).fadeIn(120).delay(2200).fadeOut(180); },

  confirm(title, text, summary, onConfirm) {
    $('#swalTitle').text(this.t(title));
    $('#swalText').text(this.t(text));
    $('#swalSummary').html(summary);
    $('#swalCancel').text(this.t('Cancel'));
    $('#swalConfirm').text(this.t('Confirm Save & Post'));
    $('#swalLite').css('display', 'flex');
    $('#swalConfirm').off('click').on('click', function () { $('#swalLite').hide(); onConfirm && onConfirm(); });
  },

  debounce(fn, delay) {
    let t;
    return function () { clearTimeout(t); t = setTimeout(() => fn.apply(this, arguments), delay); };
  }
};
