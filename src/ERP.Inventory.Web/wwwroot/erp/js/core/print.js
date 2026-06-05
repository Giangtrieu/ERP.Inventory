

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

  localizedConfig(type) {
    const t = key => UI.t(key);
    const col = (labelKey, key, width) => ({ label: t(labelKey), key, width });
    const baseHeader = [
      { label: t('Document No'), key: 'documentNo' },
      { label: t('Document Date'), key: 'documentDate' },
      { label: t('Warehouse'), key: 'warehouse' },
      { label: t('PIC'), key: 'createdBy' },
      { label: t('Remark'), key: 'note' }
    ];
    const configs = {
      inbound: {
        title: t('pdf-inbound-title'),
        headerFields: [
          { label: t('Document No'), key: 'documentNo' },
          { label: t('Inbound Date'), key: 'documentDate' },
          { label: t('Warehouse'), key: 'warehouse' },
          { label: t('Supplier / Source Party'), key: 'party' },
          { label: t('Owner'), key: 'createdBy' },
          { label: t('PIC'), key: 'approvedBy' },
          { label: t('Remark'), key: 'note' }
        ],
        columns: [col('No', '_index', '5%'), col('Item Code', 'item', '14%'), col('Item Name', 'itemName', '18%'), col('Serial Number', 'serial', '14%'), col('Barcode', 'barcode', '12%'), col('Quantity', 'qty', '8%'), col('UOM', 'uom', '8%'), col('Bin Location', 'bin', '12%'), col('Remark', 'note', '15%')],
        signatures: [t('Prepared By'), t('Checked By'), t('Approved By')]
      },
      move: {
        title: t('pdf-move-title'),
        headerFields: [
          { label: t('Move No'), key: 'documentNo' },
          { label: t('Move Date'), key: 'documentDate' },
          { label: t('From Warehouse'), key: 'warehouse' },
          { label: t('To Warehouse'), key: 'toWarehouse' },
          { label: t('PIC'), key: 'createdBy' },
          { label: t('Reason'), key: 'party' },
          { label: t('Remark'), key: 'note' }
        ],
        columns: [col('No', '_index', '5%'), col('Item Code', 'item', '16%'), col('Item Name', 'itemName', '18%'), col('Serial Number', 'serial', '14%'), col('Barcode', 'barcode', '12%'), col('Quantity', 'qty', '8%'), col('From Bin', 'from', '12%'), col('To Bin', 'to', '12%'), col('Remark', 'note', '13%')],
        signatures: [t('Prepared By'), t('Transferred By'), t('Received By'), t('Approved By')]
      },
      adjustment: {
        title: t('pdf-adjustment-title'),
        headerFields: [
          { label: t('Adjustment No'), key: 'documentNo' },
          { label: t('Adjustment Date'), key: 'documentDate' },
          { label: t('Warehouse'), key: 'warehouse' },
          { label: t('Reason'), key: 'party' },
          { label: t('PIC'), key: 'createdBy' },
          { label: t('Remark'), key: 'note' }
        ],
        columns: [col('No', '_index', '5%'), col('Item Code', 'item', '14%'), col('Item Name', 'itemName', '16%'), col('Serial Number', 'serial', '13%'), col('Barcode', 'barcode', '11%'), col('Before Qty', 'beforeQty', '9%'), col('Adjust Qty', 'qty', '9%'), col('After Qty', 'afterQty', '9%'), col('Bin Location', 'bin', '12%'), col('Remark', 'note', '12%')],
        signatures: [t('Prepared By'), t('Checked By'), t('Approved By')]
      },
      'borrow-lend': {
        title: t('pdf-borrow-lend-title'),
        headerFields: [
          { label: t('Borrow No'), key: 'documentNo' },
          { label: t('Borrow Date'), key: 'documentDate' },
          { label: t('Borrow Department'), key: 'borrowDepartment' },
          { label: t('Warehouse'), key: 'warehouse' },
          { label: t('Borrower'), key: 'party' },
          { label: t('Expected Return Date'), key: 'dueDate' },
          { label: t('PIC'), key: 'createdBy' },
          { label: t('Remark'), key: 'note' }
        ],
        extraFields: ['purpose', 'borrowDepartment', 'borrowerPhone', 'departmentOwner', 'dueDate'],
        columns: [col('No', '_index', '5%'), col('Item Code', 'item', '16%'), col('Item Name', 'itemName', '18%'), col('Serial Number', 'serial', '14%'), col('Barcode', 'barcode', '12%'), col('Quantity', 'qty', '8%'), col('UOM', 'uom', '8%'), col('Bin Location', 'fromBin', '13%'), col('Return Status', 'condition', '12%'), col('Remark', 'note', '12%')],
        signatures: [t('Borrower'), t('Warehouse Keeper'), t('Approved By')]
      },
      'borrow-return': {
        title: t('pdf-borrow-return-title'),
        headerFields: [
          { label: t('Return No'), key: 'documentNo' },
          { label: t('Return Date'), key: 'documentDate' },
          { label: t('Borrow Department'), key: 'borrowDepartment' },
          { label: t('Warehouse'), key: 'warehouse' },
          { label: t('Original Borrow No'), key: 'documentNo' },
          { label: t('PIC'), key: 'createdBy' },
          { label: t('Remark'), key: 'note' }
        ],
        columns: [col('No', '_index', '5%'), col('Item Code', 'item', '16%'), col('Item Name', 'itemName', '18%'), col('Serial Number', 'serial', '14%'), col('Barcode', 'barcode', '12%'), col('Borrowed Qty', 'borrowedQty', '9%'), col('Returned Qty', 'qty', '9%'), col('Remaining Qty', 'remainingQty', '9%'), col('Bin Location', 'targetBin', '13%'), col('Remark', 'note', '12%')],
        signatures: [t('Returned By'), t('Received By'), t('Checked By'), t('Approved By')]
      },
      'inventory-check': {
        title: t('pdf-inventory-check-title'),
        headerFields: [
          { label: t('Check No'), key: 'documentNo' },
          { label: t('Check Date'), key: 'documentDate' },
          { label: t('Warehouse'), key: 'warehouse' },
          { label: t('Checker'), key: 'party' },
          { label: t('Status'), key: 'status' },
          { label: t('Remark'), key: 'note' }
        ],
        columns: [col('No', '_index', '5%'), col('Item Code', 'item', '14%'), col('Item Name', 'itemName', '16%'), col('Serial Number', 'serial', '13%'), col('Barcode', 'barcode', '11%'), col('System Qty', 'systemQty', '9%'), col('Actual Qty', 'qty', '9%'), col('Variance', 'variance', '8%'), col('Variance Type', 'result', '12%'), col('Bin Location', 'bin', '12%'), col('Remark', 'note', '12%')],
        signatures: [t('Checked By'), t('Warehouse Keeper'), t('Approved By')]
      },
      'quantity-receive': { title: t('pdf-quantity-title'), headerFields: baseHeader, columns: [col('No', '_index', '5%'), col('Item Code', 'itemCode', '18%'), col('Item Name', 'itemName', '24%'), col('Serial Number', 'snCode', '14%'), col('Quantity', 'qty', '10%'), col('UOM', 'uom', '8%'), col('Bin Location', 'binCode', '14%'), col('Remark', 'note', '15%')], signatures: [t('Prepared By'), t('Checked By'), t('Approved By')] },
      'quantity-issue': { title: t('pdf-quantity-title'), headerFields: baseHeader, columns: [col('No', '_index', '5%'), col('Item Code', 'itemCode', '18%'), col('Item Name', 'itemName', '24%'), col('Serial Number', 'snCode', '14%'), col('Quantity', 'qty', '10%'), col('UOM', 'uom', '8%'), col('Bin Location', 'binCode', '14%'), col('Remark', 'note', '15%')], signatures: [t('Prepared By'), t('Checked By'), t('Approved By')] },
      'quantity-adjust': { title: t('pdf-quantity-title'), headerFields: baseHeader, columns: [col('No', '_index', '5%'), col('Item Code', 'itemCode', '18%'), col('Item Name', 'itemName', '24%'), col('Serial Number', 'snCode', '14%'), col('Quantity', 'qty', '10%'), col('UOM', 'uom', '8%'), col('Bin Location', 'binCode', '14%'), col('Remark', 'note', '15%')], signatures: [t('Prepared By'), t('Checked By'), t('Approved By')] }
    };
    return configs[type] || configs.inbound;
  },

  // Generate and show print voucher from document detail
  show(type, detail) {
    const config = this.localizedConfig(type);
    const header = detail.header || {};
    const extra = header.extra || {};
    const lines = detail.rows || detail.lines || [];
    const tpl = { ...(window.VoucherTemplate?.[type] || {}) };

    let html = '';
    if (tpl && tpl.vi && tpl.cn) {
      const table = renderStandardVoucherTable(type, lines);
      const data = buildVoucherTemplateData(type, header, extra, lines);
      html = renderVoucherLayout(tpl, data, table, header);
    } else {
      const headerValue = key => {
        if (Object.prototype.hasOwnProperty.call(header, key)) return header[key];
        if (Object.prototype.hasOwnProperty.call(extra, key)) return extra[key];
        return '';
      };
      const infoHtml = config.headerFields.map(f => {
        let val = headerValue(f.key);
        if (f.key === 'documentDate' || f.key === 'dueDate') val = UI.formatDate(val);
        return `<div class="voucher-info-row col-md-4"><span class="info-label">${UI.esc(f.label)}:</span><span>${UI.esc(val || '-')}</span></div>`;
      }).join('');
      let extraHtml = '';
      if (config.extraFields && Object.keys(extra).length) {
        const labels = { purpose: UI.t('Purpose'), borrowDepartment: UI.t('Borrow Department'), borrowerPhone: UI.t('Phone'), departmentOwner: UI.t('Department Owner'), dueDate: UI.t('Expected Return Date') };
        extraHtml = config.extraFields.map(key => {
          let val = extra[key] || '';
          if (key === 'dueDate') val = UI.formatDate(val);
          return `<div class="voucher-info-row col-md-4"><span class="info-label">${labels[key] || key}:</span><span>${UI.esc(val || '-')}</span></div>`;
        }).join('');
      }
      const noteHtml = header.note ? `<div class="voucher-reason"><div class="voucher-reason-label">${UI.esc(UI.t('Remark'))}:</div><div class="voucher-reason-text">${UI.esc(header.note)}</div></div>` : '';
      const thead = config.columns.map(c => `<th style="width:${c.width}">${c.label}</th>`).join('');
      const tbody = lines.map((line, i) => {
        const cells = config.columns.map(c => {
          if (c.key === '_index') return `<td class="col-stt" style="text-align:center">${i + 1}</td>`;
          if (c.key === 'qty') return `<td class="col-stt" style="text-align:center">${UI.esc(formatQty(line.qty ?? line.quantity ?? line.quantityDelta ?? 1))}</td>`;
          return `<td>${UI.esc(UI.t(line[c.key] || '-'))}</td>`;
        }).join('');
        return `<tr>${cells}</tr>`;
      }).join('');
      const sigHtml = config.signatures.map(s => `<div class="voucher-sig-block"><div class="voucher-sig-title">${UI.esc(s)}</div><div class="voucher-sig-name">${UI.esc(UI.t('Sign and full name'))}</div></div>`).join('');
      const table = `<table class="voucher-table"><thead><tr>${thead}</tr></thead><tbody>${tbody}</tbody></table>`;
      html = `<button class="btn-close-voucher" onclick="PrintVoucher.close()">x</button><div class="voucher-header"><div class="voucher-title">${UI.esc(config.title)}</div><div class="voucher-subtitle">${UI.esc(header.documentNo || '')}</div></div><div class="voucher-info row">${infoHtml}${extraHtml}</div>${noteHtml}${table}<div class="voucher-signatures">${sigHtml}</div>`;
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
function renderVoucherLayout(configTemplate, data, table, config, header, infoHtml = '', extraHtml = '', noteHtml = '') {

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
        <div class="voucher-info row">${infoHtml}${extraHtml}</div>
        ${noteHtml}
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

var BLANK_TEXT = '____________________';
var OUTSIDE_WAREHOUSE_TEXT = '仓外 / Ngoài kho';

function firstText(...values) {
    for (const value of values) {
        const text = String(value ?? '').trim();
        if (text && text !== 'null' && text !== 'undefined') return text;
    }
    return '';
}

function safeText(...values) {
    return firstText(...values) || BLANK_TEXT;
}

function parseVoucherDate(value) {
    if (!value) return null;
    const date = value instanceof Date ? value : new Date(value);
    return Number.isNaN(date.getTime()) ? null : date;
}

function twoDigit(value) {
    return String(value).padStart(2, '0');
}

function dateParts(value) {
    const date = parseVoucherDate(value);
    if (!date) return { date: BLANK_TEXT, time: BLANK_TEXT, day: BLANK_TEXT, month: BLANK_TEXT, year: BLANK_TEXT };
    const day = twoDigit(date.getDate());
    const month = twoDigit(date.getMonth() + 1);
    const year = String(date.getFullYear());
    return {
        date: UI.formatDate(value) || `${day}/${month}/${year}`,
        time: `${twoDigit(date.getHours())}:${twoDigit(date.getMinutes())}`,
        day,
        month,
        year
    };
}

function formatQty(value) {
    const n = Number(value ?? 1);
    if (!Number.isFinite(n)) return '1';
    return Number.isInteger(n) ? String(n) : String(n).replace(/\.?0+$/, '');
}

function getDistinctOwners(lines) {
    const owners = [...new Set(
        (lines || [])
            .map(x => firstText(x.ownerName, x.owner, x.itemOwner, x.itemInstanceOwnerName, x.ownerDepartment))
            .filter(Boolean)
    )];
    if (owners.length === 0) return BLANK_TEXT;
    return owners.length === 1 ? owners[0] : owners.join(', ');
}

function getParty(type, doc) {
    if (type === 'inbound') return safeText(doc.senderName, doc.partyName, doc.party, doc.operatorName, doc.sourceExternalPartyName, doc.createdBy);
    if (type === 'repair-send') return safeText(doc.senderName, doc.repairSenderName, doc.partyName, doc.party, doc.operatorName, doc.createdBy);
    if (type === 'repair-receive') return safeText(doc.receiverName, doc.repairReceiverName, doc.partyName, doc.party, doc.operatorName, doc.createdBy);
    if (type === 'borrow-lend') return safeText(doc.borrowerName, doc.partyName, doc.party, doc.operatorName, doc.createdBy);
    if (type === 'borrow-return') return safeText(doc.returnerName, doc.borrowerName, doc.partyName, doc.party, doc.operatorName, doc.createdBy);
    if (type === 'move' || type === 'inventory-check' || type === 'adjustment') return safeText(doc.operatorName, doc.partyName, doc.party, doc.createdBy);
    return safeText(doc.partyName, doc.operatorName, doc.receiverName, doc.senderName, doc.borrowerName, doc.returnerName, doc.party, doc.createdBy);
}

function buildVoucherTemplateData(type, header, extra, lines) {
    const doc = { ...header, ...extra };
    const documentDate = firstText(doc.documentDate, doc.date, doc.createdAt, doc.createdOn);
    const dueDateValue = firstText(doc.dueDate, doc.expectedReturnDate, doc.returnDueDate);
    const parts = dateParts(documentDate);
    const dueParts = dateParts(dueDateValue);
    return {
        ...parts,
        party: getParty(type, doc),
        cardNo: safeText(doc.cardNo, doc.employeeNo, doc.employeeCode, doc.operatorCode, doc.borrowerCode, doc.returnerCode, doc.senderCode, doc.receiverCode),
        department: safeText(doc.department, doc.departmentName, doc.borrowDepartment, doc.operatorDepartment, doc.senderDepartment, doc.receiverDepartment),
        purpose: safeText(doc.purpose, doc.borrowPurpose, doc.reason, doc.note),
        dueDate: dueParts.date,
        dueDay: dueParts.day,
        dueMonth: dueParts.month,
        dueYear: dueParts.year,
        ownerName: getDistinctOwners(lines),
        warehouseName: safeText(doc.warehouseName, doc.warehouse, doc.toWarehouseName, doc.toWarehouse),
        documentNo: safeText(doc.borrowDocumentNo, doc.originalDocumentNo, doc.documentNo),
        createdBy: safeText(doc.createdBy),
        approvedBy: safeText(doc.approvedBy),
        phone: safeText(doc.phone, doc.borrowerPhone, doc.partyPhone, doc.receiverPhone, doc.senderPhone)
    };
}

function buildLineText(type, line) {
    const from = firstText(line.fromBinCode, line.fromBinName, line.fromLocation, line.oldBinCode, line.oldLocation, line.currentLocation);
    const to = firstText(line.toBinCode, line.toBinName, line.toLocation, line.newBinCode, line.newLocation, line.binCode, line.binLocationCode, line.location);
    const current = firstText(line.lineText, line.moveLine, line.binCode, line.binLocationCode, line.location, line.currentLocation);
    if (type === 'borrow-lend' || type === 'repair-send') return `${from || BLANK_TEXT} --> ${OUTSIDE_WAREHOUSE_TEXT}`;
    if (type === 'borrow-return' || type === 'repair-receive') return `${OUTSIDE_WAREHOUSE_TEXT} --> ${to || current || BLANK_TEXT}`;
    if (type === 'move') {
        if (from || to) return `${from || BLANK_TEXT} --> ${to || BLANK_TEXT}`;
        return current || BLANK_TEXT;
    }
    return current || to || from || BLANK_TEXT;
}

function renderStandardVoucherTable(type, lines) {
    const tbody = (lines || []).map((line, i) => {
        const item = safeText(line.itemCode, line.item, line.itemName, line.pn, line.partNo);
        const serial = safeText(line.serialNumber, line.serial, line.sn, line.barcode);
        const qty = formatQty(line.quantity ?? line.qty ?? line.quantityDelta);
        const lineText = buildLineText(type, line);
        return `<tr>
            <td class="col-stt" style="text-align:center">${i + 1}</td>
            <td>${UI.esc(item)}</td>
            <td>${UI.esc(serial)}</td>
            <td class="col-stt" style="text-align:center">${UI.esc(qty)}</td>
            <td>${UI.esc(lineText)}</td>
        </tr>`;
    }).join('');
    return `<table class="voucher-table"><thead><tr>
        <th>NO</th>
        <th>品名 / Tên hàng</th>
        <th>SN码 / Mã SN</th>
        <th style="min-width: 150px;">数量 / SL</th>
        <th>Line</th>
    </tr></thead><tbody>${tbody}</tbody></table>`;
}

function fillTemplate(text, data) {
    return String(text || '').replace(/\{(\w+)\}/g, (_, key) => UI.esc(data[key] || BLANK_TEXT));
}

function renderSignRows(rows, data, column = false) {
    return `<div class="${column ? 'sign-row-column' : 'sign-row'}">${(rows || []).map(x => `<div>${fillTemplate(x, data)}</div>`).join('')}</div>`;
}

function renderVoucherLayout(configTemplate, data, table, header) {
    const companyName = UI.t('Default CompanyName');
    const branchName = UI.t('Default BranchName');
    const dualSign = !!configTemplate.signRows2;
    const title = `${configTemplate.titleCn} / ${configTemplate.titleVi}`;
    return `
    <button class="btn-close-voucher" onclick="PrintVoucher.close()">x</button>
    <div class="voucher-header">
        <div style="font-weight:bold">${UI.esc(UI.t(companyName))}</div>
        <div>${UI.esc(UI.t(branchName))}</div>
        <div class="voucher-title">${UI.esc(title)}</div>
        <div class="voucher-subtitle">No: ${UI.esc(header.documentNo || BLANK_TEXT)}</div>
    </div>
    <div class="borrow-paper">
        <div class="borrow-content">
            <div class="borrow-text-cn"><p>${fillTemplate(configTemplate.cn, data)}</p></div>
            <div class="borrow-text-vi"><p>${fillTemplate(configTemplate.vi, data)}</p></div>
        </div>
        <div class="borrow-table">${table}</div>
        <div class="borrow-sign-section">
            <div class="sign-group">
                <div class="sign-title">${UI.esc(configTemplate.signTitle || '')}</div>
                ${renderSignRows(configTemplate.signRows, data)}
            </div>
            ${dualSign ? `
            <div class="sign-group mt-4">
                <div class="sign-title">${UI.esc(configTemplate.signTitle2 || '')}</div>
                ${renderSignRows(configTemplate.signRows2, data, true)}
            </div>
            ` : ''}
        </div>
    </div>`;
}
