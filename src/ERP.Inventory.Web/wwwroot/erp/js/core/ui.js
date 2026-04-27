window.UI = {
  esc(v) { return $('<div>').text(v ?? '').html(); },
    t(key) {
        console.log(key)
        return (window.AppState && AppState.resources && AppState.resources[key]) || key;
    },
  enum(type, value){ return this.t(`Enum.${type}.${value}`); },
  endpoint(key){ return this.t(`Endpoint.${key}`); },
  auditAction(action){
    const key = String(action || '');
    return this.t(key.startsWith('AuditAction.') ? key : `AuditAction.${key}`);
  },
  auditEntity(entity){
    const key = String(entity || '');
    return this.t(key.startsWith('AuditEntity.') ? key : `AuditEntity.${key}`);
  },
  msg(message){
    const text = String(message || 'Request failed.');
    if(text.includes('; ')){
      return text.split(';').map(x => x.trim()).filter(Boolean).map(x => this.msg(x)).join('; ');
    }
    const exact = this.t(text);
    if(exact !== text) return exact;
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
      [/^Item instance (.+) cannot be lent\.$/, 'Item instance {0} cannot be lent.'],
      [/^Item instance (.+) is not lent out\.$/, 'Item instance {0} is not lent out.'],
      [/^Item instance (.+) is already used in another line\.$/, 'Item instance {0} is already used in another line.'],
      [/^Item instance (.+) is not part of this repair document\.$/, 'Item instance {0} is not part of this repair document.'],
      [/^Item instance (.+) does not belong to selected warehouse\.$/, 'Item instance {0} does not belong to selected warehouse.'],
      [/^Item instance (.+) is not located in a warehouse bin\.$/, 'Item instance {0} is not located in a warehouse bin.'],
      [/^Permission denied for item instance (.+)\.$/, 'Permission denied for item instance {0}.']
    ];
    for(const [pattern, key] of patterns){
      const match = text.match(pattern);
      if(match){
        let translated = this.t(key);
        match.slice(1).forEach((value, index) => {
          translated = translated.replace(`{${index}}`, value);
        });
        return translated;
      }
    }
    const required = text.match(/^(.+) is required\.$/);
    if(required){
      return this.t('{0} is required.').replace('{0}', this.fieldLabel(required[1]));
    }
    return text;
  },
  fieldLabel(field){
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
  //optionAttrs(option){
  //  const skip = new Set(['id', 'value', 'text', 'name']);
  //  return Object.keys(option || {})
  //    .filter(k => !skip.has(k) && option[k] !== null && option[k] !== undefined)
  //    .map(k => {
  //      const attr = k.replace(/[A-Z]/g, m => '-' + m.toLowerCase());
  //      return ` data-${this.esc(attr)}="${this.esc(option[k])}"`;
  //    }).join('');
  //},
  pageHeader(title, crumb, action=''){
    const breadcrumb = String(crumb || '').split('/').map(x => this.t(x.trim())).join(' / ');
    return `<div class="page-header"><div class="page-title"><h1>${this.esc(this.t(title))}</h1><div class="breadcrumb-lite">${this.esc(breadcrumb)}</div></div><div>${action}</div></div>`;
  },
  badge(status){
    const s = AppConfig.statusMeta[status] || AppConfig.statusMeta.InStock;
    return `<span class="status-badge ${s[1]}"><i class="bi ${s[0]}"></i>${this.esc(this.enum('ItemStatus', status) || status || '-')}</span>`;
  },
  input(label, type='text', value='', name=''){
    return `<label class="form-label w-100"><span class="fw-semibold small">${this.esc(this.t(label))}</span><input name="${this.esc(name || label)}" type="${type}" class="form-control" value="${this.esc(value)}" placeholder="${this.esc(this.t(label))}" /></label>`;
  },
  select(label, name, options=[], selected=''){
    const opts = [`<option value="">-- ${this.esc(this.t(label))} --</option>`]
      .concat((options || []).map(x => {
        const id = x.id ?? x.value ?? '';
          const text = x.text ?? x.name ?? id;
        return `<option value="${this.esc(id)}" ${String(id) === String(selected) ? 'selected' : ''}>${this.esc(text)}</option>`;
        /*return `<option value="${this.esc(id)}" data-base-text="${this.esc(text)}"${this.optionAttrs(x)} ${String(id) === String(selected) ? 'selected' : ''}>${this.esc(text)}</option>`;*/
      })).join('');
      return `<label class="form-label w-100"><span class="fw-semibold small">${this.esc(this.t(label))}</span><select name="${this.esc(name || label)}" class="form-select">${opts}</select></label>`;
    //return `<label class="form-label w-100 searchable-select-wrap"><span class="fw-semibold small">${this.esc(this.t(label))}</span><input type="search" class="form-control form-control-sm select-search" placeholder="${this.esc(this.t('Search options'))}" autocomplete="off" /><select name="${this.esc(name || label)}" class="form-select searchable-select">${opts}</select></label>`;
  },
  loading(text='Loading...'){ return `<div class="empty-state"><span class="spinner-border spinner-border-sm me-2"></span>${this.esc(this.t(text))}</div>`; },
  empty(text='No data'){ return `<div class="empty-state">${this.esc(this.t(text))}</div>`; },
  formatDate(v){
    if(!v) return '-';
    const d = new Date(v);
    return Number.isNaN(d.getTime()) ? this.esc(v) : d.toLocaleString();
  },
  resultError(result){
    if(!result) return this.msg('Request failed.');
    if(Array.isArray(result.errors) && result.errors.length) return result.errors.map(x => this.msg(x)).join('\n');
    return this.msg(result.message || 'Request failed.');
  },
  api(url, options={}){
    const token = $('meta[name="request-verification-token"]').attr('content');
    const ajax = {
      url,
      method: options.method || 'GET',
      contentType: options.contentType || 'application/json',
      dataType: options.dataType || 'json'
    };
    if(token) ajax.headers = { 'RequestVerificationToken': token };
    if(options.data !== undefined) ajax.data = typeof options.data === 'string' ? options.data : JSON.stringify(options.data);
    if(options.query) ajax.url += (url.includes('?') ? '&' : '?') + $.param(options.query);
    return $.ajax(ajax);
  },
  upload(url, formData){
    const token = $('meta[name="request-verification-token"]').attr('content');
    const ajax = {
      url,
      method: 'POST',
      data: formData,
      processData: false,
      contentType: false,
      dataType: 'json'
    };
    if(token) ajax.headers = { 'RequestVerificationToken': token };
    return $.ajax(ajax);
  },
  toast(msg){ $('#toastLite').text(msg).fadeIn(120).delay(2200).fadeOut(180); },
  confirm(title, text, summary, onConfirm){
    $('#swalTitle').text(this.t(title));
    $('#swalText').text(this.t(text));
    $('#swalSummary').html(summary);
    $('#swalCancel').text(this.t('Cancel'));
    $('#swalConfirm').text(this.t('Confirm Save & Post'));
    $('#swalLite').css('display','flex');
    $('#swalConfirm').off('click').on('click', function(){ $('#swalLite').hide(); onConfirm && onConfirm(); });
  },
  debounce(fn, delay){ let t; return function(){ clearTimeout(t); t = setTimeout(() => fn.apply(this, arguments), delay); }; }
};

//$(document).ready(function () {
//    $('select').select2({ width: '100%', placeholder: 'Search...', allowClear: true });
//});

//$(document).on('input', '.select-search', function(){
//  const wrapper = $(this).closest('.searchable-select-wrap, .inline-search-select');
//  const select = wrapper.find('select').first();
//  const keyword = String($(this).val() || '').trim().toLowerCase();
//  select.find('option').each(function(){
//    const option = $(this);
//    if(!option.attr('value')){ option.prop('hidden', false); return; }
//    const baseText = String(option.attr('data-base-text') || option.text() || '').toLowerCase();
//    const selected = option.is(':selected');
//    option.prop('hidden', !!keyword && !baseText.includes(keyword) && !selected);
//  });
//});

//$(document).on('change', '.searchable-select-wrap select, .inline-search-select select', function(){
//  const wrapper = $(this).closest('.searchable-select-wrap, .inline-search-select');
//  wrapper.find('.select-search').val('');
//  $(this).find('option').prop('hidden', false);
//});
