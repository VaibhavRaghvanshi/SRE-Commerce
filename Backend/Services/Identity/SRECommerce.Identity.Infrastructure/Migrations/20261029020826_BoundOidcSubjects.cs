using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRECommerce.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BoundOidcSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [OpenIddictTokens] WHERE DATALENGTH([Subject]) > 72) OR EXISTS (SELECT 1 FROM [OpenIddictAuthorizations] WHERE DATALENGTH([Subject]) > 72) THROW 50003, 'Non-GUID subjects require review before narrowing.', 1;");
            migrationBuilder.AlterColumn<string>(
                name: "Subject",
                table: "OpenIddictTokens",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(400)",
                oldMaxLength: 400,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Subject",
                table: "OpenIddictAuthorizations",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(400)",
                oldMaxLength: 400,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Subject",
                table: "OpenIddictTokens",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(36)",
                oldMaxLength: 36,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Subject",
                table: "OpenIddictAuthorizations",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(36)",
                oldMaxLength: 36,
                oldNullable: true);
        }
    }
}
