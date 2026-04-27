Router.register('dashboard', async function(){
  $('#app').html(UI.pageHeader('Dashboard','Home / Dashboard','<div class="d-flex gap-2 flex-wrap"><button class="btn btn-outline-secondary" id="btnExportDashboardPdf"><i class="bi bi-filetype-pdf me-2"></i>' + UI.t('Export PDF') + '</button><button class="btn btn-outline-secondary" id="btnReloadDashboard"><i class="bi bi-arrow-clockwise me-2"></i>' + UI.t('Refresh') + '</button></div>') +
    `<div id="dashboardPrintable">
    <div class="card mb-3"><div class="card-body">
      <div class="row g-3 align-items-end">
        <div class="col-md-4">${UI.select('Summary Warehouse','summaryWarehouseId', AppState.lookups.warehouses)}</div>
        <div class="col-md-2"><button class="btn btn-primary w-100" id="btnLoadDashboard">${UI.t('Load')}</button></div>
      </div>
    </div></div>
    <div id="dashboardCards">${UI.loading()}</div>
    <div class="row g-3 mt-1">
      <div class="col-xl-6">
        <div class="card"><div class="card-body">
          <div class="d-flex justify-content-between align-items-start gap-2 mb-2">
            <h5 class="fw-bold mb-0">${UI.t('Stock by Warehouse')}</h5>
            <div style="min-width:180px">${UI.select('Status','stockWarehouseStatus', AppState.lookups.statuses, 'InStock')}</div>
          </div>
          <div id="stockWarehouseChart" class="chart-area">${UI.loading()}</div>
        </div></div>
      </div>
      <div class="col-xl-6">
        <div class="card"><div class="card-body">
          <div class="d-flex justify-content-between align-items-start gap-2 mb-2">
            <h5 class="fw-bold mb-0">${UI.t('Stock by Status')}</h5>
            <div style="min-width:220px">${UI.select('Warehouse','stockStatusWarehouseId', AppState.lookups.warehouses)}</div>
          </div>
          <div id="stockStatusChart" class="chart-area">${UI.loading()}</div>
        </div></div>
      </div>
      <div class="col-xl-6">
        <div class="card"><div class="card-body">
          <div class="d-flex justify-content-between align-items-start gap-2 mb-2">
            <h5 class="fw-bold mb-0">${UI.t('Movement Trend')}</h5>
            <div class="d-flex gap-2" style="min-width:360px">
              ${UI.select('Warehouse','trendWarehouseId', AppState.lookups.warehouses)}
              ${UI.select('Days','trendDays', dashboardDaysOptions(), '14')}
            </div>
          </div>
          <div class="small text-muted mb-2">${UI.t('Movement Trend shows number of stock movement events by day.')}</div>
          <div id="movementTrendChart" class="chart-area">${UI.loading()}</div>
        </div></div>
      </div>
      <div class="col-xl-6">
        <div class="card"><div class="card-body">
          <div class="d-flex justify-content-between align-items-start gap-2 mb-2">
            <h5 class="fw-bold mb-0">${UI.t('Movement by Operation')}</h5>
            <div class="d-flex gap-2" style="min-width:360px">
              ${UI.select('Warehouse','actionWarehouseId', AppState.lookups.warehouses)}
              ${UI.select('Days','actionDays', dashboardDaysOptions(), '14')}
            </div>
          </div>
          <div id="movementActionChart" class="chart-area">${UI.loading()}</div>
        </div></div>
      </div>
      <div class="col-xl-6">
        <div class="card"><div class="card-body">
          <div class="d-flex justify-content-between align-items-start gap-2 mb-2">
            <h5 class="fw-bold mb-0">${UI.t('Stock by Category')}</h5>
            <div class="d-flex gap-2" style="min-width:360px">
              ${UI.select('Warehouse','categoryWarehouseId', AppState.lookups.warehouses)}
              ${UI.select('Status','categoryStatus', AppState.lookups.statuses, 'InStock')}
            </div>
          </div>
          <div id="stockCategoryChart" class="chart-area">${UI.loading()}</div>
        </div></div>
      </div>
      <div class="col-xl-6">
        <div class="card"><div class="card-body">
          <div class="d-flex justify-content-between align-items-start gap-2 mb-2">
            <h5 class="fw-bold mb-0">${UI.t('Location Utilization')}</h5>
            <div style="min-width:220px">${UI.select('Warehouse','utilizationWarehouseId', AppState.lookups.warehouses)}</div>
          </div>
          <div id="locationUtilizationChart" class="chart-area">${UI.loading()}</div>
        </div></div>
      </div>
      <div class="col-xl-6">
        <div class="card"><div class="card-body">
          <div class="d-flex justify-content-between align-items-start gap-2 mb-2">
            <h5 class="fw-bold mb-0">${UI.t('Borrow Overdue Aging')}</h5>
            <div style="min-width:220px">${UI.select('Warehouse','overdueWarehouseId', AppState.lookups.warehouses)}</div>
          </div>
          <div id="borrowOverdueChart" class="chart-area">${UI.loading()}</div>
        </div></div>
      </div>
    </div></div>`);

  $('#btnReloadDashboard').on('click', () => Router.go('dashboard'));
  $('#btnExportDashboardPdf').on('click', () => window.print());
  $('#btnLoadDashboard, #app [name="summaryWarehouseId"]').on('click change', loadDashboardSummary);
  $(document).off('click.dashboardCard').on('click.dashboardCard', '.dashboard-card', function(){
    const status = $(this).data('status') || '';
    const route = $(this).data('route') || 'inventory';
    AppState.inventoryPreset = { warehouseId: $('#app [name="summaryWarehouseId"]').val() || '', status };
    Router.go(route);
  });
  $('#app [name="stockWarehouseStatus"]').on('change', loadStockByWarehouseChart);
  $('#app [name="stockStatusWarehouseId"]').on('change', loadStockByStatusChart);
  $('#app [name="trendWarehouseId"], #app [name="trendDays"]').on('change', loadMovementTrendChart);
  $('#app [name="actionWarehouseId"], #app [name="actionDays"]').on('change', loadMovementActionChart);
  $('#app [name="categoryWarehouseId"], #app [name="categoryStatus"]').on('change', loadStockByCategoryChart);
  $('#app [name="utilizationWarehouseId"]').on('change', loadLocationUtilizationChart);
  $('#app [name="overdueWarehouseId"]').on('change', loadBorrowOverdueChart);

  await Promise.all([
    loadDashboardSummary(),
    loadStockByWarehouseChart(),
    loadStockByStatusChart(),
    loadMovementTrendChart(),
    loadMovementActionChart(),
    loadStockByCategoryChart(),
    loadLocationUtilizationChart(),
    loadBorrowOverdueChart()
  ]);
});

