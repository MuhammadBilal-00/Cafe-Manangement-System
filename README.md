# 🍽️ Cafe Management System

A comprehensive web-based Cafe Management System built with ASP.NET Core MVC, Entity Framework Core, and SQL Server. This system provides complete management capabilities for restaurant operations including menu management, order processing, staff management, inventory tracking, and more.

## 📋 Table of Contents

- [Features](#features)
- [Technology Stack](#technology-stack)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Database Setup](#database-setup)
- [Configuration](#configuration)
- [Usage](#usage)
- [Project Structure](#project-structure)
- [Key Modules](#key-modules)
- [User Roles](#user-roles)
- [Contributing](#contributing)
- [License](#license)

## ✨ Features

### 🍴 Menu Management
- **Category Management**: Organize menu items by categories
- **Menu Item Management**: Create, update, and delete menu items with detailed information
- **Nutritional Information**: Track calories, protein, carbohydrates, fat, fiber, sugar, and sodium
- **Dietary Flags**: Mark items as vegetarian, vegan, gluten-free, dairy-free, nut-free, or spicy
- **Image Gallery**: Upload and manage multiple images for each menu item
- **Pricing**: Manage regular prices, original prices (for discounts), and cost prices
- **Availability Tracking**: Mark items as available or unavailable
- **Ratings & Reviews**: Customer reviews and ratings system
- **Featured Items**: Highlight special or popular items
- **Daily Specials**: Promote special items for specific days

### 📦 Order Management
- **Order Creation**: Place new orders with multiple items
- **Order Tracking**: Monitor order status (Pending, Completed, Cancelled)
- **Order History**: View complete order history
- **Order Details**: Detailed view of order items and total amounts
- **Customer Information**: Link orders to customers

### 🏢 Branch Management
- **Multi-Branch Support**: Manage multiple restaurant branches
- **Branch-Specific Menus**: Customize menu items per branch
- **Branch Managers**: Assign managers to specific branches
- **Branch Information**: Store location, contact details, and operating hours

### 👥 Staff Management
- **Staff Records**: Maintain detailed staff information
- **Role Management**: Define and assign staff roles (Owner, Branch Manager, Staff)
- **Staff Scheduling**: Create and manage staff schedules
- **Salary Management**: Track staff salaries and payments
- **Staff Roles**: Assign specific roles and permissions to staff members

### 📊 Inventory Management
- **Inventory Items**: Track all inventory items
- **Stock Levels**: Monitor current stock levels
- **Ingredient Management**: Manage ingredients used in menu items
- **Purchase Tracking**: Record and track purchases
- **Low Stock Alerts**: Identify items that need reordering

### 🔐 User Management & Authentication
- **User Registration**: Create new user accounts
- **User Login**: Secure authentication system
- **Role-Based Access Control**: Different access levels for Owner, Branch Manager, Staff, and Customer
- **Session Management**: Secure session handling with 60-minute timeout
- **Password Security**: Encrypted password storage

### 📈 Reporting & Analytics
- **Sales Reports**: Generate sales reports and analytics
- **Customer Feedback**: Collect and manage customer feedback
- **Menu Performance**: Track popular items and ratings

### 🛡️ Security Features
- **Custom Authentication Middleware**: Secure route protection
- **Session-Based Authentication**: HTTP-only cookies with secure policy
- **Password Hashing**: Secure password storage
- **Role-Based Authorization**: Access control based on user roles

## 🛠️ Technology Stack

### Backend
- **Framework**: ASP.NET Core 8.0 (MVC)
- **Language**: C# with .NET 8.0
- **ORM**: Entity Framework Core 9.0.8
- **Database**: SQL Server (via EF Core SQL Server provider)
- **Authentication**: Custom session-based authentication with middleware
- **Caching**: In-Memory Caching

### Frontend
- **View Engine**: Razor Views
- **CSS Framework**: Bootstrap (via wwwroot)
- **JavaScript**: jQuery and vanilla JavaScript

### Development Tools
- **IDE**: Compatible with Visual Studio 2022, Visual Studio Code, or JetBrains Rider
- **Package Manager**: NuGet

## 📦 Prerequisites

Before you begin, ensure you have the following installed:

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (Express, Developer, or Enterprise edition)
  - Or [SQL Server LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (recommended) or [Visual Studio Code](https://code.visualstudio.com/)
- [SQL Server Management Studio (SSMS)](https://learn.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms) (optional, for database management)

## 🚀 Installation

### 1. Clone the Repository

```bash
git clone https://github.com/MuhammadBilal-00/Cafe-Manangement-System.git
cd Cafe-Manangement-System
```

### 2. Restore NuGet Packages

```bash
dotnet restore
```

### 3. Update Connection String

Edit the `appsettings.json` file and update the connection string to match your SQL Server configuration:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER_NAME;Database=RestaurantManagementDB;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true;"
  }
}
```

**Connection String Options:**

- **Windows Authentication** (Recommended for local development):
  ```
  Server=YOUR_SERVER_NAME;Database=RestaurantManagementDB;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true;
  ```

- **SQL Server LocalDB** (For development without full SQL Server):
  ```
  Server=(localdb)\\mssqllocaldb;Database=RestaurantManagementDB;Trusted_Connection=true;MultipleActiveResultSets=true;
  ```

- **SQL Server Authentication**:
  ```
  Server=YOUR_SERVER_NAME;Database=RestaurantManagementDB;User Id=YOUR_USERNAME;Password=YOUR_PASSWORD;MultipleActiveResultSets=true;TrustServerCertificate=true;
  ```

### 4. Create Database Migrations

```bash
dotnet ef migrations add InitialCreate
```

### 5. Apply Database Migrations

```bash
dotnet ef database update
```

This will create the `RestaurantManagementDB` database with all necessary tables.

### 6. Build the Application

```bash
dotnet build
```

### 7. Run the Application

```bash
dotnet run
```

The application will start and be available at:
- HTTPS: `https://localhost:5001`
- HTTP: `http://localhost:5000`

## 🗄️ Database Setup

### Database Schema

The system creates the following main tables:

- **Users**: User accounts with role-based access
- **Branches**: Restaurant branch information
- **Categories**: Menu item categories
- **MenuItems**: Complete menu with pricing and details
- **Orders**: Customer orders
- **OrderItems**: Individual items in orders
- **Staff**: Staff member records
- **StaffRoles**: Staff role definitions
- **StaffSalaries**: Salary tracking
- **StaffSchedules**: Staff scheduling
- **Ingredients**: Ingredient inventory
- **MenuItemIngredients**: Link between menu items and ingredients
- **InventoryItems**: Inventory tracking
- **Purchases**: Purchase records
- **Feedbacks**: Customer feedback
- **SalesReports**: Sales analytics
- **MenuItemReviews**: Customer reviews and ratings
- **DailySpecials**: Daily special items
- **Customers**: Customer information

### Initial Data Setup

After running the application for the first time, you may want to:

1. Create an initial Owner/Admin user through the registration page
2. Add your first branch location
3. Create menu categories
4. Add menu items
5. Set up staff roles and staff members

## ⚙️ Configuration

### Application Settings

The `appsettings.json` file contains important configuration:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Your connection string here"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "AllowedHosts": "*"
}
```

### Session Configuration

Sessions are configured with:
- **Idle Timeout**: 60 minutes
- **Cookie Name**: `CafeManagement.Session`
- **HTTP Only**: True (for security)
- **Secure Policy**: SameAsRequest

### Environment-Specific Settings

- **Development**: Uses `appsettings.Development.json` for development-specific settings
- **Production**: Use `appsettings.Production.json` (create this file) for production settings

## 📖 Usage

### First Time Setup

1. **Navigate to the Application**: Open your browser and go to `https://localhost:5001`

2. **Register as Owner**: 
   - Go to the registration page
   - Create your first user with "Owner" role
   - This user will have full access to all features

3. **Login**: 
   - Use your credentials to log in
   - The system will redirect you based on your role

4. **Set Up Your Restaurant**:
   - Add your first branch
   - Create menu categories (Appetizers, Main Course, Desserts, Beverages, etc.)
   - Add menu items with details, prices, and images
   - Set up staff roles and add staff members

### User Roles

The system supports four main user roles:

1. **Owner**: 
   - Full system access
   - Manage all branches
   - View all reports and analytics
   - Manage staff across all branches
   - Configure system settings

2. **Branch Manager**:
   - Manage assigned branch
   - View branch-specific reports
   - Manage branch staff
   - Update branch menu and inventory

3. **Staff**:
   - Process orders
   - Update order status
   - View menu items
   - Limited access to reports

4. **Customer**:
   - Browse menu
   - Place orders
   - View order history
   - Leave feedback and reviews

### Daily Operations

#### Taking Orders
1. Navigate to Orders > New Order
2. Select customer
3. Add menu items to the order
4. Review total amount
5. Confirm and submit order

#### Managing Menu
1. Navigate to Menu > Menu Items
2. Click "Add New Item" to create menu items
3. Fill in all required information
4. Upload images
5. Set availability and pricing

#### Staff Management
1. Navigate to Staff > Staff List
2. Add new staff members
3. Assign roles and schedules
4. Track salaries and payments

#### Viewing Reports
1. Navigate to Reports section
2. Select report type (Sales, Inventory, etc.)
3. Choose date range
4. Generate and export reports

## 📁 Project Structure

```
Cafe-Management-System/
│
├── Controllers/              # MVC Controllers
│   ├── AuthController.cs    # Authentication and user management
│   ├── BranchController.cs  # Branch management
│   ├── CategoryController.cs # Category management
│   ├── HomeController.cs    # Home and dashboard
│   ├── IngredientController.cs # Ingredient management
│   ├── MenuController.cs    # Menu and menu items
│   ├── OrderController.cs   # Order processing
│   ├── StaffController.cs   # Staff management
│   └── StaffRoleController.cs # Staff role management
│
├── Models/                   # Data models
│   ├── User.cs              # User model
│   ├── Branch.cs            # Branch model
│   ├── MenuItem.cs          # Menu item model
│   ├── Order.cs             # Order model
│   ├── Staff.cs             # Staff model
│   ├── DTOs/                # Data Transfer Objects
│   └── ViewModels/          # View Models
│
├── Views/                    # Razor views
│   ├── Auth/                # Authentication views
│   ├── Branch/              # Branch management views
│   ├── Home/                # Home and dashboard views
│   ├── Menu/                # Menu management views
│   ├── Order/               # Order views
│   ├── Staff/               # Staff management views
│   └── Shared/              # Shared layouts and partials
│
├── Data/                     # Database context
│   └── ApplicationDbContext.cs # EF Core DbContext
│
├── Services/                 # Business logic services
│   ├── IAuthService.cs      # Authentication service interface
│   └── AuthService.cs       # Authentication service implementation
│
├── Middleware/               # Custom middleware
│   └── AuthenticationMiddleware.cs # Custom auth middleware
│
├── Helpers/                  # Helper classes
│
├── Attributes/               # Custom attributes
│
├── wwwroot/                  # Static files
│   ├── css/                 # Stylesheets
│   ├── js/                  # JavaScript files
│   ├── images/              # Images
│   └── lib/                 # Third-party libraries
│
├── Properties/               # Project properties
│   └── launchSettings.json  # Launch configuration
│
├── appsettings.json         # Application configuration
├── appsettings.Development.json # Development settings
├── Program.cs               # Application entry point
├── Cafe.csproj              # Project file
└── Cafe.sln                 # Solution file
```

## 🔑 Key Modules

### 1. Authentication & Authorization
- Custom session-based authentication
- Role-based access control
- Secure password hashing
- Session management with timeout

### 2. Menu Management Module
- CRUD operations for menu items
- Category management
- Image upload and gallery
- Nutritional information tracking
- Dietary restrictions
- Pricing management

### 3. Order Management Module
- Order creation and processing
- Order status tracking
- Order history
- Customer order association
- Order item details

### 4. Branch Management Module
- Multi-branch support
- Branch-specific operations
- Branch manager assignment
- Location and contact management

### 5. Staff Management Module
- Staff records management
- Role assignment
- Schedule management
- Salary tracking
- Performance monitoring

### 6. Inventory Module
- Ingredient tracking
- Stock level monitoring
- Purchase management
- Low stock alerts
- Usage tracking

### 7. Reporting Module
- Sales reports
- Inventory reports
- Customer feedback analysis
- Menu performance analytics

## 🤝 Contributing

Contributions are welcome! Please follow these steps:

1. **Fork the Repository**
   ```bash
   # Click the "Fork" button on GitHub
   ```

2. **Clone Your Fork**
   ```bash
   git clone https://github.com/YOUR_USERNAME/Cafe-Manangement-System.git
   cd Cafe-Manangement-System
   ```

3. **Create a Feature Branch**
   ```bash
   git checkout -b feature/YourFeatureName
   ```

4. **Make Your Changes**
   - Write clean, documented code
   - Follow existing code style
   - Test your changes thoroughly

5. **Commit Your Changes**
   ```bash
   git add .
   git commit -m "Add: Description of your changes"
   ```

6. **Push to Your Fork**
   ```bash
   git push origin feature/YourFeatureName
   ```

7. **Create a Pull Request**
   - Go to the original repository on GitHub
   - Click "New Pull Request"
   - Select your feature branch
   - Describe your changes in detail

### Code Style Guidelines
- Follow C# naming conventions
- Use meaningful variable and method names
- Add XML documentation comments for public methods
- Keep methods focused and concise
- Write unit tests for new features

### Reporting Issues
- Use the GitHub Issues tab
- Provide detailed description of the issue
- Include steps to reproduce
- Add screenshots if applicable
- Mention your environment (OS, .NET version, etc.)

## 📄 License

This project is open source and available under the [MIT License](LICENSE).

## 📞 Contact & Support

- **Developer**: Muhammad Bilal
- **GitHub**: [@MuhammadBilal-00](https://github.com/MuhammadBilal-00)
- **Repository**: [Cafe-Manangement-System](https://github.com/MuhammadBilal-00/Cafe-Manangement-System)

For support, please open an issue on GitHub or contact the repository maintainer.

## 🎯 Future Enhancements

Potential features for future releases:

- [ ] Online ordering system for customers
- [ ] Mobile application (iOS/Android)
- [ ] Payment gateway integration
- [ ] Table reservation system
- [ ] Kitchen display system (KDS)
- [ ] Delivery tracking
- [ ] Loyalty program management
- [ ] Multi-language support
- [ ] Advanced analytics and business intelligence
- [ ] Email notifications
- [ ] SMS notifications
- [ ] QR code menu
- [ ] Recipe management
- [ ] Supplier management
- [ ] Employee attendance tracking
- [ ] Customer relationship management (CRM)

## 🙏 Acknowledgments

- Built with [ASP.NET Core](https://dotnet.microsoft.com/apps/aspnet)
- Entity Framework Core for database operations
- Bootstrap for responsive UI
- SQL Server for reliable data storage

---

**Note**: This is an educational/demonstration project. For production use, ensure proper security auditing, performance testing, and compliance with local regulations.

Happy Coding! 🚀
