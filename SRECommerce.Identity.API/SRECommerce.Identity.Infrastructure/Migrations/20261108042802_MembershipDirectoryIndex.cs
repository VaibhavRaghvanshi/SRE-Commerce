using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRECommerce.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MembershipDirectoryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TenantMemberships_TenantId_IsActive_Role_UserId",
                table: "TenantMemberships",
                columns: new[] { "TenantId", "IsActive", "Role", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantMemberships_TenantId_IsActive_Role_UserId",
                table: "TenantMemberships");
        }
    }
}