async function loadDashboardSummary(){
  const summary = await UI.api('/Dashboard/Summary', { query: { warehouseId: $('#app [name="summaryWarehouseId"]').val() || '' } });
  const cards = [
    ['Total items', summary.totalItems, 'bi-box-seam', 'All item instances in scope', 'inventory', ''],
    ['In stock', summary.inStock, 'bi-check-circle', 'Available for operations', 'inventory', 'InStock'],
    ['Repairing', summary.repairing, 'bi-tools', 'Items at repair vendors', 'inventory', 'Repairing'],
    ['Lent out', summary.lentOut, 'bi-hand-thumbs-up', 'Borrowed by external parties', 'inventory', 'LentOut'],
    ['Overdue return', summary.overdueReturn, 'bi-alarm', 'Borrow lines past due date', 'borrow-return', 'LentOut'],
    ['Damaged or lost', summary.damagedOrLost, 'bi-exclamation-triangle', 'Exception status', 'inventory', 'Damaged']
  ];
  $('#dashboardCards').html(`<div class="row g-3">${cards.map(c => `
    <div class="col-md-6 col-xl-4">
      <button type="button" class="card dashboard-card text-start" data-route="${UI.esc(c[4])}" data-status="${UI.esc(c[5])}"><div class="card-body d-flex justify-content-between align-items-center">
        <div><div class="text-muted small">${UI.esc(UI.t(c[0]))}</div><div class="display-6 fw-bold">${UI.esc(c[1])}</div><div class="small text-muted">${UI.esc(UI.t(c[3]))}</div></div>
        <div class="brand-icon"><i class="bi ${c[2]}"></i></div>
      </div></button>
    </div>`).join('')}</div>`);
}

async function loadStockByWarehouseChart(){
  const rows = await UI.api('/Dashboard/StockByWarehouse', { query: { status: $('#app [name="stockWarehouseStatus"]').val() || '' } });
  $('#stockWarehouseChart').html(barChart(rows, 'Warehouse'));
}

async function loadStockByStatusChart(){
  const rows = await UI.api('/Dashboard/StockByStatus', { query: { warehouseId: $('#app [name="stockStatusWarehouseId"]').val() || '' } });
  $('#stockStatusChart').html(donutChart(rows, 'ItemStatus'));
}

async function loadMovementTrendChart(){
  const rows = await UI.api('/Dashboard/MovementTrend', { query: { warehouseId: $('#app [name="trendWarehouseId"]').val() || '', days: $('#app [name="trendDays"]').val() || '14' } });
  $('#movementTrendChart').html(lineChart(rows));
}

