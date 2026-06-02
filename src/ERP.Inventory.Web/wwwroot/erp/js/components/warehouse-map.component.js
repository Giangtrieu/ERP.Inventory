window.WarehouseMapComponent = (() => {
  const viewModes = [
    { id: 'occupancy', text: 'Occupancy' },
    { id: 'itemStatus', text: 'Item Status' },
    { id: 'itemCode', text: 'Item Code' },
    { id: 'categoryCode', text: 'Category Code' }
  ];

    let currentBins = [];

  function renderShell() {
    const defaultWarehouseId = AppState.lookups.warehouses?.[0]?.id || '';
    return `<div class="card mb-3 warehouse-map-card"><div class="card-body">
      <div class="d-flex justify-content-between align-items-start gap-3 flex-wrap mb-3">
        <div>
          <h5 class="fw-bold mb-1"><i class="bi bi-grid-3x3-gap me-2 text-primary"></i>${UI.esc(UI.t('Warehouse Map'))}</h5>
          <div class="text-muted small" id="warehouseMapSummary">${UI.esc(UI.t('Physical bin status by rack, shelf and bin.'))}</div>
        </div>
        <div class="warehouse-map-controls">
          ${UI.select('Warehouse', 'mapWarehouseId', AppState.lookups.warehouses, defaultWarehouseId)}
          ${UI.select('View Mode', 'mapViewMode', viewModes.map(x => ({ id: x.id, text: UI.t(x.text) })), 'occupancy')}
        </div>
      </div>
          <div class="warehouse-map-highlight-tools mb-3">
      <div class="d-flex gap-2 align-items-end flex-wrap">
        <div style="min-width:180px">
          ${UI.select('Search Type', 'mapSearchType', [
              { id: 'pn', text: UI.t('PN / ItemCode') },
              { id: 'sn', text: UI.t('SN / SerialNumber') },
              { id: 'barcode', text: UI.t('Barcode') }
          ], 'pn')}
        </div>

        <div class="flex-grow-1">
          <label class="form-label fw-semibold small">${UI.t('Search List')}</label>
          <input type="text"
            class="form-control"
            name="mapSearchValues"
            placeholder="VD: PN001, PN002 hoặc SN001, SN002">
        </div>

        <button type="button" class="btn btn-primary" id="btnWarehouseMapSearch">
          <i class="bi bi-search me-2"></i>${UI.t('Highlight')}
        </button>

        <button type="button" class="btn btn-outline-secondary" id="btnClearWarehouseMapHighlight">
          ${UI.t('Clear')}
        </button>
      </div>

      <div id="warehouseMapHighlightResult" class="small text-muted mt-2"></div>
    </div>
          <div id="warehouseMapLegend" class="warehouse-map-legend"></div>
          <div id="warehouseMapBody">${UI.loading()}</div>
        </div></div>`;
      }

  async function load() {
    const warehouseId = $('#app [name="mapWarehouseId"]').val() || '';
    const viewMode = $('#app [name="mapViewMode"]').val() || 'occupancy';
    if (!warehouseId) {
      $('#warehouseMapSummary').text(UI.t('Select a warehouse to view the map.'));
      $('#warehouseMapLegend').empty();
      $('#warehouseMapBody').html(UI.empty('No warehouse map data'));
      return;
    }

    $('#warehouseMapBody').html(UI.loading());
    try {
      const data = await UI.api('/Dashboard/WarehouseMap', { query: { warehouseId, viewMode } });
      render(data);
    } catch (err) {
      const status = err && err.status;
      $('#warehouseMapLegend').empty();
      $('#warehouseMapSummary').text(status === 403 ? UI.t('Access denied for current role.') : UI.t('Request failed.'));
      $('#warehouseMapBody').html(UI.empty(status === 403 ? 'Access denied for current role.' : 'No warehouse map data'));
    }
  }

  function render(data) {
    const racks = data?.racks || [];
    const occupied = Number(data?.occupiedBinCount || 0);
    const bins = Number(data?.binCount || 0);
    const empty = Number(data?.emptyBinCount || 0);
    const title = [data?.warehouseCode, data?.warehouseName].filter(Boolean).join(' - ');

    $('#warehouseMapSummary').text(`${title || UI.t('Warehouse')} | ${UI.t('Occupied bins')}: ${occupied}/${bins} | ${UI.t('Empty bins')}: ${empty}`);
    $('#warehouseMapLegend').html(renderLegend(data?.legend || []));

    if (!racks.length || !bins) {
      $('#warehouseMapBody').html(UI.empty('No warehouse map data'));
      return;
    }
      currentBins = (racks || []).flatMap(r => r.shelves || []).flatMap(s => s.bins || []);
      const $body = $('#warehouseMapBody');

      $body.html(`
  <div class="warehouse-map-grid">
    ${racks.map(renderRack).join('')}
  </div>
`);

      $body
          .off('click.warehouseMapBin')
          .on('click.warehouseMapBin', '.warehouse-map-bin', function () {
              const binId = Number($(this).data('bin-id'));

              const bin = currentBins.find(
                  x => Number(x.binLocationId) === binId
              );

              if (bin) {
                  showBinInfo(bin);
              }
          });
  }
    bindHighlightEvents();

    function renderLegend(rows) {
        if (!rows || !rows.length) return '';

        return rows.map(x => `
    <button type="button"
      class="warehouse-map-legend-item"
      data-legend-color="${UI.esc(x.color || '#ffffff')}">
      <span class="warehouse-map-swatch" style="background:${UI.esc(x.color || '#ffffff')}"></span>
      <span>${UI.esc(UI.t(x.label || x.key || '-'))}</span>
    </button>
  `).join('');
    }

  function renderRack(rack) {
    const shelves = rack.shelves || [];
    return `<section class="warehouse-map-rack">
      <div class="warehouse-map-rack-head">
        <div class="fw-bold">${UI.esc(rack.rackCode || '-')}</div>
        <div class="small text-muted">${UI.esc(rack.rackName || '')}</div>
      </div>
      <div class="warehouse-map-shelves">${shelves.map(renderShelf).join('')}</div>
    </section>`;
  }

  function renderShelf(shelf) {
    const bins = shelf.bins || [];
    return `<div class="warehouse-map-shelf">
      <div class="warehouse-map-shelf-code" title="${UI.esc(shelf.shelfName || shelf.shelfCode || '')}">${UI.esc(shelf.shelfCode || '-')}</div>
      <div class="warehouse-map-bins">${bins.map(renderBin).join('')}</div>
    </div>`;
  }

    function renderBin(bin) {
        const color = bin.color || '#ffffff';
        const textColor = bin.textColor || '#111827';
        const tooltip = tooltipText(bin);
        const items = Array.isArray(bin.items) ? bin.items : [];
        const multiBadge = Number(bin.itemCount || 0) > 1
            ? `<span class="warehouse-map-bin-count">${UI.esc(bin.itemCount)}</span>`
            : '';
        const categoryPreview = items.length
            ? `<small class="warehouse-map-bin-category">
        ${UI.esc(
                [...new Set(
                    items
                        .map(x => x.itemCode)
                        .filter(Boolean)
                )].join(', ')
            )}
      </small>`
            : '';
        return `<button type="button"
  class="warehouse-map-bin ${bin.isOccupied ? 'is-occupied' : 'is-empty'}"
  style="background:${UI.esc(color)};color:${UI.esc(textColor)}"
  title="${UI.esc(tooltip)}"
  aria-label="${UI.esc(tooltip)}"
  data-bin-id="${UI.esc(bin.binLocationId)}"
  data-color="${UI.esc(color)}"
  data-item-codes="${UI.esc((bin.itemCodes || bin.ItemCodes || []).join('|'))}"
  data-serial-numbers="${UI.esc((bin.serialNumbers || bin.SerialNumbers || []).join('|'))}"
  data-barcodes="${UI.esc((bin.barcodes || bin.Barcodes || []).join('|'))}">
  ${categoryPreview}${multiBadge}
</button>`;
    }

    function tooltipText(bin) {
        if (!bin?.isOccupied) {return `${bin.fullPath || bin.binCode || '-'}\n${UI.t('Empty')}`; }
        const items = Array.isArray(bin.items) ? bin.items : [];
        return [
            bin.fullPath || bin.binCode || '-',
            `${UI.t('Total items')}: ${bin.itemCount || items.length || 1}`,
            '',
            ...items.flatMap((item, index) => [
                items.length > 1 ? `#${index + 1}` : '',
                `${UI.t('PN')}: ${item.itemCode || '-'}`,
                item.serialNumber ? `${UI.t('SN')}: ${item.serialNumber}` : '',
                `${UI.t('Category Code')}: ${item.categoryCode || '-'}`,
                `${UI.t('Status')}: ${UI.enum('ItemStatus', item.status || '')|| item.status|| '-'}`,
                ''
            ])
        ].filter(Boolean).join('\n');
    }

  function shortBinCode(binCode) {
    const text = String(binCode || '-');
    //return text.length > 6 ? text.slice(-6) : text;
    return text;
  }

    function bindHighlightEvents() {
        $(document)
            .off('click.warehouseMapLegend')
            .on('click.warehouseMapLegend', '#warehouseMapLegend .warehouse-map-legend-item', function () {
                const color = String($(this).attr('data-legend-color') || '').toLowerCase();

                console.log('legend clicked:', color);

                $('#warehouseMapLegend .warehouse-map-legend-item').removeClass('is-active');
                $(this).addClass('is-active');

                highlightWarehouseMapBins(function ($bin) {
                    return String($bin.attr('data-color') || '').toLowerCase() === color;
                });
            });

        $(document)
            .off('click.warehouseMapSearch')
            .on('click.warehouseMapSearch', '#btnWarehouseMapSearch', function () {
                const type = $('#app [name="mapSearchType"]').val() || 'pn';
                const raw = $('#app [name="mapSearchValues"]').val() || '';

                console.log('search clicked:', type, raw);

                const values = raw
                    .split(/[\n,; ]+/)
                    .map(x => x.trim().toLowerCase())
                    .filter(Boolean);

                if (!values.length) {
                    clearWarehouseMapHighlight();
                    return;
                }

                highlightWarehouseMapBins(function ($bin) {
                    const attr =
                        type === 'sn' ? 'data-serial-numbers' :
                            type === 'barcode' ? 'data-barcodes' :
                                'data-item-codes';

                    const tokens = String($bin.attr(attr) || '')
                        .split('|')
                        .map(x => x.trim().toLowerCase())
                        .filter(Boolean);

                    return tokens.some(x => values.includes(x));
                });
            });

        $(document)
            .off('click.clearWarehouseMapHighlight')
            .on('click.clearWarehouseMapHighlight', '#btnClearWarehouseMapHighlight', clearWarehouseMapHighlight);
    }

    function highlightWarehouseMapBins(matchFn) {
        const $bins = $('.warehouse-map-bin');
        let matched = 0;

        $bins.removeClass('is-highlight is-dim');

        $bins.each(function () {
            const $bin = $(this);

            if (matchFn($bin)) {
                $bin.addClass('is-highlight');
                matched++;
            } else {
                $bin.addClass('is-dim');
            }
        });

        $('#warehouseMapHighlightResult').text(
            matched > 0
                ? `${matched} ${UI.t('bins matched')}`
                : UI.t('No matched bins')
        );
    }

    function clearWarehouseMapHighlight() {
        $('.warehouse-map-bin').removeClass('is-highlight is-dim');
        $('.warehouse-map-legend-item').removeClass('is-active');
        $('#warehouseMapHighlightResult').text('');
    }

    return { renderShell, load, render };

    function showBinInfo(bin) {
        const items = Array.isArray(bin.items) ? bin.items : [];

        const html = `
    <div class="warehouse-map-bin-panel-backdrop"></div>

    <div class="warehouse-map-bin-panel">
      <div class="warehouse-map-bin-panel-header">
        <strong>${UI.esc(bin.fullPath || bin.binCode || '-')}</strong>

        <button type="button" class="warehouse-map-close-btn">
          &times;
        </button>
      </div>

      <div class="warehouse-map-bin-panel-body">
        <div>
          <b>${UI.t('Status')}:</b>
          ${bin.isOccupied ? UI.t('Occupied bins') : UI.t('Empty')}
        </div>

        <div>
          <b>${UI.t('Total items')}:</b>
          ${UI.esc(bin.itemCount || items.length || 0)}
        </div>

        ${items.length
                ? items.map((item, index) => `
              <div class="warehouse-map-bin-item">
                <div><b>#${index + 1}</b></div>
                <div><b>${UI.t('PN')}:</b> ${UI.esc(item.itemCode || '-')}</div>
                <div><b>${UI.t('SN')}:</b> ${UI.esc(item.serialNumber || '-')}</div>
                <div><b>${UI.t('Category Code')}:</b> ${UI.esc(item.categoryCode || '-')}</div>
                <div>
                  <b>${UI.t('Status')}:</b>
                  ${UI.esc(
                    UI.enum('ItemStatus', item.status || '')
                    || item.status
                    || '-'
                )}
                </div>
              </div>
            `).join('')
                : `<div>${UI.t('Empty')}</div>`
            }
      </div>
    </div>
  `;

        let $holder = $('#warehouseMapBinPanelHolder');
        if (!$holder.length) {
            $('body').append('<div id="warehouseMapBinPanelHolder"></div>');
            $holder = $('#warehouseMapBinPanelHolder');
        }

        $holder.html(html);

        $holder
            .off('click.warehouseMapClose')
            .on('click.warehouseMapClose', '.warehouse-map-close-btn, .warehouse-map-bin-panel-backdrop', function () {
                closeBinInfo();
            });
    }

    function closeBinInfo() {
        $('#warehouseMapBinPanelHolder').empty();
    }
})();
