window.AppConfig = {
  defaultRoute: 'tracking',
  menu: [
    ['dashboard','bi-speedometer2','Dashboard'],
    ['tracking','bi-upc-scan','Tracking'],
    ['inventory','bi-boxes','Inventory List'],
    ['inbound','bi-plus-circle','Inbound Create'],
    ['move','bi-arrow-left-right','Move Location'],
    ['adjustment','bi-clipboard-check','Adjustment'],
    ['inventory-check','bi-qr-code-scan','Inventory Check'],
    ['repair-send','bi-tools','Repair Send'],
    ['repair-receive','bi-wrench-adjustable','Repair Receive'],
    ['borrow-lend','bi-hand-thumbs-up','Borrow Lend'],
    ['borrow-return','bi-arrow-return-left','Borrow Return'],
    ['warehouse-structure','bi-diagram-3','Warehouse Structure'],
    ['master-data','bi-database','Master Data'],
    ['import','bi-file-earmark-spreadsheet','Import Excel'],
    ['reports','bi-clipboard-data','Reports / Audit'],
    ['system','bi-shield-lock','System']
  ],
  statusMeta: {
    InStock: ['bi-check-circle','status-instock'],
    Reserved: ['bi-clock','status-reserved'],
    Repairing: ['bi-tools','status-repairing'],
    LentOut: ['bi-hand-thumbs-up','status-lentout'],
    Returned: ['bi-arrow-return-left','status-returned'],
    Damaged: ['bi-exclamation-triangle','status-damaged'],
    Lost: ['bi-x-octagon','status-lost'],
    Disposed: ['bi-trash','status-disposed'],
    InTransit: ['bi-truck','status-intransit']
  }
};
