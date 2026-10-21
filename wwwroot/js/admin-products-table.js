$(function () {

    const $table = $('#productsTable');

    const token =
        $('input[name="__RequestVerificationToken"]').first().val();

    // Product names are typed by admins, so always escape before inserting as HTML
    const esc = $.fn.dataTable.render.text().display;

    const LOW_STOCK = 5;

    // Activate / Deactivate need a POST form with the antiforgery token
    function actionForm(id, action, btnClass, label, confirmText) {
        const confirmAttr = confirmText
            ? ` onsubmit="return confirm('${confirmText}');"`
            : '';

        return `<form method="post" action="/Admin/${action}/${id}" class="d-inline"${confirmAttr}>` +
               `<input type="hidden" name="__RequestVerificationToken" value="${esc(token)}" />` +
               `<button type="submit" class="btn btn-sm ${btnClass}">${label}</button>` +
               `</form>`;
    }

    const table = $table.DataTable({

        processing: true,
        serverSide: true,
        searchDelay: 400,
        pageLength: 10,
        lengthMenu: [10, 25, 50, 100],

        // No client-side default sort: the server returns newest first
        order: [],

        language: {
            searchPlaceholder: 'Name, category or ID',
            emptyTable: 'No products found.'
        },

        ajax: {
            url: $table.data('url'),
            type: 'POST',
            headers: { 'RequestVerificationToken': token },
            data: function (d) {
                return Object.assign(d, {
                    category: $('#fCategory').val(),
                    status: $('#fStatus').val(),
                    stock: $('#fStock').val()
                });
            }
        },

        columns: [

            {
                data: 'imageUrl',
                orderable: false,
                searchable: false,
                render: function (url) {
                    return '<img src="' + esc(url) + '" alt="" ' +
                           'style="width:60px;height:70px;object-fit:cover;border-radius:10px;" />';
                }
            },

            {
                data: 'name',
                render: function (d, t, row) {
                    return '<div class="fw-semibold">' + esc(d) + '</div>' +
                           '<small class="text-muted">ID: ' + row.id + '</small>';
                }
            },

            {
                data: 'category',
                render: $.fn.dataTable.render.text()
            },

            {
                data: 'price',
                render: function (n) {
                    return '<strong>BDT ' + Number(n).toLocaleString('en-US') + '</strong>';
                }
            },

            {
                data: 'stock',
                render: function (n) {
                    if (n === 0) {
                        return '<span class="badge bg-danger">Out of stock</span>';
                    }
                    if (n <= LOW_STOCK) {
                        return '<span class="badge bg-warning text-dark">' + n + ' left</span>';
                    }
                    return n;
                }
            },

            {
                data: 'isActive',
                render: function (active) {
                    return active
                        ? '<span class="badge bg-success">Active</span>'
                        : '<span class="badge bg-secondary">Inactive</span>';
                }
            },

            {
                data: 'id',
                orderable: false,
                searchable: false,
                className: 'text-end',
                render: function (id, t, row) {

                    const edit =
                        '<a class="btn btn-sm btn-outline-dark" href="/Admin/EditProduct/' + id + '">Edit</a>';

                    const toggle = row.isActive
                        ? actionForm(id, 'DeleteProduct', 'btn-outline-danger', 'Deactivate',
                                     'Are you sure you want to deactivate this product?')
                        : actionForm(id, 'ActivateProduct', 'btn-outline-success', 'Activate', null);

                    return '<div class="d-flex justify-content-end gap-2">' + edit + toggle + '</div>';
                }
            }
        ]
    });

    $('#fCategory, #fStatus, #fStock').on('change', function () {
        table.ajax.reload();
    });

    $('#btnReset').on('click', function () {
        $('#fCategory, #fStatus, #fStock').val('');
        table.search('').ajax.reload();
    });
});