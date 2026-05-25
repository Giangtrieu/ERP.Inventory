window.OperationLineConfig = {
    tables: {
        inbound: {
            headers: [
                ['col-stt', '#'],
                ['col-item-code', 'Item'],
                ['col-serial', 'Serial'],
                ['col-bin', 'MT'],
                ['col-bin', 'Bin'],
                ['col-select', 'Condition'],
                ['col-action', '']
            ],

            row: ({ index }) => `
            ${tdStt(index)}
            ${tdItem()}
            ${tdSerial()}
            <td class="col-bin"><input class="form-control form-control-sm" name="mt"></td>
            ${tdBin()}
            <td class="col-select">${selectInline('condition', AppState.lookups.inboundConditions, false, 'Normal')}</td>
            ${tdDelete()}
        `
        },

        adjustment: {
            headers: [
                ['col-stt', '#'],
                ['col-item-code', 'Item'],
                ['col-serial', 'Serial'],
                ['col-serial', 'New Serial'],
                ['col-select', 'New Status'],
                ['col-bin', 'Target Bin'],
                //['col-note', 'Reason'],
                ['col-action', '']
            ],

            row: ({ index }) => `
            ${tdStt(index)}
            ${tdItem()}
            ${tdSerial()}
            <td class="col-serial"><input class="form-control form-control-sm" name="newSerialNumber"></td>
            <td class="col-select">${selectInline('newStatus', AppState.lookups.returnConditions, false, 'Normal')}</td>
            ${tdTargetBin()}
            <!--<td class="col-note"><input class="form-control form-control-sm" name="reason"></td>-->
            ${tdDelete()}
        `
        },

        'inventory-check': {
            headers: [
                ['col-stt', '#'],
                ['col-item-code', 'Item'],
                ['col-serial', 'Serial'],
                ['col-bin', 'Actual Bin'],
                ['col-action', '']
            ],

            row: ({ index, bin }) => `
            ${tdStt(index)}
            ${tdItem()}
            ${tdSerial()}
            <td class="col-bin"><input class="form-control form-control-sm" name="binCode" value="${bin ? bin.binCode : ''}"></td>
            ${tdDelete()}
        `
        },

        'borrow-return': {
            headers: [
                ['col-stt', '#'],
                ['col-item-code', 'Item'],
                ['col-serial', 'Serial'],
                ['col-select', 'Condition'],
                ['col-bin', 'Target Bin'],
                ['col-action', '']
            ],

            row: ({ index }) => `
            ${tdStt(index)}
            ${tdItem()}
            ${tdSerial()}
            <td class="col-select">${selectInline('condition', AppState.lookups.returnConditions, false, 'Normal')}</td>
            ${tdTargetBin()}
            ${tdDelete()}
        `
        },

        'repair-receive': {
            headers: [
                ['col-stt', '#'],
                ['col-item-code', 'Item'],
                ['col-serial', 'Serial'],
                ['col-select', 'Result'],
                ['col-bin', 'Target Bin'],
                ['col-note', 'Note'],
                ['col-action', '']
            ],

            row: ({ index }) => `
            ${tdStt(index)}
            ${tdItem()}
            ${tdSerial()}
            <td class="col-select">${selectInline('result', AppState.lookups.repairResults, false, 'Success')}</td>
            ${tdTargetBin()}
            <td class="col-note"><input class="form-control form-control-sm" name="lineNote"></td>
            ${tdDelete()}
        `
        },

        'repair-send': {
            headers: [
                ['col-stt', '#'],
                ['col-item-code', 'Item'],
                ['col-serial', 'Serial'],
                ['col-bin', 'External Destination'],
                ['col-note', 'Note'],
                ['col-action', '']
            ],

            row: ({ index }) => `
            ${tdStt(index)}
            ${tdItem()}
            ${tdSerial()}
            <td class="col-bin"><input class="form-control form-control-sm"name="targetExternalLocation" placeholder="${UI.esc(UI.t('External Destination'))}"></td>
            <td class="col-note"><input class="form-control form-control-sm" name="lineNote"></td>
            ${tdDelete()}
        `
        },

        'borrow-lend': {
            headers: [
                ['col-stt', '#'],
                ['col-item-code', 'Item'],
                ['col-serial', 'Serial'],
                ['col-bin', 'External Destination'],
                ['col-note', 'Note'],
                ['col-action', '']
            ],

            row: ({ index }) => `
            ${tdStt(index)}
            ${tdItem()}
            ${tdSerial()}
            <td class="col-bin"><input class="form-control form-control-sm" name="targetExternalLocation"placeholder="${UI.esc(UI.t('External Destination'))}"></td>
            <td class="col-note"><input class="form-control form-control-sm" name="lineNote"></td>
            ${tdDelete()}
        `
        },

        move: {
            headers: [
                ['col-stt', '#'],
                ['col-item-code', 'Item'],
                ['col-serial', 'Serial'],
                ['col-bin', 'Target Bin'],
                ['col-action', '']
            ],

            row: ({ index }) => `
            ${tdStt(index)}
            ${tdItem()}
            ${tdSerial()}
            ${tdTargetBin()}
            ${tdDelete()}
        `
        },

        default: {
            headers: [
                ['col-stt', '#'],
                ['col-item-code', 'Item'],
                ['col-serial', 'Serial'],
                ['col-bin', 'Target Bin'],
                ['col-note', 'Note'],
                ['col-action', '']
            ],

            row: ({ index }) => `
            ${tdStt(index)}
            ${tdItem()}
            ${tdSerial()}
            ${tdBin()}
            <td class="col-note"><input class="form-control form-control-sm" name="lineNote"></td>
            ${tdDelete()}
        `
        }
    },
    headers: {
        inbound: [
            {
                col: 'col-md-3 d-none',
                type: 'select',
                label: 'Source',
                name: 'sourceExternalPartyId',
                source: () => AppState.lookups.suppliers
            },
            {
                col: 'col-md-4',
                type: 'select',
                label: 'Warehouse',
                name: 'warehouseId',
                source: () => AppState.lookups.warehouses,
                value: vm => vm.warehouseId
            },
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'date',
                label: 'Inbound Date',
                name: 'documentDate',
                value: () => today()
            },
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'text',
                label: 'Document No',
                name: 'documentNo'
            },
            {
                col: 'col-md-4',
                type: 'custom',
                render: () => UI.inputBorrorer(
                    'Receiver',
                    'text',
                    '',
                    'receiver',
                    'Code-Name'
                )
            },
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'text',
                label: 'Receiver Phone',
                name: 'receiverPhone'
            },
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'text',
                label: 'Receiver Department',
                name: 'receiverDepartment'
            },
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'text',
                label: 'Department Owner',
                name: 'departmentOwner'
            },
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'text',
                label: 'OwnerName',
                name: 'ownerName'
            },
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'text',
                label: 'Approver',
                name: 'approvedBy'
            }
        ],

        move: [
            {
                col: 'col-md-4',
                type: 'select',
                label: 'Warehouse',
                name: 'warehouseId',
                source: () => AppState.lookups.warehouses,
                value: vm => vm.warehouseId
            },
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'date',
                label: 'Move Date',
                name: 'documentDate',
                value: () => today()
            },
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'text',
                label: 'Document No Auto',
                name: 'documentNo'
            }
        ],

        adjustment: [
            {
                col: 'col-md-4',
                type: 'select',
                label: 'Warehouse',
                name: 'warehouseId',
                source: () => AppState.lookups.warehouses,
                value: vm => vm.warehouseId
            },
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'date',
                label: 'Adjustment Date',
                name: 'documentDate',
                value: () => today()
            },
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'text',
                label: 'Reason',
                name: 'reason'
            }
        ],

        'inventory-check': [
            {
                col: 'col-md-4',
                type: 'select',
                label: 'Warehouse',
                name: 'warehouseId',
                source: () => AppState.lookups.warehouses,
                value: vm => vm.warehouseId
            },
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'date',
                label: 'Session Date',
                name: 'sessionDate',
                value: () => today()
            },
            {
                col: 'col-md-4',
                type: 'select',
                label: 'DocumentPeriodType',
                name: 'documentPeriodType',
                source: () => AppState.lookups.documentPeriodType,
                value: () => AppState.lookups.documentPeriodType?.[0]?.id
            },
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'text',
                label: 'Count Method',
                name: 'countMethod',
                value: () => 'Scan'
            },
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'text',
                label: 'Responsible Staff',
                name: 'responsibleStaff',
                value: () => AppState.user.userName
            }
        ],

        'repair-send': [
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'text',
                label: 'Repair Document No',
                name: 'documentNo'
            },
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'text',
                label: 'Vendor',
                name: 'repairVendorCode'
            },
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'date',
                label: 'Send Date',
                name: 'sendDate',
                value: () => today()
            },
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'date',
                label: 'Expected Return',
                name: 'expectedReturnDate',
                value: () => today()
            },
            {
                col: 'col-md-8',
                type: 'input',
                inputType: 'text',
                label: 'Reason',
                name: 'reason'
            }
        ],

        'repair-receive': [
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'text',
                label: 'Repair Document No',
                name: 'repairDocumentNo'
            },
            //{
            //    col: 'col-md-4',
            //    type: 'select',
            //    label: 'Repair Document (select)',
            //    name: 'repairDocumentId',
            //    source: vm => vm.repairDocuments
            //},
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'text',
                label: 'Result Note',
                name: 'resultNote'
            }
        ],

        'borrow-lend': [
            {
                col: 'col-md-3',
                type: 'input',
                inputType: 'text',
                label: 'Borrow Document No',
                name: 'documentNo'
            },
            {
                col: 'col-md-3',
                type: 'select',
                label: 'Borrow Warehouse',
                name: 'warehouseId',
                source: () => AppState.lookups.warehouses,
                value: vm => vm.warehouseId
            },
            {
                col: 'col-md-3',
                type: 'input',
                inputType: 'date',
                label: 'Borrow Date',
                name: 'borrowDate',
                value: () => today()
            },
            {
                col: 'col-md-3',
                type: 'input',
                inputType: 'date',
                label: 'Due Date',
                name: 'dueDate',
                value: () => today()
            },
            {
                col: 'col-md-3',
                type: 'custom',
                render: () => UI.inputBorrorer(
                    'Borrower',
                    'text',
                    '',
                    'borrower',
                    'Code-Name'
                )
            },
            {
                col: 'col-md-3',
                type: 'input',
                inputType: 'text',
                label: 'Borrow Department',
                name: 'borrowDepartment'
            },
            {
                col: 'col-md-3',
                type: 'input',
                inputType: 'text',
                label: 'Approver',
                name: 'approvedBy'
            },
            {
                col: 'col-md-3',
                type: 'input',
                inputType: 'text',
                label: 'Borrower Phone',
                name: 'borrowerPhone'
            },
            {
                col: 'col-md-3',
                type: 'input',
                inputType: 'text',
                label: 'Department Owner',
                name: 'departmentOwner'
            },
            {
                col: 'col-md-9',
                type: 'input',
                inputType: 'text',
                label: 'Purpose',
                name: 'purpose'
            }
        ],

        'borrow-return': [
            {
                col: 'col-md-3',
                type: 'input',
                inputType: 'text',
                label: 'Borrow Document No',
                name: 'borrowDocumentNo'
            },
            {
                col: 'col-md-3',
                type: 'input',
                inputType: 'date',
                label: 'Return Date',
                name: 'returnDate',
                value: () => today()
            },
            {
                col: 'col-md-3',
                type: 'select',
                label: 'Return Warehouse',
                name: 'warehouseId',
                source: () => AppState.lookups.warehouses,
                value: vm => vm.warehouseId
            },
            {
                col: 'col-md-3',
                type: 'custom',
                render: () => UI.inputBorrorer(
                    'Returner',
                    'text',
                    '',
                    'returner',
                    'Code-Name'
                )
            },
            {
                col: 'col-md-3',
                type: 'input',
                inputType: 'text',
                label: 'Borrow Department',
                name: 'borrowDepartment'
            },
            {
                col: 'col-md-3',
                type: 'input',
                inputType: 'text',
                label: 'Approver',
                name: 'approvedBy'
            },
            {
                col: 'col-md-3',
                type: 'input',
                inputType: 'text',
                label: 'Borrower Phone',
                name: 'borrowerPhone'
            },
            {
                col: 'col-md-3',
                type: 'input',
                inputType: 'text',
                label: 'Department Owner',
                name: 'departmentOwner'
            },
            
        ],

        default: [
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'text',
                label: 'Borrow Document',
                name: 'borrowDocumentNo',
                
            },
            {
                col: 'col-md-4',
                type: 'input',
                inputType: 'date',
                label: 'Return Date',
                name: 'returnDate',
                value: () => today()
            },
            {
                col: 'col-md-4',
                type: 'select',
                label: 'Return Warehouse',
                name: 'warehouseId',
                source: () => AppState.lookups.warehouses,
                value: vm => vm.warehouseId
            }
        ]
    }
};


