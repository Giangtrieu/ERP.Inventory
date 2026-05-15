window.Pagination = {
    pageSizes: [10, 25, 50, 100, 200, 500],

    getPages(totalPages, page) {
        return Array.from({ length: totalPages },(_, i) => i + 1 ).filter(p =>p === 1 ||p === totalPages || Math.abs(p - page) <= 2);
    },

    renderPageSizeOptions(pageSize) {
        return `<option value="0" ${pageSize === 0 ? 'selected' : ''}>${UI.t('All')}</option>${this.pageSizes.map(s => `<option value="${s}" ${s === pageSize ? 'selected' : ''}>${s} </option>`).join('')}`;
    },

    renderButtons({
        page,
        pageSize,
        totalPages,
        isAll,
        onChange
    }) {
        if (isAll) return '';
        const pages = this.getPages(totalPages, page);
        return `
            <div class="btn-group btn-group-sm">
                <button class="btn btn-outline-secondary"${page <= 1 ? 'disabled' : ''}onclick="${onChange}(1, ${pageSize})">«</button>
                <button class="btn btn-outline-secondary"${page <= 1 ? 'disabled' : ''}onclick="${onChange}(${page - 1}, ${pageSize})">‹</button>
            ${pages.map((p, i) => {
            const prev = pages[i - 1];
            const dots = prev && p - prev > 1? `<span class="px-2">...</span>`: '';
            return ` ${dots} <button class="btn ${p === page ? 'btn-primary' : 'btn-outline-secondary'}"onclick="${onChange}(${p}, ${pageSize})">${p} </button>`;
            }).join('')}
        <button class="btn btn-outline-secondary"${page >= totalPages ? 'disabled' : ''}onclick="${onChange}(${page + 1}, ${pageSize})"> › </button>
        <button class="btn btn-outline-secondary"${page >= totalPages ? 'disabled' : ''}onclick="${onChange}(${totalPages}, ${pageSize})"> » </button></div>`;
    },

    render({
        page,
        pageSize,
        total,
        totalPages,
        isAll,
        selectId,
        onChange
    }) {
        return `
            <div class="d-flex justify-content-between align-items-center mt-3 flex-wrap gap-2">
                <div class="d-flex align-items-center gap-2">
                    <span class="text-muted small"> ${UI.t('Rows per page')}:</span>
                    <select id="${selectId}"class="form-select form-select-sm" style="width:110px;">${this.renderPageSizeOptions(pageSize)}</select>
                </div>
            ${this.renderButtons({page,pageSize,totalPages,isAll,onChange})}</div>`;
    },

    bindPageSize(selectId, callback) {
        $(document).off('change', `#${selectId}`).on('change', `#${selectId}`, function () {
                const size = Number($(this).val());
                AppState.pageSize = size;
                callback(1, size);
            });
    }
};