async function loadMovementActionChart(){
    const rows = await UI.api('/Dashboard/MovementByAction', { query: { warehouseId: $('#app [name="actionWarehouseId"]').val() || '', days: $('#app [name="actionDays"]').val() || '14' } });
    //$('#movementActionChart').html(barChart(rows.map(x => ({ ...x, label: UI.enum('MovementActionType', x.key || x.label) })), 'Operation'));
  $('#movementActionChart').html(columnChart(rows.map(x => ({ ...x, label: UI.enum('MovementActionType', x.key || x.label) }))));
}

async function loadStockByCategoryChart(){
  const rows = await UI.api('/Dashboard/StockByCategory', { query: { warehouseId: $('#app [name="categoryWarehouseId"]').val() || '', status: $('#app [name="categoryStatus"]').val() || '' } });
  $('#stockCategoryChart').html(barChart(rows));
}

async function loadLocationUtilizationChart(){
    const rows = await UI.api('/Dashboard/LocationUtilization', { query: { warehouseId: $('#app [name="utilizationWarehouseId"]').val() || '' } });
    // $('#locationUtilizationChart').html(donutChart(rows.map(x => ({ ...x, label: UI.t(x.label) }))));
  $('#locationUtilizationChart').html(gaugeChart(rows.map(x => ({ ...x, label: UI.t(x.label) }))));
}

async function loadBorrowOverdueChart(){
  const rows = await UI.api('/Dashboard/OverdueBorrowAging', { query: { warehouseId: $('#app [name="overdueWarehouseId"]').val() || '' } });
  $('#borrowOverdueChart').html(barChart(rows.map(x => ({ ...x, label: UI.t(x.label) }))));
}

function dashboardDaysOptions(){
  return [{ id:'7', text:UI.t('Last 7 days') }, { id:'14', text:UI.t('Last 14 days') }, { id:'30', text:UI.t('Last 30 days') }, { id:'90', text:UI.t('Last 90 days') }];
}

function barChart(rows){
  if(!rows || !rows.length) return UI.empty('No data');
  const width = 620;
  const rowH = 42;
  const labelW = 210;
  const barW = 330;
  const height = Math.max(120, rows.length * rowH + 26);
  const max = Math.max(...rows.map(x => Number(x.value || 0)), 1);
  return `<svg class="svg-chart svg-bar-chart" viewBox="0 0 ${width} ${height}" role="img" aria-label="${UI.t('Dashboard')}">
    ${rows.map((x, i) => {
      const y = 18 + i * rowH;
      const value = Number(x.value || 0);
      const w = Math.max(value > 0 ? 4 : 0, Math.round((value / max) * barW));
      return `<text x="0" y="${y + 18}" font-size="12" font-weight="700" fill="#111827">${UI.esc(truncateChartLabel(x.label))}</text>
        <rect x="${labelW}" y="${y + 6}" width="${barW}" height="16" rx="4" fill="#eef2ff"></rect>
        <rect x="${labelW}" y="${y + 6}" width="${w}" height="16" rx="4" fill="${chartColor(i)}"></rect>
        <text x="${labelW + barW + 12}" y="${y + 18}" font-size="12" font-weight="700" fill="#111827">${UI.esc(value)}</text>`;
    }).join('')}
  </svg>`;
}

function donutChart(rows, enumType){
  if(!rows || !rows.length) return UI.empty('No data');
  const total = rows.reduce((sum, x) => sum + Number(x.value || 0), 0) || 1;
  const radius = 64;
  const circumference = 2 * Math.PI * radius;
  let offset = 0;
  return `<div class="donut-wrap">
    <svg class="svg-donut" viewBox="0 0 180 180" role="img" aria-label="${UI.t('Stock by Status')}">
      <circle cx="90" cy="90" r="${radius}" fill="none" stroke="#eef2ff" stroke-width="28"></circle>
      ${rows.map((x, i) => {
        const value = Number(x.value || 0);
        const length = (value / total) * circumference;
        const segment = `<circle cx="90" cy="90" r="${radius}" fill="none" stroke="${chartColor(i)}" stroke-width="28" stroke-linecap="butt" stroke-dasharray="${length} ${circumference - length}" stroke-dashoffset="${-offset}" transform="rotate(-90 90 90)"></circle>`;
        offset += length;
        return segment;
      }).join('')}
      <circle cx="90" cy="90" r="38" fill="#fff"></circle>
      <text x="90" y="86" text-anchor="middle" font-size="12" fill="#6b7280">${UI.esc(UI.t('Total items'))}</text>
      <text x="90" y="106" text-anchor="middle" font-size="20" font-weight="800" fill="#111827">${UI.esc(total)}</text>
    </svg>
    <div class="chart-legend">${rows.map((x, i) => `<div class="legend-row">
      <div class="legend-key"><span class="legend-swatch" style="background:${chartColor(i)}"></span><span>${UI.esc(enumType ? UI.enum(enumType, x.key || x.label) : x.label)}</span></div>
      <b>${UI.esc(x.value)}</b>
    </div>`).join('')}</div>
  </div>`;
}

