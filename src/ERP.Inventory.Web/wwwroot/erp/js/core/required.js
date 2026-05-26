window.OperationRequiredConfig = {
    inbound: {
        headers: {
            documentNo: 'Document No',
            warehouseId: 'Warehouse',
            documentDate: 'Inbound Date'
        },
        rows: {
            itemCode: "PN",
            serialNumber: 'SN',
            binCode: "Bin",
        }
    },

    move: {
        headers: {
            warehouseId: 'Warehouse',
            serialNumber: 'SN',
            documentDate: 'Move Date'
        },

        rows: {
            itemCode: "PN",
            serialNumber: 'SN',
            targetBinCode: 'Target Bin',

        }
    },

    adjustment: {
        headers: {
            warehouseId: 'Warehouse',
            documentDate: 'Adjustment Date',
            reason: 'Reason'
        },

        rows: {
            itemCode: "PN",
            newStatus: 'New Status',
            serialNumber: 'SN',
            targetBinCode: "Target Bin",
            //reason: 'Reason'
        },

        //conditionalRows: [
        //    {
        //        when: row => !['Lost', 'Disposed'].includes(row.newStatus),
        //        field: 'targetBinCode',
        //        label: 'Target Bin'
        //    }
        //]
    },

    'inventory-check': {
        headers: {
            warehouseId: 'Warehouse',
            sessionDate: 'Session Date',
            countMethod: 'Count Method',
            responsibleStaff: 'Responsible Staff'
        },
        rows: {
            itemCode: "PN",
            serialNumber: "SN",
            binCode: "Bin",
        }
    },

    'repair-send': {
        headers: {
            repairSenderCode: 'RepairSenderCode',
            repairSenderName: 'RepairSenderName',
            sendDate: 'Send Date',
            //reason: 'Reason'
        },

        rows: {
            itemCode: "PN",
            serialNumber: "SN",
            targetExternalLocation: 'External Destination',
            //lineNote: 'Reason',
        }
    },

    'repair-receive': {
        headers: {
            repairDocumentNo: 'Repair Document No',
            resultNote: 'Result'
        },

        rows: {
            itemCode: "PN",
            targetBinCode: 'Target Bin',
            serialNumber: 'SN',
            result: 'Result',
            //lineNote: "Reason",
        }
    },

    'borrow-lend': {
        headers: {
            documentNo: 'Borrow Document No',
            warehouseId: 'Borrow Warehouse',
            borrowerCode: 'BorrowerCode',
            borrowerName: 'BorrowerName',
            borrowDate: 'Borrow Date',
            dueDate: 'Due Date',
            borrowDepartment: 'Borrow Department',
            approvedBy: 'Approver',
            borrowerPhone: 'Borrower Phone',
            departmentOwner: 'Department Owner',
            purpose: 'Purpose'
        },

        rows: {
            itemCode: "PN",
            //lineNote: "Note",
            targetExternalLocation: 'External Destination',
            serialNumber: 'SN',
        }
    },

    'borrow-return': {
        headers: {
            borrowDocumentNo: 'Borrow Document No',
            returnDate: 'Return Date',
            returnerName: 'ReturnerName',
            returnerCode: 'ReturnerCode',
            borrowDepartment: 'Borrow Department',
            warehouseId: 'Return Warehouse'
        },

        rows: {
            itemCode: "PN",
            //condition: 'Condition',
            serialNumber: 'SN',
            targetBinCode: "Target Bin",
        },

        //conditionalRows: [
        //    {
        //        when: row => row.condition !== 'Lost',
        //        field: 'targetBinCode',
        //        label: 'Target Bin'
        //    }
        //]
    }
};