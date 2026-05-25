window.OperationPayloadConfig = {

    inbound: {

        row: val => ({
            itemCode: val('itemCode'),
            serialNumber: val('serialNumber'),
            barcode: val('serialNumber'),
            mT: val('mt'),
            quantity: 1,
            binCode: val('binCode'),
            condition: val('condition'),
            note: val('lineNote')
        }),

        payload: (h, rows, intOrNull) => ({
            sourceExternalPartyId: intOrNull(h('sourceExternalPartyId')),
            warehouseId: intOrNull(h('warehouseId')),
            documentDate: h('documentDate'),
            documentNo: h('documentNo'),
            receiver: h('receiver'),
            receiverPhone: h('receiverPhone'),
            receiverDepartment: h('receiverDepartment'),
            departmentOwner: h('departmentOwner'),
            ownerName: h('ownerName') || null,
            approvedBy: h('approvedBy'),
            note: h('note'),
            lines: rows
        }),

        validateRows: rows => validateInboundRows(rows)
    },


    move: {

        row: val => ({
            itemCode: val('itemCode'),
            serialNumber: val('serialNumber'),
            targetBinCode: val('targetBinCode'),
            note: val('lineNote')
        }),

        payload: (h, rows, intOrNull) => ({
            warehouseId: intOrNull(h('warehouseId')),
            documentDate: h('documentDate'),
            note: h('note'),
            lines: rows
        })
    },

    adjustment: {

        row: val => ({
            itemCode: val('itemCode'),
            serialNumber: val('serialNumber'),
            newStatus: val('newStatus'),
            targetBinCode: val('targetBinCode'),
            reason: "inventory check",
            //reason: val('reason'),
            newSerialNumber: val('newSerialNumber')
        }),

        payload: (h, rows, intOrNull) => ({
            warehouseId: intOrNull(h('warehouseId')),
            documentDate: h('documentDate'),
            reason: h('reason'),
            lines: rows
        })
    },

    'inventory-check': {

        row: val => ({
            itemCode: val('itemCode'),
            serialNumber: val('serialNumber'),
            binCode: val('binCode'),
            result: val('result'),
            note: ''
        }),

        payload: (h, rows, intOrNull) => ({
            warehouseId: intOrNull(h('warehouseId')),
            documentPeriodType: h('documentPeriodType'),
            sessionDate: h('sessionDate'),
            countMethod: h('countMethod'),
            responsibleStaff: h('responsibleStaff'),
            lines: rows
        })
    },

    'repair-send': {

        row: val => ({
            itemCode: val('itemCode'),
            serialNumber: val('serialNumber'),
            targetExternalLocation: val('targetExternalLocation'),
            note: val('lineNote')
        }),

        payload: (h, rows, intOrNull) => ({
            documentNo: (h('documentNo') || '').trim().toUpperCase() || null,
            repairVendorCode: h('repairVendorCode'),
            sendDate: h('sendDate'),
            expectedReturnDate: h('expectedReturnDate') || null,
            reason: h('reason'),
            lines: rows
        })
    },

    'repair-receive': {

        row: val => ({
            itemCode: val('itemCode'),
            serialNumber: val('serialNumber'),
            targetBinCode: val('targetBinCode'),
            newSerialNumber: '',
            note: val('lineNote'),
            result: val('result')
        }),

        payload: (h, rows, intOrNull) => ({
            repairDocumentId: intOrNull(h('repairDocumentId')) || 0,
            repairDocumentNo: (h('repairDocumentNo') || '').trim().toUpperCase() || null,
            resultNote: h('resultNote'),
            lines: rows
        })
    },

    'borrow-lend': {

        row: val => ({
            itemCode: val('itemCode'),
            serialNumber: val('serialNumber'),
            targetExternalLocation: val('targetExternalLocation'),
            note: val('lineNote')
        }),

        payload: (h, rows, intOrNull) => ({
            documentNo: h('documentNo'),
            warehouseId: intOrNull(h('warehouseId')),
            borrower: h('borrower'),
            borrowDate: h('borrowDate'),
            dueDate: h('dueDate'),
            purpose: h('purpose'),
            borrowDepartment: h('borrowDepartment'),
            approvedBy: h('approvedBy'),
            borrowerPhone: h('borrowerPhone'),
            departmentOwner: h('departmentOwner'),
            lines: rows
        })
    },

    'borrow-return': {

        row: val => ({
            itemCode: val('itemCode'),
            serialNumber: val('serialNumber'),
            condition: val('condition'),
            targetBinCode: val('targetBinCode'),
            note: ''
        }),

        payload: (h, rows, intOrNull) => ({
            BorrowDocumentNo: h('borrowDocumentNo'),
            returnDate: h('returnDate'),
            returner: h('returner'),
            borrowDepartment: h('borrowDepartment'),
            approvedBy: h('approvedBy'),
            borrowerPhone: h('borrowerPhone'),
            departmentOwner: h('departmentOwner'),
            lines: rows
        })
    }

};