function lineChart(rows){
  if(!rows || !rows.length) return UI.empty('No data');
  const width = 620;
  const height = 220;
  const max = Math.max(...rows.map(x => Number(x.value || 0)), 1);
  const step = rows.length > 1 ? width / (rows.length - 1) : width;
  const points = rows.map((x, i) => {
    const y = height - 28 - ((Number(x.value || 0) / max) * (height - 58));
    return `${Math.round(i * step)},${Math.round(y)}`;
  }).join(' ');
  return `<svg class="svg-chart" viewBox="0 0 ${width} ${height}" role="img" aria-label="${UI.t('Movement Trend')}">
    <polyline points="${points}" fill="none" stroke="#1e5bff" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"></polyline>
    ${rows.map((x, i) => {
      const cx = Math.round(i * step);
      const cy = height - 28 - ((Number(x.value || 0) / max) * (height - 58));
      return `<circle cx="${cx}" cy="${cy}" r="4" fill="#1e5bff"></circle><text x="${cx}" y="${height - 6}" text-anchor="middle" font-size="11" fill="#6b7280">${UI.esc(x.label)}</text>`;
    }).join('')}
  </svg>`;
}

function columnChart(rows){
  if(!rows || !rows.length) return UI.empty('No data');
  const width = 620;
  const height = 240;
  const max = Math.max(...rows.map(x => Number(x.value || 0)), 1);
  const gap = 14;
  const count = Math.max(rows.length, 1);
  const barW = Math.max(28, Math.floor((width - gap * (count + 1)) / count));
  return `<svg class="svg-chart" viewBox="0 0 ${width} ${height}" role="img" aria-label="${UI.t('Movement by Operation')}">
    <line x1="0" y1="${height - 36}" x2="${width}" y2="${height - 36}" stroke="#e5e7eb"></line>
    ${rows.map((x, i) => {
      const value = Number(x.value || 0);
      const barH = Math.round((value / max) * (height - 76));
      const xPos = gap + i * (barW + gap);
      const y = height - 36 - barH;
      return `<rect x="${xPos}" y="${y}" width="${barW}" height="${barH}" rx="6" fill="${chartColor(i)}"></rect>
        <text x="${xPos + barW / 2}" y="${Math.max(14, y - 6)}" text-anchor="middle" font-size="12" font-weight="800" fill="#111827">${UI.esc(value)}</text>
        <text x="${xPos + barW / 2}" y="${height - 12}" text-anchor="middle" font-size="10" fill="#6b7280">${UI.esc(truncateChartLabel(x.label, 12))}</text>`;
    }).join('')}
  </svg>`;
}

function gaugeChart(rows){
  if(!rows || !rows.length) return UI.empty('No data');
  const occupied = Number((rows.find(x => x.key === 'OccupiedBins') || rows[0]).value || 0);
  const total = rows.reduce((sum, x) => sum + Number(x.value || 0), 0) || 1;
  const percent = Math.round((occupied / total) * 100);
  const radius = 74;
  const circumference = Math.PI * radius;
  const fill = Math.max(0, Math.min(circumference, circumference * percent / 100));
  return `<div class="gauge-wrap">
    <svg class="svg-gauge" viewBox="0 0 220 140" role="img" aria-label="${UI.t('Location Utilization')}">
      <path d="M36 112a74 74 0 0 1 148 0" fill="none" stroke="#eef2ff" stroke-width="24" stroke-linecap="round"></path>
      <path d="M36 112a74 74 0 0 1 148 0" fill="none" stroke="#1e5bff" stroke-width="24" stroke-linecap="round" stroke-dasharray="${fill} ${circumference}"></path>
      <text x="110" y="88" text-anchor="middle" font-size="30" font-weight="800" fill="#111827">${percent}%</text>
      <text x="110" y="112" text-anchor="middle" font-size="12" fill="#6b7280">${UI.esc(UI.t('Occupied bins'))}: ${occupied}/${total}</text>
    </svg>
    <div class="chart-legend">${rows.map((x, i) => `<div class="legend-row"><div class="legend-key"><span class="legend-swatch" style="background:${chartColor(i)}"></span><span>${UI.esc(UI.t(x.label))}</span></div><b>${UI.esc(x.value)}</b></div>`).join('')}</div>
  </div>`;
}

function chartColor(index){
  return ['#1e5bff', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#06b6d4', '#64748b', '#84cc16'][index % 8];
}

//function truncateChartLabel(label){
function truncateChartLabel(label, length=30){
    const text = String(label || '-');
    //  return text.length > 30 ? `${text.slice(0, 29)}...` : text;
  return text.length > length ? `${text.slice(0, length - 1)}...` : text;
}
