window.OperationModel = {
  configs: {
    inbound: ['Inbound Create','Home / Inventory / New Inbound',['Source','Warehouse','Inbound Date','Document No Auto'],'Line grid: item, serial/barcode, qty, bin location, condition, note. Save & Post creates stock/location/history.'],
    move: ['Move Location','Home / Inventory / Move Location',['Current Item','Target Warehouse','Target Bin','Note'],'Only InStock allowed. Save updates CurrentItemLocation and ItemMovementHistory.'],
    adjustment: ['Adjustment','Home / Inventory / Adjustment',['Warehouse','Adjustment Date','Reason','Document No Auto'],'Used to correct stock, status or location with mandatory reason. Save & Post creates adjustment transaction and append-only history.'],
    'inventory-check': ['Inventory Check','Home / Inventory / Inventory Check',['Warehouse','Session Date','Count Method','Responsible Staff'],'Scan or import actual count, compare with system stock, then generate adjustment when approved by current user.'],
    'repair-send': ['Repair Send','Home / Repair / Send To Repair',['Vendor','Send Date','Expected Return','Reason'],'Add InStock or Damaged items. Each line requires an external destination outside warehouse. Save & Post changes status Repairing and writes history.'],
    'repair-receive': ['Repair Receive','Home / Repair / Receive From Repair',['Repair Document','Result','Target Bin','New Serial if Replaced'],'Each repaired item requires its own destination bin. Replaced requires unique new serial and old-new relationship.'],
    'borrow-lend': ['Borrow Lend','Home / Borrow / Lend',['Borrower','Due Date','Purpose','Reminder'],'Select a borrow warehouse first, then choose in-stock items and external destination for each line.'],
    'borrow-return': ['Borrow Return','Home / Borrow / Return',['Borrow Document','Return Date','Condition','Target Bin / Status'],'Supports partial return. Normal requires target bin. Damaged/Lost controls target status.']
  }
};
