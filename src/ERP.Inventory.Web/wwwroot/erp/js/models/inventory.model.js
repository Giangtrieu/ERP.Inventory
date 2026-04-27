window.InventoryModel = {
  sampleItem: {
    itemInstanceId: 921,
    itemCode: 'LAP-DELL-7420',
    name: 'Dell Latitude 7420',
    serialNumber: 'SN-DELL-7420-00921',
    barcode: 'BC-8939912001',
    status: 'InStock',
    holderName: 'Kho Hà Nội / Zone A',
    referenceDocumentNo: 'INB-2026-000183',
    locationPath: 'HN-WH-01 / Zone A / Rack 03 / Shelf 02 / Bin B05',
    updatedAt: '25/04/2026 09:42',
    updatedBy: 'Nguyen Van A',
    permitted: true
  },
  historyRows: [
    ['25/04/2026 09:42','MoveLocation','Bin A01','Bin B05','InStock','MOV-2026-000041','Nguyen Van A'],
    ['20/04/2026 15:20','Inbound','Supplier','HN-WH-01','InStock','INB-2026-000183','Tran Thi B'],
    ['19/04/2026 10:10','ImportOpening','Excel','Staging','Reserved','IMP-2026-000022','System']
  ],
  rows: [
    ['ASSET-1001','SN-2026-001','InStock','HN-WH-01 / Bin B05','Warehouse'],
    ['ASSET-1002','SN-2026-002','Repairing','RepairVendor / FPT Service','Vendor'],
    ['ASSET-1003','SN-2026-003','LentOut','Borrower / IT Department','Le Van C'],
    ['ASSET-1004','SN-2026-004','Damaged','HN-WH-01 / Damaged Area','Warehouse'],
    ['ASSET-1005','SN-2026-005','InTransit','Transfer HN -> HCM','Logistics'],
    ['ASSET-1006','SN-2026-006','Returned','HN-WH-01 / Receiving Dock','Warehouse']
  ]
};
