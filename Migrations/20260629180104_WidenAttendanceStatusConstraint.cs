using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cafe.Migrations
{
    /// <inheritdoc />
    public partial class WidenAttendanceStatusConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Attendance_Status",
                table: "Attendances");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Attendance_Status",
                table: "Attendances",
                sql: "[Status] IN ('Present','Absent','Late','Half-Day','Paid Leave','Sick Leave','Casual Leave','Holiday','Work From Home','Overtime')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Attendance_Status",
                table: "Attendances");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Attendance_Status",
                table: "Attendances",
                sql: "[Status] IN ('Present','Absent','Late','Half-Day')");
        }
    }
}
