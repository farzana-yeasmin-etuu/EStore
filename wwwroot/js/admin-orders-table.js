$(function () {

    const $table = $('#ordersTable');

    const token =
        $('input[name="__RequestVerificationToken"]').first().val();

    // Customer names are typed by users, so always escape before inserting as HTML
    const esc = $.fn.dataTable.render.text().display;

    const money = n => 'BDT' + Number(n).toLocaleString('en-US');

    // Reuses the badge classes your site already has
    const paymentClass = s =>
        s === 'Paid' ? 'es-status-paid'
            : (s === 'Failed' || s === 'Cancelled') ? 'es-status-failed'
                : 'es-status-pending';

    const statusClass = s =>
        s === 'Cancelled' ? 'es-status-cancelled'
            : s === 'Pending' ? 'es-status-pending'
                : 'es-status-confirmed';

    const badge = (text, cls) =>
        `<span class="es-status-badge ${cls}">${esc(text)}</span>`;

    // Filters are read by the report page later too
    window.getOrderFilters = function () {
        return {
            status: $('#fStatus').val(),
            paymentStatus: $('#fPayment').val(),
            from: $('#fFrom').val(),
            to: $('#fTo').val()
        };
    };

    const table = $table.DataTable({

        processing: true,
        serverSide: true,
        searchDelay: 400,
        pageLength: 10,
        lengthMenu: [10, 25, 50, 100],
        order: [[2, 'desc']],

        language: {
            search: 'Search:',
            searchPlaceholder: 'Order #, name, phone, email',
            emptyTable: 'No orders found.'
        },

        ajax: {
            url: $table.data('url'),
            type: 'POST',
            headers: { 'RequestVerificationToken': token },
            data: function (d) {
                return Object.assign(d, window.getOrderFilters());
            }
        },

        columns: [
            {
                data: 'orderNumber',
                render: $.fn.dataTable.render.text()
            },
            {
                data: 'customer',
                render: function (d, t, row) {
                    return esc(d) +
                        '<br><small style="color:#888">' + esc(row.phone) + '</small>';
                }
            },
            { data: 'date' },
            {
                data: 'total',
                render: money
            },
            {
                data: 'paymentStatus',
                render: function (d, t, row) {
                    return badge(d, paymentClass(d)) +
                        '<br><small style="color:#888">' + esc(row.paymentMethod) + '</small>';
                }
            },
            {
                data: 'status',
                render: function (d) {
                    return badge(d, statusClass(d));
                }
            },
            {
                data: 'id',
                orderable: false,
                searchable: false,
                render: function (id) {
                    return '<a class="es-admin-view-btn" href="/Admin/OrderDetails/' + id + '">View</a>';
                }
            }
        ]
    });

    // The report page needs access to the table (search text, reload)
    window.ordersTable = table;

    $('#fStatus, #fPayment, #fFrom, #fTo').on('change', function () {
        table.ajax.reload();
    });

    $('#btnReset').on('click', function () {
        $('#fStatus, #fPayment, #fFrom, #fTo').val('');
        table.search('').ajax.reload();
    });
});