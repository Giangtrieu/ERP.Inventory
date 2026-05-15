

window.PrintVoucher = {
  configs: {
        inbound: {
            title: '入库单 / Phiếu nhập kho',

            headerFields: [
                { label: '单号 / Số phiếu', key: 'documentNo' },
                { label: '入库日期 / Ngày nhập', key: 'documentDate' },
                { label: '仓库 / Kho', key: 'warehouse' },
                { label: '创建人 / Người tạo', key: 'createdBy' },
                { label: '审批人 / Người duyệt', key: 'approvedBy' }
            ],

            columns: [
                { label: '序号<br><span>No</span>', key: '_index', width: '5%' },
                { label: '料号<br><span>Mã hàng</span>', key: 'item', width: '22%' },
                { label: '序列号<br><span>SN</span>', key: 'serial', width: '18%' },
                { label: '数量<br><span>Số lượng</span>', key: 'qty', width: '12%' },
                { label: '库位<br><span>Vị trí</span>', key: 'bin', width: '18%' },
                { label: '状态<br><span>Tình trạng</span>', key: 'condition', width: '15%' },
                { label: '备注<br><span>Ghi chú</span>', key: 'note', width: '22%' }
            ],

            signatures: [
                '交货人 / Người giao',
                '仓管 / Thủ kho',
                '会计 / Kế toán',
                '仓库主管 / Quản lý kho'
            ]
        },

        move: {
            title: '库位转移单 / Phiếu chuyển vị trí',

            headerFields: [
                { label: '单号 / Số phiếu', key: 'documentNo' },
                { label: '转移日期 / Ngày chuyển', key: 'documentDate' },
                { label: '仓库 / Kho', key: 'warehouse' },
                { label: '创建人 / Người tạo', key: 'createdBy' },
                { label: '审批人 / Người duyệt', key: 'approvedBy' }
            ],

            columns: [
                { label: '序号<br><span>No</span>', key: '_index', width: '5%' },
                { label: '料号<br><span>Mã hàng</span>', key: 'item', width: '22%' },
                { label: '序列号<br><span>SN</span>', key: 'serial', width: '18%' },
                { label: '数量<br><span>Số lượng</span>', key: 'qty', width: '12%' },
                { label: '从<br><span>Từ</span>', key: 'from', width: '20%' },
                { label: '到<br><span>Đến</span>', key: 'to', width: '20%' },
                { label: '备注<br><span>Ghi chú</span>', key: 'note', width: '15%' }
            ],

            signatures: [
                '转移人 / Người chuyển',
                '仓管 / Thủ kho',
                '仓库主管 / Quản lý kho'
            ]
        },

        adjustment: {
            title: '调整单 / Phiếu điều chỉnh',

            headerFields: [
                { label: '单号 / Số phiếu', key: 'documentNo' },
                { label: '调整日期 / Ngày điều chỉnh', key: 'documentDate' },
                { label: '仓库 / Kho', key: 'warehouse' },
                { label: '原因 / Lý do', key: 'party' },
                { label: '创建人 / Người tạo', key: 'createdBy' },
                { label: '审批人 / Người duyệt', key: 'approvedBy' }
            ],

            columns: [
                { label: '序号<br><span>No</span>', key: '_index', width: '5%' },
                { label: '料号<br><span>Mã hàng</span>', key: 'item', width: '15%' },
                { label: '序列号<br><span>SN</span>', key: 'serial', width: '15%' },
                { label: '数量<br><span>Số lượng</span>', key: 'qty', width: '12%' },
                { label: '旧状态<br><span>TT cũ</span>', key: 'oldStatus', width: '12%' },
                { label: '新状态<br><span>TT mới</span>', key: 'newStatus', width: '12%' },
                { label: '库位<br><span>Vị trí</span>', key: 'bin', width: '16%' },
                { label: '原因<br><span>Lý do</span>', key: 'note', width: '20%' }
            ],

            signatures: [
                '制单人 / Người lập',
                '仓管 / Thủ kho',
                '会计 / Kế toán',
                '主管 / Quản lý'
            ]
        },

        'inventory-check': {
            title: '盘点报告 / Biên bản kiểm kê',

            headerFields: [
                { label: '单号 / Số phiếu', key: 'documentNo' },
                { label: '盘点日期 / Ngày kiểm kê', key: 'documentDate' },
                { label: '仓库 / Kho', key: 'warehouse' },
                { label: '负责人 / Nhân sự phụ trách', key: 'party' },
                { label: '创建人 / Người tạo', key: 'createdBy' },
                { label: '审批人 / Người duyệt', key: 'approvedBy' }
            ],

            columns: [
                { label: '序号<br><span>No</span>', key: '_index', width: '5%' },
                { label: '料号<br><span>Mã hàng</span>', key: 'item', width: '22%' },
                { label: '序列号<br><span>SN</span>', key: 'serial', width: '18%' },
                { label: '数量<br><span>Số lượng</span>', key: 'qty', width: '12%' },
                { label: '结果<br><span>Kết quả</span>', key: 'result', width: '15%' },
                { label: '实际库位<br><span>Vị trí thực tế</span>', key: 'bin', width: '20%' },
                { label: '备注<br><span>Ghi chú</span>', key: 'note', width: '20%' }
            ],

            signatures: [
                '盘点人 / Người kiểm kê',
                '仓管 / Thủ kho',
                '会计 / Kế toán',
                '主管 / Quản lý'
            ]
        },

        'repair-send': {
            title: '送修单 / Phiếu gửi sửa chữa',

            headerFields: [
                { label: '单号 / Số phiếu', key: 'documentNo' },
                { label: '送修日期 / Ngày gửi', key: 'documentDate' },
                { label: '维修单位 / Đơn vị sửa chữa', key: 'party' },
                { label: '创建人 / Người tạo', key: 'createdBy' },
                { label: '审批人 / Người duyệt', key: 'approvedBy' }
            ],

            columns: [
                { label: '序号<br><span>No</span>', key: '_index', width: '5%' },
                { label: '料号<br><span>Mã hàng</span>', key: 'item', width: '22%' },
                { label: '序列号<br><span>SN</span>', key: 'serial', width: '18%' },
                { label: '数量<br><span>Số lượng</span>', key: 'qty', width: '12%' },
                { label: '原库位<br><span>Từ vị trí</span>', key: 'fromBin', width: '20%' },
                { label: '送修地点<br><span>Nơi gửi</span>', key: 'targetBin', width: '20%' },
                { label: '备注<br><span>Ghi chú</span>', key: 'note', width: '15%' }
            ],

            signatures: [
                '送修人 / Người gửi',
                '仓管 / Thủ kho',
                '维修方 / Bên nhận sửa chữa',
                '主管 / Quản lý'
            ]
        },

        'repair-receive': {
            title: '维修收货单 / Phiếu nhận sửa chữa',

            headerFields: [
                { label: '单号 / Số phiếu', key: 'documentNo' },
                { label: '收货日期 / Ngày nhận', key: 'documentDate' },
                { label: '维修单位 / Đơn vị sửa chữa', key: 'party' },
                { label: '创建人 / Người tạo', key: 'createdBy' },
                { label: '审批人 / Người duyệt', key: 'approvedBy' }
            ],

            columns: [
                { label: '序号<br><span>No</span>', key: '_index', width: '5%' },
                { label: '料号<br><span>Mã hàng</span>', key: 'item', width: '15%' },
                { label: '序列号<br><span>SN</span>', key: 'serial', width: '18%' },
                { label: '数量<br><span>Số lượng</span>', key: 'qty', width: '12%' },
                { label: '结果<br><span>Kết quả</span>', key: 'status', width: '12%' },
                { label: '收货库位<br><span>Vị trí nhận</span>', key: 'targetBin', width: '18%' },
                { label: '新序列号<br><span>SN mới</span>', key: 'newSerial', width: '15%' },
                { label: '备注<br><span>Ghi chú</span>', key: 'note', width: '15%' }
            ],

            signatures: [
                '交货方 / Bên giao',
                '收货仓管 / Thủ kho nhận',
                '会计 / Kế toán',
                '主管 / Quản lý'
            ]
        },

        'borrow-lend': {
            title: '借用单 / Giấy mượn hàng',

            headerFields: [
                { label: '单号 / Số phiếu', key: 'documentNo' },
                { label: '借用日期 / Ngày mượn', key: 'documentDate' },
                { label: '借用人 / Người mượn', key: 'party' },
                { label: '创建人 / Người tạo', key: 'createdBy' },
                { label: '审批人 / Người duyệt', key: 'approvedBy' }
            ],

            extraFields: [
                'purpose',
                'borrowDepartment',
                'borrowerPhone',
                'departmentOwner',
                'dueDate'
            ],

            columns: [
                { label: '序号<br><span>No</span>', key: '_index', width: '5%' },
                { label: '料号<br><span>Mã hàng</span>', key: 'item', width: '22%' },
                { label: '序列号<br><span>SN</span>', key: 'serial', width: '18%' },
                { label: '数量<br><span>Số lượng</span>', key: 'qty', width: '18%' },
                { label: '发出库位<br><span>Nơi giao</span>', key: 'fromBin', width: '22%' },
                { label: '外部位置<br><span>Vị trí ngoài</span>', key: 'targetBin', width: '21%' },
                { label: '备注<br><span>Ghi chú</span>', key: 'note', width: '12%' }
            ],

            signatures: [
                '借用方 / Bên mượn',
                '申请人 / Người làm đơn',
                '电话 / SĐT',
                '出借方 / Bên cho mượn',
                '主管 / Quản lý',
                '交接人 / Người giao'
            ]
        },

        'borrow-return': {
            title: '归还单 / Phiếu nhận trả',

            headerFields: [
                { label: '单号 / Số phiếu', key: 'documentNo' },
                { label: '归还日期 / Ngày trả', key: 'documentDate' },
                { label: '借用人 / Người mượn', key: 'party' },
                { label: '创建人 / Người tạo', key: 'createdBy' },
                { label: '审批人 / Người duyệt', key: 'approvedBy' }
            ],

            columns: [
                { label: '序号<br><span>No</span>', key: '_index', width: '5%' },
                { label: '料号<br><span>Mã hàng</span>', key: 'item', width: '22%' },
                { label: '序列号<br><span>SN</span>', key: 'serial', width: '18%' },
                { label: '数量<br><span>Số lượng</span>', key: 'qty', width: '18%' },
                { label: '状态<br><span>Tình trạng</span>', key: 'condition', width: '16%' },
                { label: '收货位置<br><span>Vị trí nhận</span>', key: 'targetBin', width: '12%' },
                { label: '备注<br><span>Ghi chú</span>', key: 'note', width: '15%' }
            ],

            signatures: [
                '归还方 / Bên trả',
                '收货仓管 / Thủ kho nhận',
                '会计 / Kế toán',
                '主管 / Quản lý'
            ]
        }

    },

  // Generate and show print voucher from document detail
  show(type, detail) {
    const config = this.configs[type] || this.configs['inbound'];
    const header = detail.header || {};
    const extra = header.extra || {};
    const lines = detail.lines || [];

    // Header info rows
    //const infoHtml = config.headerFields.map(f => {
    //  let val = header[f.key] || '';
    //  if (f.key === 'documentDate') val = UI.formatDate(val);
    //  return `<div class="voucher-info-row col-md-4"><span class="info-label">${f.label}:</span><span>${UI.esc(val || '-')}</span></div>`;
    //}).join('');

    // Extra info for borrow-lend
    let extraHtml = '';
    if (config.extraFields && Object.keys(extra).length) {
      const labels = { purpose: 'Mục đích', borrowDepartment: 'Bộ phận', borrowerPhone: 'SĐT', departmentOwner: 'Chủ quản BP', dueDate: 'Ngày hẹn trả' };
      extraHtml = config.extraFields.map(key => {
        let val = extra[key] || '';
        if (key === 'dueDate') val = UI.formatDate(val);
          return `<div class="voucher-info-row col-md-4"><span class="info-label">${labels[key] || key}:</span><span>${UI.esc(val || '-')}</span></div>`;
      }).join('');
    }

    // Note/reason
    //const noteHtml = header.note ? `<div class="voucher-reason"><div class="voucher-reason-label">Lý do / Ghi chú:</div><div class="voucher-reason-text">${UI.esc(header.note)}</div></div>` : '';

    // Table
    const thead = config.columns.map(c => `<th style="width:${c.width}">${c.label}</th>`).join('');
    const tbody = lines.map((line, i) => {
      const cells = config.columns.map(c => {
          if (c.key === '_index') return `<td class="col-stt" style="text-align:center">${i + 1}</td>`;
          else if (c.key === 'qty') return `<td class="col-stt" style="text-align:center">1</td>`;
        return `<td>${UI.esc(UI.t(line[c.key] || '-'))}</td>`;
      }).join('');
      return `<tr>${cells}</tr>`;
    }).join('');

    // Signatures
    //const sigHtml = config.signatures.map(s => `<div class="voucher-sig-block"><div class="voucher-sig-title">${s}</div><div class="voucher-sig-name">(Ký, ghi rõ họ tên)</div></div>`).join('');
    const documentDate = UI.formatDate(header.documentDate);
    const dueDate = UI.formatDate(extra.dueDate);
    const table = `<table class="voucher-table"><thead><tr>${thead}</tr></thead><tbody> ${tbody}</tbody></table>`;

      const tpl = VoucherTemplate[type];

      const data = {
          documentNo: header.documentNo,
          date: documentDate,
          party: header.party,
          approvedBy: header.approvedBy,
          createdBy: header.createdBy,
          department: extra.borrowDepartment,
          purpose: extra.purpose,
          dueDate,
          phone: extra.borrowerPhone,
          departmentOwner: extra.departmentOwner
      };

      if (tpl) {
          html = renderVoucherLayout(tpl, data, table, config, header);
      }

    let container = document.getElementById('printVoucher');
    if (!container) {
      container = document.createElement('div');
      container.id = 'printVoucher';
      container.className = 'print-voucher';
      document.body.appendChild(container);
    }
    container.innerHTML = html;
    container.classList.add('active');
  },

  // Print the voucher
  print(type, detail) {
    this.show(type, detail);
    document.body.classList.add('print-voucher-mode');
    setTimeout(() => window.print(), 300);
  },

    close() {
    document.body.classList.remove('print-voucher-mode');
    const container = document.getElementById('printVoucher');
    if (container) container.classList.remove('active');
  }
};
function fillTemplate(text, data) {
    return text.replace(/\{(\w+)\}/g, (_, key) => {return UI.esc(data[key] || '____________');});
}
function renderSignRows(rows, data, column = false) {
    return ` <div class="${column ? 'sign-row-column' : 'sign-row'}">${rows.map(x => `<div>${fillTemplate(x, data)}</div>`).join('')}</div>`;
}
function renderVoucherLayout(configTemplate, data, table, config, header) {

    const companyName = UI.t('Default CompanyName');
    const branchName = UI.t('Default BranchName');
    const dualSign = !!configTemplate.signRows2;

    return `
    <button class="btn-close-voucher"onclick="PrintVoucher.close()">✕</button>
    <div class="voucher-header">
        <div style="font-weight:bold">${UI.esc(UI.t(companyName))}</div>
        <div>${UI.esc(UI.t(branchName))}</div>
        <div class="voucher-title">${UI.esc(config.title)}</div>
        <div class="voucher-subtitle">${UI.esc(header.documentNo || '')}</div>
    </div>
    <div class="borrow-paper">
        <div class="borrow-content">
            <div class="borrow-text-cn"> <p>${fillTemplate(configTemplate.cn, data)}</p></div>
            <div class="borrow-text-vi"> <p>${fillTemplate(configTemplate.vi, data)}</p></div>
        </div>
        <div class="borrow-table">${table}</div>
        <div class="borrow-sign-section">
            <div class="sign-group">
                <div class="sign-title">${configTemplate.signTitle}</div>
                ${renderSignRows(configTemplate.signRows, data)}
            </div>
            ${dualSign ? `
            <div class="sign-group mt-4">
                <div class="sign-title">${configTemplate.signTitle2}</div>
                ${renderSignRows(configTemplate.signRows2, data, true)}
            </div>
            ` : ''}
        </div>
    </div>`;
}
