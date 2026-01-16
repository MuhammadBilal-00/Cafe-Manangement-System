function loadOrders(status = 'all-orders', page = 1) {
    const searchTerm = document.querySelector('.form-input').value;
    const branchId = document.querySelector('.form-select').value;
    const orderDate = document.querySelector('input[type="date"]').value;

    fetch(`/Order/GetOrders?status=${status}&search=${searchTerm}&branchId=${branchId}&orderDate=${orderDate}&page=${page}`)
        .then(response => response.json())
        .then(data => {
            updateOrderTable(data.orders);
            updatePagination(data.currentPage, data.totalPages, data.totalCount);
        });
}

// Update order table with data
function updateOrderTable(orders) {
    const tbody = document.querySelector('.table tbody');
    tbody.innerHTML = '';

    orders.forEach(order => {
        tbody.innerHTML += `
            <tr>
                <td class="font-medium">${order.orderNumber}</td>
                <td>${order.customerName}</td>
                <td>${order.branchName}</td>
                <td>${order.orderDate}</td>
                <td>$${order.totalAmount.toFixed(2)}</td>
                <td><span class="status status-${order.status.toLowerCase()}">${order.status}</span></td>
                <td>
                    <div class="action-buttons">
                        <button class="btn btn-outline btn-sm" onclick="viewOrder(${order.id})">
                            <i class="fas fa-eye"></i>
                        </button>
                        <button class="btn btn-outline btn-sm" onclick="updateOrderStatus(${order.id}, '${getNextStatus(order.status)}')">
                            <i class="fas fa-${getStatusIcon(order.status)}"></i>
                        </button>
                    </div>
                </td>
            </tr>
        `;
    });
}

// Load order details for modal
function viewOrder(orderId) {
    fetch(`/Order/GetOrderDetails/${orderId}`)
        .then(response => response.json())
        .then(order => {
            // Populate modal with order data
            document.getElementById('viewOrderModal').style.display = 'flex';
            // Update modal content with order details
        });
}

// Update order status
function updateOrderStatus(orderId, newStatus) {
    fetch('/Order/UpdateOrderStatus', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('[name="__RequestVerificationToken"]').value
        },
        body: JSON.stringify({ orderId: orderId, newStatus: newStatus })
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                loadOrders(); // Reload table
                alert(data.message);
            } else {
                alert('Error: ' + data.message);
            }
        });
}

// Load initial data when page loads
document.addEventListener('DOMContentLoaded', function () {
    loadOrders();
    updateStatusCounts();
});
* /