window.VoucherTemplate = {
    inbound: {
        mode: 'normal',
        cn: '兹有以下物品于 <strong>{date}</strong> 入库，现提交以下货物明细作为入库依据，特此证明。',
        vi: 'Vào ngày <strong>{date}</strong>, tiến hành nhập kho các hàng hóa dưới đây, nay lập phiếu này làm căn cứ xác nhận nhập kho.',
        signTitle: 'Bên ký gửi (寄件方)',
        signTitle2: 'Bên nhận (收货方)',
        signRows: [
            'Xét duyệt (审批): __________________',
            'Người giao (交货人): {party}',
            'SĐT (电话): __________________'
        ],
        signRows2: [
            'Kế toán kho (仓库会计): __________________ <span class="ms-5">Quản lý kho (仓库主管): __________________ </span>',
            'Thủ kho (仓管员): __________________',
            'Người tạo phiếu (制单人): {createdBy}'
        ]
    },

    move: {
        mode: 'single',
        cn: '于 <strong>{date}</strong> 对仓库内以下物品进行库位转移，现提交以下明细作为转移依据，特此证明。',
        vi: 'Vào ngày <strong>{date}</strong>, tiến hành chuyển vị trí các mặt hàng trong kho dưới đây, nay lập phiếu này làm căn cứ xác nhận chuyển vị trí.',
        signTitle: 'Người thực hiện (执行人员)',
        signRows: [
            'Xét duyệt (审批): {approvedBy}',
            'Người chuyển (转移人): {party}',
            'Người tạo phiếu (制单人): {createdBy}'
        ]
    },

    'inventory-check': {
        mode: 'single',
        cn: '于 <strong>{date}</strong> 对仓库内以下物品进行库存盘点，现提交以下盘点明细作为记录依据，特此证明。',
        vi: 'Vào ngày <strong>{date}</strong>, tiến hành kiểm kê các mặt hàng trong kho dưới đây, nay lập phiếu này làm căn cứ xác nhận kiểm kê kho.',
        signTitle: 'Người thực hiện (执行人员)',
        signRows: [
            'Xét duyệt (审批): {approvedBy}',
            'Người kiểm kê (盘点人员): {party}',
            'Người tạo phiếu (制单人): {createdBy}'
        ]
    },

    adjustment: {
        mode: 'single',
        cn: '于 <strong>{date}</strong> 对仓库内以下物品进行状态调整处理，现提交以下调整明细作为记录依据，特此证明。',
        vi: 'Vào ngày <strong>{date}</strong>, tiến hành điều chỉnh trạng thái các mặt hàng trong kho dưới đây, nay lập phiếu này làm căn cứ xác nhận điều chỉnh kho.',
        signTitle: 'Người thực hiện (执行人员)',
        signRows: [
            'Xét duyệt (审批): {approvedBy}',
            'Người điều chỉnh (调整人员): {party}',
            'Người tạo phiếu (制单人): {createdBy}'
        ]
    },

    'repair-send': {
        mode: 'single',
        cn: '于 <strong>{date}</strong> 将以下物品送往维修处理，现提交以下送修明细作为记录依据，特此证明。',
        vi: 'Vào ngày <strong>{date}</strong>, tiến hành gửi sửa chữa các mặt hàng dưới đây, nay lập phiếu này làm căn cứ xác nhận gửi sửa chữa.',
        signTitle: 'Người thực hiện (执行人员)',
        signRows: [
            'Xét duyệt (审批): ',
            'Người gửi sửa chữa (送修人员): ',
            'Người tạo phiếu (制单人): {createdBy}'
        ]
    },

    'repair-receive': {
        mode: 'single',
        cn: '于 <strong>{date}</strong> 对以下维修物品进行收货确认，现提交以下收货明细作为记录依据，特此证明。',
        vi: 'Vào ngày <strong>{date}</strong>, tiến hành nhận lại các mặt hàng sửa chữa dưới đây, nay lập phiếu này làm căn cứ xác nhận nhận hàng sửa chữa.',
        signTitle: 'Người thực hiện (执行人员)',
        signRows: [
            'Xét duyệt (审批): {approvedBy}',
            'Người nhận sửa chữa (收货人员): {party}',
            'Người tạo phiếu (制单人): {createdBy}'
        ]
    },

    'borrow-lend': {
        mode: 'borrow',
        cn: '本人<strong> {party}</strong>，工号 ____________________，部门<strong> {department}</strong>，因 <strong> {purpose}</strong > 需要借用以下物品。预计于 <strong> {dueDate}</strong > 归还，特此填写借货单作为证明，归还时收回此单。',
        vi: 'Vào ngày <strong>{date}</strong>, tôi tên là <strong>{party}</strong>, mã thẻ ____________________, bộ phận <strong>{department}</strong>, do <strong>{purpose}</strong> cần mượn bản. Dự định đến <strong>{dueDate}</strong> sẽ mang trả lại, nay viết đơn mượn hàng này để làm chứng, khi trả thu lại giấy này.',
        signTitle: 'Bên mượn (借用方)',
        signTitle2: 'Bên cho mượn (出借方)',
        signRows: [
            'Xét duyệt (审批): {approvedBy}',
            'Người làm đơn (申请人): {party}',
            'SĐT (电话): {phone}'
        ],
        signRows2: [
            'Kế toán sản xuất (生产会计): __________________ <span class="ms-5">Quản lý bộ phận (部门主管): __________________ </span>',
            'Chủ quản bộ phận (部门负责人): {departmentOwner}',
            'Người bàn giao (移交人): {createdBy}'
        ]
    },

    'borrow-return': {
        mode: 'borrow',
        cn: '本人 <strong>{party}</strong>，于 <strong>{date}</strong> 根据借用单借用仓库物品 <strong>{documentNo}</strong>，现将以下借用物品归还仓库，特此申请作为证明。',
        vi: 'Vào ngày <strong>{date}</strong>, tôi tên là <strong>{party}</strong>, đã mượn các mặt hàng trong kho theo phiếu mượn <strong>{documentNo}</strong>, nay tiến hành trả lại các hàng hóa đã mượn dưới đây về kho.',
        signTitle: 'Bên trả (归还方)',
        signTitle2: 'Bên nhận (接收方)',
        signRows: [
            'Xét duyệt (审批): {approvedBy}',
            'Người trả (归还人): {party}',
            'SĐT (电话): {phone}'
        ],
        signRows2: [
            'Thủ kho nhận (收货仓管): __________________ <span class="ms-5">Quản lý kho (仓库主管): __________________</span>',
            'Kế toán kho (仓库会计): __________________',
            'Người tạo phiếu (制单人): {createdBy}'
        ]
    }
};