/* =========================
   COMMON TD HELPERS
========================= */

function tdStt(index) {
    return `<td class="col-stt">${index}</td>`;
}

function tdItem() {
    return `<td class="col-item-code"><input class="form-control form-control-sm" name="itemCode"></td>`;
}

function tdSerial() {
    return `<td class="col-serial"><input class="form-control form-control-sm" name="serialNumber"></td>`;
}

function tdBin() {
    return `<td class="col-bin"><input class="form-control form-control-sm" name="binCode"></td>`;
}
function tdTargetBin() {
    return `<td class="col-bin"><input class="form-control form-control-sm" name="targetBinCode"></td>`;
}

//function selectInline(name, options, disabled = false, defaultValue = '') {
//    const cbId = 'cbi_' + name + '_' + Math.random().toString(36).slice(2, 7);
//    const opts = (options || []).map(x => {
//        const val  = String(x.id ?? x.value ?? '');
//        const text = x.text ?? x.name ?? val;
//        return `<div class="cbo-option" data-value="${UI.esc(val)}">${UI.esc(text)}</div>`;
//    }).join('');
//    const defStr  = String(defaultValue ?? '');
//    const defOpt  = (options || []).find(x => String(x.id ?? x.value ?? '') === defStr);
//    const defText = defOpt ? (defOpt.text ?? defOpt.name ?? defStr) : '';
//    return `<div class="cbo-wrap cbo-inline" id="${cbId}" data-name="${UI.esc(name)}" ${disabled ? 'data-disabled="true"' : ''}>` +
//        `<input type="text" class="form-control form-control-sm cbo-input" autocomplete="off" placeholder="--" value="${UI.esc(defText)}" ${disabled ? 'disabled' : ''} />` +
//        `<input type="hidden" name="${UI.esc(name)}" value="${UI.esc(defStr)}" class="cbo-value" />` +
//        `<div class="cbo-dropdown">${opts || '<div class="cbo-option cbo-empty">--</div>'}</div>` +
//        `</div>`;
//}

function selectInline(name, options, disabled = false, defaultValue = '') {
    return `<select class="form-select form-select-sm" name="${UI.esc(name)}" ${disabled ? 'disabled' : ''}><option value="">--</option>${(options || []).map(x => `<option value="${UI.esc(x.id)}" ${String(x.id) === String(defaultValue) ? 'selected' : ''} data-base-text="${UI.esc(x.text)}">${UI.esc(x.text)}</option>`).join('')}</select>`;
}

function tdDelete() {
    return `<td class="col-action">${removeBtn()}</td>`;
}

function removeBtn() {
    return '<button class="btn btn-light btn-sm btn-remove-line"><i class="bi bi-x-lg"></i></button>';
}



