# Inventory Management Module - Implementation Guide

## Overview

This document describes the production-quality Inventory Management Module implemented for the ASP.NET MVC Café Management System. This is NOT a basic CRUD module - it's a comprehensive, fully-integrated inventory system designed for real café operations.

## Key Features

### 1. Core Inventory Management
- **Branch-Specific Inventory**: Each café branch maintains separate inventory
- **Real-Time Stock Tracking**: Current quantity, minimum thresholds, and automatic status updates
- **Multiple Categories**: Dairy, Beverage, Bakery, Raw Material, Vegetables, Meat, etc.
- **Flexible Units**: kg, liters, pieces, packs, boxes, bottles, cans, etc.
- **Supplier Tracking**: Optional supplier information for each item

### 2. Inventory Operations

#### Stock In (Receiving Inventory)
- Add inventory from purchases, deliveries, or transfers
- Transaction logging with notes
- Real-time quantity calculation preview
- Automatic status updates

#### Stock Out (Removing Inventory)
- Multiple reasons: Wastage, Expiry, Damage, Transfer
- Validation prevents over-deduction
- Required notes for accountability
- Warning system for insufficient stock

#### Automatic Deduction
- Inventory automatically deducted when orders are placed
- Pre-order availability checks
- Transaction logging linked to orders

### 3. Recipe Mapping System
- Links menu items to required inventory items
- Defines quantity needed per serving
- Enables automatic inventory calculation
- Prevents orders when ingredients unavailable

### 4. Transaction History
- Complete audit trail of all inventory movements
- Tracks: Stock In, Stock Out, Order Usage, Wastage, Expiry
- Records quantity before/after each transaction
- User tracking (who performed the operation)
- Filtering by item, type, and date
- Pagination for large datasets

### 5. Alerts & Intelligence

#### Low Stock Alerts
- Automatic detection based on minimum threshold
- Visual indicators on dashboard
- Color-coded status badges:
  - 🟢 **In Stock**: Above minimum threshold
  - 🟠 **Low Stock**: At or below minimum threshold
  - 🔴 **Out of Stock**: Zero quantity

#### Dashboard Statistics
- Total inventory items
- Count by status (In Stock, Low Stock, Out of Stock)
- Total inventory value
- Recent activity feed
- Low stock alerts at-a-glance

### 6. Search, Filter & Pagination
- Search by item name or supplier
- Filter by category, status, branch
- Transaction filtering by item type and date
- Pagination for better performance
- Real-time updates via AJAX

## Architecture

### Models

#### InventoryItem
```csharp
- Id: Unique identifier
- Name: Item name
- Category: Classification (Dairy, Beverage, etc.)
- Unit: Measurement unit (kg, liters, etc.)
- CurrentQuantity: Current stock level
- MinimumThreshold: Alert threshold
- CostPerUnit: Unit cost
- Supplier: Optional supplier name
- Status: In Stock / Low Stock / Out of Stock
- LastUpdated: Timestamp
- BranchId: Associated branch
```

#### InventoryTransaction
```csharp
- Id: Unique identifier
- InventoryItemId: Referenced item
- TransactionType: Stock In, Stock Out, etc.
- Quantity: Amount changed
- QuantityBefore: Stock before transaction
- QuantityAfter: Stock after transaction
- Notes: Optional description
- TransactionDate: Timestamp
- BranchId: Associated branch
- OrderId: Optional order reference
- PerformedBy: User who performed action
```

#### InventoryRecipeMapping
```csharp
- Id: Unique identifier
- MenuItemId: Menu item reference
- InventoryItemId: Inventory item reference
- QuantityRequired: Amount per serving
- Unit: Measurement unit
```

### Services

#### InventoryService
- **StockIn()**: Add inventory with validation and transaction logging
- **StockOut()**: Remove inventory with validation
- **DeductInventoryForOrder()**: Automatic deduction with rollback on failure
- **CheckInventoryAvailability()**: Pre-order validation
- **UpdateInventoryStatus()**: Automatic status calculation
- **GetInventoryStatus()**: Status determination logic

### Controllers

#### InventoryController
- **Index**: Dashboard with statistics
- **Create**: Add new inventory item
- **Edit**: Update item details (not quantity)
- **Details**: View item with transaction history
- **Delete**: Remove item (manager/owner only)
- **StockIn**: Add inventory operation
- **StockOut**: Remove inventory operation
- **Transactions**: View complete transaction history
- **RecipeMappings**: Manage menu-inventory links
- **GetInventoryItems**: AJAX endpoint for table data

### Integration

#### OrderController Integration
```csharp
// Before creating order
foreach (var item in orderItems) {
    var hasInventory = await _inventoryService.CheckInventoryAvailability(
        item.MenuItemId, item.Quantity, branchId
    );
    if (!hasInventory) return error;
}

// After order creation
await _inventoryService.DeductInventoryForOrder(
    orderId, branchId, userName
);
```

## Security & Validation

