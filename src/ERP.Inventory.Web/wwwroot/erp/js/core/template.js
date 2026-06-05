window.VoucherTemplate = {
    inbound: {
        mode: 'normal',
        titleCn: '入库单',
        titleVi: 'PHIẾU NHẬP KHO',
        cn: '于 <strong>{time}</strong> 时，<strong>{year}</strong> 年 <strong>{month}</strong> 月 <strong>{day}</strong> 日，本人 <strong>{party}</strong>，工号 <strong>{cardNo}</strong>，部门 <strong>{department}</strong>，将以下属于 <strong>{ownerName}</strong> 的物品办理入库至 <strong>{warehouseName}</strong>。特此填写本单作为入库确认及仓库记账依据。',
        vi: 'Vào lúc <strong>{time}</strong> giờ, ngày <strong>{day}</strong> tháng <strong>{month}</strong> năm <strong>{year}</strong>, tôi tên là <strong>{party}</strong>, mã thẻ <strong>{cardNo}</strong>, bộ phận <strong>{department}</strong>, tiến hành nhập các mặt hàng dưới đây thuộc chủ sở hữu <strong>{ownerName}</strong> vào kho <strong>{warehouseName}</strong>. Nay viết phiếu này làm chứng và làm căn cứ xác nhận nhập kho.',
        signTitle: 'Bên nhập/giao hàng (入库/交货方)',
        signTitle2: 'Bên nhận kho (仓库接收方)',
        signRows: [
            'Người nhập/giao hàng (入库/交货人): {party}',
            'Mã thẻ (工号): {cardNo}',
            'Bộ phận (部门): {department}'
        ],
        signRows2: [
            'Thủ kho nhận (收货仓管): __________________',
            'Kế toán kho (仓库会计): __________________',
            'Người tạo phiếu (制单人): {createdBy}'
        ]
    },

    move: {
        mode: 'single',
        titleCn: '库位转移单',
        titleVi: 'PHIẾU CHUYỂN VỊ TRÍ',
        cn: '于 <strong>{time}</strong> 时，<strong>{year}</strong> 年 <strong>{month}</strong> 月 <strong>{day}</strong> 日，执行人 <strong>{party}</strong> 对以下物品进行库位转移。特此填写本单作为库位变更及库存管理依据。',
        vi: 'Vào lúc <strong>{time}</strong> giờ, ngày <strong>{day}</strong> tháng <strong>{month}</strong> năm <strong>{year}</strong>, người thực hiện <strong>{party}</strong> tiến hành chuyển vị trí các mặt hàng dưới đây trong kho <strong>{warehouseName}</strong>. Nay lập phiếu này làm căn cứ xác nhận thay đổi vị trí lưu kho.',
        signTitle: 'Người thực hiện (执行人员)',
        signRows: [
            'Người chuyển vị trí (转移人): {party}',
            'Thủ kho xác nhận (仓管确认): __________________',
            'Xét duyệt (审批): {approvedBy}',
            'Người tạo phiếu (制单人): {createdBy}'
        ]
    },

    adjustment: {
        mode: 'single',
        titleCn: '调整单',
        titleVi: 'PHIẾU ĐIỀU CHỈNH',
        cn: '于 <strong>{time}</strong> 时，<strong>{year}</strong> 年 <strong>{month}</strong> 月 <strong>{day}</strong> 日，执行人 <strong>{party}</strong> 对以下物品进行库存状态或信息调整。特此填写本单作为调整确认及库存记录依据。',
        vi: 'Vào lúc <strong>{time}</strong> giờ, ngày <strong>{day}</strong> tháng <strong>{month}</strong> năm <strong>{year}</strong>, người thực hiện <strong>{party}</strong> tiến hành điều chỉnh trạng thái hoặc thông tin tồn kho của các mặt hàng dưới đây tại kho <strong>{warehouseName}</strong>. Nay lập phiếu này làm căn cứ xác nhận điều chỉnh.',
        signTitle: 'Người điều chỉnh (调整人员)',
        signRows: [
            'Người điều chỉnh (调整人员): {party}',
            'Thủ kho xác nhận (仓管确认): __________________',
            'Kế toán kho (仓库会计): __________________',
            'Xét duyệt (审批): {approvedBy}'
        ]
    },

    'inventory-check': {
        mode: 'single',
        titleCn: '盘点单',
        titleVi: 'PHIẾU KIỂM KÊ',
        cn: '于 <strong>{time}</strong> 时，<strong>{year}</strong> 年 <strong>{month}</strong> 月 <strong>{day}</strong> 日，盘点人员 <strong>{party}</strong> 对以下物品进行库存盘点。特此填写本单作为库存核对及差异处理依据。',
        vi: 'Vào lúc <strong>{time}</strong> giờ, ngày <strong>{day}</strong> tháng <strong>{month}</strong> năm <strong>{year}</strong>, người kiểm kê <strong>{party}</strong> tiến hành kiểm kê các mặt hàng dưới đây tại kho <strong>{warehouseName}</strong>. Nay lập phiếu này làm căn cứ xác nhận kết quả kiểm kê.',
        signTitle: 'Người kiểm kê (盘点人员)',
        signRows: [
            'Người kiểm kê (盘点人员): {party}',
            'Thủ kho xác nhận (仓管确认): __________________',
            'Kế toán kho (仓库会计): __________________',
            'Xét duyệt (审批): {approvedBy}'
        ]
    },

    'repair-send': {
        mode: 'single',
        titleCn: '送修单',
        titleVi: 'PHIẾU GỬI SỬA CHỮA',
        cn: '于 <strong>{time}</strong> 时，<strong>{year}</strong> 年 <strong>{month}</strong> 月 <strong>{day}</strong> 日，本人 <strong>{party}</strong>，工号 <strong>{cardNo}</strong>，部门 <strong>{department}</strong>，因维修需要，将以下属于 <strong>{ownerName}</strong> 的物品送往维修处理。特此填写本单作为维修交接及后续收回依据。',
        vi: 'Vào lúc <strong>{time}</strong> giờ, ngày <strong>{day}</strong> tháng <strong>{month}</strong> năm <strong>{year}</strong>, tôi tên là <strong>{party}</strong>, mã thẻ <strong>{cardNo}</strong>, bộ phận <strong>{department}</strong>, do mặt hàng cần kiểm tra/sửa chữa, tiến hành gửi các mặt hàng dưới đây thuộc chủ sở hữu <strong>{ownerName}</strong> đi sửa chữa. Nay viết phiếu này làm chứng và làm căn cứ bàn giao sửa chữa.',
        signTitle: 'Bên gửi sửa chữa (送修方)',
        signRows: [
            'Người gửi sửa chữa (送修人): {party}',
            'Mã thẻ (工号): {cardNo}',
            'Bộ phận (部门): {department}',
            'Bên nhận sửa chữa (维修接收方): __________________',
            'Xét duyệt (审批): {approvedBy}'
        ]
    },

    'repair-receive': {
        mode: 'single',
        titleCn: '维修收货单',
        titleVi: 'PHIẾU NHẬN HÀNG SỬA CHỮA',
        cn: '于 <strong>{time}</strong> 时，<strong>{year}</strong> 年 <strong>{month}</strong> 月 <strong>{day}</strong> 日，本人 <strong>{party}</strong>，工号 <strong>{cardNo}</strong>，部门 <strong>{department}</strong>，将以下属于 <strong>{ownerName}</strong> 的维修物品收回并确认入库至 <strong>{warehouseName}</strong>。特此填写本单作为维修收货及库存恢复依据。',
        vi: 'Vào lúc <strong>{time}</strong> giờ, ngày <strong>{day}</strong> tháng <strong>{month}</strong> năm <strong>{year}</strong>, tôi tên là <strong>{party}</strong>, mã thẻ <strong>{cardNo}</strong>, bộ phận <strong>{department}</strong>, tiến hành nhận lại các mặt hàng sửa chữa dưới đây thuộc chủ sở hữu <strong>{ownerName}</strong> về kho <strong>{warehouseName}</strong>. Nay viết phiếu này làm chứng và làm căn cứ xác nhận nhận lại hàng sửa chữa.',
        signTitle: 'Bên nhận hàng sửa chữa (维修收货方)',
        signRows: [
            'Người nhận hàng (收货人): {party}',
            'Mã thẻ (工号): {cardNo}',
            'Bộ phận (部门): {department}',
            'Thủ kho xác nhận (仓管确认): __________________',
            'Xét duyệt (审批): {approvedBy}'
        ]
    },

    'borrow-lend': {
        mode: 'borrow',
        titleCn: '借货单',
        titleVi: 'PHIẾU MƯỢN HÀNG',
        cn: '于 <strong>{time}</strong> 时，<strong>{year}</strong> 年 <strong>{month}</strong> 月 <strong>{day}</strong> 日，本人 <strong>{party}</strong>，工号 <strong>{cardNo}</strong>，部门 <strong>{department}</strong>，因 <strong>{purpose}</strong> 需要借用以下属于 <strong>{ownerName}</strong> 的物品。预计于 <strong>{dueYear}</strong> 年 <strong>{dueMonth}</strong> 月 <strong>{dueDay}</strong> 日归还。特此填写借货单作为证明，归还时收回此单。',
        vi: 'Vào lúc <strong>{time}</strong> giờ, ngày <strong>{day}</strong> tháng <strong>{month}</strong> năm <strong>{year}</strong>, tôi tên là <strong>{party}</strong>, mã thẻ <strong>{cardNo}</strong>, bộ phận <strong>{department}</strong>, do <strong>{purpose}</strong> cần mượn các mặt hàng dưới đây thuộc chủ sở hữu <strong>{ownerName}</strong>. Dự định đến ngày <strong>{dueDay}</strong> tháng <strong>{dueMonth}</strong> năm <strong>{dueYear}</strong> sẽ mang trả lại. Nay viết đơn này làm chứng, khi trả hàng sẽ thu lại giấy này.',
        signTitle: 'Bên mượn (借用方)',
        signTitle2: 'Bên cho mượn (出借方)',
        signRows: [
            'Xét duyệt (审批): {approvedBy}',
            'Người mượn (借用人): {party}',
            'Mã thẻ (工号): {cardNo}',
            'Bộ phận (部门): {department}',
            'SĐT (电话): {phone}'
        ],
        signRows2: [
            'Người bàn giao (移交人): {createdBy}',
            'Quản lý bộ phận (部门主管): __________________',
            
        ]
    },

    'borrow-return': {
        mode: 'borrow',
        titleCn: '还货单',
        titleVi: 'PHIẾU TRẢ HÀNG',
        cn: '于 <strong>{time}</strong> 时，<strong>{year}</strong> 年 <strong>{month}</strong> 月 <strong>{day}</strong> 日，本人 <strong>{party}</strong>，工号 <strong>{cardNo}</strong>，部门 <strong>{department}</strong>，根据借用单 <strong>{documentNo}</strong>，现将以下属于 <strong>{ownerName}</strong> 的借用物品归还仓库。特此填写还货单作为归还确认及库存恢复依据。',
        vi: 'Vào lúc <strong>{time}</strong> giờ, ngày <strong>{day}</strong> tháng <strong>{month}</strong> năm <strong>{year}</strong>, tôi tên là <strong>{party}</strong>, mã thẻ <strong>{cardNo}</strong>, bộ phận <strong>{department}</strong>, trả lại các mặt hàng đã mượn theo phiếu <strong>{documentNo}</strong> thuộc chủ sở hữu <strong>{ownerName}</strong> về kho. Nay viết phiếu này làm chứng và làm căn cứ xác nhận hoàn trả hàng.',
        signTitle: 'Bên trả hàng (归还方)',
        signTitle2: 'Bên nhận hàng (接收方)',
        signRows: [
            'Người trả hàng (归还人): {party}',
            'Mã thẻ (工号): {cardNo}',
            'Bộ phận (部门): {department}',
            'SĐT (电话): {phone}'
        ],
        signRows2: [
            'Thủ kho nhận hàng (收货仓管): __________________',
            'Kế toán kho (仓库会计): __________________',
            'Người tạo phiếu (制单人): {createdBy}'
        ]
    }
};