### Authorization
- **Staff**: View-only access to inventory
- **Manager**: Full access to branch inventory
- **Owner**: Full access to all branches

### Validation
- Server-side validation on all inputs
- Client-side validation for better UX
- Prevention of negative stock
- Transaction-based updates for consistency
- Anti-forgery tokens on all POST requests
- Branch-based access control

### Data Integrity
- Foreign key constraints
- Check constraints on quantities and prices
- Cascade delete where appropriate
- Restrict delete for referenced items
- Transaction logging for audit trail

## User Interface

### Dashboard
- Clean, professional Tailwind CSS design
- Key metrics cards with visual indicators
- Low stock alerts section
- Recent activity feed
- Branch filter for owners
- Quick action buttons

### Forms
- Intuitive, well-labeled inputs
- Dropdown selections for consistency
- Real-time calculation previews
- Clear validation messages
- Success/error notifications
- Cancel and submit actions

### Tables
- Sortable columns
- Search functionality
- Status badges with colors
- Action buttons (View, Edit, Delete)
- Pagination controls
- Responsive design

## Database Schema

### Tables Added/Modified
1. **InventoryItems** (Enhanced)
   - Added: Category, Status, CurrentQuantity, MinimumThreshold, CostPerUnit
   - Modified: Changed Quantity to CurrentQuantity, ReorderLevel to MinimumThreshold

2. **InventoryTransactions** (New)
   - Complete transaction audit trail
   - Links to orders for automatic deductions

3. **InventoryRecipeMappings** (New)
   - Many-to-many between MenuItems and InventoryItems
   - Quantity requirements per recipe

### Migration
- Name: `InventoryManagementModule`
- Creates new tables
- Modifies existing InventoryItems table
- Adds relationships and constraints

## Usage Scenarios

### Scenario 1: Receiving Inventory
1. Navigate to Inventory → Stock In
2. Select inventory item
3. Enter quantity received
4. Add notes (e.g., "Purchase Order #12345, Supplier ABC")
5. Submit → Inventory updated, transaction logged

### Scenario 2: Recording Wastage
1. Navigate to Inventory → Stock Out
2. Select inventory item
3. Choose "Wastage" as reason
4. Enter quantity wasted
5. Add required notes explaining reason
6. Submit → Inventory reduced, transaction logged

### Scenario 3: Creating Recipe Mapping
1. Navigate to Inventory → Recipe Mappings
2. Click "Add Mapping"
3. Select menu item (e.g., Cappuccino)
4. Select inventory item (e.g., Milk)
5. Enter quantity required (e.g., 200 ml per cup)
6. Submit → Mapping created

### Scenario 4: Processing Order
1. Customer places order for 2 Cappuccinos
2. System checks inventory availability (2 × 200ml milk = 400ml)
3. If sufficient: Order created, 400ml milk auto-deducted
4. If insufficient: Order blocked with clear error message
5. Transaction logged with order reference

## Best Practices

### For Café Staff
- Always use Stock In/Out for quantity changes
- Never edit quantity directly in Edit form
- Add clear notes for all stock operations
- Review low stock alerts daily
- Report discrepancies immediately

### For Managers
- Set appropriate minimum thresholds
- Create recipe mappings for all menu items
- Review transaction history regularly
- Monitor inventory value trends
- Plan reorders based on alerts

### For Developers
- Always use InventoryService for stock operations
- Never bypass validation
- Include transaction logging
- Test edge cases (zero stock, concurrent updates)
- Maintain branch-based isolation

## Future Enhancements (Optional)

### Inventory Forecasting
- Analyze historical usage patterns
- Predict future inventory needs
- Suggest optimal stock levels

### Supplier Management
- Reorder suggestions based on low stock
- Supplier contact information
- Purchase order generation
- Delivery tracking

### Reports & Analytics
- Daily/weekly usage reports
- Cost analysis
- Wastage reports
- Inventory turnover rate
- Branch comparison

### Mobile Support
- Quick stock updates from mobile
- Barcode scanning
- Photo documentation for wastage
- Push notifications for alerts

## Troubleshooting

### "Insufficient inventory" error on order
- Check recipe mappings exist for menu item
- Verify inventory quantities are sufficient
- Review recent transactions for unexpected deductions

### Low stock alerts not showing
- Verify minimum threshold is set correctly
- Check if status calculation is running
- Ensure branch filter is correct

### Transaction not logged
- Verify using Stock In/Out forms, not Edit
- Check user permissions
- Review server logs for errors

## Technical Notes

- Framework: ASP.NET Core 8.0 MVC
- Database: SQL Server with Entity Framework Core
- UI: Tailwind CSS
- JavaScript: Vanilla JS with Fetch API
- Authentication: Session-based with role checking

## Support

For issues or questions:
1. Check transaction history for audit trail
2. Review server logs for errors
3. Verify user permissions
4. Contact system administrator

---

**Version**: 1.0  
**Last Updated**: January 2026  
**Author**: Development Team
